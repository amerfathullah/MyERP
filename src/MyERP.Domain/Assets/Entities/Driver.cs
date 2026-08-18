using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Driver — a person qualified to operate company Vehicles. Optionally linked to an Employee.
/// Maps to ERPNext setup/doctype/driver. Used by Vehicle assignment and Delivery Trip / Maintenance Visit.
/// </summary>
public class Driver : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string FullName { get; private set; } = null!;

    /// <summary>Optional link to the Employee record (if the driver is a company employee).</summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>Optional link to the Supplier acting as transporter (if the driver is contracted, not employed).</summary>
    public Guid? TransporterId { get; set; }

    public string? CellNumber { get; set; }
    public string LicenseNumber { get; set; } = null!;
    public DateTime? LicenseExpiryDate { get; set; }
    public string? Address { get; set; }

    public DriverStatus Status { get; private set; } = DriverStatus.Active;

    private readonly List<DriverLicenseCategory> _licenseCategories = new();
    public IReadOnlyList<DriverLicenseCategory> LicenseCategories => _licenseCategories.AsReadOnly();

    protected Driver() { }

    public Driver(Guid id, Guid companyId, string fullName, string licenseNumber, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SetName(fullName);
        LicenseNumber = Check.NotNullOrWhiteSpace(licenseNumber, nameof(licenseNumber), FleetConsts.MaxLicenseNumberLength);
        TenantId = tenantId;
    }

    public void SetName(string fullName)
    {
        FullName = Check.NotNullOrWhiteSpace(fullName, nameof(fullName), FleetConsts.MaxDriverNameLength);
    }

    public void SetLicenseCategories(IEnumerable<Guid> categoryIds)
    {
        _licenseCategories.Clear();
        foreach (var categoryId in categoryIds.Distinct())
        {
            _licenseCategories.Add(new DriverLicenseCategory(Guid.NewGuid(), Id, categoryId));
        }
    }

    public void Suspend()
    {
        if (Status != DriverStatus.Active)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DriverStatus.Suspended;
    }

    public void Reinstate()
    {
        if (Status != DriverStatus.Suspended)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DriverStatus.Active;
    }

    public void MarkLeft()
    {
        if (Status == DriverStatus.Left)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DriverStatus.Left;
    }
}

/// <summary>Join row: a license category a Driver is qualified for.</summary>
public class DriverLicenseCategory : Entity<Guid>
{
    public Guid DriverId { get; set; }
    public Guid CategoryId { get; set; }

    protected DriverLicenseCategory() { }

    public DriverLicenseCategory(Guid id, Guid driverId, Guid categoryId) : base(id)
    {
        DriverId = driverId;
        CategoryId = categoryId;
    }
}
