using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetSalesOrderAnalysisDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? ItemId { get; set; }
    public bool GroupBySo { get; set; }
    public bool GroupByItem { get; set; }
}

public class SalesOrderAnalysisRowDto
{
    public string? SalesOrderNumber { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateTime? Date { get; set; }
    public string? CustomerName { get; set; }
    public Guid? CustomerId { get; set; }

    public string? ItemCode { get; set; }
    public Guid? ItemId { get; set; }
    public string? Description { get; set; }
    public string? Uom { get; set; }

    public decimal Qty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal PendingQty { get; set; }
    public decimal BilledQty { get; set; }
    public decimal QtyToBill { get; set; }

    public decimal Amount { get; set; }
    public decimal DeliveredQtyAmount { get; set; }
    public decimal BilledAmount { get; set; }
    public decimal PendingAmount { get; set; }

    public DateTime? DeliveryDate { get; set; }
    public int DelayDays { get; set; }

    public string? WarehouseName { get; set; }
    public Guid? WarehouseId { get; set; }

    public string? CompanyName { get; set; }
    public Guid CompanyId { get; set; }
}

public class SalesOrderAnalysisReportDto
{
    public List<SalesOrderAnalysisRowDto> Rows { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal TotalAmountToBill { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalDeliveredQty { get; set; }
}
