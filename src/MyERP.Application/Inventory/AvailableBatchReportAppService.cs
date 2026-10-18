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
/// Available Batch Report application service.
/// Identifies available batches and their balance quantities on a given cut-off date.
/// Maps to ERPNext stock/report/available_batch_report/available_batch_report.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class AvailableBatchReportAppService : ApplicationService, IAvailableBatchReportAppService
{
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public AvailableBatchReportAppService(
        IRepository<Batch, Guid> batchRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _batchRepository = batchRepository;
        _sleRepository = sleRepository;
        _bundleRepository = bundleRepository;
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
    }

    public async Task<AvailableBatchReportDto> GetReportAsync(AvailableBatchFilterDto input)
    {
        var toDate = input.ToDate?.Date ?? DateTime.UtcNow.Date;
        var toDateCutoff = toDate.AddDays(1).AddTicks(-1);
        var today = DateTime.UtcNow.Date;

        // 1. Warehouses query
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
        if (warehouses.Count == 0)
        {
            return new AvailableBatchReportDto { ToDate = toDate };
        }
        var whMap = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var allowedWarehouseIds = whMap.Keys.ToHashSet();

        // 2. Batches query
        var batchQuery = await _batchRepository.GetQueryableAsync();
        batchQuery = batchQuery.Where(b => !b.IsDisabled && !b.IsCancelled);
        if (input.BatchId.HasValue && input.BatchId.Value != Guid.Empty)
        {
            batchQuery = batchQuery.Where(b => b.Id == input.BatchId.Value);
        }
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            batchQuery = batchQuery.Where(b => b.ItemId == input.ItemId.Value);
        }
        if (!input.IncludeExpiredBatches)
        {
            batchQuery = batchQuery.Where(b => b.ExpiryDate == null || b.ExpiryDate.Value.Date >= toDate);
        }
        var batches = batchQuery.ToList();
        if (batches.Count == 0)
        {
            return new AvailableBatchReportDto { ToDate = toDate };
        }
        var batchMap = batches.ToDictionary(b => b.Id);
        var allowedBatchIds = batchMap.Keys.ToHashSet();

        // 3. Stock Ledger Entries query
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
            .Where(s => allowedWarehouseIds.Contains(s.WarehouseId))
            .ToList();

        var balanceDict = new Dictionary<(Guid ItemId, Guid WarehouseId, Guid BatchId), decimal>();

        // 3a. Direct SLE batch balance
        foreach (var s in sles)
        {
            if (s.BatchId.HasValue && allowedBatchIds.Contains(s.BatchId.Value))
            {
                var key = (s.ItemId, s.WarehouseId, s.BatchId.Value);
                balanceDict[key] = balanceDict.GetValueOrDefault(key) + s.QuantityChange;
            }
        }

        // 3b. Bundles query for SLEs with SerialAndBatchBundleId
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
                            var key = (s.ItemId, s.WarehouseId, entry.BatchId.Value);
                            decimal qty = entry.Qty;
                            if (bundle.TypeOfTransaction == BundleTransactionType.Outward && qty > 0)
                            {
                                qty = -qty;
                            }
                            balanceDict[key] = balanceDict.GetValueOrDefault(key) + qty;
                        }
                    }
                }
            }
        }

        // 4. Filter positive balances
        var positiveBalances = balanceDict
            .Where(kv => kv.Value > 0)
            .ToList();

        if (positiveBalances.Count == 0)
        {
            return new AvailableBatchReportDto { ToDate = toDate };
        }

        // 5. Fetch items
        var itemIds = positiveBalances.Select(b => b.Key.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id)).ToList();
        var itemMap = items.ToDictionary(i => i.Id);

        // 6. Build rows
        var rows = new List<AvailableBatchRowDto>();
        foreach (var balance in positiveBalances)
        {
            var item = itemMap.GetValueOrDefault(balance.Key.ItemId);
            var whName = whMap.GetValueOrDefault(balance.Key.WarehouseId) ?? string.Empty;
            var batch = batchMap.GetValueOrDefault(balance.Key.BatchId);

            if (batch == null || item == null)
            {
                continue;
            }

            int? daysToExpiry = null;
            bool isExpired = false;
            if (batch.ExpiryDate.HasValue)
            {
                daysToExpiry = (int)(batch.ExpiryDate.Value.Date - today).TotalDays;
                isExpired = daysToExpiry < 0;
            }

            rows.Add(new AvailableBatchRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                StockUom = item.Uom,
                WarehouseId = balance.Key.WarehouseId,
                WarehouseName = whName,
                BatchId = batch.Id,
                BatchNo = batch.BatchNo,
                ExpiryDate = batch.ExpiryDate,
                BalanceQty = balance.Value,
                IsExpired = isExpired,
                DaysToExpiry = daysToExpiry
            });
        }

        // 7. Search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            rows = rows.Where(r =>
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.BatchNo.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // 8. Order by ItemCode, WarehouseName, BatchNo
        rows = rows
            .OrderBy(r => r.ItemCode)
            .ThenBy(r => r.WarehouseName)
            .ThenBy(r => r.BatchNo)
            .ToList();

        return new AvailableBatchReportDto
        {
            ToDate = toDate,
            Rows = rows,
            TotalBatches = rows.Select(r => r.BatchId).Distinct().Count(),
            TotalBalanceQty = rows.Sum(r => r.BalanceQty),
            ExpiredBatchesCount = rows.Where(r => r.IsExpired).Select(r => r.BatchId).Distinct().Count()
        };
    }
}
