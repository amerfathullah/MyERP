using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class StockQtyVsSerialNoCountAppService : ApplicationService, IStockQtyVsSerialNoCountAppService
{
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly IRepository<SerialNo, Guid> _serialNoRepo;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo;

    public StockQtyVsSerialNoCountAppService(
        IRepository<Item, Guid> itemRepo,
        IRepository<Warehouse, Guid> warehouseRepo,
        IRepository<Bin, Guid> binRepo,
        IRepository<SerialNo, Guid> serialNoRepo,
        IRepository<StockLedgerEntry, Guid> sleRepo,
        IRepository<SerialAndBatchBundle, Guid> bundleRepo)
    {
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
        _binRepo = binRepo;
        _serialNoRepo = serialNoRepo;
        _sleRepo = sleRepo;
        _bundleRepo = bundleRepo;
    }

    public async Task<StockQtyVsSerialNoCountReportDto> GetReportAsync(StockQtyVsSerialNoCountFilterDto input)
    {
        var targetWarehouseIds = await ResolveWarehouseIdsAsync(input.WarehouseId, input.CompanyId);

        var itemQuery = await _itemRepo.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.HasSerialNo);
        if (input.CompanyId.HasValue)
        {
            itemQuery = itemQuery.Where(i => i.CompanyId == input.CompanyId.Value);
        }
        if (input.ItemId.HasValue)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        if (!input.ShowDisabledItems)
        {
            itemQuery = itemQuery.Where(i => i.IsActive);
        }
        var items = itemQuery.ToList();
        var itemMap = items.ToDictionary(i => i.Id);
        var itemIds = items.Select(i => i.Id).ToList();

        if (itemIds.Count == 0)
        {
            return new StockQtyVsSerialNoCountReportDto();
        }

        var warehouseQuery = await _warehouseRepo.GetQueryableAsync();
        if (input.CompanyId.HasValue)
        {
            warehouseQuery = warehouseQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        var warehouses = warehouseQuery.Where(w => targetWarehouseIds.Contains(w.Id)).ToList();
        var warehouseMap = warehouses.ToDictionary(w => w.Id);

        // Fetch Bins
        var binQuery = await _binRepo.GetQueryableAsync();
        binQuery = binQuery.Where(b => itemIds.Contains(b.ItemId) && targetWarehouseIds.Contains(b.WarehouseId));
        var bins = binQuery.ToList();
        var binQtyMap = bins
            .GroupBy(b => (b.ItemId, b.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));

        // Fetch Serial Nos
        var serialQuery = await _serialNoRepo.GetQueryableAsync();
        serialQuery = serialQuery.Where(s =>
            itemIds.Contains(s.ItemId) &&
            s.WarehouseId.HasValue &&
            targetWarehouseIds.Contains(s.WarehouseId.Value) &&
            s.Status == SerialNoStatus.Active);
        if (input.CompanyId.HasValue)
        {
            serialQuery = serialQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        }
        var serials = serialQuery.ToList();
        var serialCountMap = serials
            .GroupBy(s => (s.ItemId, WarehouseId: s.WarehouseId!.Value))
            .ToDictionary(g => g.Key, g => g.Count());

        // Aggregate unique (ItemId, WarehouseId) pairs
        var allPairs = new HashSet<(Guid ItemId, Guid WarehouseId)>();
        foreach (var key in binQtyMap.Keys) allPairs.Add(key);
        foreach (var key in serialCountMap.Keys) allPairs.Add(key);

        var rows = new List<StockQtyVsSerialNoCountRowDto>();
        var searchTerm = input.SearchTerm?.Trim().ToLowerInvariant();

        foreach (var pair in allPairs)
        {
            if (!itemMap.TryGetValue(pair.ItemId, out var item)) continue;
            if (!warehouseMap.TryGetValue(pair.WarehouseId, out var wh)) continue;

            var serialCount = serialCountMap.TryGetValue(pair, out var sc) ? (decimal)sc : 0m;
            var stockQty = binQtyMap.TryGetValue(pair, out var sq) ? sq : 0m;
            var difference = serialCount - stockQty;

            if (input.ShowOnlyDifferences && difference == 0m)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var match = item.ItemCode.ToLowerInvariant().Contains(searchTerm) ||
                            item.ItemName.ToLowerInvariant().Contains(searchTerm) ||
                            wh.Name.ToLowerInvariant().Contains(searchTerm);
                if (!match) continue;
            }

            rows.Add(new StockQtyVsSerialNoCountRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                WarehouseId = wh.Id,
                WarehouseName = wh.Name,
                SerialNoCount = serialCount,
                StockQty = stockQty,
                Difference = difference,
            });
        }

        // Sort by ItemCode then WarehouseName
        rows = rows.OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ToList();

        return new StockQtyVsSerialNoCountReportDto
        {
            Rows = rows,
            TotalMismatches = rows.Count(r => r.Difference != 0m),
            TotalSerialCount = rows.Sum(r => r.SerialNoCount),
            TotalStockQty = rows.Sum(r => r.StockQty),
            TotalDifference = rows.Sum(r => r.Difference),
        };
    }

    public async Task<SyncSerialNoStatusResultDto> SyncSerialNoStatusAsync(SyncSerialNoStatusInputDto input)
    {
        var warehouse = await _warehouseRepo.FindAsync(input.WarehouseId);
        if (warehouse == null)
        {
            throw new BusinessException("MyERP.Stock:WarehouseNotFound", $"Warehouse {input.WarehouseId} not found.");
        }

        var serialQuery = await _serialNoRepo.GetQueryableAsync();
        serialQuery = serialQuery.Where(s =>
            s.CompanyId == input.CompanyId &&
            s.WarehouseId == input.WarehouseId &&
            (s.Status == SerialNoStatus.Active || s.Status == SerialNoStatus.Inactive));

        if (input.ItemId.HasValue)
        {
            serialQuery = serialQuery.Where(s => s.ItemId == input.ItemId.Value);
        }

        var serials = serialQuery.ToList();
        if (serials.Count == 0)
        {
            return new SyncSerialNoStatusResultDto
            {
                SyncedCount = 0,
                Message = "No serial numbers found to sync for the selected warehouse.",
            };
        }

        var sleQuery = await _sleRepo.GetQueryableAsync();
        var bundleQuery = (await _bundleRepo.WithDetailsAsync(b => b.Entries))
            .Where(b => !b.IsCancelled && b.CompanyId == input.CompanyId);

        var allBundles = bundleQuery.ToList();

        int syncedCount = 0;

        foreach (var serial in serials)
        {
            // Collect movements for this serial
            var movements = new List<SerialLedgerMove>();

            // 1. Direct SLEs
            var directSles = sleQuery
                .Where(sle => !sle.IsCancelled && sle.SerialNoId == serial.Id)
                .ToList();

            foreach (var sle in directSles)
            {
                movements.Add(new SerialLedgerMove
                {
                    PostingDateTime = sle.PostingDateTime,
                    CreationTime = sle.CreationTime,
                    Qty = sle.QuantityChange,
                    WarehouseId = sle.WarehouseId,
                    VoucherType = sle.VoucherType ?? string.Empty,
                    VoucherId = sle.VoucherId,
                });
            }

            // 2. Bundle SLEs
            var matchingBundles = allBundles
                .Where(b => b.ItemId == serial.ItemId && b.Entries.Any(e => e.SerialNo == serial.SerialNumber))
                .ToList();

            if (matchingBundles.Count > 0)
            {
                var bundleIds = matchingBundles.Select(b => b.Id).ToList();
                var bundleSles = sleQuery
                    .Where(sle => !sle.IsCancelled && sle.SerialAndBatchBundleId.HasValue && bundleIds.Contains(sle.SerialAndBatchBundleId.Value))
                    .ToList();

                foreach (var sle in bundleSles)
                {
                    var bundle = matchingBundles.FirstOrDefault(b => b.Id == sle.SerialAndBatchBundleId!.Value);
                    if (bundle != null)
                    {
                        var entry = bundle.Entries.FirstOrDefault(e => e.SerialNo == serial.SerialNumber);
                        var qty = entry != null ? (sle.QuantityChange < 0 ? -Math.Abs(entry.Qty) : Math.Abs(entry.Qty)) : sle.QuantityChange;

                        movements.Add(new SerialLedgerMove
                        {
                            PostingDateTime = sle.PostingDateTime,
                            CreationTime = sle.CreationTime,
                            Qty = qty,
                            WarehouseId = entry?.WarehouseId ?? bundle.WarehouseId,
                            VoucherType = sle.VoucherType ?? string.Empty,
                            VoucherId = sle.VoucherId,
                        });
                    }
                }
            }

            var latestMove = movements
                .OrderByDescending(m => m.PostingDateTime)
                .ThenByDescending(m => m.CreationTime)
                .FirstOrDefault();

            bool changed = false;

            if (latestMove == null)
            {
                if (serial.WarehouseId.HasValue || serial.Status != SerialNoStatus.Inactive)
                {
                    serial.WarehouseId = null;
                    serial.Status = SerialNoStatus.Inactive;
                    changed = true;
                }
            }
            else if (latestMove.Qty <= 0m)
            {
                // Left warehouse
                if (serial.WarehouseId.HasValue || serial.Status == SerialNoStatus.Active)
                {
                    serial.WarehouseId = null;
                    serial.Status = (latestMove.VoucherType == "Delivery Note" || latestMove.VoucherType == "Sales Invoice")
                        ? SerialNoStatus.Delivered
                        : SerialNoStatus.Consumed;
                    changed = true;
                }
            }
            else
            {
                // Active in latestMove.WarehouseId
                if (serial.WarehouseId != latestMove.WarehouseId || serial.Status != SerialNoStatus.Active)
                {
                    serial.WarehouseId = latestMove.WarehouseId;
                    serial.Status = SerialNoStatus.Active;
                    changed = true;
                }
            }

            if (changed)
            {
                await _serialNoRepo.UpdateAsync(serial);
                syncedCount++;
            }
        }

        return new SyncSerialNoStatusResultDto
        {
            SyncedCount = syncedCount,
            Message = $"Successfully synced {syncedCount} serial number(s) status.",
        };
    }

    private async Task<List<Guid>> ResolveWarehouseIdsAsync(Guid? warehouseId, Guid? companyId)
    {
        var warehouseQuery = await _warehouseRepo.GetQueryableAsync();
        if (companyId.HasValue)
        {
            warehouseQuery = warehouseQuery.Where(w => w.CompanyId == companyId.Value);
        }
        var allWarehouses = warehouseQuery.ToList();

        if (!warehouseId.HasValue)
        {
            return allWarehouses.Where(w => !w.IsGroup).Select(w => w.Id).ToList();
        }

        var selected = allWarehouses.FirstOrDefault(w => w.Id == warehouseId.Value);
        if (selected == null)
        {
            return new List<Guid> { warehouseId.Value };
        }

        if (!selected.IsGroup)
        {
            return new List<Guid> { selected.Id };
        }

        var result = new HashSet<Guid>();
        void AddChildren(Guid parentId)
        {
            foreach (var child in allWarehouses.Where(w => w.ParentWarehouseId == parentId))
            {
                if (child.IsGroup)
                {
                    AddChildren(child.Id);
                }
                else
                {
                    result.Add(child.Id);
                }
            }
        }
        AddChildren(selected.Id);

        return result.ToList();
    }

    private class SerialLedgerMove
    {
        public DateTime PostingDateTime { get; set; }
        public DateTime CreationTime { get; set; }
        public decimal Qty { get; set; }
        public Guid WarehouseId { get; set; }
        public string VoucherType { get; set; } = string.Empty;
        public Guid? VoucherId { get; set; }
    }
}

