using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Tax Withholding Group — standard classification grouping for tax withholding rates.
/// Maps to ERPNext accounts/doctype/tax_withholding_group.
/// </summary>
public class TaxWithholdingGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string GroupName { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected TaxWithholdingGroup() { }

    public TaxWithholdingGroup(Guid id, string groupName, string? description = null, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        SetGroupName(groupName);
        Description = description;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void SetGroupName(string groupName)
    {
        GroupName = Check.NotNullOrWhiteSpace(groupName, nameof(groupName), TaxWithholdingGroupConsts.MaxGroupNameLength);
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
