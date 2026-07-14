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
