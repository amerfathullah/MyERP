using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Location — hierarchical physical/geo location for assets (distinct from Warehouse).
/// Maps to ERPNext assets/doctype/location. Supports tree hierarchy via ParentLocationId.
/// Referenced by Asset and AssetMovement as the current/source/target location.
/// </summary>
public class Location : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string LocationName { get; private set; } = null!;

    /// <summary>Parent location for tree hierarchy (null = root).</summary>
    public Guid? ParentLocationId { get; set; }

    /// <summary>Container locations (e.g. hydroponic units) cannot hold sub-locations.</summary>
    public bool IsContainer { get; set; }

    /// <summary>Group locations cannot be assigned directly to assets, only leaf locations.</summary>
    public bool IsGroup { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    protected Location() { }

    public Location(Guid id, string locationName, Guid? parentLocationId = null, Guid? tenantId = null)
        : base(id)
    {
        SetName(locationName);
        ParentLocationId = parentLocationId;
        TenantId = tenantId;
    }

    public void SetName(string locationName)
    {
        LocationName = Check.NotNullOrWhiteSpace(locationName, nameof(locationName), LocationConsts.MaxLocationNameLength);
    }
}
