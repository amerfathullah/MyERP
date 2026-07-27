using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class ProductionAnalyticsAppService : ApplicationService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public ProductionAnalyticsAppService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _workOrderRepository = workOrderRepository;
        _itemRepository = itemRepository;
    }

    public async Task<ProductionAnalyticsDto> GetAnalyticsAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var query = await _workOrderRepository.GetQueryableAsync();
        var workOrders = query
            .Where(wo => wo.CompanyId == companyId)
            .Where(wo => wo.CreationTime >= fromDate && wo.CreationTime <= toDate.AddDays(1))
            .ToList();

        var totalCount = workOrders.Count;
        var completedCount = workOrders.Count(wo => wo.Status == WorkOrderStatus.Completed);
        var inProcessCount = workOrders.Count(wo => wo.Status == WorkOrderStatus.InProcess);
        var notStartedCount = workOrders.Count(wo => wo.Status == WorkOrderStatus.NotStarted);
        var stoppedCount = workOrders.Count(wo => wo.Status == WorkOrderStatus.Stopped);
        var draftCount = workOrders.Count(wo => wo.Status == WorkOrderStatus.Draft);

        var overdueCount = workOrders.Count(wo =>
            wo.PlannedEndDate.HasValue &&
            wo.PlannedEndDate.Value < DateTime.UtcNow &&
            wo.Status != WorkOrderStatus.Completed &&
            wo.Status != WorkOrderStatus.Cancelled);

        var totalPlannedQty = workOrders.Sum(wo => wo.Quantity);
        var totalProducedQty = workOrders.Sum(wo => wo.ProducedQuantity);
        var completionRate = totalCount > 0 ? (decimal)completedCount / totalCount * 100 : 0;

        // Status breakdown
        var statusBreakdown = new List<ProductionStatusCountDto>
        {
            new() { Status = "Draft", Count = draftCount, Color = "secondary" },
            new() { Status = "NotStarted", Count = notStartedCount, Color = "info" },
            new() { Status = "InProcess", Count = inProcessCount, Color = "primary" },
            new() { Status = "Completed", Count = completedCount, Color = "success" },
            new() { Status = "Stopped", Count = stoppedCount, Color = "warning" },
        };

        // Daily production trend (last 30 days within range)
        var trendStart = toDate.AddDays(-29) < fromDate ? fromDate : toDate.AddDays(-29);
        var completedWos = workOrders
            .Where(wo => wo.Status == WorkOrderStatus.Completed && wo.LastModificationTime.HasValue)
            .ToList();

        var dailyTrend = new List<DailyProductionPointDto>();
        for (var day = trendStart; day <= toDate; day = day.AddDays(1))
        {
            var dayProduced = completedWos
                .Where(wo => wo.LastModificationTime!.Value.Date == day.Date)
                .Sum(wo => wo.ProducedQuantity);
            dailyTrend.Add(new DailyProductionPointDto { Date = day, ProducedQty = dayProduced });
        }

        // Resolve item names
        var itemIds = workOrders.Where(wo => wo.ProducedQuantity > 0).Select(wo => wo.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id)).Select(i => new { i.Id, i.ItemName }).ToList()
            .ToDictionary(i => i.Id, i => i.ItemName ?? "");

        // Top items by production volume
        var topItems = workOrders
            .Where(wo => wo.ProducedQuantity > 0)
            .GroupBy(wo => wo.ItemId)
            .Select(g => new TopProducedItemDto
            {
                ItemId = g.Key,
                ItemName = itemNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                TotalProduced = g.Sum(wo => wo.ProducedQuantity),
                WorkOrderCount = g.Count()
            })
            .OrderByDescending(x => x.TotalProduced)
            .Take(10)
            .ToList();

        return new ProductionAnalyticsDto
        {
            TotalWorkOrders = totalCount,
            CompletedCount = completedCount,
            InProcessCount = inProcessCount,
            OverdueCount = overdueCount,
            CompletionRate = Math.Round(completionRate, 1),
            TotalPlannedQty = totalPlannedQty,
            TotalProducedQty = totalProducedQty,
            ProductionEfficiency = totalPlannedQty > 0 ? Math.Round(totalProducedQty / totalPlannedQty * 100, 1) : 0,
            StatusBreakdown = statusBreakdown,
            DailyTrend = dailyTrend,
            TopProducedItems = topItems
        };
    }
}

public class ProductionAnalyticsDto
{
    public int TotalWorkOrders { get; set; }
    public int CompletedCount { get; set; }
    public int InProcessCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal TotalPlannedQty { get; set; }
    public decimal TotalProducedQty { get; set; }
    public decimal ProductionEfficiency { get; set; }
    public List<ProductionStatusCountDto> StatusBreakdown { get; set; } = new();
    public List<DailyProductionPointDto> DailyTrend { get; set; } = new();
    public List<TopProducedItemDto> TopProducedItems { get; set; } = new();
}

public class ProductionStatusCountDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Color { get; set; } = "secondary";
}

public class DailyProductionPointDto
{
    public DateTime Date { get; set; }
    public decimal ProducedQty { get; set; }
}

public class TopProducedItemDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal TotalProduced { get; set; }
    public int WorkOrderCount { get; set; }
}
