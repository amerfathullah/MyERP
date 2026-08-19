using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.Projects.Default)]
public class ProjectTemplateAppService : ApplicationService, IProjectTemplateAppService
{
    private readonly IRepository<ProjectTemplate, Guid> _repository;

    public ProjectTemplateAppService(IRepository<ProjectTemplate, Guid> repository) => _repository = repository;

    public async Task<ListResultDto<ProjectTemplateDto>> GetListAsync()
    {
        var query = (await _repository.WithDetailsAsync()).OrderBy(t => t.TemplateName);
        return new ListResultDto<ProjectTemplateDto>(query.ToList().Select(MapToDto).ToList());
    }

    public async Task<ProjectTemplateDto> GetAsync(Guid id)
    {
        var template = (await _repository.WithDetailsAsync()).First(t => t.Id == id);
        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ProjectTemplateDto> CreateAsync(CreateUpdateProjectTemplateDto input)
    {
        var template = new ProjectTemplate(GuidGenerator.Create(), input.TemplateName, CurrentTenant.Id)
        {
            Disabled = input.Disabled,
        };
        template.SetTasks(input.Tasks.Select(ToTaskTuple));
        await _repository.InsertAsync(template);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProjectTemplate", template.Id,
            "Created", Guid.Empty,
            template.TemplateName, "Draft", "Active", CurrentUser.Id,
            $"Project template '{template.TemplateName}' created with {template.Tasks.Count} tasks", CurrentTenant.Id));

        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTemplateDto> UpdateAsync(Guid id, CreateUpdateProjectTemplateDto input)
    {
        var template = await _repository.GetAsync(id, includeDetails: true);
        template.TemplateName = input.TemplateName;
        template.Disabled = input.Disabled;
        template.SetTasks(input.Tasks.Select(ToTaskTuple));
        await _repository.UpdateAsync(template);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ProjectTemplate", template.Id,
            "Updated", Guid.Empty,
            template.TemplateName, "Active", "Active", CurrentUser.Id,
            $"Project template '{template.TemplateName}' updated", CurrentTenant.Id));

        return MapToDto(template);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static (Guid Key, string Subject, decimal TaskWeight, decimal ExpectedHours, bool IsMilestone, System.Collections.Generic.List<Guid> DependsOnKeys) ToTaskTuple(ProjectTemplateTaskDto t)
        => (t.Key, t.Subject, t.TaskWeight, t.ExpectedHours, t.IsMilestone, t.DependsOnKeys);

    private static ProjectTemplateDto MapToDto(ProjectTemplate template) => new()
    {
        Id = template.Id,
        TemplateName = template.TemplateName,
        Disabled = template.Disabled,
        Tasks = template.Tasks.Select(t => new ProjectTemplateTaskDto
        {
            Key = t.Id,
            Subject = t.Subject,
            TaskWeight = t.TaskWeight,
            ExpectedHours = t.ExpectedHours,
            IsMilestone = t.IsMilestone,
            DependsOnKeys = t.Dependencies.Select(d => d.DependsOnTaskId).ToList(),
        }).ToList(),
    };
}
