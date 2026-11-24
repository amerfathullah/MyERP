using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
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

        if (input.CustomerId.HasValue)
        {
            var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>();
            var customer = await customerRepo.FindAsync(input.CustomerId.Value);
            if (customer != null && customer.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("customerCompany", customer.CompanyId)
                    .WithData("projectCompany", input.CompanyId);
            }
        }

        if (input.SalesOrderId.HasValue)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var so = await soRepo.FindAsync(input.SalesOrderId.Value);
            if (so != null && so.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("salesOrderCompany", so.CompanyId)
                    .WithData("projectCompany", input.CompanyId);
            }
        }

        if (input.CostCenterId.HasValue)
        {
            var ccRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CostCenter, Guid>>();
            var cc = await ccRepo.FindAsync(input.CostCenterId.Value);
            if (cc != null && cc.CompanyId != input.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("costCenterCompany", cc.CompanyId)
                    .WithData("projectCompany", input.CompanyId);
            }
        }

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "Project", input.CompanyId,
            customerIds: input.CustomerId.HasValue ? new[] { input.CustomerId.Value } : null);

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
            CollectProgress = input.CollectProgress,
            Subject = input.CollectProgress && string.IsNullOrWhiteSpace(input.Subject)
                ? $"For project {input.ProjectName}, update your status"
                : input.Subject,
            Message = input.Message,
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
                TaskTypeId = templateTask.TaskTypeId,
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
        if (input.ExpectedStartDate.HasValue && input.ExpectedEndDate.HasValue && input.ExpectedEndDate.Value < input.ExpectedStartDate.Value)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.EstimatedCost < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "EstimatedCost");
        }

        var project = await _projectRepository.GetAsync(id);

        if (input.CustomerId.HasValue)
        {
            var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Customer, Guid>>();
            var customer = await customerRepo.FindAsync(input.CustomerId.Value);
            if (customer != null && customer.CompanyId != project.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("customerCompany", customer.CompanyId)
                    .WithData("projectCompany", project.CompanyId);
            }
        }

        if (input.CostCenterId.HasValue)
        {
            var ccRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CostCenter, Guid>>();
            var cc = await ccRepo.FindAsync(input.CostCenterId.Value);
            if (cc != null && cc.CompanyId != project.CompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("costCenterCompany", cc.CompanyId)
                    .WithData("projectCompany", project.CompanyId);
            }
        }

        var companyRestriction = LazyServiceProvider.LazyGetRequiredService<CompanyRestrictionValidationService>();
        await companyRestriction.ValidateTransactionCompanyAsync(
            "Project", project.CompanyId,
            customerIds: input.CustomerId.HasValue ? new[] { input.CustomerId.Value } : null);

        project.ProjectName = input.ProjectName;
        project.Priority = input.Priority;
        project.PercentCompleteMethod = input.PercentCompleteMethod;
        project.CustomerId = input.CustomerId;
        project.CostCenterId = input.CostCenterId;
        project.ExpectedStartDate = input.ExpectedStartDate;
        project.ExpectedEndDate = input.ExpectedEndDate;
        project.EstimatedCost = input.EstimatedCost;
        project.Notes = input.Notes;
        project.CollectProgress = input.CollectProgress;
        project.Subject = input.CollectProgress && string.IsNullOrWhiteSpace(input.Subject)
            ? $"For project {input.ProjectName}, update your status"
            : input.Subject;
        project.Message = input.Message;

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
        var previousStatus = project.Status.ToString();
        project.Cancel();
        await _projectRepository.UpdateAsync(project);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Project", project.Id,
            "Cancelled", project.CompanyId,
            project.ProjectNumber, previousStatus, "Cancelled", CurrentUser.Id,
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
        ProjectTask? parentTask = null;
        if (input.ParentTaskId.HasValue)
        {
            parentTask = await _taskRepository.GetAsync(input.ParentTaskId.Value);
            if (!parentTask.IsGroup)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Parent Task '{parentTask.Subject}' must be a Group Task.");
            }
            // Per ERPNext PR #60578 / commit 4973292edc: parent task must be from the same project
            if (parentTask.ProjectId != input.ProjectId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Parent Task '{parentTask.Subject}' is not in Project '{input.ProjectId}'. Pick a group task of the same project.");
            }
        }

        decimal resolvedWeight = input.TaskWeight ?? 1m;
        if (input.TaskTypeId.HasValue && !input.TaskWeight.HasValue)
        {
            var taskTypeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<TaskType, Guid>>();
            var taskType = await taskTypeRepo.GetAsync(input.TaskTypeId.Value);
            resolvedWeight = taskType.Weight;
        }

        var taskNumber = $"TASK-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var task = new ProjectTask(GuidGenerator.Create(), input.ProjectId, taskNumber, input.Subject)
        {
            Priority = input.Priority,
            ParentTaskId = input.ParentTaskId,
            TaskTypeId = input.TaskTypeId,
            IsGroup = input.IsGroup,
            IsMilestone = input.IsMilestone,
            TaskWeight = resolvedWeight,
            ExpectedStartDate = input.ExpectedStartDate,
            ExpectedEndDate = input.ExpectedEndDate,
            ExpectedHours = input.ExpectedHours,
            AssignedUserId = input.AssignedUserId,
            Description = input.Description,
        };

        // Per ERPNext PR #69181: compute default end date before validating against parent group end date
        task.SetDefaultEndDateIfMissing();
        MyERP.Projects.DomainServices.TaskDependencyValidationService.ValidateParentExpectedEndDate(task, parentTask);
        await _taskRepository.InsertAsync(task);
        await UpdateProjectProgress(input.ProjectId);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(task);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ProjectTaskDto> UpdateTaskAsync(Guid taskId, UpdateProjectTaskDto input)
    {
        if (input.Progress > 100)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Progress % for a task cannot be more than 100.");
        }

        var task = await _taskRepository.GetAsync(taskId);
        var previousScheduleEndDate = task.GetScheduleEndDate();
        var previousExpectedEndDate = task.ExpectedEndDate;
        var depValidator = LazyServiceProvider?.LazyGetService<MyERP.Projects.DomainServices.TaskDependencyValidationService>()
            ?? new MyERP.Projects.DomainServices.TaskDependencyValidationService(_taskRepository);

        // Per ERPNext PR #69181: refuse converting a group task with children to non-group
        await depValidator.ValidateGroupHasNoChildrenAsync(task, input.IsGroup);

        ProjectTask? parentTask = null;
        if (input.ParentTaskId.HasValue)
        {
            if (input.ParentTaskId.Value == taskId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Task cannot be its own parent.");
            }

            parentTask = await _taskRepository.GetAsync(input.ParentTaskId.Value);
            if (!parentTask.IsGroup)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Parent Task '{parentTask.Subject}' must be a Group Task.");
            }
            // Per ERPNext PR #60578 / commit 4973292edc: parent task must be from the same project
            if (parentTask.ProjectId != task.ProjectId)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Parent Task '{parentTask.Subject}' is not in Project '{task.ProjectId}'. Pick a group task of the same project.");
            }
        }

        // Per ERPNext PR #60635 / commit ba5e28ebee:
        // Type change fetches the task weight unless the weight changes explicitly
        var typeChanged = input.TaskTypeId != task.TaskTypeId;
        var weightChanged = input.TaskWeight.HasValue && input.TaskWeight.Value != task.TaskWeight;

        if (input.TaskTypeId.HasValue && (typeChanged && !weightChanged))
        {
            var taskTypeRepo = LazyServiceProvider!.LazyGetRequiredService<IRepository<TaskType, Guid>>();
            var taskType = await taskTypeRepo.GetAsync(input.TaskTypeId.Value);
            task.SetDefaultWeight(taskType.Weight);
        }
        else if (input.TaskWeight.HasValue)
        {
            task.TaskWeight = input.TaskWeight.Value;
        }

        task.TaskTypeId = input.TaskTypeId;
        task.Subject = input.Subject;
        task.Priority = input.Priority;
        task.ParentTaskId = input.ParentTaskId;
        task.IsGroup = input.IsGroup;
        task.IsMilestone = input.IsMilestone;
        task.Progress = input.Progress;
        task.ExpectedStartDate = input.ExpectedStartDate;
        task.ExpectedEndDate = input.ExpectedEndDate;
        task.ExpectedHours = input.ExpectedHours;
        task.AssignedUserId = input.AssignedUserId;
        task.Description = input.Description;

        // Per ERPNext PR #69181: derive missing end date BEFORE parent/children date validations
        task.SetDefaultEndDateIfMissing();
        MyERP.Projects.DomainServices.TaskDependencyValidationService.ValidateParentExpectedEndDate(task, parentTask);
        if (task.ExpectedEndDate != previousExpectedEndDate)
        {
            await depValidator.ValidateChildrenExpectedEndDateAsync(task, task.ExpectedEndDate);
        }

        var scheduleEndChanged = task.GetScheduleEndDate() != previousScheduleEndDate;
        await _taskRepository.UpdateAsync(task);

        // Per ERPNext PRs #69135 & #69136 (task.py reschedule_dependent_tasks): cascade end-date shifts
        // forward or backward (when moved earlier) to dependent tasks.
        if (scheduleEndChanged)
        {
            await depValidator.RescheduleDependentTasksAsync(task, previousEndDate: previousScheduleEndDate);
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
    public async Task<ProjectTaskDto> UpdateTaskStatusAsync(Guid taskId, ProjectTaskStatus status)
    {
        var task = await _taskRepository.GetAsync(taskId);
        if (status == ProjectTaskStatus.Completed)
        {
            var depValidator = LazyServiceProvider.LazyGetRequiredService<MyERP.Projects.DomainServices.TaskDependencyValidationService>();
            await depValidator.ValidateDependenciesCompletedAsync(taskId);
        }

        task.SetStatus(status);
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
                TaskTypeId = st.TaskTypeId,
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

    /// <summary>
    /// Project Summary report: task completion/overdue counts and progress across projects.
    /// Per ERPNext projects/report/project_summary.
    /// </summary>
    public async Task<ProjectSummaryReportDto> GetProjectSummaryReportAsync(ProjectSummaryRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        var today = DateTime.UtcNow.Date;
        var projQuery = await _projectRepository.GetQueryableAsync();
        var projects = projQuery
            .Where(p => p.CompanyId == input.CompanyId
                && (!input.Status.HasValue || p.Status == input.Status.Value)
                && (!input.ProjectTypeId.HasValue || p.ProjectTypeId == input.ProjectTypeId.Value)
                && (!input.IsActive.HasValue || (input.IsActive.Value ? p.Status != ProjectStatus.Cancelled : p.Status == ProjectStatus.Cancelled)))
            .OrderBy(p => p.ExpectedEndDate ?? DateTime.MaxValue)
            .ThenBy(p => p.ProjectNumber)
            .ToList();

        if (projects.Count == 0)
        {
            return new ProjectSummaryReportDto();
        }

        var projectIds = projects.Select(p => p.Id).ToHashSet();
        var taskQuery = await _taskRepository.GetQueryableAsync();
        var tasksByProject = taskQuery
            .Where(t => projectIds.Contains(t.ProjectId))
            .ToList()
            .GroupBy(t => t.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var typeNames = new System.Collections.Generic.Dictionary<Guid, string>();
        var typeRepo = LazyServiceProvider?.LazyGetService<IRepository<ProjectType, Guid>>();
        if (typeRepo != null)
        {
            typeNames = (await typeRepo.GetListAsync())
                .ToDictionary(t => t.Id, t => t.Name);
        }

        var rows = new System.Collections.Generic.List<ProjectSummaryRowDto>(projects.Count);
        foreach (var project in projects)
        {
            tasksByProject.TryGetValue(project.Id, out var projTasks);
            projTasks ??= new System.Collections.Generic.List<ProjectTask>();

            var completed = projTasks.Count(t => t.Status == ProjectTaskStatus.Completed);
            var overdue = projTasks.Count(t =>
                t.Status == ProjectTaskStatus.Overdue ||
                (t.Status != ProjectTaskStatus.Completed
                    && t.Status != ProjectTaskStatus.Cancelled
                    && t.ExpectedEndDate.HasValue
                    && t.ExpectedEndDate.Value.Date < today));

            string? typeName = null;
            if (project.ProjectTypeId.HasValue)
            {
                typeNames.TryGetValue(project.ProjectTypeId.Value, out typeName);
            }

            rows.Add(new ProjectSummaryRowDto
            {
                ProjectId = project.Id,
                ProjectNumber = project.ProjectNumber,
                ProjectName = project.ProjectName,
                ProjectTypeName = typeName,
                Status = project.Status,
                TotalTasks = projTasks.Count,
                CompletedTasks = completed,
                OverdueTasks = overdue,
                PercentComplete = project.PercentComplete,
                ExpectedStartDate = project.ExpectedStartDate,
                ExpectedEndDate = project.ExpectedEndDate,
            });
        }

        return new ProjectSummaryReportDto
        {
            TotalProjects = rows.Count,
            OpenProjects = rows.Count(r => r.Status == ProjectStatus.Open),
            CompletedProjects = rows.Count(r => r.Status == ProjectStatus.Completed),
            TotalTasks = rows.Sum(r => r.TotalTasks),
            CompletedTasks = rows.Sum(r => r.CompletedTasks),
            OverdueTasks = rows.Sum(r => r.OverdueTasks),
            Rows = rows,
        };
    }

    /// <summary>
    /// Delayed Tasks Summary report: computes task delay in days relative to ExpectedEndDate.
    /// Per ERPNext projects/report/delayed_tasks_summary.
    /// </summary>
    public async Task<DelayedTasksSummaryReportDto> GetDelayedTasksSummaryAsync(DelayedTasksSummaryRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var asOfDate = (input.AsOfDate ?? DateTime.UtcNow).Date;
        var fromDate = input.FromDate?.Date;
        var toDate = input.ToDate?.Date;

        var projQuery = await _projectRepository.GetQueryableAsync();
        var projects = projQuery
            .Where(p => p.CompanyId == input.CompanyId
                && (!input.ProjectId.HasValue || p.Id == input.ProjectId.Value))
            .ToDictionary(p => p.Id);

        if (projects.Count == 0)
        {
            return new DelayedTasksSummaryReportDto();
        }

        var projectIds = projects.Keys.ToHashSet();
        var taskQuery = await _taskRepository.GetQueryableAsync();
        var tasks = taskQuery
            .Where(t => projectIds.Contains(t.ProjectId)
                && t.Status != ProjectTaskStatus.Cancelled
                && (!input.Priority.HasValue || t.Priority == input.Priority.Value)
                && (!input.Status.HasValue || t.Status == input.Status.Value)
                && (!fromDate.HasValue || (t.ExpectedEndDate.HasValue && t.ExpectedEndDate.Value.Date >= fromDate.Value))
                && (!toDate.HasValue || (t.ExpectedStartDate.HasValue && t.ExpectedStartDate.Value.Date <= toDate.Value)))
            .ToList();

        var rows = new System.Collections.Generic.List<DelayedTaskSummaryRowDto>(tasks.Count);
        foreach (var task in tasks)
        {
            int delayDays = 0;
            if (task.ExpectedEndDate.HasValue)
            {
                if (task.ActualEndDate.HasValue)
                    delayDays = (task.ActualEndDate.Value.Date - task.ExpectedEndDate.Value.Date).Days;
                else if (task.Status != ProjectTaskStatus.Completed)
                    delayDays = (asOfDate - task.ExpectedEndDate.Value.Date).Days;
            }

            projects.TryGetValue(task.ProjectId, out var proj);

            rows.Add(new DelayedTaskSummaryRowDto
            {
                TaskId = task.Id,
                TaskNumber = task.TaskNumber,
                Subject = task.Subject,
                ProjectId = task.ProjectId,
                ProjectNumber = proj?.ProjectNumber ?? string.Empty,
                ProjectName = proj?.ProjectName ?? string.Empty,
                Status = task.Status,
                Priority = task.Priority,
                Progress = task.Progress,
                ExpectedStartDate = task.ExpectedStartDate,
                ExpectedEndDate = task.ExpectedEndDate,
                ActualEndDate = task.ActualEndDate,
                DelayInDays = delayDays,
            });
        }

        rows = rows
            .OrderByDescending(r => r.DelayInDays)
            .ThenBy(r => r.ExpectedEndDate ?? DateTime.MaxValue)
            .ToList();

        return new DelayedTasksSummaryReportDto
        {
            TotalTasks = rows.Count,
            DelayedCount = rows.Count(r => r.DelayInDays > 0),
            OnTrackCount = rows.Count(r => r.DelayInDays <= 0),
            Rows = rows,
        };
    }

    /// <summary>
    /// Project-wise Stock Tracking report: purchased, issued, and delivered item costs per project.
    /// Per ERPNext projects/report/project_wise_stock_tracking.
    /// </summary>
    public async Task<ProjectWiseStockTrackingReportDto> GetProjectWiseStockTrackingAsync(
        ProjectWiseStockTrackingRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        var companyRepo = GetRepo<Company>();
        var company = companyRepo != null ? await companyRepo.FindAsync(input.CompanyId) : null;
        var currencyCode = string.IsNullOrWhiteSpace(company?.CurrencyCode) ? "MYR" : company.CurrencyCode;
        var companyName = company?.Name ?? string.Empty;

        var projQuery = await _projectRepository.GetQueryableAsync();
        var projects = projQuery
            .Where(p => p.CompanyId == input.CompanyId
                && p.Status != ProjectStatus.Cancelled
                && (!input.ProjectId.HasValue || p.Id == input.ProjectId.Value)
                && (!input.Status.HasValue || p.Status == input.Status.Value))
            .OrderBy(p => p.ProjectNumber)
            .ToList();

        if (projects.Count == 0)
        {
            return new ProjectWiseStockTrackingReportDto { CurrencyCode = currencyCode };
        }

        var projectIds = projects.Select(p => p.Id).ToHashSet();
        var customerNames = await LoadCustomerNamesAsync(projects);

        var (poProjectMap, poItemProjectMap) = await LoadPurchaseOrderProjectMapsAsync(input.CompanyId);
        var (soProjectMap, soItemOrderMap) = await LoadSalesOrderProjectMapsAsync(input.CompanyId);

        var purchasedMap = await GetPurchasedItemsCostByProjectAsync(
            input.CompanyId, projectIds, poProjectMap, poItemProjectMap);
        var issuedMap = await GetIssuedItemsCostByProjectAsync(input.CompanyId, projectIds);
        var deliveredMap = await GetDeliveredItemsCostByProjectAsync(
            input.CompanyId, projectIds, soProjectMap, soItemOrderMap);

        var rows = new List<ProjectWiseStockTrackingRowDto>(projects.Count);
        foreach (var project in projects)
        {
            string? customerName = null;
            if (project.CustomerId.HasValue)
            {
                customerNames.TryGetValue(project.CustomerId.Value, out customerName);
            }

            rows.Add(new ProjectWiseStockTrackingRowDto
            {
                ProjectId = project.Id,
                ProjectNumber = project.ProjectNumber,
                ProjectName = project.ProjectName,
                Status = project.Status,
                CompanyId = project.CompanyId,
                CompanyName = companyName,
                CustomerId = project.CustomerId,
                CustomerName = customerName,
                CostOfPurchasedItems = purchasedMap.GetValueOrDefault(project.Id),
                CostOfIssuedItems = issuedMap.GetValueOrDefault(project.Id),
                CostOfDeliveredItems = deliveredMap.GetValueOrDefault(project.Id),
                EstimatedCost = project.EstimatedCost,
                ExpectedStartDate = project.ExpectedStartDate,
                ExpectedEndDate = project.ExpectedEndDate,
            });
        }

        return new ProjectWiseStockTrackingReportDto
        {
            CurrencyCode = currencyCode,
            TotalPurchasedCost = rows.Sum(r => r.CostOfPurchasedItems),
            TotalIssuedCost = rows.Sum(r => r.CostOfIssuedItems),
            TotalDeliveredCost = rows.Sum(r => r.CostOfDeliveredItems),
            TotalEstimatedCost = rows.Sum(r => r.EstimatedCost),
            Rows = rows,
        };
    }

    private async Task<Dictionary<Guid, string>> LoadCustomerNamesAsync(List<Project> projects)
    {
        var customerIds = projects
            .Where(p => p.CustomerId.HasValue)
            .Select(p => p.CustomerId!.Value)
            .Distinct()
            .ToHashSet();

        if (customerIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var customerRepo = GetRepo<Customer>();
        if (customerRepo == null)
        {
            return new Dictionary<Guid, string>();
        }

        var query = await customerRepo.GetQueryableAsync();
        return query
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);
    }

    private IRepository<TEntity, Guid>? GetRepo<TEntity>()
        where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid>
        => LazyServiceProvider?.LazyGetService<IRepository<TEntity, Guid>>();

    private async Task<(Dictionary<Guid, Guid?> PoProjects, Dictionary<Guid, Guid?> PoItemProjects)>
        LoadPurchaseOrderProjectMapsAsync(Guid companyId)
    {
        var poProjects = new Dictionary<Guid, Guid?>();
        var poItemProjects = new Dictionary<Guid, Guid?>();

        var poRepo = GetRepo<PurchaseOrder>();
        var poItemRepo = GetRepo<PurchaseOrderItem>();
        if (poRepo == null || poItemRepo == null)
        {
            return (poProjects, poItemProjects);
        }

        var poQuery = await poRepo.GetQueryableAsync();
        var orders = poQuery
            .Where(po => po.CompanyId == companyId
                && po.Status != DocumentStatus.Draft
                && po.Status != DocumentStatus.Cancelled
                && po.Status != DocumentStatus.Rejected)
            .Select(po => new { po.Id, po.ProjectId })
            .ToList();

        if (orders.Count == 0)
        {
            return (poProjects, poItemProjects);
        }

        foreach (var po in orders)
        {
            poProjects[po.Id] = po.ProjectId;
        }

        var poIds = poProjects.Keys.ToHashSet();
        var poItemQuery = await poItemRepo.GetQueryableAsync();
        var items = poItemQuery
            .Where(i => poIds.Contains(i.PurchaseOrderId))
            .Select(i => new { i.Id, i.PurchaseOrderId, i.ProjectId })
            .ToList();

        foreach (var item in items)
        {
            poProjects.TryGetValue(item.PurchaseOrderId, out var headerProj);
            poItemProjects[item.Id] = item.ProjectId ?? headerProj;
        }

        return (poProjects, poItemProjects);
    }

    private async Task<(Dictionary<Guid, Guid?> SoProjects, Dictionary<Guid, Guid> SoItemToOrder)>
        LoadSalesOrderProjectMapsAsync(Guid companyId)
    {
        var soProjects = new Dictionary<Guid, Guid?>();
        var soItemToOrder = new Dictionary<Guid, Guid>();

        var soRepo = GetRepo<SalesOrder>();
        var soItemRepo = GetRepo<SalesOrderItem>();
        if (soRepo == null || soItemRepo == null)
        {
            return (soProjects, soItemToOrder);
        }

        var soQuery = await soRepo.GetQueryableAsync();
        var orders = soQuery
            .Where(so => so.CompanyId == companyId
                && so.Status != DocumentStatus.Draft
                && so.Status != DocumentStatus.Cancelled
                && so.Status != DocumentStatus.Rejected)
            .Select(so => new { so.Id, so.ProjectId })
            .ToList();

        if (orders.Count == 0)
        {
            return (soProjects, soItemToOrder);
        }

        foreach (var so in orders)
        {
            soProjects[so.Id] = so.ProjectId;
        }

        var soIds = soProjects.Keys.ToHashSet();
        var soItemQuery = await soItemRepo.GetQueryableAsync();
        var items = soItemQuery
            .Where(i => soIds.Contains(i.SalesOrderId))
            .Select(i => new { i.Id, i.SalesOrderId })
            .ToList();

        foreach (var item in items)
        {
            soItemToOrder[item.Id] = item.SalesOrderId;
        }

        return (soProjects, soItemToOrder);
    }

    private async Task<Dictionary<Guid, decimal>> GetPurchasedItemsCostByProjectAsync(
        Guid companyId,
        HashSet<Guid> targetProjects,
        Dictionary<Guid, Guid?> poProjects,
        Dictionary<Guid, Guid?> poItemProjects)
    {
        var result = new Dictionary<Guid, decimal>();

        var prRepo = GetRepo<PurchaseReceipt>();
        var prItemRepo = GetRepo<PurchaseReceiptItem>();
        if (prRepo != null && prItemRepo != null)
        {
            var prQuery = await prRepo.GetQueryableAsync();
            var prs = prQuery
                .Where(pr => pr.CompanyId == companyId
                    && pr.Status != DocumentStatus.Draft
                    && pr.Status != DocumentStatus.Cancelled
                    && pr.Status != DocumentStatus.Rejected)
                .Select(pr => new { pr.Id, pr.PurchaseOrderId, pr.ExchangeRate })
                .ToDictionary(pr => pr.Id);

            if (prs.Count > 0)
            {
                var prIds = prs.Keys.ToHashSet();
                var prItemQuery = await prItemRepo.GetQueryableAsync();
                var prItems = prItemQuery
                    .Where(i => prIds.Contains(i.PurchaseReceiptId))
                    .Select(i => new { i.PurchaseReceiptId, i.PurchaseOrderItemId, i.Quantity, i.UnitPrice })
                    .ToList();

                foreach (var item in prItems)
                {
                    var pr = prs[item.PurchaseReceiptId];
                    Guid? projId = null;
                    if (item.PurchaseOrderItemId.HasValue
                        && poItemProjects.TryGetValue(item.PurchaseOrderItemId.Value, out var itemProj))
                    {
                        projId = itemProj;
                    }

                    if (!projId.HasValue && pr.PurchaseOrderId.HasValue
                        && poProjects.TryGetValue(pr.PurchaseOrderId.Value, out var prHeaderProj))
                    {
                        projId = prHeaderProj;
                    }

                    if (projId.HasValue && targetProjects.Contains(projId.Value))
                    {
                        var rate = pr.ExchangeRate > 0m ? pr.ExchangeRate : 1m;
                        var amount = item.Quantity * item.UnitPrice * rate;
                        result[projId.Value] = result.GetValueOrDefault(projId.Value) + amount;
                    }
                }
            }
        }

        var piRepo = GetRepo<PurchaseInvoice>();
        var piItemRepo = GetRepo<PurchaseInvoiceItem>();
        if (piRepo != null && piItemRepo != null)
        {
            var piQuery = await piRepo.GetQueryableAsync();
            var pis = piQuery
                .Where(pi => pi.CompanyId == companyId
                    && pi.Status != DocumentStatus.Draft
                    && pi.Status != DocumentStatus.Cancelled
                    && pi.Status != DocumentStatus.Rejected)
                .Select(pi => new { pi.Id, pi.ProjectId, pi.ExchangeRate, pi.UpdateStock })
                .ToDictionary(pi => pi.Id);

            if (pis.Count > 0)
            {
                var piIds = pis.Keys.ToHashSet();
                var piItemQuery = await piItemRepo.GetQueryableAsync();
                var piItems = piItemQuery
                    .Where(i => piIds.Contains(i.PurchaseInvoiceId))
                    .Select(i => new { i.PurchaseInvoiceId, i.PurchaseOrderItemId, i.PurchaseReceiptItemId, i.BilledQuantity, i.UnitPrice })
                    .ToList();

                foreach (var item in piItems)
                {
                    var pi = pis[item.PurchaseInvoiceId];
                    // Include direct non-UpdateStock Purchase Invoices not linked to a Purchase Receipt (ERPNext 4b368ed5d4)
                    if (!pi.UpdateStock && item.PurchaseReceiptItemId.HasValue)
                    {
                        continue;
                    }

                    Guid? projId = null;
                    if (item.PurchaseOrderItemId.HasValue
                        && poItemProjects.TryGetValue(item.PurchaseOrderItemId.Value, out var poItemProj))
                    {
                        projId = poItemProj;
                    }

                    projId ??= pi.ProjectId;
                    if (projId.HasValue && targetProjects.Contains(projId.Value))
                    {
                        var rate = pi.ExchangeRate > 0m ? pi.ExchangeRate : 1m;
                        var amount = item.BilledQuantity * item.UnitPrice * rate;
                        result[projId.Value] = result.GetValueOrDefault(projId.Value) + amount;
                    }
                }
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, decimal>> GetIssuedItemsCostByProjectAsync(
        Guid companyId,
        HashSet<Guid> targetProjects)
    {
        var result = new Dictionary<Guid, decimal>();
        var seRepo = GetRepo<StockEntry>();
        var seItemRepo = GetRepo<StockEntryItem>();
        if (seRepo == null || seItemRepo == null)
        {
            return result;
        }

        var seQuery = await seRepo.GetQueryableAsync();
        var entries = seQuery
            .Where(se => se.CompanyId == companyId
                && se.Status != DocumentStatus.Draft
                && se.Status != DocumentStatus.Cancelled
                && se.Status != DocumentStatus.Rejected)
            .Select(se => new { se.Id, se.ProjectId })
            .ToDictionary(se => se.Id, se => se.ProjectId);

        if (entries.Count == 0)
        {
            return result;
        }

        var seIds = entries.Keys.ToHashSet();
        var seItemQuery = await seItemRepo.GetQueryableAsync();
        var items = seItemQuery
            .Where(i => seIds.Contains(i.StockEntryId) && i.TargetWarehouseId == null)
            .Select(i => new { i.StockEntryId, i.ProjectId, i.Quantity, i.ValuationRate, i.AdditionalCost })
            .ToList();

        foreach (var item in items)
        {
            entries.TryGetValue(item.StockEntryId, out var headerProj);
            var projId = item.ProjectId ?? headerProj;
            if (projId.HasValue && targetProjects.Contains(projId.Value))
            {
                var amount = item.Quantity * (item.ValuationRate ?? 0m) + item.AdditionalCost;
                result[projId.Value] = result.GetValueOrDefault(projId.Value) + amount;
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, decimal>> GetDeliveredItemsCostByProjectAsync(
        Guid companyId,
        HashSet<Guid> targetProjects,
        Dictionary<Guid, Guid?> soProjects,
        Dictionary<Guid, Guid> soItemToOrder)
    {
        var result = new Dictionary<Guid, decimal>();
        var sleRepo = GetRepo<StockLedgerEntry>();
        if (sleRepo == null)
        {
            return result;
        }

        var sleQuery = await sleRepo.GetQueryableAsync();
        var sles = sleQuery
            .Where(s => s.CompanyId == companyId
                && !s.IsCancelled
                && (s.VoucherType == "DeliveryNote"
                    || s.VoucherType == "Delivery Note"
                    || s.VoucherType == "SalesInvoice"
                    || s.VoucherType == "Sales Invoice"))
            .Select(s => new
            {
                s.VoucherType,
                s.VoucherId,
                s.VoucherDetailNo,
                s.ProjectId,
                s.StockValueDifference,
            })
            .ToList();

        if (sles.Count == 0)
        {
            return result;
        }

        var dnOrderMap = new Dictionary<Guid, Guid?>();
        var dnItemSoItemMap = new Dictionary<Guid, Guid?>();
        var dnIds = sles
            .Where(s => (s.VoucherType == "DeliveryNote" || s.VoucherType == "Delivery Note") && s.VoucherId.HasValue)
            .Select(s => s.VoucherId!.Value)
            .Distinct()
            .ToHashSet();

        if (dnIds.Count > 0)
        {
            var dnRepo = GetRepo<DeliveryNote>();
            var dnItemRepo = GetRepo<DeliveryNoteItem>();
            if (dnRepo != null)
            {
                var dnQuery = await dnRepo.GetQueryableAsync();
                dnOrderMap = dnQuery
                    .Where(d => dnIds.Contains(d.Id))
                    .ToDictionary(d => d.Id, d => d.SalesOrderId);
            }

            if (dnItemRepo != null)
            {
                var dnItemQuery = await dnItemRepo.GetQueryableAsync();
                dnItemSoItemMap = dnItemQuery
                    .Where(i => dnIds.Contains(i.DeliveryNoteId))
                    .ToDictionary(i => i.Id, i => i.SalesOrderItemId);
            }
        }

        var siProjMap = new Dictionary<Guid, Guid?>();
        var siItemSoItemMap = new Dictionary<Guid, Guid?>();
        var siIds = sles
            .Where(s => (s.VoucherType == "SalesInvoice" || s.VoucherType == "Sales Invoice") && s.VoucherId.HasValue)
            .Select(s => s.VoucherId!.Value)
            .Distinct()
            .ToHashSet();

        if (siIds.Count > 0)
        {
            var siRepo = GetRepo<SalesInvoice>();
            var siItemRepo = GetRepo<SalesInvoiceItem>();
            if (siRepo != null)
            {
                var siQuery = await siRepo.GetQueryableAsync();
                siProjMap = siQuery
                    .Where(s => siIds.Contains(s.Id))
                    .ToDictionary(s => s.Id, s => s.ProjectId);
            }

            if (siItemRepo != null)
            {
                var siItemQuery = await siItemRepo.GetQueryableAsync();
                siItemSoItemMap = siItemQuery
                    .Where(i => siIds.Contains(i.SalesInvoiceId))
                    .ToDictionary(i => i.Id, i => i.SalesOrderItemId);
            }
        }

        foreach (var sle in sles)
        {
            var isDeliveryNote = sle.VoucherType == "DeliveryNote" || sle.VoucherType == "Delivery Note";
            var itemSoMap = isDeliveryNote ? dnItemSoItemMap : siItemSoItemMap;
            var projId = ResolveSalesOrderItemProject(sle.VoucherDetailNo, itemSoMap, soItemToOrder, soProjects);

            if (!projId.HasValue && sle.VoucherId.HasValue)
            {
                if (isDeliveryNote && dnOrderMap.TryGetValue(sle.VoucherId.Value, out var dnSoId) && dnSoId.HasValue)
                {
                    soProjects.TryGetValue(dnSoId.Value, out projId);
                }
                else if (!isDeliveryNote)
                {
                    siProjMap.TryGetValue(sle.VoucherId.Value, out projId);
                }
            }

            projId ??= sle.ProjectId;
            if (projId.HasValue && targetProjects.Contains(projId.Value))
            {
                result[projId.Value] = result.GetValueOrDefault(projId.Value) + (-sle.StockValueDifference);
            }
        }

        return result;
    }

    private static Guid? ResolveSalesOrderItemProject(
        Guid? voucherDetailNo,
        Dictionary<Guid, Guid?> itemSoMap,
        Dictionary<Guid, Guid> soItemToOrder,
        Dictionary<Guid, Guid?> soProjects)
    {
        if (voucherDetailNo.HasValue
            && itemSoMap.TryGetValue(voucherDetailNo.Value, out var soItemId)
            && soItemId.HasValue
            && soItemToOrder.TryGetValue(soItemId.Value, out var soId)
            && soProjects.TryGetValue(soId, out var projId))
        {
            return projId;
        }

        return null;
    }
}

