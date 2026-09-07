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

            // Source warehouse: stock-out (negative qty). Goes through
            // StockValuationService.CreateLedgerEntryAsync (not a direct StockLedgerEntry
            // construction) so FIFO/LIFO items get their StockQueue lot-tracking updated —
            // building the entry directly here left StockQueue permanently empty for any
            // item received via a Stock Entry, causing the NEXT stock-out through the
            // FIFO-aware path (e.g. a Sales/Delivery Note) to compute its balance from an
            // empty queue and throw InsufficientStock despite real stock being available
            // (round-77 fix — found while verifying round-76's DN cancel fix).
            //
            // Use the entry's StockValue field for the Bin update, not StockValueDifference —
            // the 9-arg StockLedgerEntry constructor CreateLedgerEntryAsync calls only sets
            // StockValue (= quantityChange * valuationRate); StockValueDifference is set only by
            // the OTHER constructor overload (with postingTime/voucherType params) and stays 0
            // here, which silently zeroed out every stock-in Bin value until caught live-testing
            // round 78's JobCard fix (GL showed the correct 100, Bin.StockValue showed 0).
            if (item.SourceWarehouseId.HasValue)
            {
                var balance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, item.SourceWarehouseId.Value);
                var sle = await _valuationService.CreateLedgerEntryAsync(
                    stockEntry.CompanyId, item.ItemId, item.SourceWarehouseId.Value,
                    stockEntry.PostingDate, -item.Quantity, balance.ValuationRate,
                    voucherType: "StockEntry", voucherId: stockEntry.Id,
                    tenantId: stockEntry.TenantId, batchId: item.BatchId);

                await _binService.ApplyStockMovementAsync(
                    item.ItemId, item.SourceWarehouseId.Value,
                    -item.Quantity, sle.StockValue, stockEntry.TenantId);
            }

            // Target warehouse: stock-in (positive qty) — same reasoning as above.
            if (item.TargetWarehouseId.HasValue)
            {
                var rate = item.ValuationRate ?? 0;
                var bundleRepo = LazyServiceProvider.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>();
                SerialAndBatchBundle? bundle = null;
                if (bundleRepo != null)
                {
                    bundle = await bundleRepo.FirstOrDefaultAsync(b =>
                        b.VoucherType == "StockEntry" &&
                        b.VoucherId == stockEntry.Id &&
                        b.VoucherDetailId == item.Id &&
                        !b.IsCancelled);
                }

                if (bundle != null && bundle.Entries.Any())
                {
                    decimal totalStockValue = 0m;
                    foreach (var entry in bundle.Entries)
                    {
                        var entryRate = entry.IncomingRate > 0 ? entry.IncomingRate : rate;
                        var sle = await _valuationService.CreateLedgerEntryAsync(
                            stockEntry.CompanyId, item.ItemId, item.TargetWarehouseId.Value,
                            stockEntry.PostingDate, entry.Qty, entryRate,
                            voucherType: "StockEntry", voucherId: stockEntry.Id,
                            tenantId: stockEntry.TenantId, batchId: entry.BatchId);
                        sle.SerialAndBatchBundleId = bundle.Id;
                        sle.VoucherDetailNo = item.Id;
                        await _sleRepository.UpdateAsync(sle);
                        totalStockValue += sle.StockValue;
                    }

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.TargetWarehouseId.Value,
                        item.Quantity, totalStockValue, stockEntry.TenantId);
                }
                else
                {
                    var sle = await _valuationService.CreateLedgerEntryAsync(
                        stockEntry.CompanyId, item.ItemId, item.TargetWarehouseId.Value,
                        stockEntry.PostingDate, item.Quantity, rate,
                        voucherType: "StockEntry", voucherId: stockEntry.Id,
                        tenantId: stockEntry.TenantId, batchId: item.BatchId);

                    await _binService.ApplyStockMovementAsync(
                        item.ItemId, item.TargetWarehouseId.Value,
                        item.Quantity, sle.StockValue, stockEntry.TenantId);
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
