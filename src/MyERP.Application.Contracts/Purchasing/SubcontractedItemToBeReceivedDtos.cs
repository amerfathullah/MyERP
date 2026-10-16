using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class SubcontractedItemToBeReceivedFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? SupplierId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class SubcontractedItemToBeReceivedRowDto
{
    public Guid SubcontractOrderId { get; set; }
    public string SubcontractOrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid FgItemId { get; set; }
    public string FgItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal PendingQty { get; set; }
}

public class SubcontractedItemToBeReceivedSummaryDto
{
    public decimal TotalRequiredQty { get; set; }
    public decimal TotalReceivedQty { get; set; }
    public decimal TotalPendingQty { get; set; }
    public int TotalOrders { get; set; }
}

public class SubcontractedItemToBeReceivedReportDto
{
    public List<SubcontractedItemToBeReceivedRowDto> Rows { get; set; } = new();
    public SubcontractedItemToBeReceivedSummaryDto Summary { get; set; } = new();
}
