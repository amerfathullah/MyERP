using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Restricts which items are selectable for a Customer/Customer Group/Supplier/Supplier Group
/// in item search on sales/purchase transaction rows.
/// A rule for a specific party overrides a group-level rule for the same restriction.
/// Maps to ERPNext selling/doctype/party_specific_item.
/// </summary>
public class PartySpecificItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public PartySpecificItemPartyType PartyType { get; set; }

    /// <summary>Id of the Customer, CustomerGroup, Supplier, or SupplierGroup, per <see cref="PartyType"/>.</summary>
    public Guid PartyId { get; set; }

    public PartySpecificItemRestrictBasedOn RestrictBasedOn { get; set; }

    /// <summary>Id of the Item, ItemGroup, or Brand, per <see cref="RestrictBasedOn"/>.</summary>
    public Guid BasedOnValueId { get; set; }

    protected PartySpecificItem() { }

    public PartySpecificItem(
        Guid id,
        PartySpecificItemPartyType partyType,
        Guid partyId,
        PartySpecificItemRestrictBasedOn restrictBasedOn,
        Guid basedOnValueId,
        Guid? tenantId = null) : base(id)
    {
        PartyType = partyType;
        PartyId = partyId;
        RestrictBasedOn = restrictBasedOn;
        BasedOnValueId = basedOnValueId;
        TenantId = tenantId;
    }
}
