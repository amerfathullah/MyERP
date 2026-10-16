using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class ProcurementTrackerFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool ShowCompletedOrders { get; set; }
}

public class ProcurementTrackerRowDto
{
    public DateTime? MaterialRequestDate { get; set; }
    public string? MaterialRequestNo { get; set; }
    public string? CostCenter { get; set; }
    public string? Project { get; set; }
    public string? RequestingSite { get; set; }
    public string? Requestor { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasurement { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PurchaseOrderDate { get; set; }
    public string? PurchaseOrderNo { get; set; }
    public string? SupplierName { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal PurchaseOrderAmount { get; set; }
    public decimal PurchaseOrderAmountCompanyCurrency { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
}

public class ProcurementTrackerSummaryDto
{
    public int TotalRequisitions { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public decimal TotalActualCost { get; set; }
    public decimal TotalPurchaseOrderAmount { get; set; }
}

public class ProcurementTrackerReportDto
{
    public List<ProcurementTrackerRowDto> Rows { get; set; } = new();
    public ProcurementTrackerSummaryDto Summary { get; set; } = new();
}
