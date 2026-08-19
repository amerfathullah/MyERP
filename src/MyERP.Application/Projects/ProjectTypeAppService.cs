using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.Projects.Default)]
public class ProjectTypeAppService : ApplicationService, IProjectTypeAppService
{
    private readonly IRepository<ProjectType, Guid> _repository;

    public ProjectTypeAppService(IRepository<ProjectType, Guid> repository)
    {
        _repository = repository;
    }

    private static ProjectTypeDto ToDto(ProjectType entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        IsActive = entity.IsActive,
        CreationTime = entity.CreationTime,
    };

    public async Task<ProjectTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ToDto(entity);
    }

    public async Task<PagedResultDto<ProjectTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var list = query.OrderBy(x => x.Name).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ProjectTypeDto>(totalCount, list.Select(ToDto).ToList());
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ProjectTypeDto> CreateAsync(CreateUpdateProjectTypeDto input)
    {
        var entity = new ProjectType(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            IsActive = input.IsActive,
        };
        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProjectType", entity.Id,
            "Created", Guid.Empty,
            entity.Name, "Draft", "Active", CurrentUser.Id,
            $"Project type '{entity.Name}' created", CurrentTenant.Id));

        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTypeDto> UpdateAsync(Guid id, CreateUpdateProjectTypeDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetName(input.Name);
        entity.IsActive = input.IsActive;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProjectType", entity.Id,
            "Updated", Guid.Empty,
            entity.Name, "Active", "Active", CurrentUser.Id,
            $"Project type '{entity.Name}' updated", CurrentTenant.Id));

        return ToDto(entity);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
