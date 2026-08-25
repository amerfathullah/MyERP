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

[Authorize(MyERPPermissions.ProjectUpdates.Default)]
public class ProjectUpdateAppService : MyERPAppService, IProjectUpdateAppService
{
    private readonly IRepository<ProjectUpdate, Guid> _repository;
    private readonly IRepository<Project, Guid> _projectRepository;

    public ProjectUpdateAppService(
        IRepository<ProjectUpdate, Guid> repository,
        IRepository<Project, Guid> projectRepository)
    {
        _repository = repository;
        _projectRepository = projectRepository;
    }

    public async Task<ProjectUpdateDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return await MapToDtoAsync(entity);
    }

    public async Task<PagedResultDto<ProjectUpdateDto>> GetListAsync(GetProjectUpdateListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ProjectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == input.ProjectId.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.Date)
                 .ThenByDescending(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = new List<ProjectUpdateDto>();
        foreach (var entity in entities)
        {
            dtos.Add(await MapToDtoAsync(entity));
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            dtos = dtos.Where(x => (x.ProjectNumber != null && x.ProjectNumber.ToLower().Contains(filter)) ||
                                   (x.ProjectName != null && x.ProjectName.ToLower().Contains(filter)) ||
                                   (x.Summary != null && x.Summary.ToLower().Contains(filter))).ToList();
        }

        return new PagedResultDto<ProjectUpdateDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.ProjectUpdates.Create)]
    public async Task<ProjectUpdateDto> CreateAsync(CreateUpdateProjectUpdateDto input)
    {
        var entity = new ProjectUpdate(
            GuidGenerator.Create(),
            input.ProjectId,
            input.Date,
            input.PercentComplete,
            input.Summary,
            input.Notes,
            input.Time,
            CurrentTenant.Id)
        {
            Sent = input.Sent
        };

        await _repository.InsertAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.ProjectUpdates.Edit)]
    public async Task<ProjectUpdateDto> UpdateAsync(Guid id, CreateUpdateProjectUpdateDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.ProjectId = input.ProjectId;
        entity.Date = input.Date;
        entity.Time = input.Time;
        entity.PercentComplete = input.PercentComplete;
        entity.Summary = input.Summary;
        entity.Notes = input.Notes;
        entity.Sent = input.Sent;

        await _repository.UpdateAsync(entity);
        return await MapToDtoAsync(entity);
    }

    [Authorize(MyERPPermissions.ProjectUpdates.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task<ProjectUpdateDto> MapToDtoAsync(ProjectUpdate entity)
    {
        var dto = new ProjectUpdateMapper().Map(entity);
        var project = await _projectRepository.FindAsync(entity.ProjectId);
        if (project != null)
        {
            dto.ProjectNumber = project.ProjectNumber;
            dto.ProjectName = project.ProjectName;
        }
        return dto;
    }
}
