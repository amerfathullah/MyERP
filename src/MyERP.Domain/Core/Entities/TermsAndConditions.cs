using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Terms and Conditions — standard legal and commercial terms attached to sales and purchases.
/// Maps to ERPNext setup/doctype/terms_and_conditions.
/// </summary>
public class TermsAndConditions : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Raw or HTML terms content.</summary>
    public string? Terms { get; set; }

    /// <summary>Applicable for Selling transactions (Quotation, Sales Order, Sales Invoice, Delivery Note).</summary>
    public bool IsSelling { get; set; } = true;

    /// <summary>Applicable for Buying transactions (Purchase Order, Purchase Receipt, Purchase Invoice, RFQ, Supplier Quotation).</summary>
    public bool IsBuying { get; set; } = true;

    public bool IsDisabled { get; set; }

    public bool CopyAttachmentsToTransaction { get; set; }

    protected TermsAndConditions() { }

    public TermsAndConditions(Guid id, Guid companyId, string title, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), TermsAndConditionsConsts.MaxTitleLength);
        TenantId = tenantId;
    }
}
