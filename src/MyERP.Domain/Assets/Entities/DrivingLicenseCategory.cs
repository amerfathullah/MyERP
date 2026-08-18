using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Driving License Category — master list of license classes (e.g. "Light Motor Vehicle",
/// "Heavy Goods Vehicle"). Assigned to Drivers via DriverLicenseCategory.
/// Maps to ERPNext setup/doctype/driving_license_category.
/// </summary>
public class DrivingLicenseCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string CategoryName { get; private set; } = null!;
    public string? Description { get; set; }

    protected DrivingLicenseCategory() { }

    public DrivingLicenseCategory(Guid id, string categoryName, Guid? tenantId = null)
        : base(id)
    {
        SetName(categoryName);
        TenantId = tenantId;
    }

    public void SetName(string categoryName)
    {
        CategoryName = Check.NotNullOrWhiteSpace(categoryName, nameof(categoryName), FleetConsts.MaxCategoryNameLength);
    }
}
