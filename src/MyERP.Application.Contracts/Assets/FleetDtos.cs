using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

// === Driving License Category ===

public class DrivingLicenseCategoryDto : FullAuditedEntityDto<Guid>
{
    public string CategoryName { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateUpdateDrivingLicenseCategoryDto
{
    [Required]
    [StringLength(FleetConsts.MaxCategoryNameLength)]
    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }
}

public interface IDrivingLicenseCategoryAppService : IApplicationService
{
    Task<DrivingLicenseCategoryDto> GetAsync(Guid id);
    Task<PagedResultDto<DrivingLicenseCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<DrivingLicenseCategoryDto> CreateAsync(CreateUpdateDrivingLicenseCategoryDto input);
    Task<DrivingLicenseCategoryDto> UpdateAsync(Guid id, CreateUpdateDrivingLicenseCategoryDto input);
    Task DeleteAsync(Guid id);
}

// === Driver ===

public class DriverDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string FullName { get; set; } = null!;
    public Guid? EmployeeId { get; set; }
    public Guid? TransporterId { get; set; }
    public string? CellNumber { get; set; }
    public string LicenseNumber { get; set; } = null!;
    public DateTime? LicenseExpiryDate { get; set; }
    public string? Address { get; set; }
    public DriverStatus Status { get; set; }
    public List<Guid> LicenseCategoryIds { get; set; } = new();
}

public class CreateUpdateDriverDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(FleetConsts.MaxDriverNameLength)]
    public string FullName { get; set; } = null!;

    public Guid? EmployeeId { get; set; }
    public Guid? TransporterId { get; set; }
    public string? CellNumber { get; set; }

    [Required]
    [StringLength(FleetConsts.MaxLicenseNumberLength)]
    public string LicenseNumber { get; set; } = null!;

    public DateTime? LicenseExpiryDate { get; set; }
    public string? Address { get; set; }
    public List<Guid> LicenseCategoryIds { get; set; } = new();
}

public interface IDriverAppService : IApplicationService
{
    Task<DriverDto> GetAsync(Guid id);
    Task<PagedResultDto<DriverDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<DriverDto> CreateAsync(CreateUpdateDriverDto input);
    Task<DriverDto> UpdateAsync(Guid id, CreateUpdateDriverDto input);
    Task<DriverDto> SuspendAsync(Guid id);
    Task<DriverDto> ReinstateAsync(Guid id);
    Task<DriverDto> MarkLeftAsync(Guid id);
    Task DeleteAsync(Guid id);
}

// === Vehicle ===

public class VehicleDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string LicensePlate { get; set; } = null!;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? ChassisNumber { get; set; }
    public string? Color { get; set; }
    public VehicleFuelType FuelType { get; set; }
    public string? FuelUom { get; set; }
    public decimal LastOdometer { get; set; }
    public decimal? CarryingCapacity { get; set; }
    public int? Wheels { get; set; }
    public int? Doors { get; set; }
    public decimal? VehicleValue { get; set; }
    public DateTime? AcquisitionDate { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? LocationId { get; set; }
    public string? InsuranceCompany { get; set; }
    public string? PolicyNumber { get; set; }
    public DateTime? InsuranceStartDate { get; set; }
    public DateTime? InsuranceEndDate { get; set; }
    public DateTime? RoadTaxExpiryDate { get; set; }
    public DateTime? FitnessCertificateExpiryDate { get; set; }
    public bool IsDisabled { get; set; }
}

public class CreateUpdateVehicleDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(FleetConsts.MaxLicensePlateLength)]
    public string LicensePlate { get; set; } = null!;

    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? ChassisNumber { get; set; }
    public string? Color { get; set; }
    public VehicleFuelType FuelType { get; set; } = VehicleFuelType.Petrol;
    public string? FuelUom { get; set; }
    public decimal LastOdometer { get; set; }
    public decimal? CarryingCapacity { get; set; }
    public int? Wheels { get; set; }
    public int? Doors { get; set; }
    public decimal? VehicleValue { get; set; }
    public DateTime? AcquisitionDate { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? LocationId { get; set; }
    public string? InsuranceCompany { get; set; }
    public string? PolicyNumber { get; set; }
    public DateTime? InsuranceStartDate { get; set; }
    public DateTime? InsuranceEndDate { get; set; }
    public DateTime? RoadTaxExpiryDate { get; set; }
    public DateTime? FitnessCertificateExpiryDate { get; set; }
}

public interface IVehicleAppService : IApplicationService
{
    Task<VehicleDto> GetAsync(Guid id);
    Task<PagedResultDto<VehicleDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<VehicleDto> CreateAsync(CreateUpdateVehicleDto input);
    Task<VehicleDto> UpdateAsync(Guid id, CreateUpdateVehicleDto input);
    Task<VehicleDto> DisableAsync(Guid id);
    Task<VehicleDto> EnableAsync(Guid id);
    Task DeleteAsync(Guid id);
}
