using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Plant Floor — factory floor / production area organizing workstations.
/// Maps to ERPNext manufacturing/doctype/plant_floor.
/// </summary>
public class PlantFloor : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string FloorName { get; set; } = null!;
    public Guid? WarehouseId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected PlantFloor() { }

    public PlantFloor(Guid id, Guid companyId, string floorName, Guid? warehouseId = null, string? description = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        FloorName = Check.NotNullOrWhiteSpace(floorName, nameof(floorName), maxLength: PlantFloorConsts.MaxFloorNameLength);
        WarehouseId = warehouseId;
        Description = description;
        TenantId = tenantId;
    }
}
