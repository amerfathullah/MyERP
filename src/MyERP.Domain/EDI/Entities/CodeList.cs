using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.EDI.Entities;

/// <summary>
/// Code List — specification standard code list for EDI, PEPPOL BIS, UBL, and e-invoicing data interchange.
/// Maps to ERPNext edi/doctype/code_list.
/// </summary>
public class CodeList : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = null!;
    public string? CanonicalUri { get; set; }
    public string? Url { get; set; }
    public string? DefaultCommonCode { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public string? PublisherId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected CodeList() { }

    public CodeList(
        Guid id,
        string title,
        string? canonicalUri = null,
        string? url = null,
        string? defaultCommonCode = null,
        string? version = null,
        string? publisher = null,
        string? publisherId = null,
        string? description = null,
        bool isActive = true,
        Guid? tenantId = null)
        : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), EDIConsts.MaxTitleLength);
        CanonicalUri = canonicalUri;
        Url = url;
        DefaultCommonCode = defaultCommonCode;
        Version = version;
        Publisher = publisher;
        PublisherId = publisherId;
        Description = description;
        IsActive = isActive;
        TenantId = tenantId;
    }
}
