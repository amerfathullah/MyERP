using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Maintenance.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Maintenance.BackgroundJobs;

/// <summary>
/// Background job that monitors upcoming preventive maintenance schedules and alerts service technicians.
/// Per ERPNext: maintenance_schedule.send_maintenance_reminder (daily scheduler).
/// </summary>
public class MaintenanceScheduleReminderJob : AsyncBackgroundJob<MaintenanceScheduleReminderJobArgs>, ITransientDependency
{
    private readonly IRepository<MaintenanceSchedule, Guid> _scheduleRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<MaintenanceScheduleReminderJob> _logger;

    public MaintenanceScheduleReminderJob(
        IRepository<MaintenanceSchedule, Guid> scheduleRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<MaintenanceScheduleReminderJob> logger)
    {
        _scheduleRepository = scheduleRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(MaintenanceScheduleReminderJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var upcomingWindow = asOfDate.AddDays(7);

        _logger.LogInformation("MaintenanceScheduleReminderJob: Checking upcoming maintenance visits for company {CompanyId} up to {Date}",
            args.CompanyId, upcomingWindow.ToString("yyyy-MM-dd"));

        var query = await _scheduleRepository.WithDetailsAsync(s => s.Details);
        var activeSchedules = query
            .Where(s => s.CompanyId == args.CompanyId && s.Status == MaintenanceScheduleStatus.Submitted)
            .ToList();

        if (!activeSchedules.Any())
            return;

        var dueSchedules = activeSchedules
            .Where(s => s.Details.Any(d => !d.IsCompleted && d.ScheduledDate <= upcomingWindow))
            .ToList();

        if (!dueSchedules.Any())
            return;

        var usersQuery = await _userRepository.GetQueryableAsync();
        var technicians = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"[MAINTENANCE REMINDER] {dueSchedules.Count} Scheduled Maintenance Visits Due";
        var body = $@"<h3>Upcoming Preventive Maintenance Visits</h3>
<p>There are {dueSchedules.Count} maintenance contract schedule(s) with visits due in the next 7 days:</p>
<ul>
{string.Join("", dueSchedules.Select(s => $"<li><strong>Schedule: {s.Id}</strong> - Periodicity: {s.Periodicity} | Due visits: {s.Details.Count(d => !d.IsCompleted && d.ScheduledDate <= upcomingWindow)}</li>"))}
</ul>
<p><em>Please review schedule details and dispatch field technicians.</em></p>";

        foreach (var tech in technicians)
        {
            try
            {
                await _emailSender.SendAsync(tech.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MaintenanceScheduleReminderJob: Failed to send maintenance reminder to {Email}", tech.Email);
            }
        }

        _logger.LogInformation("MaintenanceScheduleReminderJob: Sent reminder for {Count} due schedules for company {CompanyId}",
            dueSchedules.Count, args.CompanyId);
    }
}

public class MaintenanceScheduleReminderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
