using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class IssuedItemsAgainstWorkOrderRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
}

public class IssuedItemsAgainstWorkOrderReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<IssuedItemsAgainstWorkOrderRowDto> Rows { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public int TotalIssuedLines { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
}

public class IssuedItemsAgainstWorkOrderRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public Guid StockEntryId { get; set; }
    public string StockEntryNumber { get; set; } = null!;
    public Guid FinishedGoodItemId { get; set; }
    public string FinishedGoodCode { get; set; } = null!;
    public string FinishedGoodName { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = null!;
    public decimal Amount { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public string? SourceWarehouseName { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public string? TargetWarehouseName { get; set; }
    public decimal ProjectedQtyAtSource { get; set; }
    public decimal ActualQtyAtSource { get; set; }
}
