using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Opportunity Type master — classification for sales opportunities (e.g. Sales, Support, Maintenance, Services).
/// Maps to ERPNext crm/doctype/opportunity_type.
/// </summary>
public class OpportunityType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected OpportunityType() { }

    public OpportunityType(Guid id, string name, string? description = null, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        Description = description;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), OpportunityTypeConsts.MaxNameLength);
    }

    public void Enable()
    {
        IsActive = true;
    }

    public void Disable()
    {
        IsActive = false;
    }
}
