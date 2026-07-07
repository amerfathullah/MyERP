using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Individual line in a journal entry (one debit or credit posting).
/// Maps to ERPNext accounts/doctype/journal_entry_account.
/// </summary>
public class JournalEntryLine : CreationAuditedEntity<Guid>
{
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Always positive. Direction determined by IsDebit.</summary>
    public decimal Amount { get; set; }

    /// <summary>True = Debit, False = Credit.</summary>
    public bool IsDebit { get; set; }

    public string? Description { get; set; }

    /// <summary>Optional: party reference (customer/supplier) for subledger tracking.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>Party type: "Customer" or "Supplier".</summary>
    public string? PartyType { get; set; }

    protected JournalEntryLine() { }

    public JournalEntryLine(Guid id, Guid journalEntryId, Guid accountId, decimal amount, bool isDebit, string? description = null)
        : base(id)
    {
        JournalEntryId = journalEntryId;
        AccountId = accountId;
        Amount = amount;
        IsDebit = isDebit;
        Description = description;
    }
}
