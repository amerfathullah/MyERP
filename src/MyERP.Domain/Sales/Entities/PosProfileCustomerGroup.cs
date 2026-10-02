using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Customer Group configured for POS Profile (per ERPNext accounts/doctype/pos_profile/pos_profile.json: customer_groups).
/// Filters customers selectable in POS terminal.
/// </summary>
public class PosProfileCustomerGroup : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid PosProfileId { get; set; }
    public Guid CustomerGroupId { get; set; }

    protected PosProfileCustomerGroup() { }

    public PosProfileCustomerGroup(Guid id, Guid posProfileId, Guid customerGroupId, Guid? tenantId = null)
        : base(id)
    {
        PosProfileId = posProfileId;
        CustomerGroupId = customerGroupId;
        TenantId = tenantId;
    }
}
