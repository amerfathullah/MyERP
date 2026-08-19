using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that notifies quality inspectors of pending/uncompleted quality inspections.
/// Per ERPNext: quality_inspection.notify_inspectors (daily scheduler).
/// </summary>
public class QualityInspectionReminderJob : AsyncBackgroundJob<QualityInspectionReminderJobArgs>, ITransientDependency
{
    private readonly IRepository<QualityInspection, Guid> _qiRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<QualityInspectionReminderJob> _logger;

    public QualityInspectionReminderJob(
        IRepository<QualityInspection, Guid> qiRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<QualityInspectionReminderJob> logger)
    {
        _qiRepository = qiRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(QualityInspectionReminderJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var pendingThreshold = asOfDate.AddDays(-3);

        _logger.LogInformation("QualityInspectionReminderJob: Checking overdue draft quality inspections for company {CompanyId}",
            args.CompanyId);

        var query = await _qiRepository.GetQueryableAsync();
        var pendingQis = query
            .Where(q => q.CompanyId == args.CompanyId &&
                        q.DocStatus == DocumentStatus.Draft &&
                        q.CreationTime <= pendingThreshold)
            .ToList();

        if (!pendingQis.Any())
            return;

        var usersQuery = await _userRepository.GetQueryableAsync();
        var qualityUsers = usersQuery
            .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
            .Take(5)
            .ToList();

        var subject = $"[ACTION REQUIRED] {pendingQis.Count} Pending Quality Inspections Overdue";
        var body = $@"<h3>Pending Quality Inspections Alert</h3>
<p>There are {pendingQis.Count} quality inspection(s) pending completion for over 3 days:</p>
<ul>
{string.Join("", pendingQis.Take(10).Select(q => $"<li><strong>{q.InspectionNumber ?? q.Id.ToString()}</strong> - Item: {q.ItemName ?? q.ItemId.ToString()} | Type: {q.InspectionType} | Date: {q.InspectionDate:yyyy-MM-dd}</li>"))}
</ul>
<p><em>Please complete sample testing and submit inspection readings in MyERP.</em></p>";

        foreach (var user in qualityUsers)
        {
            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QualityInspectionReminderJob: Failed to send reminder email to {Email}", user.Email);
            }
        }

        _logger.LogInformation("QualityInspectionReminderJob: Sent reminder for {Count} pending inspections for company {CompanyId}",
            pendingQis.Count, args.CompanyId);
    }
}

public class QualityInspectionReminderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
