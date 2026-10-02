using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Item Group configured for POS Profile (per ERPNext accounts/doctype/pos_profile/pos_profile.json: item_groups).
/// Filters items visible in POS terminal to this item group and its descendants.
/// </summary>
public class PosProfileItemGroup : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid ItemGroupId { get; set; }

    protected PosProfileItemGroup() { }

    public PosProfileItemGroup(Guid id, Guid posProfileId, Guid itemGroupId, Guid? tenantId = null)
        : base(id)
    {
        PosProfileId = posProfileId;
        ItemGroupId = itemGroupId;
        TenantId = tenantId;
    }
}
