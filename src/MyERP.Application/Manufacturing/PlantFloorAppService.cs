using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.PlantFloors.Default)]
public class PlantFloorAppService : MyERPAppService, IPlantFloorAppService
{
    private readonly IRepository<PlantFloor, Guid> _repository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public PlantFloorAppService(
        IRepository<PlantFloor, Guid> repository,
        IRepository<Company, Guid> companyRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<PlantFloorDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new PlantFloorMapper().Map(entity);

        var company = await _companyRepository.FindAsync(entity.CompanyId);
        dto.CompanyName = company?.Name;

        if (entity.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepository.FindAsync(entity.WarehouseId.Value);
            dto.WarehouseName = warehouse?.Name;
        }

        return dto;
    }

    public async Task<PagedResultDto<PlantFloorDto>> GetListAsync(GetPlantFloorListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.FloorName.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.FloorName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new PlantFloorMapper().Map(e)).ToList();

        var companyIds = entities.Select(e => e.CompanyId).Distinct().ToList();
        var warehouseIds = entities.Where(e => e.WarehouseId.HasValue).Select(e => e.WarehouseId!.Value).Distinct().ToList();

        var companies = (await _companyRepository.GetQueryableAsync())
            .Where(c => companyIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        var warehouses = (await _warehouseRepository.GetQueryableAsync())
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        foreach (var dto in dtos)
        {
            if (companies.TryGetValue(dto.CompanyId, out var compName))
                dto.CompanyName = compName;

            if (dto.WarehouseId.HasValue && warehouses.TryGetValue(dto.WarehouseId.Value, out var whName))
                dto.WarehouseName = whName;
        }

        return new PagedResultDto<PlantFloorDto>(totalCount, dtos);
    }

    public async Task<List<PlantFloorDto>> GetAllListAsync(Guid companyId)
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(
            query.Where(x => x.CompanyId == companyId && x.IsActive)
                 .OrderBy(x => x.FloorName));

        return entities.Select(e => new PlantFloorMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.PlantFloors.Create)]
    public async Task<PlantFloorDto> CreateAsync(CreateUpdatePlantFloorDto input)
    {
        var entity = new PlantFloor(
            GuidGenerator.Create(),
            input.CompanyId,
            input.FloorName,
            input.WarehouseId,
            input.Description,
            CurrentTenant.Id)
        {
            IsActive = input.IsActive
        };

        await _repository.InsertAsync(entity);
        return new PlantFloorMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.PlantFloors.Edit)]
    public async Task<PlantFloorDto> UpdateAsync(Guid id, CreateUpdatePlantFloorDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.CompanyId = input.CompanyId;
        entity.FloorName = input.FloorName;
        entity.WarehouseId = input.WarehouseId;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new PlantFloorMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.PlantFloors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
