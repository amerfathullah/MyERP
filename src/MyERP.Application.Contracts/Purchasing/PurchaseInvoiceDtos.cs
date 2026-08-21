using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Purchasing;

public class PurchaseInvoiceDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierTin { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
    public decimal BaseOutstandingAmount { get; set; }
    public string Status { get; set; } = null!;
    public string EInvoiceStatus { get; set; } = null!;
    public string? LhdnUuid { get; set; }
    public bool IsReturn { get; set; }
    public bool IsSubcontracted { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }
    public Guid CreditToAccountId { get; set; }

    /// <summary>Days past due date. 0 when not overdue or no due date.</summary>
    public int DaysOverdue { get; set; }

    /// <summary>True when posted, has outstanding, past due date, and not a return.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>3-way matching status: FullyMatched, PartiallyMatched, Unmatched, DirectPurchase.</summary>
    public string? MatchingStatus { get; set; }

    public string? HoldComment { get; set; }
    public DateTime? ReleaseDate { get; set; }

    /// <summary>True when OnHold and no release date has passed yet — blocks Payment Entry.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>True when: Posted + outstanding > 0 + not on hold + fully matched (or no PO link).</summary>
    public bool IsReadyForPayment { get; set; }

    public bool OnHold { get; set; }

    /// <summary>Set when auto-created from an inter-company Sales Invoice, or when this PI auto-created one.</summary>
    public Guid? InterCompanyInvoiceId { get; set; }
    public string? InterCompanyInvoiceNumber { get; set; }
    public string? InterCompanyCompanyName { get; set; }

    public List<PurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class PurchaseInvoiceItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public Guid? PurchaseReceiptItemId { get; set; }
    public bool EnableDeferredExpense { get; set; }
    public Guid? DeferredExpenseAccountId { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public DateTime? ServiceStopDate { get; set; }
}

/// <summary>3-way matching detail per PI item — enriched with PO/PR quantities and rates.</summary>
public class ThreeWayMatchingItemDto
{
    public Guid PiItemId { get; set; }
    public string ItemDescription { get; set; } = null!;
    public decimal BilledQty { get; set; }
    public decimal BilledRate { get; set; }
    public decimal? OrderedQty { get; set; }
    public decimal? OrderedRate { get; set; }
    public decimal? ReceivedQty { get; set; }
    /// <summary>Qty variance: received - billed (negative = under-receipt, positive = over-receipt vs billing)</summary>
    public decimal? QtyVariance { get; set; }
    /// <summary>Rate variance: PO rate - PI rate (negative = PI charged more than PO)</summary>
    public decimal? RateVariance { get; set; }
    /// <summary>3-Way (PO+PR+PI), 2-Way (PO+PI), Direct (PI only)</summary>
    public string MatchLevel { get; set; } = "Direct";
    public bool HasQtyDiscrepancy { get; set; }
    public bool HasRateDiscrepancy { get; set; }
}

/// <summary>Result of real-time supplier invoice duplicate check (advisory, non-blocking).</summary>
public class DuplicateInvoiceCheckResultDto
{
    public bool IsDuplicate { get; set; }
    public Guid? ExistingInvoiceId { get; set; }
    public string? ExistingInvoiceNumber { get; set; }
    public DateTime? ExistingInvoiceDate { get; set; }
    public decimal? ExistingInvoiceAmount { get; set; }
}

/// <summary>Tax withholding (TDS/WHT) entry for a purchase invoice — per Malaysia Section 107A.</summary>
public class TaxWithholdingEntryDto
{
    public Guid Id { get; set; }
    public string? TaxCategory { get; set; }
    public decimal WithholdingRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public DateTime PostingDate { get; set; }
    public bool HasLDC { get; set; }
    public decimal? LdcRate { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Status { get; set; }
}

public class CreatePurchaseInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid SupplierId { get; set; }
    [Required] public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? PaymentTermsTemplateId { get; set; }
    [StringLength(100)] public string? SupplierInvoiceNumber { get; set; }
    [StringLength(3)] public string CurrencyCode { get; set; } = "MYR";
    public string? Notes { get; set; }

    /// <summary>Cost center for departmental P&L attribution.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Project for project-wise expense tracking.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Mark as opening balance invoice (data migration). Blocks update_stock, clears payment terms.</summary>
    public bool IsOpening { get; set; }

    /// <summary>Mark as return (debit note). Items must have negative quantities.</summary>
    public bool IsReturn { get; set; }

    /// <summary>Mark as subcontracted purchase invoice.</summary>
    public bool IsSubcontracted { get; set; }

    /// <summary>Original invoice this return is against.</summary>
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>When true, stock is received on invoice submit (direct purchase without PR).</summary>
    public bool UpdateStock { get; set; }

    /// <summary>Warehouse for stock receipt when UpdateStock=true.</summary>
    public Guid? WarehouseId { get; set; }

    [Required][MinLength(1)] public List<CreatePurchaseInvoiceItemDto> Items { get; set; } = new();
}

public class CreatePurchaseInvoiceItemDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(500)] public string Description { get; set; } = null!;
    /// <summary>Quantity (positive for normal invoices, negative for debit notes/returns).</summary>
    [Required] public decimal Quantity { get; set; }
    [Required][Range(0, double.MaxValue)] public decimal UnitPrice { get; set; }
    [Range(0, double.MaxValue)] public decimal TaxAmount { get; set; }
    [StringLength(50)] public string Uom { get; set; } = "Unit";
    public bool EnableDeferredExpense { get; set; }
    public Guid? DeferredExpenseAccountId { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public DateTime? ServiceStopDate { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public Guid? PurchaseReceiptItemId { get; set; }
}

/// <summary>
/// KPI summary for the Purchase Invoice list page.
/// Provides at-a-glance payables visibility: total payable, overdue, monthly spend.
/// </summary>
public class PurchaseInvoiceListSummaryDto
{
    /// <summary>Total outstanding amount across all posted non-return purchase invoices.</summary>
    public decimal TotalPayable { get; set; }

    /// <summary>Number of invoices past due date with outstanding > 0.</summary>
    public int OverdueCount { get; set; }

    /// <summary>Total overdue amount.</summary>
    public decimal OverdueAmount { get; set; }

    /// <summary>Total spend this month (sum of GrandTotal for PIs posted this calendar month).</summary>
    public decimal MonthlySpend { get; set; }

    /// <summary>Number of invoices posted this month.</summary>
    public int MonthlyInvoiceCount { get; set; }

    /// <summary>Total number of posted purchase invoices.</summary>
    public int PostedInvoiceCount { get; set; }
}

/// <summary>Payment entry linked to an invoice (for payment history display).</summary>
public class InvoicePaymentDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
}

/// <summary>DTO for an unbilled Purchase Receipt item ready for billing.</summary>
public class UnbilledReceiptItemDto
{
    public Guid PurchaseReceiptId { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime ReceiptDate { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public string? Uom { get; set; }
    public Guid PurchaseReceiptItemId { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
}

/// <summary>DTO for an unbilled Purchase Order item ready for billing.</summary>
public class UnbilledPurchaseOrderItemDto
{
    public Guid PurchaseOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public DateTime OrderDate { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public string? Uom { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
}

/// <summary>DTO for an unbilled Purchase Receipt item ready for billing.</summary>
public class UnbilledPurchaseReceiptItemDto
{
    public Guid PurchaseReceiptId { get; set; }
    public string? ReceiptNumber { get; set; }
    public DateTime ReceiptDate { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public string? Uom { get; set; }
    public Guid PurchaseReceiptItemId { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public Guid? WarehouseId { get; set; }
}

