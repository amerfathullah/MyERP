using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class RequestedItemsFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class RequestedItemToOrderAndReceiveRowDto
{
    public Guid MaterialRequestId { get; set; }
    public string MaterialRequestNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Qty { get; set; }
    public decimal StockQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal QtyToOrder { get; set; }
    public decimal QtyToReceive { get; set; }
    public string Uom { get; set; } = string.Empty;
    public string StockUom { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
}

public class RequestedItemsToOrderAndReceiveReportDto
{
    public List<RequestedItemToOrderAndReceiveRowDto> Rows { get; set; } = new();
    public decimal TotalQty { get; set; }
    public decimal TotalOrderedQty { get; set; }
    public decimal TotalReceivedQty { get; set; }
    public decimal TotalQtyToOrder { get; set; }
    public decimal TotalQtyToReceive { get; set; }
}
