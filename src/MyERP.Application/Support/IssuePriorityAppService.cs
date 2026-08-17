using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Support.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support;

[Authorize(MyERPPermissions.IssuePriorities.Default)]
public class IssuePriorityAppService : ApplicationService, IIssuePriorityAppService
{
    private readonly IRepository<IssuePriority, Guid> _repository;

    public IssuePriorityAppService(IRepository<IssuePriority, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<IssuePriorityDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<IssuePriority, IssuePriorityDto>(entity);
    }

    public async Task<PagedResultDto<IssuePriorityDto>> GetListAsync(GetIssuePriorityListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(p => p.Name.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(p => p.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<IssuePriorityDto>(totalCount,
            items.Select(ObjectMapper.Map<IssuePriority, IssuePriorityDto>).ToList());
    }

    [Authorize(MyERPPermissions.IssuePriorities.Create)]
    public async Task<IssuePriorityDto> CreateAsync(CreateUpdateIssuePriorityDto input)
    {
        var entity = new IssuePriority(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
        };
        await _repository.InsertAsync(entity);
        return ObjectMapper.Map<IssuePriority, IssuePriorityDto>(entity);
    }

    [Authorize(MyERPPermissions.IssuePriorities.Edit)]
    public async Task<IssuePriorityDto> UpdateAsync(Guid id, CreateUpdateIssuePriorityDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.Description = input.Description;
        await _repository.UpdateAsync(entity);
        return ObjectMapper.Map<IssuePriority, IssuePriorityDto>(entity);
    }

    [Authorize(MyERPPermissions.IssuePriorities.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
