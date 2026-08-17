using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>Market Segment master. Maps to ERPNext crm/doctype/market_segment.</summary>
public class MarketSegment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    protected MarketSegment() { }

    public MarketSegment(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MarketSegmentConsts.MaxNameLength);
        TenantId = tenantId;
    }
}
