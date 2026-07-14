using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Entry Reference — allocation of a payment against a specific invoice/order.
/// Enables multi-invoice payment allocation (one PE → multiple SI/PI allocations).
/// Per ERPNext: payment_entry.references child table.
/// </summary>
public class PaymentEntryReference : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid PaymentEntryId { get; set; }

    /// <summary>Document type: "SalesInvoice", "PurchaseInvoice", "SalesOrder", "PurchaseOrder".</summary>
    public string ReferenceType { get; set; } = null!;

    /// <summary>Document ID being allocated against.</summary>
    public Guid ReferenceId { get; set; }

    /// <summary>Reference document number (for display).</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Total amount of the referenced document.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Outstanding amount at time of allocation.</summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>Amount allocated from this payment to this reference.</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>Exchange rate for this reference (for multi-currency payments).</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    protected PaymentEntryReference() { }

    public PaymentEntryReference(
        Guid id, Guid paymentEntryId, string referenceType, Guid referenceId,
        decimal totalAmount, decimal outstandingAmount, decimal allocatedAmount,
        string? referenceNumber = null)
        : base(id)
    {
        PaymentEntryId = paymentEntryId;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        TotalAmount = totalAmount;
        OutstandingAmount = outstandingAmount;
        AllocatedAmount = allocatedAmount;
        ReferenceNumber = referenceNumber;
    }
}
