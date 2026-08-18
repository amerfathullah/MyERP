using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// A reusable template of account rows for creating recurring Journal Entries quickly
/// (e.g. monthly accrual entries, standard payroll postings). Maps to ERPNext
/// accounts/doctype/journal_entry_template.
///
/// Per general-ledger-full.md "Journal Entry Template — Entity Validation":
/// - Each row's account must belong to the template's company.
/// - PartyType is only allowed on Receivable/Payable rows.
/// - Party requires PartyType.
/// These are enforced by JournalEntryTemplateAppService at create/update time (needs
/// Account lookups the domain entity itself doesn't have access to).
/// </summary>
public class JournalEntryTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string TemplateName { get; private set; } = null!;
    public JournalEntryVoucherType VoucherType { get; set; } = JournalEntryVoucherType.JournalEntry;
    public bool IsActive { get; set; } = true;

    private readonly List<JournalEntryTemplateLine> _lines = new();
    public IReadOnlyList<JournalEntryTemplateLine> Lines => _lines.AsReadOnly();

    protected JournalEntryTemplate() { }

    public JournalEntryTemplate(Guid id, Guid companyId, string templateName, Guid? tenantId = null) : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SetName(templateName);
        TenantId = tenantId;
    }

    public void SetName(string templateName)
    {
        TemplateName = Check.NotNullOrWhiteSpace(templateName, nameof(templateName), 200);
    }

    public void ClearLines() => _lines.Clear();

    public void AddLine(Guid accountId, bool isDebit, decimal defaultAmount, string? partyType, string? description)
    {
        _lines.Add(new JournalEntryTemplateLine(Guid.NewGuid(), Id, accountId, isDebit, defaultAmount, partyType, description));
    }
}

public class JournalEntryTemplateLine : Entity<Guid>
{
    public Guid JournalEntryTemplateId { get; set; }
    public Guid AccountId { get; set; }
    public bool IsDebit { get; set; }
    public decimal DefaultAmount { get; set; }

    /// <summary>Only valid on Receivable/Payable accounts; null for all other rows.</summary>
    public string? PartyType { get; set; }
    public string? Description { get; set; }

    protected JournalEntryTemplateLine() { }

    public JournalEntryTemplateLine(Guid id, Guid templateId, Guid accountId, bool isDebit,
        decimal defaultAmount, string? partyType, string? description) : base(id)
    {
        JournalEntryTemplateId = templateId;
        AccountId = accountId;
        IsDebit = isDebit;
        DefaultAmount = defaultAmount;
        PartyType = partyType;
        Description = description;
    }
}
