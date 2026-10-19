using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Handles posting of stock transactions:
/// StockEntry → StockLedgerEntry creation → Bin updates.
/// Ensures stock movements are recorded immutably in the ledger
/// and Bin balances stay in sync.
/// </summary>
public class StockPostingService : DomainService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly BinService _binService;
    private readonly StockValuationService _valuationService;

    public StockPostingService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        BinService binService,
        StockValuationService valuationService)
    {
        _sleRepository = sleRepository;
        _companyRepository = companyRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _binService = binService;
        _valuationService = valuationService;
    }

    /// <summary>
    /// Post a stock entry — creates SLE entries for each item line and updates Bins.
    /// Validates stock frozen date before posting.
    /// </summary>
    public async Task PostStockEntryAsync(StockEntry stockEntry)
    {
        await ValidateStockFrozenDateAsync(stockEntry.CompanyId, stockEntry.PostingDate);

        foreach (var item in stockEntry.Items)
        {
            // Skip zero-quantity rows (per ERPNext PR #57980 / commit e4f9c664a8)
            if (item.Quantity <= 0)
                continue;

            // Skip non-stock items (service items don't create SLE entries)
            var itemEntity = await _itemRepository.FindAsync(item.ItemId);
            if (itemEntity != null && !itemEntity.MaintainStock)
                continue;

            // Validate group warehouse restriction
            // Per DO-NOT: "group warehouses cannot receive stock"
            if (item.TargetWarehouseId.HasValue)
            {
                var targetWh = await _warehouseRepository.FindAsync(item.TargetWarehouseId.Value);
                if (targetWh?.IsGroup == true)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouse", targetWh.Name);
                }
            }
            if (item.SourceWarehouseId.HasValue)
            {
                var sourceWh = await _warehouseRepository.FindAsync(item.SourceWarehouseId.Value);
                if (sourceWh?.IsGroup == true)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouse", sourceWh.Name);
                }
            }
        }

        var bundleRepo = LazyServiceProvider?.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>();
        var outwardStockValueByItem = new Dictionary<Guid, decimal>();
        decimal totalRawMaterialOutwardValue = 0m;
        bool hasOutwardBundles = false;

        // Pass 1: Source warehouse deductions (stock-out)
        // Per ERPNext PR #60214 / commit fc069b5516 & f8ac29ea0f: source entries post first to establish consumed cost
        foreach (var item in stockEntry.Items)
        {
            if (item.Quantity <= 0)
                continue;

            var itemEntity = await _itemRepository.FindAsync(item.ItemId);
            if (itemEntity != null && !itemEntity.MaintainStock)
                continue;

            if (item.SourceWarehouseId.HasValue)
            {
                decimal outwardStockValue = 0m;
                SerialAndBatchBundle? outwardBundle = null;

                if (bundleRepo != null)
                {
                    var bundleQuery = await bundleRepo.WithDetailsAsync(b => b.Entries);
                    outwardBundle = bundleQuery.FirstOrDefault(b =>
                        b.VoucherType == "StockEntry" &&
                        b.VoucherId == stockEntry.Id &&
                        b.VoucherDetailId == item.Id &&
                        b.TypeOfTransaction == BundleTransactionType.Outward &&
                        !b.IsCancelled);
                }

                if (outwardBundle != null && outwardBundle.Entries.Any())
                {
                    hasOutwardBundles = true;
                    decimal totalOutwardValue = 0m;
                    foreach (var entry in outwardBundle.Entries)
                    {
                        var entryRate = entry.IncomingRate > 0 ? entry.IncomingRate : (item.ValuationRate ?? 0m);
                        if (entryRate <= 0)
                        {
                            entryRate = await _valuationService.GetValuationRateAsync(
                                item.ItemId, item.SourceWarehouseId.Value, entry.BatchId,
                                asOfDate: stockEntry.PostingDate,
                                excludeVoucherId: stockEntry.Id,
                                postingDateTime: stockEntry.PostingDate);
                            entry.IncomingRate = entryRate;
                        }

                        var sle = await _valuationService.CreateLedgerEntryAsync(
                            stockEntry.CompanyId, item.ItemId, item.SourceWarehouseId.Value,
                            stockEntry.PostingDate, -Math.Abs(entry.Qty), entryRate,
                            voucherType: "StockEntry", voucherId: stockEntry.Id,
                            tenantId: stockEntry.TenantId, batchId: entry.BatchId);
                        sle.SerialAndBatchBundleId = outwardBundle.Id;
                        sle.VoucherDetailNo = item.Id;
                        sle.OutgoingRate = entryRate;
                        await _sleRepository.UpdateAsync(sle);

                        totalOutwardValue += Math.Abs(sle.StockValueDifference != 0 ? sle.StockValueDifference : sle.StockValue);
                    }

                    outwardBundle.Recalculate();
                    await bundleRepo!.UpdateAsync(outwardBundle);

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.SourceWarehouseId.Value,
                        -item.Quantity, -totalOutwardValue, stockEntry.TenantId);

                    outwardStockValue = totalOutwardValue;
                }
                else
                {
                    var rate = item.ValuationRate ?? 0m;
                    if (rate <= 0)
                    {
                        rate = await _valuationService.GetValuationRateAsync(
                            item.ItemId, item.SourceWarehouseId.Value, item.BatchId,
                            asOfDate: stockEntry.PostingDate,
                            excludeVoucherId: stockEntry.Id,
                            postingDateTime: stockEntry.PostingDate);
                    }

                    var sle = await _valuationService.CreateLedgerEntryAsync(
                        stockEntry.CompanyId, item.ItemId, item.SourceWarehouseId.Value,
                        stockEntry.PostingDate, -item.Quantity, rate,
                        voucherType: "StockEntry", voucherId: stockEntry.Id,
                        tenantId: stockEntry.TenantId, batchId: item.BatchId);
                    sle.VoucherDetailNo = item.Id;
                    sle.OutgoingRate = rate;
                    await _sleRepository.UpdateAsync(sle);

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.SourceWarehouseId.Value,
                        -item.Quantity, sle.StockValue, stockEntry.TenantId);

                    outwardStockValue = Math.Abs(sle.StockValueDifference != 0 ? sle.StockValueDifference : sle.StockValue);
                }

                outwardStockValueByItem[item.Id] = outwardStockValue;
                if (!item.TargetWarehouseId.HasValue)
                {
                    totalRawMaterialOutwardValue += outwardStockValue;
                }
            }
        }

        // Pass 2: Target warehouse additions (stock-in)
        // Per ERPNext PR #60214, PR #59881 & PR #60393 (commits 34d5dae561, 77a6486599, edcb4cd99b):
        // Outputs carry exactly the consumed value plus additional costs. Each output row is rounded
        // on its own, so the largest finished good takes what rounding left over.
        var isProducedEntry = (stockEntry.EntryType == StockEntryType.Repack ||
                              (stockEntry.EntryType == StockEntryType.Manufacture && (!stockEntry.WorkOrderId.HasValue || totalRawMaterialOutwardValue > 0)))
                              && totalRawMaterialOutwardValue > 0;

        StockEntryItem? producedAbsorber = null;
        decimal absorberTotalValue = 0m;
        List<StockEntryItem>? producedOutputs = null;

        if (isProducedEntry)
        {
            producedOutputs = stockEntry.Items
                .Where(d => d.TargetWarehouseId.HasValue && !d.SourceWarehouseId.HasValue && d.Quantity > 0)
                .ToList();

            if (producedOutputs.Count > 0 && (!stockEntry.WorkOrderId.HasValue || stockEntry.EntryType == StockEntryType.Repack))
            {
                var finishedItemsQty = stockEntry.GetFinishedItemsQty();
                var costedOutItemsCost = producedOutputs
                    .Where(d => d.SetBasicRateManually && !d.AllowZeroValuationRate)
                    .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

                var netOutgoingCost = Math.Max(0m, totalRawMaterialOutwardValue - costedOutItemsCost);
                var basicRate = finishedItemsQty > 0 ? (netOutgoingCost / finishedItemsQty) : 0m;

                foreach (var output in producedOutputs)
                {
                    if (output.SetBasicRateManually)
                    {
                        // Keep manual rate
                    }
                    else if (output.AllowZeroValuationRate)
                    {
                        output.ValuationRate = 0m;
                    }
                    else
                    {
                        var additionalCostPerUnit = output.Quantity > 0 ? (output.AdditionalCost / output.Quantity) : 0m;
                        output.ValuationRate = Math.Round(basicRate + additionalCostPerUnit, 2);
                    }
                }

                // Absorber is the largest finished good row taking what rounding left over (order by Amount desc, Id asc)
                // Per PR #60393: only entries without manually rated outputs absorb rounding remainder
                var anyManualRate = producedOutputs.Any(d => d.SetBasicRateManually);
                if (!anyManualRate)
                {
                    producedAbsorber = producedOutputs
                        .Where(d => (d.IsFinishedItem || (!d.SourceWarehouseId.HasValue && string.IsNullOrWhiteSpace(d.SecondaryItemType)))
                                    && !d.AllowZeroValuationRate)
                        .OrderByDescending(d => d.Amount > 0 ? d.Amount : (d.Quantity * (d.ValuationRate ?? 0m)))
                        .ThenBy(d => d.Id)
                        .FirstOrDefault();

                    if (producedAbsorber != null)
                    {
                        var totalAdditionalCost = stockEntry.TotalAdditionalCosts > 0
                            ? stockEntry.TotalAdditionalCosts
                            : stockEntry.Items.Sum(d => d.AdditionalCost);
                        var otherOutputsAmount = producedOutputs
                            .Where(d => d.Id != producedAbsorber.Id)
                            .Sum(d => Math.Round(d.Quantity * (d.ValuationRate ?? 0m), 2));
                        absorberTotalValue = Math.Max(0m, totalRawMaterialOutwardValue + totalAdditionalCost - otherOutputsAmount);
                        producedAbsorber.ValuationRate = producedAbsorber.Quantity > 0 ? Math.Round(absorberTotalValue / producedAbsorber.Quantity, 2) : 0m;
                    }
                }

            }
        }


        foreach (var item in stockEntry.Items)
        {
            if (item.Quantity <= 0)
                continue;

            var itemEntity = await _itemRepository.FindAsync(item.ItemId);
            if (itemEntity != null && !itemEntity.MaintainStock)
                continue;

            if (item.TargetWarehouseId.HasValue)
            {
                var rate = item.ValuationRate ?? 0m;
                var isTransfer = item.SourceWarehouseId.HasValue && item.Quantity > 0;

                // Per ERPNext PR #59546 (commit 801a524f80): value a transfer's inward leg at what left the source + additional cost
                if (isTransfer)
                {
                    var outwardStockValue = outwardStockValueByItem.GetValueOrDefault(item.Id, 0m);
                    var totalInwardValue = outwardStockValue + item.AdditionalCost;
                    rate = item.Quantity > 0 ? totalInwardValue / item.Quantity : rate;
                }
                else if (producedOutputs != null && producedOutputs.Any(o => o.Id == item.Id))
                {
                    rate = item.ValuationRate ?? 0m;
                }
                else if (stockEntry.EntryType == StockEntryType.Repack &&
                         (item.IsFinishedItem || !item.SourceWarehouseId.HasValue) &&
                         (!item.SetBasicRateManually || rate <= 0 || hasOutwardBundles))
                {
                    // Per ERPNext PR #60214 & PR #59881 (commit 37f16db9d8):
                    // Repack finished good values at consumed raw materials cost + additional cost,
                    // split across all eligible finished good rows.
                    if (item.AllowZeroValuationRate)
                    {
                        rate = 0m;
                    }
                    else
                    {
                        var finishedItemsQty = stockEntry.GetFinishedItemsQty();
                        var costedOutItemsCost = stockEntry.Items
                            .Where(d => (d.IsFinishedItem || !d.SourceWarehouseId.HasValue) &&
                                        d.TargetWarehouseId.HasValue &&
                                        d.SetBasicRateManually &&
                                        !d.AllowZeroValuationRate)
                            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

                        var netOutgoingCost = Math.Max(0m, totalRawMaterialOutwardValue - costedOutItemsCost);
                        var basicRate = finishedItemsQty > 0 ? (netOutgoingCost / finishedItemsQty) : 0m;
                        var additionalCostPerUnit = item.Quantity > 0 ? (item.AdditionalCost / item.Quantity) : 0m;
                        rate = Math.Round(basicRate + additionalCostPerUnit, 4);
                    }
                    item.ValuationRate = rate;
                }
                else if (stockEntry.EntryType == StockEntryType.Manufacture &&
                         !stockEntry.WorkOrderId.HasValue &&
                         (item.IsFinishedItem || !item.SourceWarehouseId.HasValue) &&
                         (!item.SetBasicRateManually || rate <= 0))
                {
                    // Standalone manufacture without work order derives FG valuation rate from consumed raw materials
                    // split across all eligible finished good rows (PR #59881 / commit 37f16db9d8 & commit 8148dd486b).
                    if (item.AllowZeroValuationRate)
                    {
                        rate = 0m;
                    }
                    else
                    {
                        var finishedItemsQty = stockEntry.GetFinishedItemsQty();
                        var costedOutItemsCost = stockEntry.Items
                            .Where(d => (d.IsFinishedItem || !d.SourceWarehouseId.HasValue) &&
                                        d.TargetWarehouseId.HasValue &&
                                        d.SetBasicRateManually &&
                                        !d.AllowZeroValuationRate)
                            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

                        var netOutgoingCost = Math.Max(0m, totalRawMaterialOutwardValue - costedOutItemsCost);
                        var basicRate = finishedItemsQty > 0 ? (netOutgoingCost / finishedItemsQty) : 0m;
                        var additionalCostPerUnit = item.Quantity > 0 ? (item.AdditionalCost / item.Quantity) : 0m;
                        rate = Math.Round(basicRate + additionalCostPerUnit, 4);
                    }
                    item.ValuationRate = rate;
                }


                SerialAndBatchBundle? outwardBundle = null;
                SerialAndBatchBundle? inwardBundle = null;
                if (bundleRepo != null)
                {
                    var bundleQuery = await bundleRepo.WithDetailsAsync(b => b.Entries);
                    if (isTransfer)
                    {
                        outwardBundle = bundleQuery.FirstOrDefault(b =>
                            b.VoucherType == "StockEntry" &&
                            b.VoucherId == stockEntry.Id &&
                            b.VoucherDetailId == item.Id &&
                            b.TypeOfTransaction == BundleTransactionType.Outward &&
                            !b.IsCancelled);
                    }

                    inwardBundle = bundleQuery.FirstOrDefault(b =>
                        b.VoucherType == "StockEntry" &&
                        b.VoucherId == stockEntry.Id &&
                        b.VoucherDetailId == item.Id &&
                        b.TypeOfTransaction == BundleTransactionType.Inward &&
                        !b.IsCancelled);

                    if (inwardBundle == null)
                    {
                        inwardBundle = bundleQuery.FirstOrDefault(b =>
                            b.VoucherType == "StockEntry" &&
                            b.VoucherId == stockEntry.Id &&
                            b.VoucherDetailId == item.Id &&
                            (outwardBundle == null || b.Id != outwardBundle.Id) &&
                            !b.IsCancelled);
                    }
                }

                if (inwardBundle != null && inwardBundle.Entries.Any())
                {
                    // Per ERPNext PR #59657 (commit 1cf560fbc7) & PR #60629 (commit 6e580e3f18):
                    // Keep each batch's rate through a material transfer, adding proportional additional cost,
                    // falling back to the row rate for any unmatched entry.
                    if (isTransfer && outwardBundle != null && outwardBundle.Entries.Any())
                    {
                        inwardBundle.ApplyTransferRates(outwardBundle, item.AdditionalCost, item.Quantity, rate);
                        await bundleRepo!.UpdateAsync(inwardBundle);
                    }

                    decimal totalStockValue = 0m;
                    foreach (var entry in inwardBundle.Entries)
                    {
                        var entryRate = entry.IncomingRate > 0 ? entry.IncomingRate : rate;
                        if ((stockEntry.EntryType == StockEntryType.Repack || (stockEntry.EntryType == StockEntryType.Manufacture && !stockEntry.WorkOrderId.HasValue)) && (!item.SetBasicRateManually || entry.IncomingRate <= 0))
                        {
                            entryRate = rate;
                            entry.IncomingRate = rate;
                        }

                        var sle = await _valuationService.CreateLedgerEntryAsync(
                            stockEntry.CompanyId, item.ItemId, item.TargetWarehouseId.Value,
                            stockEntry.PostingDate, entry.Qty, entryRate,
                            voucherType: "StockEntry", voucherId: stockEntry.Id,
                            tenantId: stockEntry.TenantId, batchId: entry.BatchId);
                        sle.SerialAndBatchBundleId = inwardBundle.Id;
                        sle.VoucherDetailNo = item.Id;
                        sle.IncomingRate = entryRate;
                        await _sleRepository.UpdateAsync(sle);
                        totalStockValue += sle.StockValue;
                    }

                    if ((stockEntry.EntryType == StockEntryType.Repack || (stockEntry.EntryType == StockEntryType.Manufacture && !stockEntry.WorkOrderId.HasValue)) && !item.SetBasicRateManually)
                    {
                        inwardBundle.Recalculate();
                        await bundleRepo!.UpdateAsync(inwardBundle);
                    }

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.TargetWarehouseId.Value,
                        item.Quantity, totalStockValue, stockEntry.TenantId);
                }
                else if (isTransfer && outwardBundle != null && outwardBundle.Entries.Any())
                {
                    // Transfer with outward bundle but no separate inward bundle:
                    // Propagate each batch from outward bundle to target warehouse preserving batch rate + additional cost.
                    var additionalCostPerUnit = item.Quantity > 0 ? (item.AdditionalCost / item.Quantity) : 0m;
                    decimal totalStockValue = 0m;
                    foreach (var outwardEntry in outwardBundle.Entries)
                    {
                        var entryRate = outwardEntry.IncomingRate + additionalCostPerUnit;
                        var sle = await _valuationService.CreateLedgerEntryAsync(
                            stockEntry.CompanyId, item.ItemId, item.TargetWarehouseId.Value,
                            stockEntry.PostingDate, outwardEntry.Qty, entryRate,
                            voucherType: "StockEntry", voucherId: stockEntry.Id,
                            tenantId: stockEntry.TenantId, batchId: outwardEntry.BatchId);
                        sle.SerialAndBatchBundleId = outwardBundle.Id;
                        sle.VoucherDetailNo = item.Id;
                        sle.IncomingRate = entryRate;
                        await _sleRepository.UpdateAsync(sle);
                        totalStockValue += sle.StockValue;
                    }

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.TargetWarehouseId.Value,
                        item.Quantity, totalStockValue, stockEntry.TenantId);
                }
                else
                {
                    var isAbsorberRow = producedAbsorber != null && item.Id == producedAbsorber.Id;
                    var incomingRateForSle = (isAbsorberRow && producedAbsorber!.Quantity > 0)
                        ? (absorberTotalValue / producedAbsorber.Quantity)
                        : rate;

                    var sle = await _valuationService.CreateLedgerEntryAsync(
                        stockEntry.CompanyId, item.ItemId, item.TargetWarehouseId.Value,
                        stockEntry.PostingDate, item.Quantity, incomingRateForSle,
                        voucherType: "StockEntry", voucherId: stockEntry.Id,
                        tenantId: stockEntry.TenantId, batchId: item.BatchId);
                    sle.VoucherDetailNo = item.Id;
                    sle.IncomingRate = rate;
                    if (isAbsorberRow)
                    {
                        sle.StockValue = absorberTotalValue;
                        sle.StockValueDifference = absorberTotalValue;
                    }
                    await _sleRepository.UpdateAsync(sle);

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.TargetWarehouseId.Value,
                        item.Quantity, isAbsorberRow ? absorberTotalValue : sle.StockValue, stockEntry.TenantId);
                }

            }
        }
    }

    /// <summary>
    /// Reverse a stock posting (for cancellation).
    /// Marks existing SLEs as cancelled and reverses Bin updates, then triggers revaluation.
    /// </summary>
    public async Task ReverseStockEntryAsync(StockEntry stockEntry)
    {
        await ValidateStockFrozenDateAsync(stockEntry.CompanyId, stockEntry.PostingDate);

        var existingSles = await _sleRepository.GetListAsync(
            e => e.VoucherType == "StockEntry" && e.VoucherId == stockEntry.Id);

        if (!existingSles.Any()) return;

        foreach (var sle in existingSles)
        {
            sle.IsCancelled = true;
            
            // Reverse bin stock
            await _binService.ApplyStockMovementAsync(
                sle.ItemId, sle.WarehouseId,
                -sle.QuantityChange, -sle.StockValueDifference, stockEntry.TenantId);
        }

        await _sleRepository.UpdateManyAsync(existingSles);

        // Revaluate from the posting date for all affected item/warehouse combos
        var itemWarehouses = existingSles
            .Select(e => new { e.ItemId, e.WarehouseId })
            .Distinct()
            .ToList();

        foreach (var combo in itemWarehouses)
        {
            await _valuationService.RevaluateFromDateAsync(
                combo.ItemId, combo.WarehouseId, stockEntry.PostingDate);
            await _binService.ResetBinIfNoLedgerEntriesAsync(
                combo.ItemId, combo.WarehouseId, _sleRepository, stockEntry.TenantId);
        }
    }

    /// <summary>
    /// Validates that the posting date is not before the company's stock frozen date.
    /// Blocks stock transactions in frozen periods to protect closed inventory balances.
    /// Per ERPNext: stock_auth_role setting lets authorized users bypass the freeze.
    /// Also supports stock_frozen_upto_days as an alternative to absolute date.
    /// </summary>
    private async Task ValidateStockFrozenDateAsync(Guid companyId, DateTime postingDate, IEnumerable<string>? currentUserRoles = null)
    {
        var company = await _companyRepository.GetAsync(companyId);

        // Determine effective frozen date (absolute date or N days before today)
        DateTime? effectiveFrozenDate = company.StockFrozenUpto;
        if (!effectiveFrozenDate.HasValue && company.StockFrozenUptoDays > 0)
        {
            effectiveFrozenDate = DateTime.UtcNow.Date.AddDays(-company.StockFrozenUptoDays);
        }

        if (effectiveFrozenDate.HasValue && postingDate <= effectiveFrozenDate.Value)
        {
            // Role bypass: users with stock_auth_role can post to frozen periods
            if (!string.IsNullOrWhiteSpace(company.StockAuthRole)
                && currentUserRoles != null
                && currentUserRoles.Contains(company.StockAuthRole, StringComparer.OrdinalIgnoreCase))
            {
                return; // authorized role bypass
            }

            throw new BusinessException(MyERPDomainErrorCodes.StockFrozenPeriod)
                .WithData("frozenUpto", effectiveFrozenDate.Value.ToString("yyyy-MM-dd"))
                .WithData("postingDate", postingDate.ToString("yyyy-MM-dd"));
        }
    }
}
