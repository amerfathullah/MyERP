using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Inventory Aging Report — identifies slow-moving and dead stock.
/// Critical for working capital optimization and warehouse space management.
/// 
/// ERPNext equivalent: stock/report/stock_ageing
/// Uses last stock movement date to determine age of each item in each warehouse.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class InventoryAgingAppService : ApplicationService, IInventoryAgingAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public InventoryAgingAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _binRepository = binRepository;
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<InventoryAgingReportDto> GetReportAsync(InventoryAgingRequestDto input)
    {
        var asOfDate = DateTime.UtcNow.Date;

        // Get all bins with positive stock for the company's warehouses
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        var companyWarehouseIds = warehouseQuery
            .Where(w => w.CompanyId == input.CompanyId && !w.IsGroup)
            .Select(w => w.Id)
            .ToHashSet();

        var binQuery = await _binRepository.GetQueryableAsync();
        var stockBins = binQuery
            .Where(b => companyWarehouseIds.Contains(b.WarehouseId) && b.ActualQty > 0)
            .Take(500)
            .ToList();

        if (!stockBins.Any())
        {
            return new InventoryAgingReportDto
            {
                AsOfDate = asOfDate,
                Buckets = GetEmptyBuckets()
            };
        }

        // Get last movement date per (item, warehouse) from SLE
        var itemWarehousePairs = stockBins.Select(b => new { b.ItemId, b.WarehouseId }).ToList();
        var sleQuery = await _sleRepository.GetQueryableAsync();

        // For each item+warehouse, find the last stock movement date
        var lastMovementLookup = new Dictionary<(Guid, Guid), DateTime>();
        var itemIds = stockBins.Select(b => b.ItemId).Distinct().ToList();

        var recentSles = sleQuery
            .Where(s => itemIds.Contains(s.ItemId) && companyWarehouseIds.Contains(s.WarehouseId) && !s.IsCancelled)
            .GroupBy(s => new { s.ItemId, s.WarehouseId })
            .Select(g => new { g.Key.ItemId, g.Key.WarehouseId, LastDate = g.Max(s => s.PostingDate) })
            .ToList();

        foreach (var sle in recentSles)
            lastMovementLookup[(sle.ItemId, sle.WarehouseId)] = sle.LastDate;

        // Resolve item + warehouse names
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemInfo = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });

        var warehouseNames = warehouseQuery
            .Where(w => companyWarehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionary(w => w.Id, w => w.Name);

        // Build result items with aging calculation
        var items = new List<InventoryAgingItemDto>();
        foreach (var bin in stockBins)
        {
            var lastMovement = lastMovementLookup.GetValueOrDefault((bin.ItemId, bin.WarehouseId));
            var ageDays = lastMovement != default
                ? (int)(asOfDate - lastMovement).TotalDays
                : 365; // No movement found → assume very old

            var item = itemInfo.GetValueOrDefault(bin.ItemId);
            var stockValue = bin.ActualQty * bin.ValuationRate;

            items.Add(new InventoryAgingItemDto
            {
                ItemId = bin.ItemId,
                ItemCode = item?.ItemCode ?? "—",
                ItemName = item?.ItemName ?? "—",
                WarehouseId = bin.WarehouseId,
                WarehouseName = warehouseNames.GetValueOrDefault(bin.WarehouseId, "—"),
                Quantity = bin.ActualQty,
                ValuationRate = bin.ValuationRate,
                StockValue = stockValue,
                LastMovementDate = lastMovement != default ? lastMovement : null,
                AgeDays = ageDays,
                AgeBucket = GetAgeBucket(ageDays, input.SlowMovingDays, input.DeadStockDays)
            });
        }

        // Sort by age descending (oldest first — most problematic at top)
        items = items.OrderByDescending(i => i.AgeDays).ToList();

        // Calculate summary
        var slowMovingItems = items.Where(i => i.AgeDays >= input.SlowMovingDays && i.AgeDays < input.DeadStockDays).ToList();
        var deadStockItems = items.Where(i => i.AgeDays >= input.DeadStockDays).ToList();
        var totalValue = items.Sum(i => i.StockValue);

        // Build bucket summary
        var buckets = BuildBuckets(items, totalValue, input.SlowMovingDays, input.DeadStockDays);

        return new InventoryAgingReportDto
        {
            AsOfDate = asOfDate,
            TotalItems = items.Count,
            TotalStockValue = totalValue,
            SlowMovingValue = slowMovingItems.Sum(i => i.StockValue),
            SlowMovingCount = slowMovingItems.Count,
            DeadStockValue = deadStockItems.Sum(i => i.StockValue),
            DeadStockCount = deadStockItems.Count,
            Buckets = buckets,
            Items = items.Take(100).ToList() // Top 100 oldest items
        };
    }

    private static string GetAgeBucket(int ageDays, int slowMovingDays, int deadStockDays)
    {
        if (ageDays < 30) return "0-30 days";
        if (ageDays < 60) return "31-60 days";
        if (ageDays < slowMovingDays) return $"61-{slowMovingDays - 1} days";
        if (ageDays < deadStockDays) return $"Slow Moving ({slowMovingDays}-{deadStockDays - 1}d)";
        return $"Dead Stock ({deadStockDays}+d)";
    }

    private static List<InventoryAgingBucketDto> BuildBuckets(
        List<InventoryAgingItemDto> items, decimal totalValue, int slowMovingDays, int deadStockDays)
    {
        var bucketDefs = new[]
        {
            ("0-30 days", 0, 30),
            ("31-60 days", 31, 60),
            ($"61-{slowMovingDays - 1} days", 61, slowMovingDays - 1),
            ($"Slow Moving ({slowMovingDays}-{deadStockDays - 1}d)", slowMovingDays, deadStockDays - 1),
            ($"Dead Stock ({deadStockDays}+d)", deadStockDays, int.MaxValue)
        };

        return bucketDefs.Select(b =>
        {
            var bucketItems = items.Where(i => i.AgeDays >= b.Item2 && i.AgeDays <= b.Item3).ToList();
            var bucketValue = bucketItems.Sum(i => i.StockValue);
            return new InventoryAgingBucketDto
            {
                Label = b.Item1,
                ItemCount = bucketItems.Count,
                StockValue = bucketValue,
                Percentage = totalValue > 0 ? Math.Round(bucketValue / totalValue * 100, 1) : 0
            };
        }).ToList();
    }

    private static List<InventoryAgingBucketDto> GetEmptyBuckets() =>
    [
        new() { Label = "0-30 days" },
        new() { Label = "31-60 days" },
        new() { Label = "61-89 days" },
        new() { Label = "Slow Moving (90-179d)" },
        new() { Label = "Dead Stock (180+d)" }
    ];
}
