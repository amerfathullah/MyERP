using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Subscription Settings — global options for grace period, cancellation and proration.
/// Maps to ERPNext accounts/doctype/subscription_settings.
/// </summary>
public class SubscriptionSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public int GracePeriod { get; set; } = 1;
    public bool CancelAfterGrace { get; set; }
    public bool Prorate { get; set; } = true;

    protected SubscriptionSettings() { }

    public SubscriptionSettings(
        Guid id,
        int gracePeriod = 1,
        bool cancelAfterGrace = false,
        bool prorate = true,
        Guid? tenantId = null)
        : base(id)
    {
        GracePeriod = gracePeriod >= 0 ? gracePeriod : 1;
        CancelAfterGrace = cancelAfterGrace;
        Prorate = prorate;
        TenantId = tenantId;
    }
}
