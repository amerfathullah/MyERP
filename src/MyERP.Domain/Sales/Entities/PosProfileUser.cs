using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Child entity of POS Profile representing a user permitted to use the profile.
/// Maps to ERPNext POS Profile User child table.
/// Per ERPNext PR #58508 (commit 9018573179):
/// Users assigned to a POS Profile are authorized to open sessions and complete sales against it.
/// </summary>
public class PosProfileUser : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid UserId { get; set; }
    public bool IsDefault { get; set; }

    protected PosProfileUser() { }

    public PosProfileUser(Guid id, Guid posProfileId, Guid userId, bool isDefault = false, Guid? tenantId = null)
        : base(id)
    {
        PosProfileId = posProfileId;
        UserId = userId;
        IsDefault = isDefault;
        TenantId = tenantId;
    }
}
