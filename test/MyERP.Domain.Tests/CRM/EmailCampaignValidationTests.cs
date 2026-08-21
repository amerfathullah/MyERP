using System;
using MyERP.CRM;
using MyERP.CRM.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.CrmTests;

/// <summary>
/// Unit tests for EmailCampaign lifecycle and validation rules (Gotchas #2227, #2228, #2229):
/// 1. StartDate in past throws validation error
/// 2. EndDate is computed from StartDate + maxSendAfterDays
/// 3. Status transitions correctly across Scheduled -> InProgress -> Completed -> Unsubscribed
/// </summary>
public class EmailCampaignValidationTests
{
    private readonly Guid _campaignId = Guid.NewGuid();
    private readonly Guid _leadId = Guid.NewGuid();

    [Fact]
    public void EmailCampaign_StartDateInPast_ThrowsBusinessException()
    {
        var pastDate = DateTime.UtcNow.AddDays(-2);

        var ex = Assert.Throws<BusinessException>(() => new EmailCampaign(
            Guid.NewGuid(),
            _campaignId,
            EmailCampaignFor.Lead,
            _leadId,
            pastDate,
            14
        ));

        Assert.Equal(MyERPDomainErrorCodes.EmailCampaignStartDateInPast, ex.Code);
    }

    [Fact]
    public void EmailCampaign_ComputesEndDate_FromMaxSendAfterDays()
    {
        var startDate = DateTime.UtcNow.AddDays(1).Date;
        var campaign = new EmailCampaign(
            Guid.NewGuid(),
            _campaignId,
            EmailCampaignFor.Lead,
            _leadId,
            startDate,
            21
        );

        Assert.Equal(startDate.AddDays(21), campaign.EndDate);
    }

    [Fact]
    public void EmailCampaign_UpdateStatus_TransitionsAcrossDates()
    {
        var startDate = DateTime.UtcNow.AddDays(2).Date;
        var campaign = new EmailCampaign(
            Guid.NewGuid(),
            _campaignId,
            EmailCampaignFor.Lead,
            _leadId,
            startDate,
            10
        );

        // Before start
        campaign.UpdateStatus(startDate.AddDays(-1));
        Assert.Equal(EmailCampaignStatus.Scheduled, campaign.Status);

        // In progress
        campaign.UpdateStatus(startDate.AddDays(3));
        Assert.Equal(EmailCampaignStatus.InProgress, campaign.Status);

        // Completed
        campaign.UpdateStatus(startDate.AddDays(11));
        Assert.Equal(EmailCampaignStatus.Completed, campaign.Status);

        // Unsubscribed sticks
        campaign.Unsubscribe();
        campaign.UpdateStatus(startDate.AddDays(3));
        Assert.Equal(EmailCampaignStatus.Unsubscribed, campaign.Status);
    }
}
