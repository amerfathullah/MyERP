using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class DepartmentAppService : ApplicationService, IDepartmentAppService
{
    private readonly IRepository<Department, Guid> _repository;

    public DepartmentAppService(IRepository<Department, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<DepartmentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(d => d.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<DepartmentDto>(totalCount, items.Select(ObjectMapper.Map<Department, DepartmentDto>).ToList());
    }

    public async Task<DepartmentDto> GetAsync(Guid id)
        => ObjectMapper.Map<Department, DepartmentDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<DepartmentDto> CreateAsync(CreateUpdateDepartmentDto input)
    {
        var department = new Department(GuidGenerator.Create(), input.Name, input.CompanyId, input.IsGroup, input.ParentId, CurrentTenant.Id)
        {
            IsActive = input.IsActive,
        };
        await _repository.InsertAsync(department);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Department", department.Id,
            "Created", department.CompanyId,
            department.Name, "Draft", "Active", CurrentUser.Id,
            $"Department '{department.Name}' created", CurrentTenant.Id));

        return ObjectMapper.Map<Department, DepartmentDto>(department);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<DepartmentDto> UpdateAsync(Guid id, CreateUpdateDepartmentDto input)
    {
        if (input.ParentId == id)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("reason", "A department cannot be its own parent.");
        }

        var department = await _repository.GetAsync(id);
        department.Rename(input.Name);
        department.CompanyId = input.CompanyId;
        department.ParentId = input.ParentId;
        department.IsGroup = input.IsGroup;
        department.IsActive = input.IsActive;
        await _repository.UpdateAsync(department);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Department", department.Id,
            "Updated", department.CompanyId,
            department.Name, "Active", "Active", CurrentUser.Id,
            $"Department '{department.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<Department, DepartmentDto>(department);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
