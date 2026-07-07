using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Configurable rule that determines how a document type generates journal entries.
/// This is the CRITICAL rules engine that replaces hardcoded accounting logic.
/// Example: "When SalesInvoice is posted, Debit Accounts Receivable for GrandTotal"
/// </summary>
public class AccountingRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; private set; } = null!;

    /// <summary>Document type this rule applies to (e.g., "SalesInvoice", "PurchaseInvoice").</summary>
    public string DocumentType { get; set; } = null!;

    /// <summary>True = Debit, False = Credit.</summary>
    public bool IsDebit { get; set; }

    /// <summary>How to determine which account to post to.</summary>
    public AccountSource AccountSource { get; set; }

    /// <summary>Fixed account ID (used when AccountSource = FixedAccount).</summary>
    public Guid? FixedAccountId { get; set; }

    /// <summary>Which amount from the document to use.</summary>
    public AmountSource AmountSource { get; set; }

    /// <summary>Execution order within the same document type.</summary>
    public int SortOrder { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected AccountingRule() { }

    public AccountingRule(
        Guid id,
        Guid companyId,
        string name,
        string documentType,
        bool isDebit,
        AccountSource accountSource,
        AmountSource amountSource,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        DocumentType = Check.NotNullOrWhiteSpace(documentType, nameof(documentType), AccountingRuleConsts.MaxDocumentTypeLength);
        IsDebit = isDebit;
        AccountSource = accountSource;
        AmountSource = amountSource;
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), AccountingRuleConsts.MaxNameLength);
    }
}
