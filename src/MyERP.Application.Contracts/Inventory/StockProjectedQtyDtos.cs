using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockProjectedQtyFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? Brand { get; set; }
    public bool ShowOnlyShortage { get; set; }
    public string? SearchTerm { get; set; }
}

public class StockProjectedQtyRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroup { get; set; }
    public string? Brand { get; set; }
    public string Uom { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal ActualQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal IndentedQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal ReservedQtyForProduction { get; set; }
    public decimal ReservedQtyForSubContract { get; set; }
    public decimal ReservedQtyForProductionPlan { get; set; }
    public decimal TotalReservedQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQty { get; set; }
    public decimal ShortageQty { get; set; }
    public bool HasShortage { get; set; }
}

public class StockProjectedQtyReportDto
{
    public List<StockProjectedQtyRowDto> Rows { get; set; } = new();
    public int TotalItems => Rows.Count;
    public int ShortageCount { get; set; }
    public decimal TotalActualQty { get; set; }
    public decimal TotalProjectedQty { get; set; }
    public decimal TotalShortageQty { get; set; }
}
