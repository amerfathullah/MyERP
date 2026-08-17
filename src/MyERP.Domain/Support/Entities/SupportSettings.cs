using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Support.Entities;

/// <summary>
/// Support Settings — singleton per-company configuration.
/// Maps to ERPNext support/doctype/support_settings.
/// </summary>
public class SupportSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Track and enforce Service Level Agreements on Issues.</summary>
    public bool TrackServiceLevelAgreement { get; set; } = true;

    /// <summary>Allow manually resetting an Issue's SLA after a breach.</summary>
    public bool AllowResettingServiceLevelAgreement { get; set; }

    /// <summary>Auto-close Replied issues after N days of inactivity. Null disables auto-close.</summary>
    public int? CloseIssueAfterDays { get; set; }

    protected SupportSettings() { }

    public SupportSettings(Guid id, Guid companyId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        TenantId = tenantId;
    }
}
