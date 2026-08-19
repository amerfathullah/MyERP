using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Projects.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects.BackgroundJobs;

/// <summary>
/// Background job that aggregates project status and completion progress.
/// Per ERPNext: project.collect_project_status (hourly/daily scheduler).
/// </summary>
public class ProjectStatusCollectionJob : AsyncBackgroundJob<ProjectStatusCollectionJobArgs>, ITransientDependency
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly ILogger<ProjectStatusCollectionJob> _logger;

    public ProjectStatusCollectionJob(
        IRepository<Project, Guid> projectRepository,
        ILogger<ProjectStatusCollectionJob> logger)
    {
        _projectRepository = projectRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ProjectStatusCollectionJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("ProjectStatusCollectionJob: Collecting project progress for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _projectRepository.WithDetailsAsync(p => p.Tasks);
        var activeProjects = query
            .Where(p => p.CompanyId == args.CompanyId &&
                        p.Status == ProjectStatus.Open)
            .ToList();

        var updatedCount = 0;
        foreach (var project in activeProjects)
        {
            var prevProgress = project.PercentComplete;
            var prevStatus = project.Status;

            project.UpdateProgress();

            if (project.PercentComplete != prevProgress || project.Status != prevStatus)
            {
                await _projectRepository.UpdateAsync(project);
                updatedCount++;
            }
        }

        _logger.LogInformation("ProjectStatusCollectionJob: Updated progress for {Count} of {Total} active projects for company {CompanyId}",
            updatedCount, activeProjects.Count, args.CompanyId);
    }
}

public class ProjectStatusCollectionJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
