using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

[Authorize(MyERPPermissions.Manufacturing.Default)]
public class WorkstationTypeAppService : ApplicationService, IWorkstationTypeAppService
{
    private readonly IRepository<WorkstationType, Guid> _repository;

    public WorkstationTypeAppService(IRepository<WorkstationType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<WorkstationTypeDto> GetAsync(Guid id)
    {
        var type = await _repository.GetAsync(id);
        return ObjectMapper.Map<WorkstationType, WorkstationTypeDto>(type);
    }

    public async Task<PagedResultDto<WorkstationTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var count = query.Count();
        var items = query.OrderBy(x => x.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<WorkstationTypeDto>(count, items.Select(ObjectMapper.Map<WorkstationType, WorkstationTypeDto>).ToList());
    }

    [Authorize(MyERPPermissions.Manufacturing.Create)]
    public async Task<WorkstationTypeDto> CreateAsync(CreateWorkstationTypeDto input)
    {
        var type = new WorkstationType(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
        };
        type.ReplaceCosts(input.Costs.Select(c => (c.Component, c.OperatingCost)));
        await _repository.InsertAsync(type);
        return ObjectMapper.Map<WorkstationType, WorkstationTypeDto>(type);
    }

    [Authorize(MyERPPermissions.Manufacturing.Edit)]
    public async Task<WorkstationTypeDto> UpdateAsync(Guid id, CreateWorkstationTypeDto input)
    {
        var type = await _repository.GetAsync(id);
        type.Name = input.Name;
        type.Description = input.Description;
        type.ReplaceCosts(input.Costs.Select(c => (c.Component, c.OperatingCost)));
        await _repository.UpdateAsync(type);
        return ObjectMapper.Map<WorkstationType, WorkstationTypeDto>(type);
    }

    [Authorize(MyERPPermissions.Manufacturing.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
