using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class InventoryAgingItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public int AgeDays { get; set; }
    public string AgeBucket { get; set; } = null!;
}

public class InventoryAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal SlowMovingValue { get; set; }
    public decimal DeadStockValue { get; set; }
    public int SlowMovingCount { get; set; }
    public int DeadStockCount { get; set; }
    public List<InventoryAgingBucketDto> Buckets { get; set; } = [];
    public List<InventoryAgingItemDto> Items { get; set; } = [];
}

public class InventoryAgingBucketDto
{
    public string Label { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal StockValue { get; set; }
    public decimal Percentage { get; set; }
}

public class InventoryAgingRequestDto
{
    public Guid CompanyId { get; set; }
    public int SlowMovingDays { get; set; } = 90;
    public int DeadStockDays { get; set; } = 180;
}
