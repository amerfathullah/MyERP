using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// UOM Category — groups Units of Measure that are convertible with each other
/// (e.g. "Mass": Kg/Gram/Tonne; "Length": Metre/cm/Inch). Maps to ERPNext
/// stock/doctype/uom_category. Uom.Category currently stores this as free text;
/// this master gives it a controlled picklist without breaking existing data.
/// </summary>
public class UomCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;

    protected UomCategory() { }

    public UomCategory(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        SetName(name);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100);
    }
}
