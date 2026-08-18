using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.DrivingLicenseCategories.Default)]
public class DrivingLicenseCategoryAppService : ApplicationService, IDrivingLicenseCategoryAppService
{
    private readonly IRepository<DrivingLicenseCategory, Guid> _repository;

    public DrivingLicenseCategoryAppService(IRepository<DrivingLicenseCategory, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<DrivingLicenseCategoryDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<DrivingLicenseCategory, DrivingLicenseCategoryDto>(entity);
    }

    public async Task<PagedResultDto<DrivingLicenseCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var list = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.CategoryName).Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<DrivingLicenseCategoryDto>(
            totalCount, ObjectMapper.Map<System.Collections.Generic.List<DrivingLicenseCategory>, System.Collections.Generic.List<DrivingLicenseCategoryDto>>(list));
    }

    [Authorize(MyERPPermissions.DrivingLicenseCategories.Create)]
    public async Task<DrivingLicenseCategoryDto> CreateAsync(CreateUpdateDrivingLicenseCategoryDto input)
    {
        var entity = new DrivingLicenseCategory(GuidGenerator.Create(), input.CategoryName, CurrentTenant.Id)
        {
            Description = input.Description,
        };
        await _repository.InsertAsync(entity);
        return ObjectMapper.Map<DrivingLicenseCategory, DrivingLicenseCategoryDto>(entity);
    }

    [Authorize(MyERPPermissions.DrivingLicenseCategories.Edit)]
    public async Task<DrivingLicenseCategoryDto> UpdateAsync(Guid id, CreateUpdateDrivingLicenseCategoryDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetName(input.CategoryName);
        entity.Description = input.Description;
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<DrivingLicenseCategory, DrivingLicenseCategoryDto>(entity);
    }

    [Authorize(MyERPPermissions.DrivingLicenseCategories.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}

[Authorize(MyERPPermissions.Drivers.Default)]
public class DriverAppService : ApplicationService, IDriverAppService
{
    private readonly IRepository<Driver, Guid> _repository;

    public DriverAppService(IRepository<Driver, Guid> repository)
    {
        _repository = repository;
    }

    private static DriverDto ToDto(Driver entity)
    {
        return new DriverDto
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            FullName = entity.FullName,
            EmployeeId = entity.EmployeeId,
            TransporterId = entity.TransporterId,
            CellNumber = entity.CellNumber,
            LicenseNumber = entity.LicenseNumber,
            LicenseExpiryDate = entity.LicenseExpiryDate,
            Address = entity.Address,
            Status = entity.Status,
            LicenseCategoryIds = entity.LicenseCategories.Select(c => c.CategoryId).ToList(),
            CreationTime = entity.CreationTime,
        };
    }

    public async Task<DriverDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync();
        var entity = query.First(x => x.Id == id);
        return ToDto(entity);
    }

    public async Task<PagedResultDto<DriverDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.WithDetailsAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.FullName.Contains(input.Filter) || x.LicenseNumber.Contains(input.Filter));
        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DriverStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var list = query.OrderBy(x => x.FullName).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<DriverDto>(totalCount, list.Select(ToDto).ToList());
    }

    [Authorize(MyERPPermissions.Drivers.Create)]
    public async Task<DriverDto> CreateAsync(CreateUpdateDriverDto input)
    {
        var entity = new Driver(GuidGenerator.Create(), input.CompanyId, input.FullName, input.LicenseNumber, CurrentTenant.Id)
        {
            EmployeeId = input.EmployeeId,
            TransporterId = input.TransporterId,
            CellNumber = input.CellNumber,
            LicenseExpiryDate = input.LicenseExpiryDate,
            Address = input.Address,
        };
        entity.SetLicenseCategories(input.LicenseCategoryIds);

        await _repository.InsertAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Drivers.Edit)]
    public async Task<DriverDto> UpdateAsync(Guid id, CreateUpdateDriverDto input)
    {
        var query = await _repository.WithDetailsAsync();
        var entity = query.First(x => x.Id == id);

        entity.SetName(input.FullName);
        entity.EmployeeId = input.EmployeeId;
        entity.TransporterId = input.TransporterId;
        entity.CellNumber = input.CellNumber;
        entity.LicenseNumber = input.LicenseNumber;
        entity.LicenseExpiryDate = input.LicenseExpiryDate;
        entity.Address = input.Address;
        entity.SetLicenseCategories(input.LicenseCategoryIds);

        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Drivers.Edit)]
    public async Task<DriverDto> SuspendAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Suspend();
        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Drivers.Edit)]
    public async Task<DriverDto> ReinstateAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Reinstate();
        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Drivers.Edit)]
    public async Task<DriverDto> MarkLeftAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.MarkLeft();
        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Drivers.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}

