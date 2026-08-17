using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>Competitor master — referenced from Quotation's Competitor Detail child table. Maps to ERPNext crm/doctype/competitor.</summary>
public class Competitor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public string? Website { get; set; }

    protected Competitor() { }

    public Competitor(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), CompetitorConsts.MaxNameLength);
        TenantId = tenantId;
    }
}
