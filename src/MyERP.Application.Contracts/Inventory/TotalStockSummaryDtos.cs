using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class TotalStockSummaryFilterDto
{
    public Guid? CompanyId { get; set; }
    public string GroupBy { get; set; } = "Warehouse"; // "Warehouse" | "Company"
    public Guid? WarehouseId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? SearchTerm { get; set; }
}

public class TotalStockSummaryRowDto
{
    public string GroupName { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ItemGroup { get; set; }
    public string? Uom { get; set; }
    public decimal CurrentQty { get; set; }
    public decimal StockValue { get; set; }
    public decimal ValuationRate { get; set; }
}

public class TotalStockSummaryReportDto
{
    public string GroupBy { get; set; } = "Warehouse";
    public List<TotalStockSummaryRowDto> Rows { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalStockValue { get; set; }
}