[Authorize(MyERPPermissions.Vehicles.Default)]
public class VehicleAppService : ApplicationService, IVehicleAppService
{
    private readonly IRepository<Vehicle, Guid> _repository;
    private readonly IRepository<Driver, Guid> _driverRepository;

    public VehicleAppService(IRepository<Vehicle, Guid> repository, IRepository<Driver, Guid> driverRepository)
    {
        _repository = repository;
        _driverRepository = driverRepository;
    }

    private static VehicleDto ToDto(Vehicle entity, string? driverName = null)
    {
        return new VehicleDto
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            LicensePlate = entity.LicensePlate,
            Make = entity.Make,
            Model = entity.Model,
            ChassisNumber = entity.ChassisNumber,
            Color = entity.Color,
            FuelType = entity.FuelType,
            FuelUom = entity.FuelUom,
            LastOdometer = entity.LastOdometer,
            CarryingCapacity = entity.CarryingCapacity,
            Wheels = entity.Wheels,
            Doors = entity.Doors,
            VehicleValue = entity.VehicleValue,
            AcquisitionDate = entity.AcquisitionDate,
            DriverId = entity.DriverId,
            DriverName = driverName,
            LocationId = entity.LocationId,
            InsuranceCompany = entity.InsuranceCompany,
            PolicyNumber = entity.PolicyNumber,
            InsuranceStartDate = entity.InsuranceStartDate,
            InsuranceEndDate = entity.InsuranceEndDate,
            RoadTaxExpiryDate = entity.RoadTaxExpiryDate,
            FitnessCertificateExpiryDate = entity.FitnessCertificateExpiryDate,
            IsDisabled = entity.IsDisabled,
            CreationTime = entity.CreationTime,
        };
    }

    public async Task<VehicleDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        string? driverName = null;
        if (entity.DriverId.HasValue)
        {
            var driver = await _driverRepository.FindAsync(entity.DriverId.Value);
            driverName = driver?.FullName;
        }
        return ToDto(entity, driverName);
    }

    public async Task<PagedResultDto<VehicleDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.LicensePlate.Contains(input.Filter)
                || (x.Make != null && x.Make.Contains(input.Filter))
                || (x.Model != null && x.Model.Contains(input.Filter)));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var list = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.LicensePlate).Skip(input.SkipCount).Take(input.MaxResultCount));

        var driverIds = list.Where(x => x.DriverId.HasValue).Select(x => x.DriverId!.Value).Distinct().ToList();
        var driverNames = driverIds.Count == 0
            ? new System.Collections.Generic.Dictionary<Guid, string>()
            : (await _driverRepository.GetListAsync(d => driverIds.Contains(d.Id)))
                .ToDictionary(d => d.Id, d => d.FullName);

        var dtos = list.Select(v => ToDto(v, v.DriverId.HasValue && driverNames.TryGetValue(v.DriverId.Value, out var n) ? n : null)).ToList();
        return new PagedResultDto<VehicleDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Vehicles.Create)]
    public async Task<VehicleDto> CreateAsync(CreateUpdateVehicleDto input)
    {
        var entity = new Vehicle(GuidGenerator.Create(), input.CompanyId, input.LicensePlate, CurrentTenant.Id);
        ApplyInput(entity, input);

        await _repository.InsertAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Vehicles.Edit)]
    public async Task<VehicleDto> UpdateAsync(Guid id, CreateUpdateVehicleDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetLicensePlate(input.LicensePlate);
        ApplyInput(entity, input);

        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    private static void ApplyInput(Vehicle entity, CreateUpdateVehicleDto input)
    {
        entity.Make = input.Make;
        entity.Model = input.Model;
        entity.ChassisNumber = input.ChassisNumber;
        entity.Color = input.Color;
        entity.FuelType = input.FuelType;
        entity.FuelUom = input.FuelUom;
        entity.LastOdometer = input.LastOdometer;
        entity.CarryingCapacity = input.CarryingCapacity;
        entity.Wheels = input.Wheels;
        entity.Doors = input.Doors;
        entity.VehicleValue = input.VehicleValue;
        entity.AcquisitionDate = input.AcquisitionDate;
        entity.DriverId = input.DriverId;
        entity.LocationId = input.LocationId;
        entity.InsuranceCompany = input.InsuranceCompany;
        entity.PolicyNumber = input.PolicyNumber;
        entity.InsuranceStartDate = input.InsuranceStartDate;
        entity.InsuranceEndDate = input.InsuranceEndDate;
        entity.RoadTaxExpiryDate = input.RoadTaxExpiryDate;
        entity.FitnessCertificateExpiryDate = input.FitnessCertificateExpiryDate;
    }

    [Authorize(MyERPPermissions.Vehicles.Edit)]
    public async Task<VehicleDto> DisableAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Disable();
        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Vehicles.Edit)]
    public async Task<VehicleDto> EnableAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Enable();
        await _repository.UpdateAsync(entity);
        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Vehicles.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
