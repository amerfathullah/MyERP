using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Persisted payment schedule entry on an invoice.
/// Generated from PaymentTermsTemplate at invoice creation.
/// Tracks per-term outstanding for partial payment allocation.
/// Per ERPNext: payment_schedule child table on SI/PI with mutable outstanding.
/// </summary>
public class PaymentScheduleEntry : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Parent document type ("SalesInvoice" or "PurchaseInvoice").</summary>
    public string ParentType { get; set; } = null!;

    /// <summary>Parent document ID.</summary>
    public Guid ParentId { get; set; }

    /// <summary>Payment term description (e.g., "Net 30", "50% Advance").</summary>
    public string? Description { get; set; }

    /// <summary>Due date for this installment.</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Percentage of invoice total for this term (sum across all = 100%).</summary>
    public decimal InvoicePortion { get; set; }

    /// <summary>Scheduled payment amount for this term.</summary>
    public decimal PaymentAmount { get; set; }

    /// <summary>Amount already paid against this specific term.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Outstanding for this term: PaymentAmount - PaidAmount.</summary>
    public decimal Outstanding => PaymentAmount - PaidAmount;

    /// <summary>Whether this term is fully paid.</summary>
    public bool IsFullyPaid => Outstanding <= 0.01m;

    /// <summary>Mode of payment for this term (optional).</summary>
    public Guid? ModeOfPaymentId { get; set; }

    // --- Early Payment Discount (per ERPNext payment_schedule.discount_type/percentage/date) ---

    /// <summary>
    /// Discount type: "Percentage" or "Amount". Null = no early payment discount.
    /// Per ERPNext: discount_type on payment_schedule row.
    /// </summary>
    public string? DiscountType { get; set; }

    /// <summary>
    /// Discount value (percentage if DiscountType=Percentage, fixed amount if Amount).
    /// Per ERPNext: discount on payment_schedule row.
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// Date until which the early payment discount is valid.
    /// Payment after this date forfeits the discount.
    /// Per ERPNext: discount_date on payment_schedule row.
    /// </summary>
    public DateTime? DiscountValidTill { get; set; }

    /// <summary>
    /// Pre-calculated discounted amount (PaymentAmount minus discount).
    /// Per ERPNext: discounted_amount on payment_schedule row.
    /// </summary>
    public decimal DiscountedAmount { get; set; }

    /// <summary>
    /// Whether the discount is still available (not expired and not fully paid).
    /// </summary>
    public bool IsDiscountAvailable(DateTime asOfDate) =>
        DiscountType != null
        && DiscountPercentage > 0
        && (!DiscountValidTill.HasValue || asOfDate.Date <= DiscountValidTill.Value.Date)
        && !IsFullyPaid;

    /// <summary>
    /// Calculates the amount to pay with early payment discount applied.
    /// Returns the reduced amount if discount is available, else full PaymentAmount.
    /// </summary>
    public decimal GetPayableAmount(DateTime asOfDate)
    {
        if (!IsDiscountAvailable(asOfDate))
            return Outstanding;

        if (DiscountedAmount > 0)
            return Math.Max(0, DiscountedAmount - PaidAmount);

        // Fallback calculation
        var discountAmount = DiscountType == "Percentage"
            ? PaymentAmount * DiscountPercentage / 100m
            : DiscountPercentage;

        return Math.Max(0, PaymentAmount - discountAmount - PaidAmount);
    }

    protected PaymentScheduleEntry() { }

    public PaymentScheduleEntry(
        Guid id, string parentType, Guid parentId,
        DateTime dueDate, decimal invoicePortion, decimal paymentAmount,
        string? description = null)
        : base(id)
    {
        ParentType = parentType;
        ParentId = parentId;
        DueDate = dueDate;
        InvoicePortion = invoicePortion;
        PaymentAmount = paymentAmount;
        Description = description;
    }

    /// <summary>
    /// Records a payment against this schedule entry.
    /// Returns the amount actually allocated (may be less than requested if entry is nearly paid).
    /// </summary>
    public decimal RecordPayment(decimal amount)
    {
        var allocatable = Math.Min(amount, Outstanding);
        if (allocatable <= 0) return 0;
        PaidAmount += allocatable;
        return allocatable;
    }
}
