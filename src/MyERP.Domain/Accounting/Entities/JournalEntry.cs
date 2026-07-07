using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Journal Entry — the core of double-entry accounting.
/// Every financial transaction must produce a balanced JournalEntry.
/// Maps to ERPNext accounts/doctype/journal_entry.
/// </summary>
public class JournalEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    public string? EntryNumber { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>Source document type (e.g., "SalesInvoice", "PurchaseInvoice", "Manual").</summary>
    public string? ReferenceType { get; set; }

    /// <summary>Source document ID.</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Human-readable reference number.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Narration { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyList<JournalEntryLine> Lines => _lines.AsReadOnly();

    protected JournalEntry() { }

    public JournalEntry(Guid id, Guid companyId, Guid fiscalYearId, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddLine(Guid accountId, decimal amount, bool isDebit, string? description = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        _lines.Add(new JournalEntryLine(
            Guid.NewGuid(),
            Id,
            accountId,
            amount,
            isDebit,
            description));

        RecalculateTotals();
    }

    /// <summary>
    /// Validates that Total Debit = Total Credit (double-entry requirement).
    /// </summary>
    public void Validate()
    {
        if (!_lines.Any())
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry);

        RecalculateTotals();

        if (TotalDebit != TotalCredit)
            throw new BusinessException(MyERPDomainErrorCodes.UnbalancedJournalEntry)
                .WithData("totalDebit", TotalDebit)
                .WithData("totalCredit", TotalCredit);
    }

    public void Post()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Validate();
        Status = DocumentStatus.Posted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        TotalDebit = _lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        TotalCredit = _lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
    }
}
