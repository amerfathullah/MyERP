using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Dtos;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockReconciliations.Default)]
public class StockReconciliationAppService : ApplicationService
{
    private readonly IRepository<StockReconciliation, Guid> _repository;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;

    public StockReconciliationAppService(
        IRepository<StockReconciliation, Guid> repository,
        StockValuationService valuationService,
        BinService binService)
    {
        _repository = repository;
        _valuationService = valuationService;
        _binService = binService;
    }

    public async Task<PagedResultDto<StockReconciliationDto>> GetListAsync(GetStockReconciliationListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(s => (s.ReconciliationNumber ?? "").ToLower().Contains(f)
                                  || (s.Purpose ?? "").ToLower().Contains(f));
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

        await _repository.UpdateAsync(sr);

        var activityRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo2.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "StockReconciliation", sr.Id, "Cancelled",
            sr.CompanyId, sr.ReconciliationNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: sr.TenantId));

        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }
}
