using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Serial and Batch-Wise Stock Balance — Stock Balance with a row per batch under each item row,
/// and the serial numbers currently in stock.
/// Maps to ERPNext stock/report/serial_and_batch_wise_stock_balance/serial_and_batch_wise_stock_balance.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class SerialAndBatchWiseStockBalanceAppService : ApplicationService, ISerialAndBatchWiseStockBalanceAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;

    public SerialAndBatchWiseStockBalanceAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<SerialNo, Guid> serialNoRepository)
    {
        _sleRepository = sleRepository;
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _batchRepository = batchRepository;
        _serialNoRepository = serialNoRepository;
    }

    private class TransactionSlice
    {
        public Guid ItemId { get; set; }
        public Guid WarehouseId { get; set; }
        public Guid? BatchId { get; set; }
        public string? SerialNo { get; set; }
        public decimal Quantity { get; set; }
        public decimal ValueDiff { get; set; }
        public DateTime PostingDate { get; set; }
    }

    private class BalanceBucket
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

    public async Task<SerialAndBatchWiseStockBalanceReportDto> GetReportAsync(SerialAndBatchWiseStockBalanceFilterDto input)
    {
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow;
        var fromDate = input.FromDate?.Date;

        // 1. Preload master entities
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.HasBatchNo || i.HasSerialNo);

        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        var items = itemQuery.ToList();
        var itemsById = items.ToDictionary(i => i.Id, i => i);

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.Id == input.WarehouseId.Value);
        }
        var warehouses = whQuery.ToList();
        var warehousesById = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var targetWarehouseIds = warehouses.Select(w => w.Id).ToHashSet();

        var batches = await _batchRepository.GetListAsync();
        var batchesById = batches.ToDictionary(b => b.Id, b => b.BatchNo);

        var serialNos = await _serialNoRepository.GetListAsync();
        var serialNosById = serialNos.ToDictionary(s => s.Id, s => s.SerialNumber);

        // 2. Load transactions from SLE and SerialAndBatchBundle
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && s.PostingDate <= toDate && targetWarehouseIds.Contains(s.WarehouseId));
        if (items.Count > 0)
        {
            var itemIds = items.Select(i => i.Id).ToList();
            sleQuery = sleQuery.Where(s => itemIds.Contains(s.ItemId));
        }
        var sles = sleQuery.ToList();

        // Bundle lookup
        var bundleQuery = await _bundleRepository.WithDetailsAsync(b => b.Entries);
        var bundles = bundleQuery.Where(b => !b.IsCancelled).ToList();
        var bundlesById = bundles.ToDictionary(b => b.Id, b => b);

        var transactions = new List<TransactionSlice>();

        foreach (var sle in sles)
        {
            string? sleSerialNo = sle.SerialNoId.HasValue ? serialNosById.GetValueOrDefault(sle.SerialNoId.Value) : null;

            if (sle.SerialAndBatchBundleId.HasValue &&
                bundlesById.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundle))
            {
                foreach (var entry in bundle.Entries)
                {
                    var qty = bundle.TypeOfTransaction == BundleTransactionType.Inward
                        ? Math.Abs(entry.Qty)
                        : -Math.Abs(entry.Qty);

                    transactions.Add(new TransactionSlice
                    {
                        ItemId = sle.ItemId,
                        WarehouseId = sle.WarehouseId,
                        BatchId = entry.BatchId ?? sle.BatchId,
                        SerialNo = entry.SerialNo ?? sleSerialNo,
                        Quantity = qty,
                        ValueDiff = entry.StockValueDifference != 0 ? entry.StockValueDifference : sle.StockValueDifference,
                        PostingDate = sle.PostingDate
                    });
                }
            }
            else
            {
                transactions.Add(new TransactionSlice
                {
                    ItemId = sle.ItemId,
                    WarehouseId = sle.WarehouseId,
                    BatchId = sle.BatchId,
                    SerialNo = sleSerialNo,
                    Quantity = sle.QuantityChange,
                    ValueDiff = sle.StockValueDifference,
                    PostingDate = sle.PostingDate
                });
            }
        }

        // 3. Aggregate balances per (ItemId, WarehouseId) and per (ItemId, WarehouseId, BatchId)
        var itemWhGroups = transactions.GroupBy(t => new { t.ItemId, t.WarehouseId });
        var rows = new List<SerialAndBatchWiseStockBalanceRowDto>();

        foreach (var group in itemWhGroups)
        {
            if (!itemsById.TryGetValue(group.Key.ItemId, out var item)) continue;
            warehousesById.TryGetValue(group.Key.WarehouseId, out var whName);

            var itemBucket = new BalanceBucket();
            var batchBuckets = new Dictionary<Guid, BalanceBucket>();
            var serialCounters = new Dictionary<string, int>();
            var batchSerialCounters = new Dictionary<Guid, Dictionary<string, int>>();

            foreach (var tx in group)
            {
                bool isOpening = fromDate.HasValue && tx.PostingDate.Date < fromDate.Value;

                // Update item-level bucket
                if (isOpening)
                {
                    itemBucket.OpeningQty += tx.Quantity;
                    itemBucket.OpeningValue += tx.ValueDiff;
                }
                else
                {
                    if (tx.Quantity > 0)
                    {
                        itemBucket.InQty += tx.Quantity;
                        itemBucket.InValue += tx.ValueDiff;
                    }
                    else if (tx.Quantity < 0)
                    {
                        itemBucket.OutQty += Math.Abs(tx.Quantity);
                        itemBucket.OutValue += Math.Abs(tx.ValueDiff);
                    }
                }

                // Update batch-level bucket
                if (tx.BatchId.HasValue)
                {
                    var bId = tx.BatchId.Value;
                    if (!batchBuckets.TryGetValue(bId, out var bBucket))
                    {
                        bBucket = new BalanceBucket();
                        batchBuckets[bId] = bBucket;
                    }

                    if (isOpening)
                    {
                        bBucket.OpeningQty += tx.Quantity;
                        bBucket.OpeningValue += tx.ValueDiff;
                    }
                    else
                    {
                        if (tx.Quantity > 0)
                        {
                            bBucket.InQty += tx.Quantity;
                            bBucket.InValue += tx.ValueDiff;
                        }
                        else if (tx.Quantity < 0)
                        {
                            bBucket.OutQty += Math.Abs(tx.Quantity);
                            bBucket.OutValue += Math.Abs(tx.ValueDiff);
                        }
                    }

                    // Serial within batch
                    if (!string.IsNullOrWhiteSpace(tx.SerialNo))
                    {
                        if (!batchSerialCounters.TryGetValue(bId, out var bSerials))
                        {
                            bSerials = new Dictionary<string, int>();
                            batchSerialCounters[bId] = bSerials;
                        }
                        var delta = tx.Quantity > 0 ? 1 : -1;
                        bSerials[tx.SerialNo] = bSerials.GetValueOrDefault(tx.SerialNo, 0) + delta;
                    }
                }

                // Global serial for item
                if (!string.IsNullOrWhiteSpace(tx.SerialNo))
                {
                    var delta = tx.Quantity > 0 ? 1 : -1;
                    serialCounters[tx.SerialNo] = serialCounters.GetValueOrDefault(tx.SerialNo, 0) + delta;
                }
            }

            // Check if zero stock should be hidden
            if (!input.IncludeZeroStockItems && itemBucket.BalanceQty == 0)
            {
                continue;
            }

            // Build serial list in stock
            var inStockSerials = serialCounters.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key).OrderBy(s => s).ToList();

            // Build child batch rows
            var childBatchRows = new List<SerialAndBatchWiseStockBalanceRowDto>();
            foreach (var kvp in batchBuckets.OrderBy(b => batchesById.GetValueOrDefault(b.Key, string.Empty)))
            {
                var bId = kvp.Key;
                var bBucket = kvp.Value;
                if (!input.IncludeZeroStockItems && bBucket.BalanceQty == 0) continue;

                var bSerials = batchSerialCounters.GetValueOrDefault(bId)?
                    .Where(k => k.Value > 0).Select(k => k.Key).OrderBy(s => s).ToList() ?? new List<string>();

                batchesById.TryGetValue(bId, out var bNo);

                childBatchRows.Add(new SerialAndBatchWiseStockBalanceRowDto
                {
                    RowId = Guid.NewGuid(),
                    Indent = 1,
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    WarehouseId = group.Key.WarehouseId,
                    WarehouseName = whName ?? string.Empty,
                    StockUom = item.Uom,
                    BatchId = bId,
                    BatchNo = bNo ?? string.Empty,
                    SerialNos = bSerials.Count > 0 ? string.Join("\n", bSerials) : null,
                    OpeningQty = bBucket.OpeningQty,
                    OpeningValue = bBucket.OpeningValue,
                    InQty = bBucket.InQty,
                    InValue = bBucket.InValue,
                    OutQty = bBucket.OutQty,
                    OutValue = bBucket.OutValue,
                    BalanceQty = bBucket.BalanceQty,
                    BalanceValue = bBucket.BalanceValue,
                    ValuationRate = bBucket.ValuationRate
                });
            }

            // Parent row (Indent = 0)
            var batchNumbersString = childBatchRows.Count > 0
                ? string.Join("\n", childBatchRows.Select(r => r.BatchNo).Where(n => !string.IsNullOrEmpty(n)))
                : null;

            var parentRow = new SerialAndBatchWiseStockBalanceRowDto
            {
                RowId = Guid.NewGuid(),
                Indent = 0,
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                WarehouseId = group.Key.WarehouseId,
                WarehouseName = whName ?? string.Empty,
                StockUom = item.Uom,
                BatchId = null,
                BatchNo = batchNumbersString,
                SerialNos = inStockSerials.Count > 0 ? string.Join("\n", inStockSerials) : null,
                OpeningQty = itemBucket.OpeningQty,
                OpeningValue = itemBucket.OpeningValue,
                InQty = itemBucket.InQty,
                InValue = itemBucket.InValue,
                OutQty = itemBucket.OutQty,
                OutValue = itemBucket.OutValue,
                BalanceQty = itemBucket.BalanceQty,
                BalanceValue = itemBucket.BalanceValue,
                ValuationRate = itemBucket.ValuationRate
            };

            rows.Add(parentRow);
            rows.AddRange(childBatchRows);
        }

        // 4. Optional search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                r.WarehouseName.ToLowerInvariant().Contains(term) ||
                (r.BatchNo != null && r.BatchNo.ToLowerInvariant().Contains(term)) ||
                (r.SerialNos != null && r.SerialNos.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        var parentRows = rows.Where(r => r.Indent == 0).ToList();
        var totalBalanceQty = parentRows.Sum(r => r.BalanceQty);
        var totalBalanceValue = parentRows.Sum(r => r.BalanceValue);
        var totalBatches = rows.Count(r => r.Indent == 1);

        return new SerialAndBatchWiseStockBalanceReportDto
        {
            TotalItemsCount = parentRows.Count,
            TotalBatchesCount = totalBatches,
            TotalBalanceQty = totalBalanceQty,
            TotalBalanceValue = totalBalanceValue,
            Rows = rows
        };
    }
}
