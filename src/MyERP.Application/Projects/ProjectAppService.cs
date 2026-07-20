using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.Projects.Default)]
public class ProjectAppService : ApplicationService, IProjectAppService
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ProjectAppService(
        IRepository<Project, Guid> projectRepository,
        IRepository<ProjectTask, Guid> taskRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _numberGenerator = numberGenerator;
    }

    public async Task<ProjectDto> GetAsync(Guid id)
    {
        var project = await _projectRepository.GetAsync(id, includeDetails: true);
        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    public async Task<PagedResultDto<ProjectDto>> GetListAsync(GetProjectListDto input)
    {
        var query = await _projectRepository.GetQueryableAsync();

        if (input.Status.HasValue)
            query = query.Where(p => p.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(p => p.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(p =>
                p.ProjectName.ToLower().Contains(filter.ToLower()) ||
                p.ProjectNumber.Contains(filter));
        }

        var totalCount = query.Count();
        query = query.OrderByDescending(p => p.CreationTime);
        var items = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ProjectDto>(totalCount, items.Select(ObjectMapper.Map<Project, ProjectDto>).ToList());
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ProjectDto> CreateAsync(CreateProjectDto input)
    {
        var number = await _numberGenerator.GenerateAsync("Project", input.CompanyId);
        var project = new Project(GuidGenerator.Create(), input.CompanyId, number, input.ProjectName, CurrentTenant.Id)
        {
            Priority = input.Priority,
            PercentCompleteMethod = input.PercentCompleteMethod,
            CustomerId = input.CustomerId,
            SalesOrderId = input.SalesOrderId,
            ExpectedStartDate = input.ExpectedStartDate,
            ExpectedEndDate = input.ExpectedEndDate,
            EstimatedCost = input.EstimatedCost,
            Notes = input.Notes,
        };

        await _projectRepository.InsertAsync(project);
        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto input)
    {
        var project = await _projectRepository.GetAsync(id);
        project.ProjectName = input.ProjectName;
        project.Priority = input.Priority;
        project.PercentCompleteMethod = input.PercentCompleteMethod;
        project.CustomerId = input.CustomerId;
        project.ExpectedStartDate = input.ExpectedStartDate;
        project.ExpectedEndDate = input.ExpectedEndDate;
        project.EstimatedCost = input.EstimatedCost;
        project.Notes = input.Notes;

        await _projectRepository.UpdateAsync(project);
        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _projectRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectDto> CompleteAsync(Guid id)
    {
        var project = await _projectRepository.GetAsync(id, includeDetails: true);
        project.Complete();
        await _projectRepository.UpdateAsync(project);
        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectDto> CancelAsync(Guid id)
    {
        var project = await _projectRepository.GetAsync(id);
        project.Cancel();
        await _projectRepository.UpdateAsync(project);
        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    // --- Tasks ---

    public async Task<ProjectTaskDto[]> GetTasksAsync(Guid projectId)
    {
        var tasks = await _taskRepository.GetListAsync(t => t.ProjectId == projectId);
        return tasks.OrderBy(t => t.CreationTime).Select(ObjectMapper.Map<ProjectTask, ProjectTaskDto>).ToArray();
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ProjectTaskDto> CreateTaskAsync(CreateProjectTaskDto input)
    {
        var taskNumber = $"TASK-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var task = new ProjectTask(GuidGenerator.Create(), input.ProjectId, taskNumber, input.Subject)
        {
            Priority = input.Priority,
            ParentTaskId = input.ParentTaskId,
            IsGroup = input.IsGroup,
            IsMilestone = input.IsMilestone,
            TaskWeight = input.TaskWeight,
            ExpectedStartDate = input.ExpectedStartDate,
            ExpectedEndDate = input.ExpectedEndDate,
            ExpectedHours = input.ExpectedHours,
            AssignedUserId = input.AssignedUserId,
            Description = input.Description,
        };

        await _taskRepository.InsertAsync(task);
        await UpdateProjectProgress(input.ProjectId);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> UpdateTaskAsync(Guid taskId, UpdateProjectTaskDto input)
    {
        var task = await _taskRepository.GetAsync(taskId);
        task.Subject = input.Subject;
        task.Priority = input.Priority;
        task.ParentTaskId = input.ParentTaskId;
        task.IsGroup = input.IsGroup;
        task.IsMilestone = input.IsMilestone;
        task.TaskWeight = input.TaskWeight;
        task.Progress = input.Progress;
        task.ExpectedStartDate = input.ExpectedStartDate;
        task.ExpectedEndDate = input.ExpectedEndDate;
        task.ExpectedHours = input.ExpectedHours;
        task.AssignedUserId = input.AssignedUserId;
        task.Description = input.Description;

        await _taskRepository.UpdateAsync(task);
        await UpdateProjectProgress(task.ProjectId);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        var projectId = task.ProjectId;
        await _taskRepository.DeleteAsync(task);
        await UpdateProjectProgress(projectId);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> StartTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        task.Start();
        await _taskRepository.UpdateAsync(task);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> CompleteTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);

        // Per DO-NOT: "Allow tasks to be marked Completed when dependencies are incomplete"
        var depValidator = LazyServiceProvider.LazyGetRequiredService<MyERP.Projects.DomainServices.TaskDependencyValidationService>();
        await depValidator.ValidateDependenciesCompletedAsync(taskId);

        task.Complete();
        await _taskRepository.UpdateAsync(task);
        await UpdateProjectProgress(task.ProjectId);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> CancelTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        task.Cancel();
        await _taskRepository.UpdateAsync(task);
        await UpdateProjectProgress(task.ProjectId);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    private async Task UpdateProjectProgress(Guid projectId)
    {
        var project = await _projectRepository.GetAsync(projectId, includeDetails: true);
        project.UpdateProgress();
        await _projectRepository.UpdateAsync(project);
    }
}

