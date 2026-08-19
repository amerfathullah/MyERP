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
/// Background job that notifies maintenance personnel and customers of upcoming maintenance schedules.
/// Per ERPNext: maintenance_schedule.generate_schedule (daily scheduler).
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

        _logger.LogInformation("MaintenanceScheduleReminderJob: Checking maintenance schedules for company {CompanyId} up to {Window}",
            args.CompanyId, upcomingWindow);

        var query = await _scheduleRepository.WithDetailsAsync(s => s.Details);
        var activeSchedules = query
            .Where(s => s.CompanyId == args.CompanyId &&
                        s.Status == MaintenanceScheduleStatus.Submitted)
            .ToList();

        var remindedCount = 0;
        foreach (var schedule in activeSchedules)
        {
            var dueDetails = schedule.Details
                .Where(d => d.ScheduledDate >= asOfDate &&
                            d.ScheduledDate <= upcomingWindow &&
                            !d.IsCompleted)
                .ToList();

            if (!dueDetails.Any())
                continue;

            if (schedule.SalesPersonId.HasValue)
            {
                var user = await _userRepository.FindAsync(schedule.SalesPersonId.Value);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    try
                    {
                        var subject = $"Upcoming Maintenance Visits: {schedule.Periodicity} Schedule";
                        var body = $@"<h3>Upcoming Preventive Maintenance</h3>
<p>You have {dueDetails.Count} maintenance visit(s) scheduled between {asOfDate:yyyy-MM-dd} and {upcomingWindow:yyyy-MM-dd}.</p>
<p>Schedule ID: {schedule.Id}</p>";

                        await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
                        remindedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "MaintenanceScheduleReminderJob: Failed to send maintenance reminder to user {UserId}", user.Id);
                    }
                }
            }
        }

        _logger.LogInformation("MaintenanceScheduleReminderJob: Sent {Count} maintenance reminders for company {CompanyId}",
            remindedCount, args.CompanyId);
    }
}

public class MaintenanceScheduleReminderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
