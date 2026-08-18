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
public class DesignationAppService : ApplicationService, IDesignationAppService
{
    private readonly IRepository<Designation, Guid> _repository;

    public DesignationAppService(IRepository<Designation, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<DesignationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(d => d.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<DesignationDto>(totalCount, items.Select(ObjectMapper.Map<Designation, DesignationDto>).ToList());
    }

    public async Task<DesignationDto> GetAsync(Guid id)
        => ObjectMapper.Map<Designation, DesignationDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<DesignationDto> CreateAsync(CreateUpdateDesignationDto input)
    {
        var designation = new Designation(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
        };
        await _repository.InsertAsync(designation);
        return ObjectMapper.Map<Designation, DesignationDto>(designation);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<DesignationDto> UpdateAsync(Guid id, CreateUpdateDesignationDto input)
    {
        var designation = await _repository.GetAsync(id);
        designation.Rename(input.Name);
        designation.Description = input.Description;
        await _repository.UpdateAsync(designation);
        return ObjectMapper.Map<Designation, DesignationDto>(designation);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
