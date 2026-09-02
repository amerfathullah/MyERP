using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

[Authorize(MyERPPermissions.Projects.Default)]
public class ProjectAppService : ApplicationService, IProjectAppService
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IRepository<ProjectUser, Guid> _projectUserRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public ProjectAppService(
        IRepository<Project, Guid> projectRepository,
        IRepository<ProjectTask, Guid> taskRepository,
        IRepository<ProjectUser, Guid> projectUserRepository,
        IDocumentNumberGenerator numberGenerator)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _projectUserRepository = projectUserRepository;
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
                p.ProjectName.Contains(filter) ||
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
        if (input.ExpectedStartDate.HasValue && input.ExpectedEndDate.HasValue && input.ExpectedEndDate.Value < input.ExpectedStartDate.Value)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.EstimatedCost < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "EstimatedCost");
        }

        var number = await _numberGenerator.GenerateAsync("Project", input.CompanyId);
        var project = new Project(GuidGenerator.Create(), input.CompanyId, number, input.ProjectName, CurrentTenant.Id)
        {
            Priority = input.Priority,
            PercentCompleteMethod = input.PercentCompleteMethod,
            CustomerId = input.CustomerId,
            SalesOrderId = input.SalesOrderId,
            CostCenterId = input.CostCenterId,
            ExpectedStartDate = input.ExpectedStartDate,
            ExpectedEndDate = input.ExpectedEndDate,
            EstimatedCost = input.EstimatedCost,
            Notes = input.Notes,
        };

        await _projectRepository.InsertAsync(project);

        if (input.ProjectTemplateId.HasValue)
        {
            await CreateTasksFromTemplateAsync(project.Id, input.ProjectTemplateId.Value);
        }

        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    /// <summary>
    /// Clones every task in the template onto the project, remapping intra-template
    /// dependency edges to the newly-created ProjectTask ids. Per ERPNext
    /// Project._create_task_from_template / check_depends_on_value.
    /// </summary>
    private async Task CreateTasksFromTemplateAsync(Guid projectId, Guid projectTemplateId)
    {
        var templateRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ProjectTemplate, Guid>>();
        var template = await templateRepo.GetAsync(projectTemplateId, includeDetails: true);

        var idByTemplateTaskId = new System.Collections.Generic.Dictionary<Guid, Guid>();
        var newTasks = new System.Collections.Generic.List<ProjectTask>();

        foreach (var templateTask in template.Tasks)
        {
            var taskNumber = $"TASK-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            var task = new ProjectTask(GuidGenerator.Create(), projectId, taskNumber, templateTask.Subject)
            {
                TaskWeight = templateTask.TaskWeight,
                ExpectedHours = templateTask.ExpectedHours,
                IsMilestone = templateTask.IsMilestone,
            };
            idByTemplateTaskId[templateTask.Id] = task.Id;
            newTasks.Add(task);
        }

        foreach (var (templateTask, task) in template.Tasks.Zip(newTasks))
        {
            foreach (var dep in templateTask.Dependencies)
                task.AddDependency(idByTemplateTaskId[dep.DependsOnTaskId]);
        }

        foreach (var task in newTasks)
            await _taskRepository.InsertAsync(task);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto input)
    {
        var project = await _projectRepository.GetAsync(id);
        project.ProjectName = input.ProjectName;
        project.Priority = input.Priority;
        project.PercentCompleteMethod = input.PercentCompleteMethod;
        project.CustomerId = input.CustomerId;
        project.CostCenterId = input.CostCenterId;
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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Project", project.Id,
            "Completed", project.CompanyId,
            project.ProjectNumber, "Open", "Completed", CurrentUser.Id,
            $"Project {project.ProjectNumber} ({project.ProjectName}) completed", CurrentTenant.Id));

        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectDto> CancelAsync(Guid id)
    {
        var project = await _projectRepository.GetAsync(id);
        project.Cancel();
        await _projectRepository.UpdateAsync(project);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Project", project.Id,
            "Cancelled", project.CompanyId,
            project.ProjectNumber, project.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Project {project.ProjectNumber} ({project.ProjectName}) cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    // --- Team Members ---

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectUserDto> CreateUserAsync(AddProjectUserDto input)
    {
        var exists = await _projectUserRepository.AnyAsync(
            u => u.ProjectId == input.ProjectId && u.UserId == input.UserId);
        if (exists)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("entity", "Project User");

        var projectUser = new ProjectUser(GuidGenerator.Create(), input.ProjectId, input.UserId)
        {
            ViewAttachments = input.ViewAttachments,
            HideTimesheets = input.HideTimesheets,
        };
        await _projectUserRepository.InsertAsync(projectUser);

        return new ProjectUserDto
        {
            Id = projectUser.Id,
            ProjectId = projectUser.ProjectId,
            UserId = projectUser.UserId,
            ViewAttachments = projectUser.ViewAttachments,
            HideTimesheets = projectUser.HideTimesheets,
        };
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectUserDto> UpdateUserAsync(Guid projectUserId, UpdateProjectUserDto input)
    {
        var projectUser = await _projectUserRepository.GetAsync(projectUserId);
        projectUser.ViewAttachments = input.ViewAttachments;
        projectUser.HideTimesheets = input.HideTimesheets;
        await _projectUserRepository.UpdateAsync(projectUser);

        return new ProjectUserDto
        {
            Id = projectUser.Id,
            ProjectId = projectUser.ProjectId,
            UserId = projectUser.UserId,
            ViewAttachments = projectUser.ViewAttachments,
            HideTimesheets = projectUser.HideTimesheets,
        };
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteUserAsync(Guid projectUserId)
    {
        await _projectUserRepository.DeleteAsync(projectUserId);
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

        task.SetDefaultEndDateIfMissing();
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
        task.SetDefaultEndDateIfMissing();

        var endDateChanged = task.ExpectedEndDate != input.ExpectedEndDate;
        await _taskRepository.UpdateAsync(task);

        // Per ERPNext task.py reschedule_dependent_tasks (Gotcha #1302): pushing this task's end
        // date out must cascade to dependent tasks whose start would otherwise overlap it.
        if (endDateChanged)
        {
            var depValidator = LazyServiceProvider.LazyGetRequiredService<MyERP.Projects.DomainServices.TaskDependencyValidationService>();
            await depValidator.RescheduleDependentTasksAsync(task);
        }

        await UpdateProjectProgress(task.ProjectId);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        var projectId = task.ProjectId;
        await _taskRepository.DeleteAsync(task, autoSave: true);
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

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> AddTaskDependencyAsync(Guid taskId, Guid dependsOnTaskId)
    {
        var task = await _taskRepository.GetAsync(taskId);

        // Per DO-NOT: "Skip circular reference detection on task dependency changes" —
        // ProjectTask.AddDependency's own doc comment says full cycle detection requires this
        // service; it had no caller anywhere until now (the only prior AddDependency call site,
        // template instantiation, mirrors a template's own graph rather than accepting arbitrary
        // user input).
        var depValidator = LazyServiceProvider.LazyGetRequiredService<MyERP.Projects.DomainServices.TaskDependencyValidationService>();
        await depValidator.ValidateNoCycleAsync(task.ProjectId, taskId, dependsOnTaskId);

        task.AddDependency(dependsOnTaskId);
        await _taskRepository.UpdateAsync(task);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> RemoveTaskDependencyAsync(Guid taskId, Guid dependencyId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        task.Dependencies.RemoveAll(d => d.Id == dependencyId);
        await _taskRepository.UpdateAsync(task);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    private async Task UpdateProjectProgress(Guid projectId)
    {
        var project = await _projectRepository.GetAsync(projectId, includeDetails: true);
        // Query tasks directly rather than trusting project.Tasks (AutoInclude): confirmed on both
        // SQLite and PostgreSQL that the navigation can still reflect the pre-mutation task set
        // immediately after a same-request insert/delete, while a direct repository query is fresh.
        var currentTasks = await _taskRepository.GetListAsync(t => t.ProjectId == projectId);
        project.UpdateProgress(currentTasks);
        await _projectRepository.UpdateAsync(project);
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ProjectDto> DuplicateProjectAsync(Guid sourceProjectId, string newProjectName)
    {
        Check.NotNullOrWhiteSpace(newProjectName, nameof(newProjectName));

        var source = await _projectRepository.GetAsync(sourceProjectId, includeDetails: true);
        if (string.Equals(source.ProjectName, newProjectName, StringComparison.OrdinalIgnoreCase))
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "New project name must be different from source project name.");
        }

        var number = await _numberGenerator.GenerateAsync("Project", source.CompanyId);
        var newProject = new Project(GuidGenerator.Create(), source.CompanyId, number, newProjectName, CurrentTenant.Id)
        {
            Priority = source.Priority,
            PercentCompleteMethod = source.PercentCompleteMethod,
            CustomerId = source.CustomerId,
            ExpectedStartDate = source.ExpectedStartDate,
            ExpectedEndDate = source.ExpectedEndDate,
            EstimatedCost = source.EstimatedCost,
            Notes = source.Notes,
        };

        await _projectRepository.InsertAsync(newProject, autoSave: true);

        // Duplicate tasks
        var sourceTasks = await _taskRepository.GetListAsync(t => t.ProjectId == sourceProjectId);
        var taskIdMap = new System.Collections.Generic.Dictionary<Guid, Guid>();

        foreach (var st in sourceTasks.OrderBy(t => t.CreationTime))
        {
            var taskNumber = $"TASK-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            var newTask = new ProjectTask(GuidGenerator.Create(), newProject.Id, taskNumber, st.Subject)
            {
                Priority = st.Priority,
                IsGroup = st.IsGroup,
                IsMilestone = st.IsMilestone,
                TaskWeight = st.TaskWeight,
                ExpectedStartDate = st.ExpectedStartDate,
                ExpectedEndDate = st.ExpectedEndDate,
                ExpectedHours = st.ExpectedHours,
                AssignedUserId = st.AssignedUserId,
                Description = st.Description,
            };
            taskIdMap[st.Id] = newTask.Id;
            await _taskRepository.InsertAsync(newTask, autoSave: true);
        }

        // Map parent task hierarchy
        foreach (var st in sourceTasks.Where(t => t.ParentTaskId.HasValue))
        {
            if (taskIdMap.TryGetValue(st.Id, out var newTaskId) &&
                taskIdMap.TryGetValue(st.ParentTaskId!.Value, out var newParentId))
            {
                var nt = await _taskRepository.GetAsync(newTaskId);
                nt.ParentTaskId = newParentId;
                await _taskRepository.UpdateAsync(nt);
            }
        }

        await UpdateProjectProgress(newProject.Id);
        return ObjectMapper.Map<Project, ProjectDto>(newProject);
    }
}

