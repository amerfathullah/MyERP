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
/// Negative Batch Report application service.
/// Detects and reports transactions where batch inventory went negative in a warehouse.
/// Maps to ERPNext stock/report/negative_batch_report/negative_batch_report.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class NegativeBatchReportAppService : ApplicationService, INegativeBatchReportAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public NegativeBatchReportAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _sleRepository = sleRepository;
        _bundleRepository = bundleRepository;
        _batchRepository = batchRepository;
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
    }

    private class BatchMovement
    {
        public DateTime PostingDateTime { get; set; }
        public DateTime CreationTime { get; set; }
        public Guid WarehouseId { get; set; }
        public Guid ItemId { get; set; }
        public Guid BatchId { get; set; }
        public decimal Qty { get; set; }
        public string VoucherType { get; set; } = string.Empty;
        public Guid? VoucherId { get; set; }
    }

    public async Task<NegativeBatchReportDto> GetReportAsync(NegativeBatchFilterDto input)
    {
        var toDate = input.ToDate?.Date ?? DateTime.UtcNow.Date;
        var toDateCutoff = toDate.AddDays(1).AddTicks(-1);

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
        var warehouses = whQuery.ToList();
        var whMap = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var allowedWhIds = whMap.Keys.ToHashSet();

        if (allowedWhIds.Count == 0)
        {
            return new NegativeBatchReportDto { FromDate = input.FromDate, ToDate = input.ToDate };
        }

        // 2. Batches
        var batchQuery = await _batchRepository.GetQueryableAsync();
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            batchQuery = batchQuery.Where(b => b.ItemId == input.ItemId.Value);
        }
        if (input.BatchId.HasValue && input.BatchId.Value != Guid.Empty)
        {
            batchQuery = batchQuery.Where(b => b.Id == input.BatchId.Value);
        }
        var batches = batchQuery.ToList();
        var batchMap = batches.ToDictionary(b => b.Id);
        var allowedBatchIds = batchMap.Keys.ToHashSet();

        if (allowedBatchIds.Count == 0)
        {
            return new NegativeBatchReportDto { FromDate = input.FromDate, ToDate = input.ToDate };
        }

        // 3. Items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.ToList();
        var itemMap = items.ToDictionary(i => i.Id);

        // 4. Stock Ledger Entries
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && s.PostingDateTime <= toDateCutoff);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        }
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);
        }
        var sles = sleQuery
            .Where(s => allowedWhIds.Contains(s.WarehouseId))
            .ToList();

        var movements = new List<BatchMovement>();

        // 4a. Direct SLE batch movements
        foreach (var s in sles)
        {
            if (s.BatchId.HasValue && allowedBatchIds.Contains(s.BatchId.Value))
            {
                movements.Add(new BatchMovement
                {
                    PostingDateTime = s.PostingDateTime,
                    CreationTime = s.CreationTime,
                    WarehouseId = s.WarehouseId,
                    ItemId = s.ItemId,
                    BatchId = s.BatchId.Value,
                    Qty = s.QuantityChange,
                    VoucherType = s.VoucherType ?? string.Empty,
                    VoucherId = s.VoucherId
                });
            }
        }

        // 4b. Bundle movements
        var bundleIds = sles
            .Where(s => s.SerialAndBatchBundleId.HasValue && s.SerialAndBatchBundleId.Value != Guid.Empty)
            .Select(s => s.SerialAndBatchBundleId!.Value)
            .Distinct()
            .ToList();

        if (bundleIds.Count > 0)
        {
            var bundleQuery = await _bundleRepository.WithDetailsAsync(b => b.Entries);
            var bundles = bundleQuery
                .Where(b => bundleIds.Contains(b.Id) && !b.IsCancelled)
                .ToList();
            var bundleMap = bundles.ToDictionary(b => b.Id);

            foreach (var s in sles)
            {
                if (s.SerialAndBatchBundleId.HasValue && bundleMap.TryGetValue(s.SerialAndBatchBundleId.Value, out var bundle))
                {
                    foreach (var entry in bundle.Entries)
                    {
                        if (entry.BatchId.HasValue && allowedBatchIds.Contains(entry.BatchId.Value))
                        {
                            decimal qty = entry.Qty;
                            if (bundle.TypeOfTransaction == BundleTransactionType.Outward && qty > 0)
                            {
                                qty = -qty;
                            }
                            movements.Add(new BatchMovement
                            {
                                PostingDateTime = s.PostingDateTime,
                                CreationTime = s.CreationTime,
                                WarehouseId = s.WarehouseId,
                                ItemId = s.ItemId,
                                BatchId = entry.BatchId.Value,
                                Qty = qty,
                                VoucherType = s.VoucherType ?? string.Empty,
                                VoucherId = s.VoucherId
                            });
                        }
                    }
                }
            }
        }

        // Group by (WarehouseId, BatchId) and track chronological balance
        var negativeRows = new List<NegativeBatchRowDto>();
        var distinctNegativeBatches = new HashSet<Guid>();

        var groupedMovements = movements
            .GroupBy(m => (m.WarehouseId, m.BatchId));

        foreach (var group in groupedMovements)
        {
            var ordered = group
                .OrderBy(m => m.PostingDateTime)
                .ThenBy(m => m.CreationTime)
                .ToList();

            decimal runningQty = 0;
            bool incidentRecordedForGroup = false;

            foreach (var m in ordered)
            {
                decimal previousQty = runningQty;
                runningQty += m.Qty;

                if (!incidentRecordedForGroup && runningQty < 0)
                {
                    // Check FromDate filter if specified
                    if (!input.FromDate.HasValue || m.PostingDateTime >= input.FromDate.Value.Date)
                    {
                        var batch = batchMap.GetValueOrDefault(m.BatchId);
                        var item = itemMap.GetValueOrDefault(m.ItemId);
                        var whName = whMap.GetValueOrDefault(m.WarehouseId) ?? "Unknown";

                        var row = new NegativeBatchRowDto
                        {
                            PostingDateTime = m.PostingDateTime,
                            BatchId = m.BatchId,
                            BatchNo = batch?.BatchNo ?? string.Empty,
                            ItemId = m.ItemId,
                            ItemCode = item?.ItemCode ?? string.Empty,
                            ItemName = item?.ItemName ?? string.Empty,
                            WarehouseId = m.WarehouseId,
                            WarehouseName = whName,
                            PreviousQty = previousQty,
                            TransactionQty = m.Qty,
                            QtyAfterTransaction = runningQty,
                            VoucherType = m.VoucherType,
                            VoucherId = m.VoucherId,
                            VoucherNo = m.VoucherId.HasValue ? m.VoucherId.Value.ToString().Substring(0, 8).ToUpper() : string.Empty
                        };

                        if (string.IsNullOrWhiteSpace(input.SearchTerm) ||
                            row.BatchNo.Contains(input.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                            row.ItemCode.Contains(input.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                            row.ItemName.Contains(input.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                            row.WarehouseName.Contains(input.SearchTerm, StringComparison.OrdinalIgnoreCase))
                        {
                            negativeRows.Add(row);
                            distinctNegativeBatches.Add(m.BatchId);
                        }

                        incidentRecordedForGroup = true;
                    }
                }
            }
        }

        return new NegativeBatchReportDto
        {
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = negativeRows.OrderByDescending(r => r.PostingDateTime).ToList(),
            DistinctNegativeBatches = distinctNegativeBatches.Count
        };
    }
}
