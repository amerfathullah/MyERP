using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemShortageReportRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? SearchText { get; set; }
}

public class ItemShortageReportRowDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal ActualQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal ReservedQtyForProduction { get; set; }
    public decimal ProjectedQty { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
}

public class ItemShortageChartItemDto
{
    public string ItemCode { get; set; } = null!;
    public decimal ProjectedQty { get; set; }
}

public class ItemShortageReportDto
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = null!;
    public List<ItemShortageReportRowDto> Rows { get; set; } = new();
    public List<ItemShortageChartItemDto> ChartData { get; set; } = new();
    public int TotalShortageItems { get; set; }
    public decimal TotalProjectedDeficit { get; set; }
    public decimal TotalActualQty { get; set; }
    public decimal TotalOrderedQty { get; set; }
    public decimal TotalPlannedQty { get; set; }
    public decimal TotalReservedQty { get; set; }
}
