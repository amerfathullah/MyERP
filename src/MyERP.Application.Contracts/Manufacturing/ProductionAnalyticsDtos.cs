using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

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
