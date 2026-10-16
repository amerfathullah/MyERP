using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class GetPurchaseOrderAnalysisDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ProjectId { get; set; }
    public bool GroupByPo { get; set; }
    public bool GroupByItem { get; set; }
}

public class PurchaseOrderAnalysisRowDto
{
    public string? PurchaseOrderNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? RequiredDate { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Status { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public string? Uom { get; set; }
    public decimal Qty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal PendingQty { get; set; }
    public decimal BilledQty { get; set; }
    public decimal QtyToBill { get; set; }
    public decimal Amount { get; set; }
    public decimal ReceivedQtyAmount { get; set; }
    public decimal BilledAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public int DelayDays { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid CompanyId { get; set; }
    public string? CompanyName { get; set; }
}

public class PurchaseOrderAnalysisReportDto
{
    public List<PurchaseOrderAnalysisRowDto> Rows { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal TotalAmountToBill { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalReceivedQty { get; set; }
}
