using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Support.Entities;

/// <summary>Configurable Issue priority master (e.g. Low, Medium, High, Urgent). Maps to ERPNext Issue Priority.</summary>
public class IssuePriority : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    protected IssuePriority() { }

    public IssuePriority(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), IssuePriorityConsts.MaxNameLength);
        TenantId = tenantId;
    }
}
