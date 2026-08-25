using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Task Type — classifies project tasks (e.g., Development, Design, Bugfix, QA).
/// Defines default relative task weight for progress calculation.
/// 
/// Maps to ERPNext projects/doctype/task_type.
/// </summary>
public class TaskType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public decimal Weight { get; set; } = 1;
    public string? Description { get; set; }

    protected TaskType() { }

    public TaskType(Guid id, string name, decimal weight = 1, string? description = null, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: TaskTypeConsts.MaxNameLength);
        Weight = weight >= 0 ? weight : 1;
        Description = description;
        TenantId = tenantId;
    }
}
