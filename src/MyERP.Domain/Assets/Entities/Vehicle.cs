using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Vehicle — a company-owned or leased vehicle used for delivery/fleet operations.
/// Maps to ERPNext setup/doctype/vehicle. Referenced by Driver assignment and
/// Delivery Trip / Maintenance Visit (fleet management), independent of the fixed-Asset register.
/// </summary>
public class Vehicle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string LicensePlate { get; private set; } = null!;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? ChassisNumber { get; set; }
    public string? Color { get; set; }

    public VehicleFuelType FuelType { get; set; } = VehicleFuelType.Petrol;
    /// <summary>Unit of measure for fuel consumption tracking (e.g. "Litre", "kWh").</summary>
    public string? FuelUom { get; set; }

    public decimal LastOdometer { get; set; }
    public decimal? CarryingCapacity { get; set; }
    public int? Wheels { get; set; }
    public int? Doors { get; set; }

    public decimal? VehicleValue { get; set; }
    public DateTime? AcquisitionDate { get; set; }

    /// <summary>Currently assigned driver.</summary>
    public Guid? DriverId { get; set; }

    /// <summary>Current physical location/depot.</summary>
    public Guid? LocationId { get; set; }

    // Insurance
    public string? InsuranceCompany { get; set; }
    public string? PolicyNumber { get; set; }
    public DateTime? InsuranceStartDate { get; set; }
    public DateTime? InsuranceEndDate { get; set; }

    // Road tax / fitness certificate
    public DateTime? RoadTaxExpiryDate { get; set; }
    public DateTime? FitnessCertificateExpiryDate { get; set; }

    public bool IsDisabled { get; set; }

    protected Vehicle() { }

    public Vehicle(Guid id, Guid companyId, string licensePlate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SetLicensePlate(licensePlate);
        TenantId = tenantId;
    }

    public void SetLicensePlate(string licensePlate)
    {
        LicensePlate = Check.NotNullOrWhiteSpace(licensePlate, nameof(licensePlate), FleetConsts.MaxLicensePlateLength);
    }

    public void Disable() => IsDisabled = true;
    public void Enable() => IsDisabled = false;
}
