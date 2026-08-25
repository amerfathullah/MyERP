using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.ActivityCosts.Default)]
public class ActivityCostAppService : MyERPAppService, IActivityCostAppService
{
    private readonly IRepository<ActivityCost, Guid> _repository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<ActivityType, Guid> _activityTypeRepository;

    public ActivityCostAppService(
        IRepository<ActivityCost, Guid> repository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<ActivityType, Guid> activityTypeRepository)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _activityTypeRepository = activityTypeRepository;
    }

    public async Task<ActivityCostDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new ActivityCostMapper().Map(entity);

        var employee = await _employeeRepository.FindAsync(entity.EmployeeId);
        dto.EmployeeName = employee?.FullName;
        dto.Department = employee?.Department;

        var activityType = await _activityTypeRepository.FindAsync(entity.ActivityTypeId);
        dto.ActivityTypeName = activityType?.Name;

        return dto;
    }

    public async Task<PagedResultDto<ActivityCostDto>> GetListAsync(GetActivityCostListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.EmployeeId.HasValue)
            query = query.Where(x => x.EmployeeId == input.EmployeeId.Value);
        if (input.ActivityTypeId.HasValue)
            query = query.Where(x => x.ActivityTypeId == input.ActivityTypeId.Value);

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new ActivityCostMapper().Map(e)).ToList();

        var empIds = entities.Select(e => e.EmployeeId).Distinct().ToList();
        var actIds = entities.Select(e => e.ActivityTypeId).Distinct().ToList();

        var employees = (await _employeeRepository.GetQueryableAsync())
            .Where(e => empIds.Contains(e.Id))
            .ToDictionary(e => e.Id, e => new { e.FullName, e.Department });

        var activityTypes = (await _activityTypeRepository.GetQueryableAsync())
            .Where(a => actIds.Contains(a.Id))
            .ToDictionary(a => a.Id, a => a.Name);

        foreach (var dto in dtos)
        {
            if (employees.TryGetValue(dto.EmployeeId, out var emp))
            {
                dto.EmployeeName = emp.FullName;
                dto.Department = emp.Department;
            }

            if (activityTypes.TryGetValue(dto.ActivityTypeId, out var actName))
            {
                dto.ActivityTypeName = actName;
            }
        }

        return new PagedResultDto<ActivityCostDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.ActivityCosts.Create)]
    public async Task<ActivityCostDto> CreateAsync(CreateUpdateActivityCostDto input)
    {
        var entity = new ActivityCost(
            GuidGenerator.Create(),
            input.EmployeeId,
            input.ActivityTypeId,
            input.BillingRate,
            input.CostingRate,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new ActivityCostMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.ActivityCosts.Edit)]
    public async Task<ActivityCostDto> UpdateAsync(Guid id, CreateUpdateActivityCostDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.EmployeeId = input.EmployeeId;
        entity.ActivityTypeId = input.ActivityTypeId;
        entity.BillingRate = input.BillingRate;
        entity.CostingRate = input.CostingRate;

        await _repository.UpdateAsync(entity);
        return new ActivityCostMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.ActivityCosts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
