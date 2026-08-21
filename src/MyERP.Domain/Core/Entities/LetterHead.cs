using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Letter Head — branding headers and footers for printed documents and reports.
/// Maps to ERPNext printing/doctype/letter_head.
/// In v16: includes LetterHeadFor (DocType vs Report) with separate default tracking (gotcha #147).
/// </summary>
public class LetterHead : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string LetterHeadName { get; set; } = null!;

    /// <summary>Scope of the letter head: DocType (transactions) or Report (financial/analytical reports).</summary>
    public LetterHeadFor LetterHeadFor { get; set; } = LetterHeadFor.DocType;

    /// <summary>Whether this is the default letter head for its category (DocType vs Report).</summary>
    public bool IsDefault { get; set; }

    /// <summary>HTML or image URL content for the header.</summary>
    public string? HeaderContent { get; set; }

    /// <summary>HTML content for the footer.</summary>
    public string? FooterContent { get; set; }

    public bool IsDisabled { get; set; }

    protected LetterHead() { }

    public LetterHead(Guid id, Guid companyId, string letterHeadName, LetterHeadFor letterHeadFor = LetterHeadFor.DocType, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        LetterHeadName = Check.NotNullOrWhiteSpace(letterHeadName, nameof(letterHeadName), LetterHeadConsts.MaxNameLength);
        LetterHeadFor = letterHeadFor;
        TenantId = tenantId;
    }
}
