using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class PendingSoItemsForPurchaseRequestRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class PendingSoItemsForPurchaseRequestReportDto
{
    public Guid CompanyId { get; set; }
    public List<PendingSoItemsForPurchaseRequestRowDto> Rows { get; set; } = new();
    public int TotalSalesOrders { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalSoQuantity { get; set; }
    public decimal TotalRequestedQuantity { get; set; }
    public decimal TotalPendingQuantity { get; set; }
}

public class PendingSoItemsForPurchaseRequestRowDto
{
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string MaterialRequests { get; set; } = string.Empty;
    public decimal SoQuantity { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal PendingQuantity { get; set; }
    public bool IsProductBundleComponent { get; set; }
}
