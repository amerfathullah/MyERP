using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Support.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.Support.BackgroundJobs;

/// <summary>
/// Background job that monitors active support tickets for SLA response and resolution breaches.
/// Per ERPNext: support.doctype.issue.issue.set_service_level_agreement_variance (hourly/daily scheduler).
/// </summary>
public class SupportSlaBreachJob : AsyncBackgroundJob<SupportSlaBreachJobArgs>, ITransientDependency
{
    private readonly IRepository<Issue, Guid> _issueRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SupportSlaBreachJob> _logger;

    public SupportSlaBreachJob(
        IRepository<Issue, Guid> issueRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<SupportSlaBreachJob> logger)
    {
        _issueRepository = issueRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SupportSlaBreachJobArgs args)
    {
        _logger.LogInformation("SupportSlaBreachJob: Monitoring SLA compliance for company {CompanyId}",
            args.CompanyId);

        var query = await _issueRepository.GetQueryableAsync();
        var openIssues = query
            .Where(i => i.CompanyId == args.CompanyId &&
                        (i.Status == IssueStatus.Open || i.Status == IssueStatus.Replied) &&
                        (i.FirstResponseTime.HasValue || i.ResolutionTime.HasValue))
            .ToList();

        if (!openIssues.Any())
            return;

        var newlyBreached = 0;
        var now = DateTime.UtcNow;

        foreach (var issue in openIssues)
        {
            var isBreached = false;

            // 1. Check First Response SLA
            if (issue.FirstResponseTime.HasValue && !issue.FirstRespondedOn.HasValue)
            {
                var elapsedHours = (decimal)(now - issue.OpeningDate).TotalHours;
                if (elapsedHours > issue.FirstResponseTime.Value)
                {
                    isBreached = true;
                }
            }

            // 2. Check Resolution SLA
            if (issue.ResolutionTime.HasValue)
            {
                var elapsedHours = (decimal)(now - issue.OpeningDate).TotalHours - issue.TotalHoldTime;
                if (elapsedHours > issue.ResolutionTime.Value)
                {
                    isBreached = true;
                }
            }

            if (isBreached && !issue.IsSlaBreach)
            {
                issue.IsSlaBreach = true;
                await _issueRepository.UpdateAsync(issue);
                newlyBreached++;
            }
        }

        if (newlyBreached > 0)
        {
            _logger.LogWarning("SupportSlaBreachJob: Flagged {Count} newly breached support tickets for company {CompanyId}",
                newlyBreached, args.CompanyId);

            var usersQuery = await _userRepository.GetQueryableAsync();
            var supportManagers = usersQuery
                .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
                .Take(5)
                .ToList();

            var subject = $"[SLA BREACH ALERT] {newlyBreached} Support Tickets Have Breached SLA Targets";
            var body = $@"<h3>Support Ticket SLA Breach Notice</h3>
<p>There are {newlyBreached} active support ticket(s) that have exceeded their target first-response or resolution SLA window.</p>
<p><em>Please review escalated support tickets in MyERP immediately.</em></p>";

            foreach (var user in supportManagers)
            {
                try
                {
                    await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SupportSlaBreachJob: Failed to send SLA alert email to {Email}", user.Email);
                }
            }
        }
    }
}

public class SupportSlaBreachJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
