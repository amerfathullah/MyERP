using System;
using System.Collections.Generic;
using MyERP.Core;

namespace MyERP.Inventory;

public class LandedCostReportFilterDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ReceiptDocumentType { get; set; } // "All", "PurchaseReceipt", "PurchaseInvoice"
    public string? ReceiptDocumentNo { get; set; }
    public DocumentStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
}

public class LandedCostReportRowDto
{
    public Guid LandedCostVoucherId { get; set; }
    public string LandedCostVoucherNo { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public DocumentStatus Status { get; set; }
    public LandedCostDistributionMethod DistributionMethod { get; set; }
    public decimal TotalLandedCost { get; set; }
    public string ReceiptDocumentType { get; set; } = string.Empty;
    public Guid ReceiptDocumentId { get; set; }
    public string ReceiptDocumentNo { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal ReceiptTotalAmount { get; set; }
    public decimal AllocatedCharges { get; set; }
    public int ItemCount { get; set; }
    public string ChargesSummary { get; set; } = string.Empty;
    public string ItemsSummary { get; set; } = string.Empty;
}

public class LandedCostReportDto
{
    public int TotalVouchersCount { get; set; }
    public decimal TotalLandedCost { get; set; }
    public int TotalReceiptsAffected { get; set; }
    public List<LandedCostReportRowDto> Rows { get; set; } = new();
}
