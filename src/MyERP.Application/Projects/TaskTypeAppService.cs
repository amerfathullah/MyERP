using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.TaskTypes.Default)]
public class TaskTypeAppService : MyERPAppService, ITaskTypeAppService
{
    private readonly IRepository<TaskType, Guid> _repository;

    public TaskTypeAppService(IRepository<TaskType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<TaskTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new TaskTypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<TaskTypeDto>> GetListAsync(GetTaskTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new TaskTypeMapper().Map(e)).ToList();
        return new PagedResultDto<TaskTypeDto>(totalCount, dtos);
    }

    public async Task<List<TaskTypeDto>> GetAllListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Name));
        return entities.Select(e => new TaskTypeMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.TaskTypes.Create)]
    public async Task<TaskTypeDto> CreateAsync(CreateUpdateTaskTypeDto input)
    {
        var entity = new TaskType(
            GuidGenerator.Create(),
            input.Name,
            input.Weight,
            input.Description,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new TaskTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TaskTypes.Edit)]
    public async Task<TaskTypeDto> UpdateAsync(Guid id, CreateUpdateTaskTypeDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.Weight = input.Weight;
        entity.Description = input.Description;

        await _repository.UpdateAsync(entity);
        return new TaskTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TaskTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
