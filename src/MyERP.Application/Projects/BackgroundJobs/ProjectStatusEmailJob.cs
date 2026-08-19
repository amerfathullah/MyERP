using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Projects.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Projects.BackgroundJobs;

/// <summary>
/// Background job that emails project progress summaries to assigned team members.
/// Per ERPNext: project.send_project_status_email_to_users (daily/weekly scheduler).
/// </summary>
public class ProjectStatusEmailJob : AsyncBackgroundJob<ProjectStatusEmailJobArgs>, ITransientDependency
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ProjectStatusEmailJob> _logger;

    public ProjectStatusEmailJob(
        IRepository<Project, Guid> projectRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<ProjectStatusEmailJob> logger)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ProjectStatusEmailJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("ProjectStatusEmailJob: Sending project status digest for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _projectRepository.WithDetailsAsync(p => p.Users, p => p.Tasks);
        var activeProjects = query
            .Where(p => p.CompanyId == args.CompanyId && p.Status == ProjectStatus.Open)
            .ToList();

        if (!activeProjects.Any())
            return;

        var sentEmails = 0;
        foreach (var project in activeProjects)
        {
            if (!project.Users.Any())
                continue;

            var userIds = project.Users.Select(u => u.UserId).Distinct().ToList();
            var usersQuery = await _userRepository.GetQueryableAsync();
            var users = usersQuery
                .Where(u => userIds.Contains(u.Id) && !string.IsNullOrEmpty(u.Email))
                .ToList();

            var overdueTasks = project.Tasks.Count(t => t.Status == ProjectTaskStatus.Overdue);
            var completedTasks = project.Tasks.Count(t => t.Status == ProjectTaskStatus.Completed);
            var totalTasks = project.Tasks.Count;

            var subject = $"Project Status Update: {project.ProjectName} ({project.ProjectNumber})";
            var body = $@"<h3>Project Status Summary</h3>
<p><strong>Project:</strong> {project.ProjectName} ({project.ProjectNumber})</p>
<p><strong>Progress:</strong> {project.PercentComplete:N0}%</p>
<p><strong>Total Tasks:</strong> {totalTasks}</p>
<p><strong>Completed Tasks:</strong> {completedTasks}</p>
<p><strong>Overdue Tasks:</strong> {overdueTasks}</p>
<p><em>As of date: {asOfDate:yyyy-MM-dd}</em></p>";

            foreach (var user in users)
            {
                try
                {
                    await _emailSender.SendAsync(user.Email!, subject, body, isBodyHtml: true);
                    sentEmails++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ProjectStatusEmailJob: Failed to send status email to user {UserId} ({Email}) for project {ProjectId}",
                        user.Id, user.Email, project.Id);
                }
            }
        }

        _logger.LogInformation("ProjectStatusEmailJob: Sent {SentCount} project digest emails for company {CompanyId}",
            sentEmails, args.CompanyId);
    }
}

public class ProjectStatusEmailJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
