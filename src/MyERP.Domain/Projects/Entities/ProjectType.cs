using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Project Type — classifies a Project (e.g. "Internal", "External", "Other").
/// Maps to ERPNext projects/doctype/project_type.
/// </summary>
public class ProjectType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public bool IsActive { get; set; } = true;

    protected ProjectType() { }

    public ProjectType(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100);
    }
}
