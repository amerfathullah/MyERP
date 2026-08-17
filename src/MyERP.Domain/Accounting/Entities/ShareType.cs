using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Share Type — classification of shares (e.g. Equity, Preference).
/// Maps to ERPNext accounts/doctype/share_type.
/// </summary>
public class ShareType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    protected ShareType() { }

    public ShareType(Guid id, string title, Guid? tenantId = null) : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 140);
        TenantId = tenantId;
    }
}
