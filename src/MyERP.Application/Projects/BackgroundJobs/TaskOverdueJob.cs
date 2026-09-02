using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Projects.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects.BackgroundJobs;

/// <summary>
/// Background job that marks tasks past expected end date as Overdue.
/// Per ERPNext: task.set_tasks_as_overdue (daily scheduler).
/// </summary>
public class TaskOverdueJob : AsyncBackgroundJob<TaskOverdueJobArgs>, ITransientDependency
{
    private readonly IRepository<ProjectTask, Guid> _taskRepository;
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly ILogger<TaskOverdueJob> _logger;

    public TaskOverdueJob(
        IRepository<ProjectTask, Guid> taskRepository,
        IRepository<Project, Guid> projectRepository,
        ILogger<TaskOverdueJob> logger)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(TaskOverdueJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("TaskOverdueJob: Checking overdue tasks for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var projectQuery = await _projectRepository.GetQueryableAsync();
        var companyProjectIds = args.ProjectId.HasValue
            ? new HashSet<Guid> { args.ProjectId.Value }
            : projectQuery
                .Where(p => p.CompanyId == args.CompanyId)
                .Select(p => p.Id)
                .ToHashSet();

        if (!companyProjectIds.Any())
            return;

        var taskQuery = await _taskRepository.GetQueryableAsync();
        var overdueTasks = taskQuery
            .Where(t => companyProjectIds.Contains(t.ProjectId) &&
                        (t.Status == ProjectTaskStatus.Open || t.Status == ProjectTaskStatus.Working) &&
                        t.ExpectedEndDate.HasValue &&
                        t.ExpectedEndDate.Value < asOfDate)
            .ToList();

        var markedCount = 0;
        foreach (var task in overdueTasks)
        {
            task.MarkOverdue();
            await _taskRepository.UpdateAsync(task);
            markedCount++;
        }

        _logger.LogInformation("TaskOverdueJob: Marked {Count} tasks as Overdue for company {CompanyId}",
            markedCount, args.CompanyId);
    }
}

public class TaskOverdueJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
