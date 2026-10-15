using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using MyERP.Settings;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class StockBalanceAppService : ApplicationService, IStockBalanceAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly DomainServices.BinService _binService;

    public StockBalanceAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Batch, Guid> batchRepository,
        DomainServices.BinService binService)
    {
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _sleRepository = sleRepository;
        _batchRepository = batchRepository;
        _binService = binService;
    }

    /// <summary>
    /// Get stock balance report — all Bins with their projected quantities.
    /// </summary>
    public async Task<PagedResultDto<StockBalanceDto>> GetStockBalanceAsync(GetStockBalanceRequestDto input)
    {
        var query = await _binRepository.GetQueryableAsync();

        if (input.ItemId.HasValue)
            query = query.Where(b => b.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == input.WarehouseId.Value);

        // Per ERPNext PR #57458: include zero stock items by default
        if (input.ExcludeZeroStock)
            query = query.Where(b => b.ActualQty != 0 || b.OrderedQty != 0 || b.ReservedQty != 0 || b.PlannedQty != 0);

        var totalCount = query.Count();
        var items = query
            .OrderBy(b => b.ItemId)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = items.Select(ObjectMapper.Map<Bin, StockBalanceDto>).ToList();

        // Resolve item and warehouse names
        var itemIds = dtos.Select(d => d.ItemId).Distinct().ToList();
        var warehouseIds = dtos.Select(d => d.WarehouseId).Distinct().ToList();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id, i => $"{i.ItemCode} — {i.ItemName}");

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouseNames = whQuery.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        foreach (var dto in dtos)
        {
            dto.ItemName = itemNames.GetValueOrDefault(dto.ItemId, dto.ItemId.ToString()[..8]);
            dto.WarehouseName = warehouseNames.GetValueOrDefault(dto.WarehouseId, dto.WarehouseId.ToString()[..8]);
        }

        return new PagedResultDto<StockBalanceDto>(totalCount, dtos);
    }

    /// <summary>
    /// Get a single item's stock across all warehouses.
    /// </summary>
    public async Task<List<StockBalanceDto>> GetItemStockAsync(Guid itemId)
    {
        var query = await _binRepository.GetQueryableAsync();
        var bins = query.Where(b => b.ItemId == itemId && b.ActualQty != 0).ToList();

        var dtos = bins.Select(ObjectMapper.Map<Bin, StockBalanceDto>).ToList();

        // Resolve warehouse names
        var warehouseIds = dtos.Select(d => d.WarehouseId).Distinct().ToList();
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouseNames = whQuery.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        foreach (var dto in dtos)
        {
            dto.WarehouseName = warehouseNames.GetValueOrDefault(dto.WarehouseId, dto.WarehouseId.ToString()[..8]);
        }

        return dtos;
    }

    /// <summary>
    /// Batch stock availability check for multiple items — used by transaction forms
    /// to show real-time stock alongside item selection. Per ERPNext update_bin_details.
    /// Returns projected qty per item (across all company warehouses).
    /// </summary>
    public async Task<List<ItemAvailabilityDto>> GetItemsAvailabilityAsync(GetItemsAvailabilityInput input)
    {
        if (input.ItemIds == null || input.ItemIds.Count == 0)
            return new List<ItemAvailabilityDto>();

        var query = await _binRepository.GetQueryableAsync();

        // Filter by company warehouses if companyId provided
        IQueryable<Bin> binQuery = query.Where(b => input.ItemIds.Contains(b.ItemId));

        if (input.WarehouseId.HasValue)
        {
            binQuery = binQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        }

        var bins = binQuery.ToList();

        // Aggregate per item (sum across all matching warehouses)
        var grouped = bins.GroupBy(b => b.ItemId).Select(g => new ItemAvailabilityDto
        {
            ItemId = g.Key,
            ActualQty = g.Sum(b => b.ActualQty),
            ReservedQty = g.Sum(b => b.ReservedQty),
            OrderedQty = g.Sum(b => b.OrderedQty),
            ProjectedQty = g.Sum(b => b.ProjectedQty),
            AvailableQty = g.Sum(b => b.ActualQty) - g.Sum(b => b.ReservedQty),
        }).ToList();

        // Items not in Bin table (zero stock) — include with zero values
        var foundItemIds = grouped.Select(g => g.ItemId).ToHashSet();
        foreach (var itemId in input.ItemIds.Where(id => !foundItemIds.Contains(id)))
        {
            grouped.Add(new ItemAvailabilityDto { ItemId = itemId });
        }

        return grouped;
    }

    /// <summary>
    /// Serial and Batch-Wise Stock Balance: shows per-batch and serialized stock across warehouses.
    /// Per ERPNext PR #59321 (Serial and batch wise stock balance report): aggregates SLE by batch/serial with in-stock serial numbers.
    /// </summary>
    public async Task<BatchWiseBalanceReportDto> GetBatchWiseBalanceAsync(GetBatchWiseBalanceRequestDto input)
    {
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && (s.BatchId != null || s.SerialNoId != null || s.SerialAndBatchBundleId != null));

        if (input.CompanyId.HasValue)
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue)
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);
        if (input.BatchId.HasValue)
            sleQuery = sleQuery.Where(s => s.BatchId == input.BatchId.Value);
        if (input.FromDate.HasValue)
            sleQuery = sleQuery.Where(s => s.PostingDate >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            sleQuery = sleQuery.Where(s => s.PostingDate <= input.ToDate.Value);

        // Aggregate by (item, batch, warehouse) — net qty from SLE
        var grouped = sleQuery
            .GroupBy(s => new { s.ItemId, s.BatchId, s.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemId,
                BatchId = g.Key.BatchId,
                g.Key.WarehouseId,
                Balance = g.Sum(s => s.QuantityChange),
                StockValue = g.Sum(s => s.QuantityChange * s.ValuationRate),
            })
            .ToList();

        // Filter out zero-balance batches unless requested
        if (!input.IncludeZeroBalance)
            grouped = grouped.Where(g => g.Balance != 0).ToList();

        // Resolve names
        var itemIds = grouped.Select(g => g.ItemId).Distinct().ToList();
        var batchIds = grouped.Where(g => g.BatchId.HasValue).Select(g => g.BatchId!.Value).Distinct().ToList();
        var warehouseIds = grouped.Select(g => g.WarehouseId).Distinct().ToList();

        var itemQ = await _itemRepository.GetQueryableAsync();
        var itemMap = itemQ.Where(i => itemIds.Contains(i.Id))
            .Select(i => new
            {
                i.Id,
                i.ItemCode,
                i.ItemName,
                i.HasSerialNo,
                i.HasBatchNo,
                i.ValuationMethod,
                i.UseSerialNoWiseValuation,
                i.StandardBuyingPrice
            }).ToList()
            .ToDictionary(i => i.Id);

        var doNotUseBatchwiseValuation = await SettingProvider.IsTrueAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation);

        // Per ERPNext PR #60673 / commits c520e2f067 & 90aa56065e:
        // Value pooled batches at the warehouse rate at report cutoff.
        var pooledItemIds = itemMap.Values
            .Where(i => (i.HasSerialNo && !i.UseSerialNoWiseValuation) || (doNotUseBatchwiseValuation && i.ValuationMethod == ValuationMethod.WeightedAverage))
            .Select(i => i.Id)
            .ToHashSet();

        var pooledRates = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();
        if (pooledItemIds.Count > 0)
        {
            var cutoff = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;
            var allSleQuery = await _sleRepository.GetQueryableAsync();
            var latestSles = allSleQuery
                .Where(s => !s.IsCancelled && pooledItemIds.Contains(s.ItemId) && warehouseIds.Contains(s.WarehouseId) && s.PostingDateTime <= cutoff)
                .OrderByDescending(s => s.PostingDateTime).ThenByDescending(s => s.CreationTime)
                .ToList();

            foreach (var s in latestSles)
            {
                var key = (s.ItemId, s.WarehouseId);
                if (!pooledRates.ContainsKey(key))
                {
                    pooledRates[key] = s.ValuationRate;
                }
            }
        }

        var batchQ = await _batchRepository.GetQueryableAsync();
        var batchMap = batchQ.Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo, b.ExpiryDate, b.IsDisabled }).ToList()
            .ToDictionary(b => b.Id);

        var whQ = await _warehouseRepository.GetQueryableAsync();
        var whMap = whQ.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        // Query active serial numbers in stock (PR #59321)
        var serialMap = new Dictionary<(Guid ItemId, Guid WarehouseId, Guid? BatchId), List<string>>();
        var serialRepo = LazyServiceProvider.LazyGetService<IRepository<SerialNo, Guid>>();
        if (serialRepo != null && grouped.Count > 0)
        {
            var serialQuery = await serialRepo.GetQueryableAsync();
            serialQuery = serialQuery.Where(s => s.Status == SerialNoStatus.Active && s.WarehouseId != null && itemIds.Contains(s.ItemId));
            if (input.CompanyId.HasValue)
                serialQuery = serialQuery.Where(s => s.CompanyId == input.CompanyId.Value);
            if (input.WarehouseId.HasValue)
                serialQuery = serialQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);

            var serials = serialQuery.Select(s => new { s.ItemId, s.WarehouseId, s.BatchId, s.SerialNumber }).ToList();
            foreach (var s in serials)
            {
                var key = (s.ItemId, s.WarehouseId!.Value, s.BatchId);
                if (!serialMap.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    serialMap[key] = list;
                }
                list.Add(s.SerialNumber);
            }
        }

        // Query active reserved stock (PR #59008 / commit 000dcfc23d)
        var reservedMap = new Dictionary<(Guid ItemId, Guid WarehouseId, Guid? BatchId), decimal>();
        var sreRepo = LazyServiceProvider.LazyGetService<IRepository<StockReservationEntry, Guid>>();
        if (sreRepo != null && grouped.Count > 0)
        {
            var sreQuery = await sreRepo.GetQueryableAsync();
            sreQuery = sreQuery.Where(s => s.Status == Core.DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0);

            if (input.CompanyId.HasValue)
                sreQuery = sreQuery.Where(s => s.CompanyId == input.CompanyId.Value);
            if (input.ItemId.HasValue)
                sreQuery = sreQuery.Where(s => s.ItemId == input.ItemId.Value);
            if (input.WarehouseId.HasValue)
                sreQuery = sreQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);

            var sres = sreQuery.ToList();

            // 1) Direct BatchId reservations
            foreach (var sre in sres.Where(s => s.BatchId.HasValue && batchIds.Contains(s.BatchId!.Value)))
            {
                var key = (sre.ItemId, sre.WarehouseId, (Guid?)sre.BatchId!.Value);
                var activeReserved = Math.Max(0m, sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty);
                reservedMap[key] = reservedMap.GetValueOrDefault(key, 0m) + activeReserved;
            }

            // 2) Serial and Batch Bundle reservations
            var bundleIds = sres
                .Where(s => s.SerialAndBatchBundleId.HasValue)
                .Select(s => s.SerialAndBatchBundleId!.Value)
                .Distinct()
                .ToList();

            if (bundleIds.Count > 0)
            {
                var bundleRepo = LazyServiceProvider.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>();
                if (bundleRepo != null)
                {
                    var bundleQuery = await bundleRepo.GetQueryableAsync();
                    var bundles = bundleQuery.Where(b => bundleIds.Contains(b.Id) && !b.IsCancelled).ToList();
                    var bundleLookup = bundles.ToDictionary(b => b.Id);

                    foreach (var sre in sres.Where(s => s.SerialAndBatchBundleId.HasValue))
                    {
                        if (bundleLookup.TryGetValue(sre.SerialAndBatchBundleId!.Value, out var bundle))
                        {
                            var totalBundleQty = Math.Abs(bundle.TotalQty) > 0 ? Math.Abs(bundle.TotalQty) : bundle.Entries.Sum(e => e.Qty);
                            var unfulfilledSre = Math.Max(0m, sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty);
                            var ratio = totalBundleQty > 0 ? Math.Min(1m, unfulfilledSre / totalBundleQty) : 1m;

                            foreach (var entry in bundle.Entries.Where(e => e.BatchId.HasValue && batchIds.Contains(e.BatchId!.Value)))
                            {
                                var key = (sre.ItemId, sre.WarehouseId, (Guid?)entry.BatchId!.Value);
                                var entryReserved = entry.Qty * ratio;
                                reservedMap[key] = reservedMap.GetValueOrDefault(key, 0m) + entryReserved;
                            }
                        }
                    }
                }
            }
        }

        var rows = grouped.Select(g =>
        {
            var item = itemMap.GetValueOrDefault(g.ItemId);
            var batch = g.BatchId.HasValue ? batchMap.GetValueOrDefault(g.BatchId.Value) : null;
            var reservedKey = (g.ItemId, g.WarehouseId, g.BatchId);
            var serialList = serialMap.GetValueOrDefault(reservedKey)
                ?? (g.BatchId == null ? serialMap.GetValueOrDefault((g.ItemId, g.WarehouseId, null)) : null);

            var isPooled = pooledItemIds.Contains(g.ItemId);
            decimal stockValue;
            decimal valRate;

            if (isPooled)
            {
                var rate = pooledRates.GetValueOrDefault((g.ItemId, g.WarehouseId));
                if (rate == 0 && item?.StandardBuyingPrice.HasValue == true)
                    rate = item.StandardBuyingPrice.Value;

                stockValue = Math.Round(g.Balance * rate, 4);
                valRate = Math.Round(rate, 4);
            }
            else
            {
                stockValue = g.StockValue;
                valRate = g.Balance != 0 ? Math.Round(g.StockValue / g.Balance, 4) : 0m;
            }

            return new BatchWiseBalanceRowDto
            {
                ItemId = g.ItemId,
                ItemName = item != null ? $"{item.ItemCode} — {item.ItemName}" : "—",
                BatchId = g.BatchId ?? Guid.Empty,
                BatchNo = batch?.BatchNo ?? (g.BatchId.HasValue ? "—" : (item != null && item.HasSerialNo ? "(Serialized)" : "—")),
                WarehouseId = g.WarehouseId,
                WarehouseName = whMap.GetValueOrDefault(g.WarehouseId, "—"),
                Balance = g.Balance,
                StockValue = stockValue,
                ValuationRate = valRate,
                SerialNos = serialList != null && serialList.Count > 0 ? string.Join(", ", serialList.OrderBy(x => x)) : null,
                ReservedStockQty = Math.Round(reservedMap.GetValueOrDefault(reservedKey, 0m), 4),
                ExpiryDate = batch?.ExpiryDate,
                IsExpired = batch?.ExpiryDate.HasValue == true && batch.ExpiryDate < DateTime.UtcNow.Date,
                IsDisabled = batch?.IsDisabled ?? false,
            };
        })
        .OrderBy(r => r.ItemName).ThenBy(r => r.BatchNo).ThenBy(r => r.WarehouseName)
        .ToList();

        return new BatchWiseBalanceReportDto
        {
            Rows = rows,
            TotalBatches = rows.Where(r => r.BatchId != Guid.Empty).Select(r => r.BatchId).Distinct().Count(),
            TotalQuantity = rows.Sum(r => r.Balance),
            TotalStockValue = rows.Sum(r => r.StockValue),
            TotalReservedStock = rows.Sum(r => r.ReservedStockQty),
            ExpiredBatchCount = rows.Count(r => r.IsExpired),
        };
    }

    /// <summary>
    /// Stock Balance Report — shows opening, inward, outward, and closing quantities and values.
    /// Incorporates inventory dimension handling and applies reconciliations against total warehouse balance
    /// to prevent dimension double-counting per ERPNext PR #60698 / commit c8eb481d32.
    /// </summary>
    public async Task<StockBalanceReportDto> GetStockBalanceReportAsync(GetStockBalanceReportRequestDto input)
    {
        var fromDate = input.FromDate ?? DateTime.MinValue;
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && s.PostingDateTime <= toDate);

        if (input.CompanyId.HasValue)
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue)
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);

        var entries = sleQuery
            .OrderBy(s => s.PostingDateTime)
            .ThenBy(s => s.CreationTime)
            .ToList();

        var rowMap = new Dictionary<(Guid ItemId, Guid WarehouseId, Guid? BatchId), StockBalanceReportRowDto>();
        var itemWarehouseBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), (decimal BalQty, decimal BalVal)>();

        foreach (var entry in entries)
        {
            var itemWhKey = (entry.ItemId, entry.WarehouseId);
            if (!itemWarehouseBalances.TryGetValue(itemWhKey, out var whBal))
            {
                whBal = (0m, 0m);
            }

            var batchKey = input.ShowDimensionWiseStock ? entry.BatchId : null;
            var groupKey = (entry.ItemId, entry.WarehouseId, batchKey);

            if (!rowMap.TryGetValue(groupKey, out var row))
            {
                row = new StockBalanceReportRowDto
                {
                    ItemId = entry.ItemId,
                    WarehouseId = entry.WarehouseId,
                    BatchId = batchKey
                };
                rowMap[groupKey] = row;
            }

            decimal qtyDiff;
            decimal valueDiff;

            if (entry.VoucherType == "StockReconciliation")
            {
                if (input.ShowDimensionWiseStock)
                {
                    // Per ERPNext PR #60698 / commit c8eb481d32:
                    // A normal Stock Reconciliation has no inventory dimension, so with Show
                    // Dimension Wise Stock it lands on the row without dimensions.
                    // Calculate change against total item-warehouse balance, not the dimension row's balance,
                    // so the row does not absorb other dimensions' stock.
                    qtyDiff = entry.BalanceQuantity - whBal.BalQty;
                    valueDiff = entry.BalanceValue - whBal.BalVal;
                }
                else
                {
                    qtyDiff = entry.BalanceQuantity - row.BalanceQty;
                    valueDiff = entry.BalanceValue - row.BalanceValue;
                }
            }
            else
            {
                qtyDiff = entry.QuantityChange;
                valueDiff = entry.StockValueDifference != 0 ? entry.StockValueDifference : entry.StockValue;
            }

            // Update row balance
            if (entry.PostingDateTime < fromDate)
            {
                row.OpeningQty += qtyDiff;
                row.OpeningValue += valueDiff;
            }
            else
            {
                if (qtyDiff > 0)
                {
                    row.InQty += qtyDiff;
                    row.InValue += valueDiff;
                }
                else if (qtyDiff < 0)
                {
                    row.OutQty += Math.Abs(qtyDiff);
                    row.OutValue += Math.Abs(valueDiff);
                }
            }

            row.BalanceQty += qtyDiff;
            row.BalanceValue += valueDiff;
            row.ValuationRate = entry.ValuationRate;

            // Always update running item-warehouse balance
            whBal.BalQty += qtyDiff;
            whBal.BalVal += valueDiff;
            itemWarehouseBalances[itemWhKey] = whBal;
        }

        var rows = rowMap.Values.ToList();

        if (input.ExcludeZeroStock)
        {
            rows = rows.Where(r => r.OpeningQty != 0 || r.InQty != 0 || r.OutQty != 0 || r.BalanceQty != 0).ToList();
        }

        // Resolve names
        var itemIds = rows.Select(r => r.ItemId).Distinct().ToList();
        var whIds = rows.Select(r => r.WarehouseId).Distinct().ToList();
        var batchIds = rows.Where(r => r.BatchId.HasValue).Select(r => r.BatchId!.Value).Distinct().ToList();

        var itemQ = await _itemRepository.GetQueryableAsync();
        var items = itemQ.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id);

        var whQ = await _warehouseRepository.GetQueryableAsync();
        var warehouses = whQ.Where(w => whIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionary(w => w.Id, w => w.Name);

        var batchQ = await _batchRepository.GetQueryableAsync();
        var batches = batchQ.Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo })
            .ToDictionary(b => b.Id, b => b.BatchNo);

        foreach (var r in rows)
        {
            if (items.TryGetValue(r.ItemId, out var item))
            {
                r.ItemCode = item.ItemCode;
                r.ItemName = item.ItemName;
            }
            r.WarehouseName = warehouses.GetValueOrDefault(r.WarehouseId, "Unknown");
            if (r.BatchId.HasValue)
            {
                r.BatchNo = batches.GetValueOrDefault(r.BatchId.Value, "Unknown");
            }
        }

        rows = rows.OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ThenBy(r => r.BatchNo).ToList();

        return new StockBalanceReportDto
        {
            FromDate = input.FromDate ?? DateTime.MinValue,
            ToDate = input.ToDate ?? DateTime.UtcNow,
            Rows = rows,
            TotalOpeningQty = rows.Sum(r => r.OpeningQty),
            TotalInQty = rows.Sum(r => r.InQty),
            TotalOutQty = rows.Sum(r => r.OutQty),
            TotalBalanceQty = rows.Sum(r => r.BalanceQty),
            TotalBalanceValue = rows.Sum(r => r.BalanceValue)
        };
    }

    /// <summary>
    /// Recalculates the stock quantities in the Bin for the specified item and warehouse from source ledger entries.
    /// Per ERPNext PR #47125 / commit 36081413d8: provision to recalculate the qty in the Bin.
    /// </summary>
    public async Task RecalculateBinQtyAsync(Guid itemId, Guid warehouseId)
    {
        await _binService.RecalculateFullBinAsync(itemId, warehouseId, _sleRepository);
    }
}

