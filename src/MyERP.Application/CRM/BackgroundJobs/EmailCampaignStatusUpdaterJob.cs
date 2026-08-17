using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.CRM.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM.BackgroundJobs;

/// <summary>
/// Recomputes EmailCampaign.Status for all non-terminal campaigns against today's date.
/// ERPNext equivalent: crm/doctype/email_campaign/email_campaign.py set_email_campaign_status
/// (daily scheduled). Enqueued the same way as MyERP.Assets.BackgroundJobs.DepreciationSchedulerJob —
/// no recurring-cron infrastructure exists yet in this codebase for either job.
/// </summary>
public class EmailCampaignStatusUpdaterJob : AsyncBackgroundJob<EmailCampaignStatusUpdaterArgs>, ITransientDependency
{
    private readonly IRepository<EmailCampaign, Guid> _repository;
    private readonly ILogger<EmailCampaignStatusUpdaterJob> _logger;

    public EmailCampaignStatusUpdaterJob(IRepository<EmailCampaign, Guid> repository, ILogger<EmailCampaignStatusUpdaterJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(EmailCampaignStatusUpdaterArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;

        var query = await _repository.GetQueryableAsync();
        var campaigns = query
            .Where(e => e.Status != EmailCampaignStatus.Completed && e.Status != EmailCampaignStatus.Unsubscribed)
            .ToList();

        var updated = 0;
        foreach (var campaign in campaigns)
        {
            var before = campaign.Status;
            campaign.UpdateStatus(asOfDate);
            if (campaign.Status != before)
            {
                await _repository.UpdateAsync(campaign);
                updated++;
            }
        }

        _logger.LogInformation("EmailCampaignStatusUpdaterJob updated {Count} of {Total} campaigns for {Date}", updated, campaigns.Count, asOfDate);
    }
}

public class EmailCampaignStatusUpdaterArgs
{
    public DateTime? AsOfDate { get; set; }
}
