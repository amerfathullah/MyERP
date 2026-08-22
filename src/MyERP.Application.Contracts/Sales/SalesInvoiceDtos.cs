using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class PaymentScheduleDto
{
    public Guid Id { get; set; }
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }

    // Early Payment Discount fields (per ERPNext payment_schedule.discount_type)
    public string? DiscountType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime? DiscountValidTill { get; set; }
    public decimal DiscountedAmount { get; set; }
}

public class SalesTeamEntryDto
{
    public Guid SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public decimal AllocatedPercentage { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal Incentives { get; set; }
}

public class SalesTeamAllocationInputDto
{
    [Required]
    public Guid SalesPersonId { get; set; }

    [Range(0, 100)]
    public decimal AllocatedPercentage { get; set; }

    /// <summary>Row-level commission rate override. Falls back to the Sales Person's own rate when null.</summary>
    [Range(0, 100)]
    public decimal? CommissionRate { get; set; }
}

public class InvoicePaymentHistoryDto
{
    public Guid Id { get; set; }
    public string? PaymentNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public string? PaymentType { get; set; }
    public decimal Amount { get; set; }
}

public class SalesInvoiceDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1m;
    public Guid? PriceListId { get; set; }
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
    public string? EInvoiceStatus { get; set; }
    public string? LhdnUuid { get; set; }
    public string? LhdnLongId { get; set; }
    public Guid? LhdnSubmissionId { get; set; }
    public DateTime? LhdnSubmittedAt { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }
    public Guid DebitToAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }

    /// <summary>Days past due date. 0 when not overdue or no due date.</summary>
    public int DaysOverdue { get; set; }

    /// <summary>True when posted, has outstanding, past due date, and not a return.</summary>
    public bool IsOverdue { get; set; }

    public List<SalesInvoiceItemDto> Items { get; set; } = new();

    /// <summary>Commission split across sales persons. Empty when no commission is tracked.</summary>
    public List<SalesTeamEntryDto> SalesTeam { get; set; } = new();

    /// <summary>Sum of SalesTeam.Incentives — total commission payable on this invoice.</summary>
    public decimal TotalCommission { get; set; }

    /// <summary>Set when auto-created from an inter-company Purchase Invoice, or when this SI auto-created one.</summary>
    public Guid? InterCompanyPurchaseInvoiceId { get; set; }
    public string? InterCompanyPurchaseInvoiceNumber { get; set; }
    public string? InterCompanyCompanyName { get; set; }
}

public class SalesInvoiceItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal GrossProfit { get; set; }
    public bool EnableDeferredRevenue { get; set; }
    public Guid? DeferredRevenueAccountId { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public DateTime? ServiceStopDate { get; set; }
}

public class CreateSalesInvoiceDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public DateTime IssueDate { get; set; }

    public DateTime? DueDate { get; set; }

    [StringLength(SalesInvoiceConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Selling Price List. When omitted, defaults from Customer.DefaultPriceListId.</summary>
    public Guid? PriceListId { get; set; }

    public string? Notes { get; set; }

    public Guid? PaymentTermsTemplateId { get; set; }
    public bool IsReturn { get; set; }
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>Mark as opening balance invoice (data migration). Blocks update_stock, clears payment terms.</summary>
    public bool IsOpening { get; set; }

    /// <summary>Cost center for departmental P&L attribution (per ERPNext: mandatory for P&L accounts).</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Link to project for timesheet-based billing (auto-fetches unbilled timesheets).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>When true, stock is deducted on invoice submit (POS/direct sale without DN).</summary>
    public bool UpdateStock { get; set; }

    /// <summary>Warehouse for stock deduction when UpdateStock=true.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Coupon code to apply pricing discount (validated + recorded on creation).</summary>
    public string? CouponCode { get; set; }

    /// <summary>Loyalty points to redeem against this invoice (reduces payable amount).</summary>
    public int LoyaltyPointsToRedeem { get; set; }

    /// <summary>
    /// Document-level discount amount (per ERPNext additional_discount_section).
    /// Distributed proportionally across items or applied after tax based on ApplyDiscountOn.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>"GrandTotal" or "NetTotal" — determines at which stage the discount is applied.</summary>
    public string? ApplyDiscountOn { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateSalesInvoiceItemDto> Items { get; set; } = new();

    /// <summary>
    /// Commission split across sales persons (per ERPNext Sales Team child table).
    /// When provided, AllocatedPercentage across all rows must sum to exactly 100.
    /// </summary>
    public List<SalesTeamAllocationInputDto>? SalesTeam { get; set; }
}

public class CreateSalesInvoiceItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = null!;

    /// <summary>Quantity (positive for normal invoices, negative for credit notes/returns).</summary>
    [Required]
    public decimal Quantity { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; set; }

    [StringLength(20)]
    public string Uom { get; set; } = "Unit";

    public bool EnableDeferredRevenue { get; set; }
    public Guid? DeferredRevenueAccountId { get; set; }
    public DateTime? ServiceStartDate { get; set; }
    public DateTime? ServiceEndDate { get; set; }
    public DateTime? ServiceStopDate { get; set; }
}

/// <summary>
/// Creates a consolidated Sales Invoice from multiple submitted Delivery Notes.
/// Per ERPNext: primary billing workflow for goods-based businesses (deliver daily, invoice weekly/monthly).
/// All DNs must belong to the same customer and company.
/// </summary>
public class CreateInvoiceFromDeliveryNotesDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    /// <summary>List of Delivery Note IDs to consolidate into one invoice.</summary>
    [Required]
    [MinLength(1)]
    public List<Guid> DeliveryNoteIds { get; set; } = new();

    public DateTime? IssueDate { get; set; }

    [StringLength(SalesInvoiceConsts.MaxCurrencyCodeLength)]
    public string CurrencyCode { get; set; } = "MYR";

    public Guid? PaymentTermsTemplateId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// KPI summary for the Sales Invoice list page.
/// Enables dashboard-style cards (outstanding, overdue, monthly revenue) without fetching all invoices.
/// </summary>
public class SalesInvoiceListSummaryDto
{
    /// <summary>Total outstanding amount across all posted non-return invoices.</summary>
    public decimal TotalOutstanding { get; set; }

    /// <summary>Number of invoices with outstanding > 0 and past due date.</summary>
    public int OverdueCount { get; set; }

    /// <summary>Total overdue amount (sum of outstanding on past-due invoices).</summary>
    public decimal OverdueAmount { get; set; }

    /// <summary>Revenue posted this month (sum of GrandTotal for invoices posted this calendar month).</summary>
    public decimal MonthlyRevenue { get; set; }

    /// <summary>Number of invoices posted this month.</summary>
    public int MonthlyInvoiceCount { get; set; }

    /// <summary>Total number of posted invoices (all time for this company).</summary>
    public int PostedInvoiceCount { get; set; }
}

