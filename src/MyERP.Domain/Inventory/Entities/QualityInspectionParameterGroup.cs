using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Inspection Parameter Group — category/group for quality inspection parameters.
/// Maps to ERPNext stock/doctype/quality_inspection_parameter_group.
/// </summary>
public class QualityInspectionParameterGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string GroupName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected QualityInspectionParameterGroup() { }

    public QualityInspectionParameterGroup(Guid id, string groupName, string? description = null, Guid? tenantId = null)
        : base(id)
    {
        GroupName = Check.NotNullOrWhiteSpace(groupName, nameof(groupName), maxLength: QualityInspectionParameterGroupConsts.MaxGroupNameLength);
        Description = description;
        TenantId = tenantId;
    }
}
