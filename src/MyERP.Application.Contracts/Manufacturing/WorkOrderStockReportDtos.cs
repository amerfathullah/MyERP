using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class WorkOrderStockReportRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? ItemId { get; set; }
    public bool ReadyToBuildOnly { get; set; }
}

public class WorkOrderStockReportDto
{
    public Guid CompanyId { get; set; }
    public List<WorkOrderStockReportRowDto> Rows { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public int ReadyToBuildCount { get; set; }
    public int NotReadyCount { get; set; }
    public decimal TotalBuildableQty { get; set; }
}

public class WorkOrderStockReportRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid BomId { get; set; }
    public string BomNumber { get; set; } = null!;
    public string? Description { get; set; }
    public Guid ProductionItemId { get; set; }
    public string ProductionItemCode { get; set; } = null!;
    public string ProductionItemName { get; set; } = null!;
    public Guid? SourceWarehouseId { get; set; }
    public string? SourceWarehouseName { get; set; }
    public decimal QtyToBuild { get; set; }
    public int RequiredItemsCount { get; set; }
    public int InStockItemsCount { get; set; }
    public decimal BuildableQty { get; set; }
    public bool ReadyToBuild { get; set; }
    public List<WorkOrderStockItemDetailDto> ComponentDetails { get; set; } = new();
}

public class WorkOrderStockItemDetailDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal RequiredQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal BuildableQty { get; set; }
    public bool IsInStock { get; set; }
}
