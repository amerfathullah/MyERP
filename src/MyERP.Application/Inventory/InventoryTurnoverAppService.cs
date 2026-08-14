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

[Authorize(MyERPPermissions.Items.Default)]
public class InventoryTurnoverAppService : ApplicationService, IInventoryTurnoverAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;

    public InventoryTurnoverAppService(
        IRepository<StockLedgerEntry, Guid> sleRepo,
        IRepository<Bin, Guid> binRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<Warehouse, Guid> warehouseRepo)
    {
        _sleRepo = sleRepo;
        _binRepo = binRepo;
        _itemRepo = itemRepo;
        _warehouseRepo = warehouseRepo;
    }

    /// <summary>
    /// Inventory Turnover Analysis — shows consumption velocity per item.
    /// Per ERPNext Stock Analytics: COGS / Average Inventory = turnover ratio.
    /// </summary>
    public async Task<InventoryTurnoverReportDto> GetReportAsync(
        Guid companyId, DateTime fromDate, DateTime toDate, Guid? warehouseId = null)
    {
        var sleQuery = await _sleRepo.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => s.PostingDate >= fromDate && s.PostingDate <= toDate && !s.IsCancelled);

        if (warehouseId.HasValue)
            sleQuery = sleQuery.Where(s => s.WarehouseId == warehouseId.Value);

        // Get outgoing movements (consumption/sales) per item
        var outgoingByItem = sleQuery
            .Where(s => s.QuantityChange < 0)
            .GroupBy(s => s.ItemId)
            .Select(g => new { ItemId = g.Key, TotalConsumed = g.Sum(x => Math.Abs(x.QuantityChange)), TotalValue = g.Sum(x => Math.Abs(x.StockValueDifference)) })
            .ToList();

        // Get current stock per item from Bins
        var binQuery = await _binRepo.GetQueryableAsync();
        if (warehouseId.HasValue)
            binQuery = binQuery.Where(b => b.WarehouseId == warehouseId.Value);

        var currentStock = binQuery
            .Where(b => b.ActualQty > 0)
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, AvgQty = g.Sum(x => x.ActualQty), StockValue = g.Sum(x => x.ActualQty * x.ValuationRate) })
            .ToList();

        // Resolve item names
        var allItemIds = outgoingByItem.Select(x => x.ItemId).Union(currentStock.Select(x => x.ItemId)).Distinct().ToList();
        var itemQuery = await _itemRepo.GetQueryableAsync();
        var items = itemQuery.Where(i => allItemIds.Contains(i.Id)).Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList();
        var itemMap = items.ToDictionary(i => i.Id);

        var periodDays = Math.Max(1, (toDate - fromDate).Days);
        var rows = new List<InventoryTurnoverItemDto>();

        foreach (var consumed in outgoingByItem)
        {
            var stock = currentStock.FirstOrDefault(s => s.ItemId == consumed.ItemId);
            var avgInventory = stock?.StockValue ?? 0m;
            var turnoverRatio = avgInventory > 0 ? consumed.TotalValue / avgInventory : 0m;
            var daysToSell = turnoverRatio > 0 ? periodDays / (double)turnoverRatio : 0;

            itemMap.TryGetValue(consumed.ItemId, out var item);

            rows.Add(new InventoryTurnoverItemDto
            {
                ItemId = consumed.ItemId,
                ItemCode = item?.ItemCode ?? "",
                ItemName = item?.ItemName ?? "",
                ConsumedQty = consumed.TotalConsumed,
                ConsumedValue = consumed.TotalValue,
                CurrentStockQty = stock?.AvgQty ?? 0,
                CurrentStockValue = avgInventory,
                TurnoverRatio = turnoverRatio,
                DaysToSell = Math.Round(daysToSell, 1),
                Category = ClassifyTurnover(turnoverRatio, periodDays)
            });
        }

        // Add items with stock but no movement (dead stock)
        var consumedIds = outgoingByItem.Select(x => x.ItemId).ToHashSet();
        foreach (var stock in currentStock.Where(s => !consumedIds.Contains(s.ItemId)))
        {
            itemMap.TryGetValue(stock.ItemId, out var item);
            rows.Add(new InventoryTurnoverItemDto
            {
                ItemId = stock.ItemId,
                ItemCode = item?.ItemCode ?? "",
                ItemName = item?.ItemName ?? "",
                ConsumedQty = 0,
                ConsumedValue = 0,
                CurrentStockQty = stock.AvgQty,
                CurrentStockValue = stock.StockValue,
                TurnoverRatio = 0,
                DaysToSell = 0,
                Category = "Dead Stock"
            });
        }

        rows = rows.OrderByDescending(r => r.TurnoverRatio).ToList();

        return new InventoryTurnoverReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            PeriodDays = periodDays,
            TotalItems = rows.Count,
            FastMovingCount = rows.Count(r => r.Category == "Fast Moving"),
            SlowMovingCount = rows.Count(r => r.Category == "Slow Moving"),
            DeadStockCount = rows.Count(r => r.Category == "Dead Stock"),
            TotalStockValue = rows.Sum(r => r.CurrentStockValue),
            TotalConsumedValue = rows.Sum(r => r.ConsumedValue),
            Items = rows
        };
    }

    private static string ClassifyTurnover(decimal ratio, int periodDays)
    {
        // Annualized turnover classification
        var annualizedRatio = periodDays > 0 ? ratio * 365m / periodDays : 0;
        if (annualizedRatio >= 6) return "Fast Moving";    // Turns over 6+ times per year
        if (annualizedRatio >= 2) return "Normal";         // 2-6 times per year
        if (annualizedRatio > 0) return "Slow Moving";     // Less than 2 times per year
        return "Dead Stock";                               // No movement at all
    }
}

