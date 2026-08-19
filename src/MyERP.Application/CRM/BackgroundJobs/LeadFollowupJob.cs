using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.CRM.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.CRM.BackgroundJobs;

/// <summary>
/// Background job that notifies sales reps of open leads that have had no activity for more than N days.
/// Per ERPNext: crm.send_lead_followup_email (daily scheduler).
/// </summary>
public class LeadFollowupJob : AsyncBackgroundJob<LeadFollowupJobArgs>, ITransientDependency
{
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<LeadFollowupJob> _logger;

    public LeadFollowupJob(
        IRepository<Lead, Guid> leadRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<LeadFollowupJob> logger)
    {
        _leadRepository = leadRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(LeadFollowupJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var inactivityThresholdDays = args.InactivityThresholdDays > 0 ? args.InactivityThresholdDays : 7;
        var thresholdDate = asOfDate.AddDays(-inactivityThresholdDays);

        _logger.LogInformation("LeadFollowupJob: Checking stale leads for company {CompanyId} before {Threshold}",
            args.CompanyId, thresholdDate);

        var query = await _leadRepository.GetQueryableAsync();
        var staleLeads = query
            .Where(l => l.CompanyId == args.CompanyId &&
                        (l.Status == LeadStatus.New || l.Status == LeadStatus.Open || l.Status == LeadStatus.Interested) &&
                        l.AssignedUserId.HasValue &&
                        (l.LastModificationTime ?? l.CreationTime) <= thresholdDate)
            .ToList();

        if (!staleLeads.Any())
            return;

        var userGroups = staleLeads.GroupBy(l => l.AssignedUserId!.Value).ToList();
        var remindedCount = 0;

        foreach (var group in userGroups)
        {
            var user = await _userRepository.FindAsync(group.Key);
            if (user == null || string.IsNullOrWhiteSpace(user.Email) || !user.IsActive)
                continue;

            var leadsList = group.ToList();
            var subject = $"Action Required: {leadsList.Count} Inactive Leads Need Follow-up";
            var body = $@"<h3>Lead Follow-up Reminder</h3>
<p>You have {leadsList.Count} active lead(s) with no updates for over {inactivityThresholdDays} days:</p>
<ul>
{string.Join("", leadsList.Take(10).Select(l => $"<li><strong>{l.GetFullName()}</strong> ({l.LeadNumber}) - {l.CompanyName ?? "Individual"} | Status: {l.Status}</li>"))}
</ul>
<p><em>Please review and log your follow-up activities in MyERP.</em></p>";

            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
                remindedCount += leadsList.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LeadFollowupJob: Failed to send reminder email to user {UserId}", user.Id);
            }
        }

        _logger.LogInformation("LeadFollowupJob: Reminded for {Count} stale leads across {UserCount} reps for company {CompanyId}",
            remindedCount, userGroups.Count, args.CompanyId);
    }
}

public class LeadFollowupJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public int InactivityThresholdDays { get; set; } = 7;
}
