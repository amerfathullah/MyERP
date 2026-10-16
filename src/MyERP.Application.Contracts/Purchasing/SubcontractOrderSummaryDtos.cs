using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class SubcontractOrderSummaryFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? SubcontractOrderId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class SubcontractOrderSummaryRowDto
{
    public Guid SubcontractOrderId { get; set; }
    public string SubcontractOrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? FgItemId { get; set; }
    public string? FgItemCode { get; set; }
    public string? FgItemName { get; set; }
    public decimal OrderQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal PendingFgQty => Math.Max(0m, OrderQty - ReceivedQty);
    public Guid? RmItemId { get; set; }
    public string? RmItemCode { get; set; }
    public string? RmItemName { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal SuppliedQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal PendingRmQty => Math.Max(0m, RequiredQty - SuppliedQty);
}

public class SubcontractOrderSummaryReportDto
{
    public List<SubcontractOrderSummaryRowDto> Rows { get; set; } = new();
    public int TotalOrders { get; set; }
    public decimal TotalOrderQty { get; set; }
    public decimal TotalReceivedQty { get; set; }
    public decimal TotalRequiredQty { get; set; }
    public decimal TotalSuppliedQty { get; set; }
    public decimal TotalConsumedQty { get; set; }
}
