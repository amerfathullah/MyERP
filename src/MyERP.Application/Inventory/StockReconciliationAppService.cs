using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.DomainServices;
using MyERP.Dtos;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockReconciliations.Default)]
public class StockReconciliationAppService : ApplicationService, IStockReconciliationAppService
{
    private readonly IRepository<StockReconciliation, Guid> _repository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly WarehouseAccountService _warehouseAccountService;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public StockReconciliationAppService(
        IRepository<StockReconciliation, Guid> repository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        StockValuationService valuationService,
        BinService binService,
        WarehouseAccountService warehouseAccountService,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _valuationService = valuationService;
        _binService = binService;
        _warehouseAccountService = warehouseAccountService;
        _numberGenerator = numberGenerator;
    }

    /// <summary>
    /// Posts the GL impact of a stock reconciliation's valuation change: one Stock-account line per
    /// affected warehouse (DR if that warehouse's value increased, CR if it decreased), balanced
    /// against a single line on the document's own Difference/Expense Account for the net total.
    /// Per ERPNext stock_reconciliation.py: the same "Difference Account" pattern.
    /// </summary>
    private async Task<JournalEntry?> BuildReconciliationJournalEntryAsync(StockReconciliation sr, bool isReversal)
    {
        var warehouseTotals = new Dictionary<Guid, decimal>();
        foreach (var item in sr.Items)
        {
            if (item.DifferenceAmount == 0) continue;
            warehouseTotals[item.WarehouseId] = warehouseTotals.GetValueOrDefault(item.WarehouseId) + item.DifferenceAmount;
        }
        // Remove warehouses that net to zero across their items
        foreach (var key in warehouseTotals.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList())
            warehouseTotals.Remove(key);

        if (warehouseTotals.Count == 0) return null;

        var totalDiff = warehouseTotals.Values.Sum();
        if (totalDiff != 0 && !sr.ExpenseAccountId.HasValue)
            throw new BusinessException(MyERPDomainErrorCodes.StockReconciliationMissingExpenseAccount);

        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fiscalYear = fyQuery.FirstOrDefault(fy =>
            fy.CompanyId == sr.CompanyId && fy.StartDate <= sr.PostingDate && fy.EndDate >= sr.PostingDate);
        if (fiscalYear == null)
            throw new BusinessException(MyERPDomainErrorCodes.FiscalYearClosed)
                .WithData("postingDate", sr.PostingDate.ToString("yyyy-MM-dd"));

        var multiplier = isReversal ? -1m : 1m;
        var jeNumber = await _numberGenerator.GenerateAsync("JE", sr.CompanyId);
        var je = new JournalEntry(GuidGenerator.Create(), sr.CompanyId, fiscalYear.Id, sr.PostingDate, sr.TenantId)
        {
            EntryNumber = jeNumber,
            ReferenceType = "StockReconciliation",
            ReferenceId = sr.Id,
            Narration = isReversal
                ? $"Reversal of stock reconciliation valuation adjustment ({sr.ReconciliationNumber})"
                : $"Stock reconciliation valuation adjustment ({sr.ReconciliationNumber})",
        };

        foreach (var (warehouseId, amount) in warehouseTotals)
        {
            var stockAccountId = await _warehouseAccountService.ResolveStockAccountAsync(warehouseId, sr.CompanyId);
            var lineAmount = amount * multiplier;
            je.AddLine(stockAccountId, Math.Abs(lineAmount), isDebit: lineAmount > 0,
                description: "Stock reconciliation valuation adjustment");
        }

        if (totalDiff != 0)
        {
            var netAmount = totalDiff * multiplier;
            je.AddLine(sr.ExpenseAccountId!.Value, Math.Abs(netAmount), isDebit: netAmount < 0,
                description: "Stock reconciliation difference account");
        }

        je.Validate();
        je.Post();
        return je;
    }

    public async Task<PagedResultDto<StockReconciliationDto>> GetListAsync(GetStockReconciliationListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => (s.ReconciliationNumber ?? "").Contains(f)
                                    || (s.Purpose ?? "").Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<StockReconciliationDto>(totalCount, items.Select(x => ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(x)).ToList());
    }

    public async Task<StockReconciliationDto> GetAsync(Guid id)
    {
        var sr = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }

