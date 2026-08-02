using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockGlComparisonRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class StockGlComparisonDto
{
    public decimal TotalStockValue { get; set; }
    public decimal TotalGlBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsMatched { get; set; }
    public int WarehouseCount { get; set; }
    public int ItemCount { get; set; }
    public DateTime AsOfDate { get; set; }
    public List<StockGlWarehouseComparisonDto> PerWarehouse { get; set; } = new();
}

public class StockGlWarehouseComparisonDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal StockValue { get; set; }
    public decimal GlBalance { get; set; }
    public decimal Difference { get; set; }
    public bool HasMismatch { get; set; }
    public Guid? StockAccountId { get; set; }
    public string? StockAccountName { get; set; }
}
