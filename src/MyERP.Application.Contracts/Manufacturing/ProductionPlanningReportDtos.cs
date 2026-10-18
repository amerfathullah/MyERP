using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class ProductionPlanningReportFilterDto
{
    public Guid? CompanyId { get; set; }
    public string BasedOn { get; set; } = "WorkOrder"; // "WorkOrder", "SalesOrder", "MaterialRequest"
    public Guid? RawMaterialWarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public string? SearchTerm { get; set; }
}

public class ProductionPlanningReportRowDto
{
    public Guid RowId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string ProductionItemCode { get; set; } = string.Empty;
    public string ProductionItemName { get; set; } = string.Empty;
    public string ForWarehouse { get; set; } = string.Empty;
    public decimal QtyToManufacture { get; set; }
    public decimal AvailableProductionQty { get; set; }
    public DateTime? PlannedDate { get; set; }
    public string RawMaterialCode { get; set; } = string.Empty;
    public string RawMaterialName { get; set; } = string.Empty;
    public string RawMaterialWarehouse { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal AllottedQty { get; set; }
    public decimal ShortageQty => Math.Max(0m, RequiredQty - AllottedQty);
    public DateTime? ExpectedArrivalDate { get; set; }
    public decimal ExpectedArrivalQty { get; set; }
}

public class ProductionPlanningReportDto
{
    public int TotalOrdersCount { get; set; }
    public int TotalRawMaterialsCount { get; set; }
    public decimal TotalRequiredQty { get; set; }
    public decimal TotalAllottedQty { get; set; }
    public decimal TotalShortageQty { get; set; }
    public decimal TotalExpectedArrivalQty { get; set; }
    public List<ProductionPlanningReportRowDto> Rows { get; set; } = new();
}
