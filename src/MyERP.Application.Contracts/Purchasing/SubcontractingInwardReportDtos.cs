using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

/// <summary>
/// Filter criteria for Subcontracting Inward reports (ERPNext PR #59395).
/// </summary>
public class SubcontractingInwardReportFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? SubcontractingInwardOrderId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ItemId { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Row item for Subcontracted Items to be Delivered report (ERPNext PR #59395).
/// </summary>
public class SubcontractedItemToBeDeliveredRowDto
{
    public Guid SubcontractingInwardOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Uom { get; set; } = string.Empty;
    public decimal OrderQty { get; set; }
    public decimal ProducedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal PendingQty { get; set; }
}

/// <summary>
/// Report DTO for Subcontracted Items to be Delivered (ERPNext PR #59395).
/// </summary>
public class SubcontractedItemsToBeDeliveredReportDto
{
    public List<SubcontractedItemToBeDeliveredRowDto> Rows { get; set; } = new();
    public decimal TotalOrderQty { get; set; }
    public decimal TotalProducedQty { get; set; }
    public decimal TotalDeliveredQty { get; set; }
    public decimal TotalPendingQty { get; set; }
}

/// <summary>
/// Row item for Subcontracting Inward Order Summary report (ERPNext PR #59395).
/// </summary>
public class SubcontractingInwardOrderSummaryRowDto
{
    public Guid SubcontractingInwardOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public SubcontractingInwardOrderStatus Status { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Uom { get; set; } = string.Empty;
    public decimal OrderQty { get; set; }
    public decimal ProducedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal PendingQty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Report DTO for Subcontracting Inward Order Summary (ERPNext PR #59395).
/// </summary>
public class SubcontractingInwardOrderSummaryReportDto
{
    public List<SubcontractingInwardOrderSummaryRowDto> Rows { get; set; } = new();
    public decimal TotalOrderQty { get; set; }
    public decimal TotalProducedQty { get; set; }
    public decimal TotalDeliveredQty { get; set; }
    public decimal TotalPendingQty { get; set; }
    public decimal TotalAmount { get; set; }
}
