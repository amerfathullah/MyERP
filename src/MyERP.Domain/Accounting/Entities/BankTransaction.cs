using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Represents a bank transaction imported from a bank statement.
/// Used for matching against Payment Entries for reconciliation.
/// Migrated from ERPNext banking module (bank_transaction doctype).
/// </summary>
public class BankTransaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }

    /// <summary>Transaction date from the bank statement.</summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>Description/narration from bank statement.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Amount (positive = credit/deposit, negative = debit/withdrawal).</summary>
    public decimal Amount { get; set; }

    /// <summary>Bank reference number.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Whether this transaction has been reconciled with a payment entry.</summary>
    public bool IsReconciled { get; set; }

    /// <summary>Linked Payment Entry ID (set when reconciled).</summary>
    public Guid? PaymentEntryId { get; set; }

    /// <summary>Matched document reference (e.g., invoice number).</summary>
    public string? MatchedDocumentRef { get; set; }

    /// <summary>Date when reconciliation was performed.</summary>
    public DateTime? ReconciledAt { get; set; }

    protected BankTransaction() { }

    public BankTransaction(Guid id, Guid companyId, Guid bankAccountId,
        DateTime transactionDate, string description, decimal amount, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        BankAccountId = bankAccountId;
        TransactionDate = transactionDate;
        Description = description;
        Amount = amount;
        TenantId = tenantId;
    }

    public void Reconcile(Guid paymentEntryId, string? matchedDocRef)
    {
        IsReconciled = true;
        PaymentEntryId = paymentEntryId;
        MatchedDocumentRef = matchedDocRef;
        ReconciledAt = DateTime.UtcNow;
    }

    public void Unreconcile()
    {
        IsReconciled = false;
        PaymentEntryId = null;
        MatchedDocumentRef = null;
        ReconciledAt = null;
    }
}
