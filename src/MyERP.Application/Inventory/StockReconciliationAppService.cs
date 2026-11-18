using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Dtos;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

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

    /// <summary>
    /// Per ERPNext stock_reconciliation.py validate_reserved_stock(): a reconciliation that changes
    /// an item/warehouse's on-hand qty must not silently invalidate an active Stock Reservation
    /// Entry's basis. Unlike Pick List's SRE conflict check (scoped to one Sales Order), this looks
    /// up outstanding reservations for the (Item, Warehouse) pairs directly, since a Stock
    /// Reconciliation isn't tied to a single voucher.
    /// </summary>
    private async Task ValidateExpenseAccountAsync(StockReconciliation sr)
    {
        if (!sr.ExpenseAccountId.HasValue) return;

        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var account = await accountRepo.FindAsync(sr.ExpenseAccountId.Value);
        if (account == null) return;

        if (account.AccountSubType == AccountSubType.Stock)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Difference Account '{account.AccountName}' cannot be a Stock account.");
        }

        // Per ERPNext PR #62578 (commit 333273300a): check opening stock reconciliation per company
        var isOpening = sr.Purpose == "Opening Stock";
        if (!isOpening)
        {
            var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var sleQuery = await sleRepo.GetQueryableAsync();
            isOpening = !sleQuery.Any(sle => sle.CompanyId == sr.CompanyId && !sle.IsCancelled);
        }

        if (isOpening && (account.AccountType == AccountType.Revenue || account.AccountType == AccountType.Expense))
        {
            throw new BusinessException(MyERPDomainErrorCodes.OpeningBalanceOnlyBSAccounts)
                .WithData("detail", "Difference Account must be an Asset/Liability type account, since this Stock Reconciliation is an Opening Entry.");
        }
    }

    private async Task ValidateNoActiveReservationsAsync(StockReconciliation sr)
    {
        var affectedPairs = sr.Items
            .Where(item => item.QuantityDifference != 0)
            .Select(item => (item.ItemId, item.WarehouseId))
            .Distinct()
            .ToList();

        if (affectedPairs.Count == 0) return;

        var itemIds = affectedPairs.Select(p => p.ItemId).Distinct().ToArray();
        var warehouseIds = affectedPairs.Select(p => p.WarehouseId).Distinct().ToArray();

        var sreRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockReservationEntry, Guid>>();
        var sreQuery = await sreRepo.GetQueryableAsync();
        var reservedPairs = sreQuery
            .Where(s => itemIds.Contains(s.ItemId)
                && warehouseIds.Contains(s.WarehouseId)
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .Select(s => new { s.ItemId, s.WarehouseId })
            .Distinct()
            .ToList()
            .Select(s => (s.ItemId, s.WarehouseId))
            .ToHashSet();

        if (affectedPairs.Any(reservedPairs.Contains))
        {
            throw new BusinessException(MyERPDomainErrorCodes.StockReconciliationActiveReservation);
        }
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
        // Validate that all rows for a Standard Cost item share the same rate (ERPNext PR #56799)
        await ValidateStandardCostRatesAsync(input);

        // Per ERPNext stock_reconciliation.py validate_data: negative qty/rate are never valid, and
        // the same item+warehouse combination cannot appear twice in one reconciliation (the second
        // row would silently overwrite the SLE the first row just created for that item/warehouse).
        var seenCombinations = new HashSet<(Guid ItemId, Guid WarehouseId)>();
        foreach (var item in input.Items)
        {
            if (item.NewQuantity < 0)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Negative quantity is not allowed.");
            if (item.NewValuationRate < 0)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Negative valuation rate is not allowed.");
            if (item.NewQuantity > 0 && item.NewValuationRate == 0 && !item.AllowZeroValuationRate)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Valuation Rate required for Item {item.ItemId}. Set Allow Zero Valuation Rate if intended.");
            if (!seenCombinations.Add((item.ItemId, item.WarehouseId)))
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Same item and warehouse combination already entered.");
        }

        // Company-restriction check: every other transaction AppService referencing a
        // company-restricted master wires this in.
        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "StockReconciliation", input.CompanyId,
            itemIds: input.Items.Select(i => i.ItemId).Distinct().ToArray(),
            warehouseIds: input.Items.Select(i => i.WarehouseId).Distinct().ToArray());

        var sr = new StockReconciliation(GuidGenerator.Create(), input.CompanyId,
            input.PostingDate, CurrentTenant.Id)
        {
            Purpose = input.Purpose,
            Notes = input.Notes,
            ExpenseAccountId = input.ExpenseAccountId,
            CostCenterId = input.CostCenterId,
        };

        await ValidateExpenseAccountAsync(sr);

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemMap = (await itemRepo.GetQueryableAsync())
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => i.Uom);

        // ERPNext validate_item: end-of-life/disabled items and non-stock items cannot be reconciled.
        await LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>()
            .ValidateItemsForTransactionAsync(itemIds.ToArray());
        var nonStock = (await itemRepo.GetQueryableAsync())
            .Where(i => itemIds.Contains(i.Id) && !i.MaintainStock)
            .Select(i => i.ItemName)
            .FirstOrDefault();
        if (nonStock != null)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Item {nonStock} is not a stock Item.");

        foreach (var item in input.Items)
        {
            var uom = !string.IsNullOrWhiteSpace(item.StockUom)
                ? item.StockUom
                : (itemMap.TryGetValue(item.ItemId, out var defaultUom) ? defaultUom : null);

            sr.AddItem(item.ItemId, item.WarehouseId, item.NewQuantity, item.NewValuationRate,
                item.CurrentQuantity, item.CurrentValuationRate, uom,
                item.SerialAndBatchBundleId, item.CurrentSerialAndBatchBundleId,
                item.AllowZeroValuationRate);
        }

        await ValidateSerialAndBatchBundlesAsync(sr);
        await _repository.InsertAsync(sr);
        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }

    private async Task ValidateSerialAndBatchBundlesAsync(StockReconciliation sr)
    {
        var sabbValidator = LazyServiceProvider?.LazyGetService<SerialBatchBundleValidationService>();
        if (sabbValidator == null)
        {
            return;
        }

        await sabbValidator.ValidateBundleCompanyAsync(
            sr.CompanyId,
            sr.Items.SelectMany(i => new[] { i.SerialAndBatchBundleId, i.CurrentSerialAndBatchBundleId }),
            expectedVoucherType: "StockReconciliation",
            expectedVoucherId: sr.Id);
    }

    private async Task ValidateStandardCostRatesAsync(CreateStockReconciliationDto input)
    {
        if (input.Items == null || !input.Items.Any()) return;

        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var standardCostItems = itemQuery
            .Where(i => itemIds.Contains(i.Id) && i.ValuationMethod == ValuationMethod.StandardCost)
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToList();

        if (!standardCostItems.Any()) return;

        var standardCostMap = standardCostItems.ToDictionary(i => i.Id, i => i);
        var ratesByItem = new Dictionary<Guid, decimal>();

        foreach (var item in input.Items)
        {
            if (!standardCostMap.ContainsKey(item.ItemId)) continue;

            if (item.NewValuationRate > 0)
            {
                var rate = Math.Round(item.NewValuationRate, 4);
                if (ratesByItem.TryGetValue(item.ItemId, out var existingRate) && existingRate != rate)
                {
                    var itemInfo = standardCostMap[item.ItemId];
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Valuation Rate for Item {itemInfo.ItemName ?? itemInfo.ItemCode} must be the same across all rows, as it is the item's company-wide Standard Cost.");
                }
                ratesByItem[item.ItemId] = rate;
            }
        }
    }

    [Authorize(MyERPPermissions.StockReconciliations.Submit)]
    public async Task<StockReconciliationDto> SubmitAsync(Guid id)
    {
        var sr = (await _repository.WithDetailsAsync()).First(s => s.Id == id);

        // Validate posting period is not frozen/closed before creating SLE entries
        var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(sr.CompanyId, sr.PostingDate, "StockReconciliation");

        await ValidateSerialAndBatchBundlesAsync(sr);
        await ValidateNoActiveReservationsAsync(sr);
        await ValidateExpenseAccountAsync(sr);

        sr.Submit();

        // Create SLE entries for each item adjustment (absolute qty set, not delta)
        // Stock Reconciliation uses ABSOLUTE quantity — the SLE entry sets qty to new level
        var entriesCreated = 0;
        foreach (var item in sr.Items)
        {
            var qtyDiff = item.QuantityDifference; // NewQty - CurrentQty
            var prevSle = await _valuationService.GetPreviousSleAsync(item.ItemId, item.WarehouseId, sr.PostingDate);
            var prevQty = prevSle?.BalanceQuantity ?? item.CurrentQuantity;
            var prevVal = prevSle?.BalanceValue ?? (item.CurrentQuantity * item.CurrentValuationRate);

            var isStrandedValueAdjustment = item.NewQuantity == 0 && prevQty == 0 && prevVal != 0;
            if (qtyDiff == 0 && item.NewValuationRate == item.CurrentValuationRate && !isStrandedValueAdjustment)
                continue; // No change needed

            entriesCreated++;

            var isAdjustment = isStrandedValueAdjustment || qtyDiff == 0;

            // Create SLE with the difference quantity and new rate
            await _valuationService.CreateLedgerEntryAsync(
                sr.CompanyId, item.ItemId, item.WarehouseId,
                sr.PostingDate,
                quantityChange: qtyDiff,
                incomingRate: item.NewValuationRate,
                voucherType: "StockReconciliation",
                voucherId: sr.Id,
                tenantId: sr.TenantId,
                serialAndBatchBundleId: item.SerialAndBatchBundleId ?? item.CurrentSerialAndBatchBundleId,
                isAdjustmentEntry: isAdjustment);

            // Update Bin with the difference
            var valueDiff = item.DifferenceAmount;
            await _binService.ApplyStockMovementAsync(
                item.ItemId, item.WarehouseId,
                qtyDiff, valueDiff, sr.TenantId);
        }

        // Validate that at least one SLE was created (ERPNext PR #47292 / commit 3d36d0b1df)
        if (entriesCreated == 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "No stock ledger entries were created. Please set the quantity or valuation rate for the items properly and try again.");
        }

        // Post GL: DR/CR the affected warehouses' Stock accounts, balanced against the Difference/
        // Expense Account. Never previously wired — physical count adjustments only ever touched
        // Stock Ledger + Bin, GL and inventory value permanently diverged.
        var je = await BuildReconciliationJournalEntryAsync(sr, isReversal: false);
        if (je != null)
            await _journalEntryRepository.InsertAsync(je);

        // Submit linked Serial and Batch Bundles if draft (ERPNext PR #47457 / commit ad25636afb)
        var bundleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
        foreach (var item in sr.Items)
        {
            var bundleIds = new[] { item.CurrentSerialAndBatchBundleId, item.SerialAndBatchBundleId }
                .Where(bId => bId.HasValue)
                .Select(bId => bId!.Value)
                .Distinct();

            foreach (var bId in bundleIds)
            {
                var bundle = await bundleRepo.FindAsync(bId);
                if (bundle != null && bundle.Status == DocumentStatus.Draft)
                {
                    bundle.Submit(sr.ReconciliationNumber);
                    await bundleRepo.UpdateAsync(bundle);
                }
            }
        }

        // Per ERPNext #65668 (validate_future_batch_quantity): block a backdated reconciliation from
        // lowering a batch below what later entries already consumed (even if warehouse/global total stays >= 0).
        await ValidateFutureBatchQuantityAsync(sr, bundleRepo);

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

        await ValidateNoActiveReservationsAsync(sr);

        sr.Cancel();

        // Per ERPNext PR #59530 / commit e13c2d99fe: cancel existing reconciliation SLEs so that
        // recreating stock ledgers does not compare against still-active entries of the cancelled voucher
        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var existingSles = await sleRepo.GetListAsync(
            e => e.VoucherType == "StockReconciliation" && e.VoucherId == sr.Id && !e.IsCancelled);
        foreach (var sle in existingSles)
        {
            sle.IsCancelled = true;
        }
        if (existingSles.Any())
        {
            await sleRepo.UpdateManyAsync(existingSles);
        }

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
                tenantId: sr.TenantId,
                isAdjustmentEntry: qtyDiff == 0);

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

        // Cancel linked Serial and Batch Bundles if submitted
        var bundleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
        foreach (var item in sr.Items)
        {
            var bundleIds = new[] { item.CurrentSerialAndBatchBundleId, item.SerialAndBatchBundleId }
                .Where(bId => bId.HasValue)
                .Select(bId => bId!.Value)
                .Distinct();

            foreach (var bId in bundleIds)
            {
                var bundle = await bundleRepo.FindAsync(bId);
                if (bundle != null && bundle.Status == DocumentStatus.Submitted)
                {
                    bundle.Cancel();
                    await bundleRepo.UpdateAsync(bundle);
                }
            }
        }

        await _repository.UpdateAsync(sr);

        var activityRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo2.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "StockReconciliation", sr.Id, "Cancelled",
            sr.CompanyId, sr.ReconciliationNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: sr.TenantId));

        return ObjectMapper.Map<StockReconciliation, StockReconciliationDto>(sr);
    }

    /// <summary>
    /// Fetches items for stock reconciliation for the specified warehouse as of posting date.
    /// Per ERPNext stock_reconciliation.py get_items() and commit b132e3f22a / PR #60263:
    /// Enforces warehouse & item company validation, expands group warehouse to descendant leaf warehouses,
    /// and returns batch-wise stock balance with individual batch valuation rate for batched items.
    /// </summary>
    public async Task<List<StockReconciliationItemPreviewDto>> GetItemsForReconciliationAsync(GetStockReconciliationItemsInputDto input)
    {
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouse = await whRepo.FindAsync(input.WarehouseId);
        if (warehouse == null)
            throw new EntityNotFoundException(typeof(Warehouse), input.WarehouseId);

        if (warehouse.CompanyId != input.CompanyId)
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("warehouseCompany", warehouse.CompanyId)
                .WithData("reconciliationCompany", input.CompanyId);

        List<Warehouse> targetWarehouses;
        if (warehouse.IsGroup)
        {
            var allWhs = (await whRepo.GetQueryableAsync())
                .Where(w => w.CompanyId == input.CompanyId && w.IsActive)
                .ToList();

            var leafWhs = new List<Warehouse>();
            void CollectLeaves(Guid parentId)
            {
                var children = allWhs.Where(w => w.ParentWarehouseId == parentId).ToList();
                foreach (var child in children)
                {
                    if (child.IsGroup)
                        CollectLeaves(child.Id);
                    else
                        leafWhs.Add(child);
                }
            }
            CollectLeaves(warehouse.Id);
            targetWarehouses = leafWhs;
        }
        else
        {
            targetWarehouses = new List<Warehouse> { warehouse };
        }

        if (targetWarehouses.Count == 0)
            return new List<StockReconciliationItemPreviewDto>();

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var itemQ = await itemRepo.GetQueryableAsync();
        var itemsQuery = itemQ.Where(i => i.CompanyId == input.CompanyId && i.MaintainStock && i.IsActive);
        if (input.ItemId.HasValue)
        {
            var singleItem = await itemRepo.FindAsync(input.ItemId.Value);
            if (singleItem == null)
                throw new EntityNotFoundException(typeof(Item), input.ItemId.Value);

            if (singleItem.CompanyId != input.CompanyId)
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("itemCompany", singleItem.CompanyId)
                    .WithData("reconciliationCompany", input.CompanyId);

            itemsQuery = itemsQuery.Where(i => i.Id == input.ItemId.Value);
        }
        var items = itemsQuery.OrderBy(i => i.ItemCode).ToList();
        if (items.Count == 0)
            return new List<StockReconciliationItemPreviewDto>();

        var batchedItemIds = items.Where(i => i.HasBatchNo).Select(i => i.Id).ToHashSet();
        var targetWarehouseIds = targetWarehouses.Select(w => w.Id).ToHashSet();

        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQ = await sleRepo.GetQueryableAsync();

        var batchSles = sleQ
            .Where(s => s.CompanyId == input.CompanyId
                && targetWarehouseIds.Contains(s.WarehouseId)
                && batchedItemIds.Contains(s.ItemId)
                && s.BatchId != null
                && s.PostingDate <= input.PostingDate
                && !s.IsCancelled)
            .ToList();

        var batchIds = batchSles.Select(s => s.BatchId!.Value).Distinct().ToList();
        var batchRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Batch, Guid>>();
        var batchQ = await batchRepo.GetQueryableAsync();
        var batches = batchQ.Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo })
            .ToDictionary(b => b.Id);

        var batchGroups = batchSles
            .GroupBy(s => new { s.ItemId, s.WarehouseId, BatchId = s.BatchId!.Value })
            .ToDictionary(g => g.Key, g => new
            {
                Qty = g.Sum(s => s.QuantityChange),
                StockValue = g.Sum(s => s.StockValueDifference != 0 ? s.StockValueDifference : s.StockValue),
                LastValuationRate = g.OrderByDescending(s => s.PostingDateTime).ThenByDescending(s => s.CreationTime).Select(s => s.ValuationRate).FirstOrDefault()
            });

        var result = new List<StockReconciliationItemPreviewDto>();

        foreach (var targetWh in targetWarehouses)
        {
            foreach (var item in items)
            {
                if (item.HasBatchNo)
                {
                    var itemBatches = batchGroups.Where(kv => kv.Key.ItemId == item.Id && kv.Key.WarehouseId == targetWh.Id).ToList();
                    if (itemBatches.Count > 0)
                    {
                        foreach (var (key, data) in itemBatches)
                        {
                            if (input.IgnoreEmptyStock && data.Qty == 0)
                                continue;

                            var batchInfo = batches.GetValueOrDefault(key.BatchId);
                            // Per ERPNext commit b132e3f22a: fetch batch-wise valuation rate
                            decimal batchRate;
                            if (data.Qty > 0 && data.StockValue > 0)
                            {
                                batchRate = Math.Round(data.StockValue / data.Qty, 4);
                            }
                            else
                            {
                                batchRate = data.LastValuationRate > 0 ? data.LastValuationRate : (item.StandardBuyingPrice ?? 0);
                            }

                            result.Add(new StockReconciliationItemPreviewDto
                            {
                                ItemId = item.Id,
                                ItemCode = item.ItemCode,
                                ItemName = item.ItemName,
                                WarehouseId = targetWh.Id,
                                StockUom = item.Uom,
                                CurrentQuantity = Math.Round(data.Qty, 4),
                                CurrentValuationRate = Math.Round(batchRate, 4),
                                BatchId = key.BatchId,
                                BatchNo = batchInfo?.BatchNo,
                                HasBatchNo = true,
                                HasSerialNo = item.HasSerialNo
                            });
                        }
                    }
                    else if (!input.IgnoreEmptyStock)
                    {
                        result.Add(new StockReconciliationItemPreviewDto
                        {
                            ItemId = item.Id,
                            ItemCode = item.ItemCode,
                            ItemName = item.ItemName,
                            WarehouseId = targetWh.Id,
                            StockUom = item.Uom,
                            CurrentQuantity = 0,
                            CurrentValuationRate = item.StandardBuyingPrice ?? 0,
                            BatchId = null,
                            BatchNo = null,
                            HasBatchNo = true,
                            HasSerialNo = item.HasSerialNo
                        });
                    }
                }
                else
                {
                    var prevSle = await _valuationService.GetPreviousSleAsync(item.Id, targetWh.Id, input.PostingDate);
                    var qty = prevSle?.BalanceQuantity ?? 0m;
                    var rate = prevSle?.ValuationRate ?? (item.StandardBuyingPrice ?? 0m);

                    if (input.IgnoreEmptyStock && qty == 0)
                        continue;

                    result.Add(new StockReconciliationItemPreviewDto
                    {
                        ItemId = item.Id,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        WarehouseId = targetWh.Id,
                        StockUom = item.Uom,
                        CurrentQuantity = Math.Round(qty, 4),
                        CurrentValuationRate = Math.Round(rate, 4),
                        BatchId = null,
                        BatchNo = null,
                        HasBatchNo = false,
                        HasSerialNo = item.HasSerialNo
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Fetches single-item stock balance and valuation rate for stock reconciliation row lookup.
    /// Per ERPNext stock_reconciliation.py get_stock_balance_for() / PR #60263:
    /// Enforces warehouse & item company validation, prevents querying group warehouses directly.
    /// </summary>
    public async Task<StockBalanceForPreviewDto> GetStockBalanceForAsync(GetStockBalanceForInputDto input)
    {
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouse = await whRepo.FindAsync(input.WarehouseId);
        if (warehouse == null)
            throw new EntityNotFoundException(typeof(Warehouse), input.WarehouseId);

        if (warehouse.CompanyId != input.CompanyId)
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("warehouseCompany", warehouse.CompanyId)
                .WithData("reconciliationCompany", input.CompanyId);

        if (warehouse.IsGroup)
            throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                .WithData("warehouseName", warehouse.Name);

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var item = await itemRepo.FindAsync(input.ItemId);
        if (item == null)
            throw new EntityNotFoundException(typeof(Item), input.ItemId);

        if (item.CompanyId != input.CompanyId)
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("itemCompany", item.CompanyId)
                .WithData("reconciliationCompany", input.CompanyId);

        if (item.HasBatchNo && input.BatchId.HasValue)
        {
            var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var sleQ = await sleRepo.GetQueryableAsync();
            var batchSles = sleQ
                .Where(s => s.CompanyId == input.CompanyId
                    && s.WarehouseId == input.WarehouseId
                    && s.ItemId == input.ItemId
                    && s.BatchId == input.BatchId.Value
                    && s.PostingDate <= input.PostingDate
                    && !s.IsCancelled)
                .ToList();

            var qty = batchSles.Sum(s => s.QuantityChange);
            var stockVal = batchSles.Sum(s => s.StockValueDifference != 0 ? s.StockValueDifference : s.StockValue);
            var lastValRate = batchSles.OrderByDescending(s => s.PostingDateTime).ThenByDescending(s => s.CreationTime).Select(s => s.ValuationRate).FirstOrDefault();

            decimal rate;
            if (qty > 0 && stockVal > 0)
                rate = Math.Round(stockVal / qty, 4);
            else
                rate = lastValRate > 0 ? lastValRate : (item.StandardBuyingPrice ?? 0);

            var batchRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Batch, Guid>>();
            var batch = await batchRepo.FindAsync(input.BatchId.Value);

            return new StockBalanceForPreviewDto
            {
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                CurrentQuantity = Math.Round(qty, 4),
                CurrentValuationRate = Math.Round(rate, 4),
                BatchId = input.BatchId.Value,
                BatchNo = batch?.BatchNo,
                StockUom = item.Uom
            };
        }
        else
        {
            var prevSle = await _valuationService.GetPreviousSleAsync(item.Id, input.WarehouseId, input.PostingDate);
            var qty = prevSle?.BalanceQuantity ?? 0m;
            var rate = prevSle?.ValuationRate ?? (item.StandardBuyingPrice ?? 0m);

            return new StockBalanceForPreviewDto
            {
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                CurrentQuantity = Math.Round(qty, 4),
                CurrentValuationRate = Math.Round(rate, 4),
                StockUom = item.Uom
            };
        }
    }

    /// <summary>
    /// Validates that lowering batch quantities in a backdated reconciliation does not cause any
    /// affected batch to go negative in later stock ledger or bundle movements.
    /// Per ERPNext PR #65668 (validate_future_batch_quantity).
    /// </summary>
    private async Task ValidateFutureBatchQuantityAsync(
        StockReconciliation sr,
        IRepository<SerialAndBatchBundle, Guid> bundleRepo)
    {
        var referencedBundleIds = sr.Items
            .SelectMany(i => new[] { i.CurrentSerialAndBatchBundleId, i.SerialAndBatchBundleId })
            .Where(bId => bId.HasValue)
            .Select(bId => bId!.Value)
            .Distinct()
            .ToList();

        if (referencedBundleIds.Count == 0)
            return;

        var settingProvider = LazyServiceProvider.LazyGetService<Volo.Abp.Settings.ISettingProvider>();
        if (settingProvider != null && await settingProvider.IsTrueAsync(Settings.MyERPSettings.Stock.AllowNegativeStock))
            return;

        var bundleQueryable = await bundleRepo.WithDetailsAsync(b => b.Entries);
        var referencedBundles = bundleQueryable
            .Where(b => referencedBundleIds.Contains(b.Id))
            .ToList();

        var targetKeys = referencedBundles
            .SelectMany(b => b.Entries
                .Where(e => e.BatchId.HasValue)
                .Select(e => (b.ItemId, b.WarehouseId, BatchId: e.BatchId!.Value)))
            .Distinct()
            .ToList();

        if (targetKeys.Count == 0)
            return;

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var targetItemIds = targetKeys.Select(k => k.ItemId).Distinct().ToList();
        var itemMap = (await itemRepo.GetQueryableAsync())
            .Where(i => targetItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.HasBatchNo, i.AllowNegativeStock })
            .ToDictionary(i => i.Id);

        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQueryable = await sleRepo.GetQueryableAsync();

        foreach (var itemWhGroup in targetKeys.GroupBy(k => (k.ItemId, k.WarehouseId)))
        {
            var itemId = itemWhGroup.Key.ItemId;
            var warehouseId = itemWhGroup.Key.WarehouseId;
            if (!itemMap.TryGetValue(itemId, out var itemInfo) || !itemInfo.HasBatchNo || itemInfo.AllowNegativeStock)
                continue;

            var targetBatchIds = itemWhGroup.Select(k => k.BatchId).ToHashSet();

            // 1. Direct batch SLEs (those without a linked SABB, to avoid double counting)
            var directSles = sleQueryable
                .Where(s => s.ItemId == itemId
                    && s.WarehouseId == warehouseId
                    && !s.IsCancelled
                    && s.BatchId != null
                    && s.SerialAndBatchBundleId == null
                    && targetBatchIds.Contains(s.BatchId.Value))
                .Select(s => new { BatchId = s.BatchId!.Value, s.PostingDate, s.CreationTime, Qty = s.QuantityChange })
                .ToList();

            // 2. Submitted Serial and Batch Bundle movements for this item & warehouse
            // Include bundles referenced by this reconciliation even before UoW flush updates Status in SQL.
            var bundles = bundleQueryable
                .Where(b => b.ItemId == itemId
                    && b.WarehouseId == warehouseId
                    && !b.IsCancelled
                    && (b.Status == DocumentStatus.Submitted || referencedBundleIds.Contains(b.Id)))
                .ToList();

            var movementsByBatch = new Dictionary<Guid, List<(DateTime PostingDate, DateTime CreationTime, decimal Qty, bool IsCurrentSr)>>();
            foreach (var sle in directSles)
            {
                if (!movementsByBatch.TryGetValue(sle.BatchId, out var list))
                {
                    list = new List<(DateTime, DateTime, decimal, bool)>();
                    movementsByBatch[sle.BatchId] = list;
                }
                list.Add((sle.PostingDate, sle.CreationTime, sle.Qty, false));
            }

            foreach (var bundle in bundles)
            {
                var isCurrentSr = referencedBundleIds.Contains(bundle.Id);
                var sign = bundle.TypeOfTransaction == BundleTransactionType.Outward ? -1m : 1m;
                foreach (var entry in bundle.Entries)
                {
                    if (!entry.BatchId.HasValue || !targetBatchIds.Contains(entry.BatchId.Value))
                        continue;

                    if (!movementsByBatch.TryGetValue(entry.BatchId.Value, out var list))
                    {
                        list = new List<(DateTime, DateTime, decimal, bool)>();
                        movementsByBatch[entry.BatchId.Value] = list;
                    }
                    list.Add((bundle.PostingDate, bundle.CreationTime, sign * Math.Abs(entry.Qty), isCurrentSr));
                }
            }

            foreach (var batchId in targetBatchIds)
            {
                if (!movementsByBatch.TryGetValue(batchId, out var movements))
                    continue;

                var priorBalance = movements
                    .Where(m => m.PostingDate < sr.PostingDate)
                    .Sum(m => m.Qty);

                // Net all movements belonging to this reconciliation at sr.PostingDate simultaneously
                var srNetChange = movements
                    .Where(m => m.IsCurrentSr && m.PostingDate >= sr.PostingDate)
                    .Sum(m => m.Qty);

                var balanceAfterSr = priorBalance + srNetChange;
                if (Math.Round(balanceAfterSr, 4) < 0m)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                        .WithData("itemId", itemId)
                        .WithData("warehouseId", warehouseId)
                        .WithData("batchId", batchId)
                        .WithData("available", Math.Round(balanceAfterSr, 4));
                }

                var running = balanceAfterSr;
                foreach (var future in movements
                    .Where(m => !m.IsCurrentSr && m.PostingDate >= sr.PostingDate)
                    .OrderBy(m => m.PostingDate)
                    .ThenBy(m => m.CreationTime))
                {
                    running += future.Qty;
                    if (Math.Round(running, 4) < 0m)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                            .WithData("itemId", itemId)
                            .WithData("warehouseId", warehouseId)
                            .WithData("batchId", batchId)
                            .WithData("available", Math.Round(running, 4));
                    }
                }
            }
        }
    }
}


