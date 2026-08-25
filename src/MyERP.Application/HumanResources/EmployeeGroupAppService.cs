using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.EmployeeGroups.Default)]
public class EmployeeGroupAppService : MyERPAppService, IEmployeeGroupAppService
{
    private readonly IRepository<EmployeeGroup, Guid> _repository;

    public EmployeeGroupAppService(IRepository<EmployeeGroup, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<EmployeeGroupDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(x => x.Items);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id));
        if (entity == null)
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(EmployeeGroup), id);

        return new EmployeeGroupMapper().Map(entity);
    }

    public async Task<PagedResultDto<EmployeeGroupDto>> GetListAsync(GetEmployeeGroupListDto input)
    {
        var query = await _repository.WithDetailsAsync(x => x.Items);
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (input.IsDisabled.HasValue)
            query = query.Where(x => x.IsDisabled == input.IsDisabled.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.GroupName.Contains(input.Filter));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.GroupName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        return new PagedResultDto<EmployeeGroupDto>(
            totalCount,
            entities.Select(e => new EmployeeGroupMapper().Map(e)).ToList());
    }

    [Authorize(MyERPPermissions.EmployeeGroups.Create)]
    public async Task<EmployeeGroupDto> CreateAsync(CreateUpdateEmployeeGroupDto input)
    {
        var entity = new EmployeeGroup(GuidGenerator.Create(), input.CompanyId, input.GroupName, CurrentTenant.Id)
        {
            IsDisabled = input.IsDisabled,
        };

        if (input.Items != null)
        {
            foreach (var item in input.Items)
            {
                entity.AddEmployee(item.EmployeeId, item.EmployeeName, item.Designation);
            }
        }

        await _repository.InsertAsync(entity);
        return new EmployeeGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.EmployeeGroups.Edit)]
    public async Task<EmployeeGroupDto> UpdateAsync(Guid id, CreateUpdateEmployeeGroupDto input)
    {
        var query = await _repository.WithDetailsAsync(x => x.Items);
        var entity = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id));
        if (entity == null)
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(EmployeeGroup), id);

        entity.GroupName = input.GroupName;
        entity.IsDisabled = input.IsDisabled;

        entity.ClearEmployees();
        if (input.Items != null)
        {
            foreach (var item in input.Items)
            {
                entity.AddEmployee(item.EmployeeId, item.EmployeeName, item.Designation);
            }
        }

        await _repository.UpdateAsync(entity);
        return new EmployeeGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.EmployeeGroups.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
