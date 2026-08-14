using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class InventoryTurnoverReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PeriodDays { get; set; }
    public int TotalItems { get; set; }
    public int FastMovingCount { get; set; }
    public int SlowMovingCount { get; set; }
    public int DeadStockCount { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal TotalConsumedValue { get; set; }
    public List<InventoryTurnoverItemDto> Items { get; set; } = new();
}

public class InventoryTurnoverItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public decimal ConsumedQty { get; set; }
    public decimal ConsumedValue { get; set; }
    public decimal CurrentStockQty { get; set; }
    public decimal CurrentStockValue { get; set; }
    public decimal TurnoverRatio { get; set; }
    public double DaysToSell { get; set; }
    public string Category { get; set; } = "";
}