    [Authorize(MyERPPermissions.StockReconciliations.Create)]
    public async Task<StockReconciliationDto> CreateAsync(CreateStockReconciliationDto input)
    {
        var sr = new StockReconciliation(GuidGenerator.Create(), input.CompanyId,
            input.PostingDate, CurrentTenant.Id)
        {
            Purpose = input.Purpose,
            Notes = input.Notes,
            ExpenseAccountId = input.ExpenseAccountId,
            CostCenterId = input.CostCenterId,
        };

        foreach (var item in input.Items)
            sr.AddItem(item.ItemId, item.WarehouseId, item.NewQuantity, item.NewValuationRate,
                item.CurrentQuantity, item.CurrentValuationRate);

        await _repository.InsertAsync(sr);
        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }

    [Authorize(MyERPPermissions.StockReconciliations.Submit)]
    public async Task<StockReconciliationDto> SubmitAsync(Guid id)
    {
        var sr = (await _repository.WithDetailsAsync()).First(s => s.Id == id);

        // Validate posting period is not frozen/closed before creating SLE entries
        var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(sr.CompanyId, sr.PostingDate, "StockReconciliation");

        sr.Submit();

        // Create SLE entries for each item adjustment (absolute qty set, not delta)
        // Stock Reconciliation uses ABSOLUTE quantity — the SLE entry sets qty to new level
        foreach (var item in sr.Items)
        {
            var qtyDiff = item.QuantityDifference; // NewQty - CurrentQty
            if (qtyDiff == 0 && item.NewValuationRate == item.CurrentValuationRate)
                continue; // No change needed

            // Create SLE with the difference quantity and new rate
            await _valuationService.CreateLedgerEntryAsync(
                sr.CompanyId, item.ItemId, item.WarehouseId,
                sr.PostingDate,
                quantityChange: qtyDiff,
                incomingRate: item.NewValuationRate,
                voucherType: "StockReconciliation",
                voucherId: sr.Id,
                tenantId: sr.TenantId);

            // Update Bin with the difference
            var valueDiff = item.DifferenceAmount;
            await _binService.ApplyStockMovementAsync(
                item.ItemId, item.WarehouseId,
                qtyDiff, valueDiff, sr.TenantId);
        }

        // Post GL: DR/CR the affected warehouses' Stock accounts, balanced against the Difference/
        // Expense Account. Never previously wired — physical count adjustments only ever touched
        // Stock Ledger + Bin, GL and inventory value permanently diverged.
        var je = await BuildReconciliationJournalEntryAsync(sr, isReversal: false);
        if (je != null)
            await _journalEntryRepository.InsertAsync(je);

        await _repository.UpdateAsync(sr);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "StockReconciliation", sr.Id, "Submitted",
            sr.CompanyId, sr.ReconciliationNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: sr.TenantId));

        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }

    [Authorize(MyERPPermissions.StockReconciliations.Cancel)]
    public async Task<StockReconciliationDto> CancelAsync(Guid id)
    {
        var sr = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        sr.Cancel();

        // Reverse SLE entries for each item
        foreach (var item in sr.Items)
        {
            var qtyDiff = item.QuantityDifference;
            if (qtyDiff == 0 && item.NewValuationRate == item.CurrentValuationRate)
                continue;

            // Reverse: negative of original diff
            await _valuationService.CreateLedgerEntryAsync(
                sr.CompanyId, item.ItemId, item.WarehouseId,
                sr.PostingDate,
                quantityChange: -qtyDiff,
                incomingRate: item.CurrentValuationRate, // Restore original rate
                voucherType: "StockReconciliation",
                voucherId: sr.Id,
                tenantId: sr.TenantId);

            var valueDiff = item.DifferenceAmount;
            await _binService.ApplyStockMovementAsync(
                item.ItemId, item.WarehouseId,
                -qtyDiff, -valueDiff, sr.TenantId);
        }

        // Reverse the GL entry posted on Submit (recomputed from the same item data, swapped sign —
        // consistent with how the SLE reversal above also recomputes rather than looking up the original).
        var reversalJe = await BuildReconciliationJournalEntryAsync(sr, isReversal: true);
        if (reversalJe != null)
            await _journalEntryRepository.InsertAsync(reversalJe);

        await _repository.UpdateAsync(sr);

        var activityRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo2.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "StockReconciliation", sr.Id, "Cancelled",
            sr.CompanyId, sr.ReconciliationNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: sr.TenantId));

        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }
}

