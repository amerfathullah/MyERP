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
/// Available Serial No report application service.
/// Provides chronological ledger of serialized items with running available serial numbers per warehouse.
/// Maps to ERPNext stock/report/available_serial_no/available_serial_no.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class AvailableSerialNoReportAppService : ApplicationService, IAvailableSerialNoReportAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;

    public AvailableSerialNoReportAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository)
    {
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _serialNoRepository = serialNoRepository;
        _bundleRepository = bundleRepository;
    }

    public async Task<AvailableSerialNoReportDto> GetReportAsync(AvailableSerialNoFilterDto input)
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
        var whMap = warehouses.ToDictionary(w => w.Id);
        var allowedWhIds = whMap.Keys.ToHashSet();

        if (allowedWhIds.Count == 0)
        {
            return new AvailableSerialNoReportDto { FromDate = input.FromDate, ToDate = input.ToDate };
        }

        // 2. Items (Serialized only)
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.HasSerialNo);
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLower();
            itemQuery = itemQuery.Where(i => i.ItemCode.ToLower().Contains(term) || (i.ItemName != null && i.ItemName.ToLower().Contains(term)));
        }
        var items = itemQuery.ToList();
        var itemMap = items.ToDictionary(i => i.Id);
        var allowedItemIds = itemMap.Keys.ToHashSet();

        if (allowedItemIds.Count == 0)
        {
            return new AvailableSerialNoReportDto { FromDate = input.FromDate, ToDate = input.ToDate };
        }

        // 3. Stock Ledger Entries (all up to toDateCutoff to accurately compute opening/running serial numbers)
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var sles = sleQuery
            .Where(s => !s.IsCancelled && allowedWhIds.Contains(s.WarehouseId) && allowedItemIds.Contains(s.ItemId) && s.PostingDateTime <= toDateCutoff)
            .OrderBy(s => s.PostingDateTime)
            .ThenBy(s => s.CreationTime)
            .ToList();

        if (sles.Count == 0)
        {
            return new AvailableSerialNoReportDto { FromDate = input.FromDate, ToDate = input.ToDate };
        }

        // 4. Resolve Direct Serial Numbers
        var serialNoIds = sles
            .Where(s => s.SerialNoId.HasValue && s.SerialNoId.Value != Guid.Empty)
            .Select(s => s.SerialNoId!.Value)
            .Distinct()
            .ToList();

        var serialNoMap = new Dictionary<Guid, string>();
        if (serialNoIds.Count > 0)
        {
            var snList = await _serialNoRepository.GetListAsync(s => serialNoIds.Contains(s.Id));
            serialNoMap = snList.ToDictionary(s => s.Id, s => s.SerialNumber);
        }

        // 5. Resolve Bundles
        var bundleIds = sles
            .Where(s => s.SerialAndBatchBundleId.HasValue && s.SerialAndBatchBundleId.Value != Guid.Empty)
            .Select(s => s.SerialAndBatchBundleId!.Value)
            .Distinct()
            .ToList();

        var bundleSerialMap = new Dictionary<Guid, List<string>>();
        if (bundleIds.Count > 0)
        {
            var bundleQuery = await _bundleRepository.WithDetailsAsync(b => b.Entries);
            var bundles = bundleQuery
                .Where(b => bundleIds.Contains(b.Id) && !b.IsCancelled)
                .ToList();

            foreach (var b in bundles)
            {
                var sns = b.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.SerialNo))
                    .Select(e => e.SerialNo!.Trim())
                    .ToList();
                bundleSerialMap[b.Id] = sns;
            }
        }

        // 6. Chronological tracking per (Item, Warehouse)
        var groups = sles.GroupBy(s => (s.ItemId, s.WarehouseId));
        var fromDateCutoff = input.FromDate?.Date;
        var rows = new List<AvailableSerialNoRowDto>();
        decimal totalInQty = 0m;
        decimal totalOutQty = 0m;

        foreach (var group in groups)
        {
            if (!itemMap.TryGetValue(group.Key.ItemId, out var item)) continue;
            if (!whMap.TryGetValue(group.Key.WarehouseId, out var wh)) continue;

            var activeSerialNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sle in group)
            {
                // Extract serial numbers moved
                var movedSerials = new List<string>();
                if (sle.SerialNoId.HasValue && serialNoMap.TryGetValue(sle.SerialNoId.Value, out var sn))
                {
                    movedSerials.Add(sn);
                }
                else if (sle.SerialAndBatchBundleId.HasValue && bundleSerialMap.TryGetValue(sle.SerialAndBatchBundleId.Value, out var bundleSns))
                {
                    movedSerials.AddRange(bundleSns);
                }

                var inQty = Math.Max(0m, sle.QuantityChange);
                var outQty = Math.Abs(Math.Min(0m, sle.QuantityChange));

                // Update active serials set
                if (sle.QuantityChange > 0)
                {
                    foreach (var s in movedSerials)
                    {
                        activeSerialNos.Add(s);
                    }
                }
                else if (sle.QuantityChange < 0)
                {
                    foreach (var s in movedSerials)
                    {
                        activeSerialNos.Remove(s);
                    }
                }

                // Check date window
                if (fromDateCutoff.HasValue && sle.PostingDateTime < fromDateCutoff.Value)
                {
                    continue; // Processed for opening balance, skip row output
                }

                totalInQty += inQty;
                totalOutQty += outQty;

                var balanceList = activeSerialNos.OrderBy(s => s).ToList();
                var balanceCount = balanceList.Count;
                var hasDiscrepancy = (int)Math.Round(sle.BalanceQuantity) != balanceCount;

                var voucherNo = sle.VoucherId.HasValue
                    ? $"{sle.VoucherType}-{sle.VoucherId.Value.ToString()[..8].ToUpperInvariant()}"
                    : sle.VoucherType;

                rows.Add(new AvailableSerialNoRowDto
                {
                    SleId = sle.Id,
                    PostingDateTime = sle.PostingDateTime,
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    StockUom = item.Uom,
                    WarehouseId = wh.Id,
                    WarehouseName = wh.Name,
                    InQty = inQty,
                    OutQty = outQty,
                    BalanceQty = sle.BalanceQuantity,
                    SerialNosMoved = movedSerials,
                    BalanceSerialNos = balanceList,
                    BalanceSerialCount = balanceCount,
                    IncomingRate = sle.IncomingRate,
                    ValuationRate = sle.ValuationRate,
                    StockValue = sle.BalanceValue,
                    StockValueDifference = sle.StockValueDifference,
                    VoucherType = sle.VoucherType,
                    VoucherId = sle.VoucherId,
                    VoucherNo = voucherNo,
                    HasDiscrepancy = hasDiscrepancy
                });
            }
        }

        var latestPerGroup = rows
            .GroupBy(r => (r.ItemId, r.WarehouseId))
            .Select(g => g.Last())
            .ToList();

        var finalBalanceQty = latestPerGroup.Sum(r => r.BalanceQty);
        var finalSerialCount = latestPerGroup.Sum(r => r.BalanceSerialCount);

        return new AvailableSerialNoReportDto
        {
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            TotalTransactions = rows.Count,
            TotalInQty = totalInQty,
            TotalOutQty = totalOutQty,
            FinalBalanceQty = finalBalanceQty,
            FinalAvailableSerialCount = finalSerialCount,
            DiscrepancyCount = rows.Count(r => r.HasDiscrepancy),
            Rows = rows
        };
    }
}
