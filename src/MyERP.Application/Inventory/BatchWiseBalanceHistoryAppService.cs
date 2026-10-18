using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Batch-Wise Balance History application service.
/// Provides historical opening, in, out, and closing balances for each batch and warehouse,
/// along with current active stock reservations.
/// Maps to ERPNext stock/report/batch_wise_balance_history/batch_wise_balance_history.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class BatchWiseBalanceHistoryAppService : ApplicationService, IBatchWiseBalanceHistoryAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<StockReservationEntry, Guid> _reservationRepository;

    public BatchWiseBalanceHistoryAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<StockReservationEntry, Guid> reservationRepository)
    {
        _sleRepository = sleRepository;
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _batchRepository = batchRepository;
        _reservationRepository = reservationRepository;
    }

    private class TransactionSlice
    {
        public Guid ItemId { get; set; }
        public Guid WarehouseId { get; set; }
        public Guid BatchId { get; set; }
        public decimal Quantity { get; set; }
        public decimal ValueDiff { get; set; }
        public DateTime PostingDate { get; set; }
    }

    private class BatchBucket
    {
        public decimal OpeningQty { get; set; }
        public decimal OpeningValue { get; set; }
        public decimal InQty { get; set; }
        public decimal InValue { get; set; }
        public decimal OutQty { get; set; }
        public decimal OutValue { get; set; }

        public decimal BalanceQty => OpeningQty + InQty - OutQty;
        public decimal BalanceValue => OpeningValue + InValue - OutValue;
        public decimal ValuationRate => BalanceQty != 0 ? BalanceValue / BalanceQty : 0m;
    }

    public async Task<BatchWiseBalanceHistoryReportDto> GetReportAsync(BatchWiseBalanceHistoryFilterDto input)
    {
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow;
        var fromDate = input.FromDate?.Date;

        // 1. Warehouses
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        whQuery = whQuery.Where(w => !w.IsGroup && w.IsActive);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.Id == input.WarehouseId.Value);
        }
        if (input.WarehouseType.HasValue)
        {
            whQuery = whQuery.Where(w => w.WarehouseType == input.WarehouseType.Value);
        }
        var warehouses = whQuery.ToList();
        var warehousesById = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var targetWarehouseIds = warehouses.Select(w => w.Id).ToHashSet();

        // 2. Items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.HasBatchNo || i.ItemType == ItemType.Goods);
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        var items = itemQuery.ToList();
        var itemsById = items.ToDictionary(i => i.Id, i => i);
        var targetItemIds = items.Select(i => i.Id).ToHashSet();

        // 3. Batches
        var batchQuery = await _batchRepository.GetQueryableAsync();
        batchQuery = batchQuery.Where(b => !b.IsCancelled);
        if (input.BatchId.HasValue && input.BatchId.Value != Guid.Empty)
        {
            batchQuery = batchQuery.Where(b => b.Id == input.BatchId.Value);
        }
        var batches = batchQuery.ToList();
        var batchesById = batches.ToDictionary(b => b.Id, b => b);
        var targetBatchIds = batches.Select(b => b.Id).ToHashSet();

        // 4. Load transactions from SLE and SerialAndBatchBundle
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && s.PostingDate <= toDate && targetWarehouseIds.Contains(s.WarehouseId));
        if (targetItemIds.Count > 0)
        {
            sleQuery = sleQuery.Where(s => targetItemIds.Contains(s.ItemId));
        }
        var sles = sleQuery.ToList();

        // Bundle lookup
        var bundleQuery = await _bundleRepository.WithDetailsAsync(b => b.Entries);
        var bundles = bundleQuery.Where(b => !b.IsCancelled).ToList();
        var bundlesById = bundles.ToDictionary(b => b.Id, b => b);

        var transactions = new List<TransactionSlice>();

        foreach (var sle in sles)
        {
            if (sle.SerialAndBatchBundleId.HasValue &&
                bundlesById.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundle))
            {
                foreach (var entry in bundle.Entries)
                {
                    var bId = entry.BatchId ?? sle.BatchId;
                    if (!bId.HasValue || !targetBatchIds.Contains(bId.Value)) continue;

                    var qty = bundle.TypeOfTransaction == BundleTransactionType.Inward
                        ? Math.Abs(entry.Qty)
                        : -Math.Abs(entry.Qty);

                    transactions.Add(new TransactionSlice
                    {
                        ItemId = sle.ItemId,
                        WarehouseId = sle.WarehouseId,
                        BatchId = bId.Value,
                        Quantity = qty,
                        ValueDiff = entry.StockValueDifference != 0 ? entry.StockValueDifference : sle.StockValueDifference,
                        PostingDate = sle.PostingDate
                    });
                }
            }
            else if (sle.BatchId.HasValue && targetBatchIds.Contains(sle.BatchId.Value))
            {
                transactions.Add(new TransactionSlice
                {
                    ItemId = sle.ItemId,
                    WarehouseId = sle.WarehouseId,
                    BatchId = sle.BatchId.Value,
                    Quantity = sle.QuantityChange,
                    ValueDiff = sle.StockValueDifference,
                    PostingDate = sle.PostingDate
                });
            }
        }

        // 5. Aggregate buckets per (ItemId, WarehouseId, BatchId)
        var groupedTransactions = transactions.GroupBy(t => new { t.ItemId, t.WarehouseId, t.BatchId });
        var buckets = new Dictionary<(Guid ItemId, Guid WarehouseId, Guid BatchId), BatchBucket>();

        foreach (var group in groupedTransactions)
        {
            var bucket = new BatchBucket();
            foreach (var tx in group)
            {
                bool isOpening = fromDate.HasValue && tx.PostingDate.Date < fromDate.Value;
                if (isOpening)
                {
                    bucket.OpeningQty += tx.Quantity;
                    bucket.OpeningValue += tx.ValueDiff;
                }
                else
                {
                    if (tx.Quantity > 0)
                    {
                        bucket.InQty += tx.Quantity;
                        bucket.InValue += tx.ValueDiff;
                    }
                    else if (tx.Quantity < 0)
                    {
                        bucket.OutQty += Math.Abs(tx.Quantity);
                        bucket.OutValue += Math.Abs(tx.ValueDiff);
                    }
                }
            }
            buckets[(group.Key.ItemId, group.Key.WarehouseId, group.Key.BatchId)] = bucket;
        }

        // 6. Active Stock Reservations per (ItemId, WarehouseId, BatchId)
        var reservationQuery = await _reservationRepository.GetQueryableAsync();
        reservationQuery = reservationQuery.Where(r => r.Status != DocumentStatus.Cancelled && targetWarehouseIds.Contains(r.WarehouseId));
        if (targetItemIds.Count > 0)
        {
            reservationQuery = reservationQuery.Where(r => targetItemIds.Contains(r.ItemId));
        }
        var reservations = reservationQuery.ToList();

        var reservedStockMap = new Dictionary<(Guid ItemId, Guid WarehouseId, Guid BatchId), decimal>();
        foreach (var r in reservations)
        {
            if (r.AvailableQty <= 0) continue;

            if (r.BatchId.HasValue && targetBatchIds.Contains(r.BatchId.Value))
            {
                var key = (r.ItemId, r.WarehouseId, r.BatchId.Value);
                reservedStockMap[key] = reservedStockMap.GetValueOrDefault(key, 0m) + r.AvailableQty;
            }
            else if (r.SerialAndBatchBundleId.HasValue &&
                     bundlesById.TryGetValue(r.SerialAndBatchBundleId.Value, out var bundle))
            {
                foreach (var entry in bundle.Entries)
                {
                    if (entry.BatchId.HasValue && targetBatchIds.Contains(entry.BatchId.Value))
                    {
                        var key = (r.ItemId, r.WarehouseId, entry.BatchId.Value);
                        reservedStockMap[key] = reservedStockMap.GetValueOrDefault(key, 0m) + Math.Abs(entry.Qty);
                    }
                }
            }
        }

        // 7. Build Rows
        var allKeys = buckets.Keys.Union(reservedStockMap.Keys).Distinct().ToList();
        var rows = new List<BatchWiseBalanceHistoryRowDto>();

        foreach (var key in allKeys)
        {
            if (!itemsById.TryGetValue(key.ItemId, out var item)) continue;
            warehousesById.TryGetValue(key.WarehouseId, out var whName);
            batchesById.TryGetValue(key.BatchId, out var batch);

            var bucket = buckets.GetValueOrDefault(key, new BatchBucket());
            var reservedQty = reservedStockMap.GetValueOrDefault(key, 0m);

            if (!input.IncludeZeroStockBatches && bucket.BalanceQty == 0 && reservedQty == 0 && bucket.OpeningQty == 0 && bucket.InQty == 0 && bucket.OutQty == 0)
            {
                continue;
            }

            rows.Add(new BatchWiseBalanceHistoryRowDto
            {
                RowId = Guid.NewGuid(),
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                WarehouseId = key.WarehouseId,
                WarehouseName = whName ?? string.Empty,
                BatchId = key.BatchId,
                BatchNo = batch?.BatchNo ?? string.Empty,
                StockUom = item.Uom,
                OpeningQty = bucket.OpeningQty,
                InQty = bucket.InQty,
                OutQty = bucket.OutQty,
                BalanceQty = bucket.BalanceQty,
                ValuationRate = bucket.ValuationRate,
                BalanceValue = bucket.BalanceValue,
                ReservedQty = reservedQty
            });
        }

        // 8. Search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                r.WarehouseName.ToLowerInvariant().Contains(term) ||
                r.BatchNo.ToLowerInvariant().Contains(term) ||
                (r.Description != null && r.Description.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        rows = rows.OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ThenBy(r => r.BatchNo).ToList();

        var totalItems = rows.Select(r => r.ItemId).Distinct().Count();
        var totalBatches = rows.Select(r => r.BatchId).Distinct().Count();
        var totalBalanceQty = rows.Sum(r => r.BalanceQty);
        var totalBalanceValue = rows.Sum(r => r.BalanceValue);
        var totalReservedQty = rows.Sum(r => r.ReservedQty);

        return new BatchWiseBalanceHistoryReportDto
        {
            TotalItemsCount = totalItems,
            TotalBatchesCount = totalBatches,
            TotalBalanceQty = totalBalanceQty,
            TotalBalanceValue = totalBalanceValue,
            TotalReservedQty = totalReservedQty,
            Rows = rows
        };
    }
}
