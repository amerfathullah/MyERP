using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Email Campaign — schedules a Campaign's email touchpoints against one Lead or Contact.
/// EndDate is computed from the Campaign's furthest-out email schedule; Status is derived
/// from the current date relative to Start/EndDate, or forced to Unsubscribed.
/// Maps to ERPNext crm/doctype/email_campaign.
/// </summary>
public class EmailCampaign : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CampaignId { get; set; }
    public EmailCampaignFor EmailCampaignFor { get; set; }

    /// <summary>The Lead or Contact this campaign is running against.</summary>
    public Guid RecipientId { get; set; }

    public Guid? SenderId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; private set; }

    public EmailCampaignStatus Status { get; private set; } = EmailCampaignStatus.Scheduled;

    protected EmailCampaign() { }

    public EmailCampaign(Guid id, Guid campaignId, EmailCampaignFor emailCampaignFor, Guid recipientId,
        DateTime startDate, int maxSendAfterDays, Guid? tenantId = null)
        : base(id)
    {
        if (startDate.Date < DateTime.UtcNow.Date)
            throw new BusinessException(MyERPDomainErrorCodes.EmailCampaignStartDateInPast)
                .WithData("startDate", startDate.ToString("yyyy-MM-dd"));

        CampaignId = campaignId;
        EmailCampaignFor = emailCampaignFor;
        RecipientId = Check.NotDefaultOrNull<Guid>(recipientId, nameof(recipientId));
        StartDate = startDate;
        EndDate = startDate.AddDays(maxSendAfterDays);
        TenantId = tenantId;
    }

    /// <summary>Recomputes Status from the current date. Called by the recurring scheduler job.</summary>
    public void UpdateStatus(DateTime asOfDate)
    {
        if (Status == EmailCampaignStatus.Unsubscribed)
            return;

        if (asOfDate.Date < StartDate.Date)
            Status = EmailCampaignStatus.Scheduled;
        else if (asOfDate.Date > EndDate.Date)
            Status = EmailCampaignStatus.Completed;
        else
            Status = EmailCampaignStatus.InProgress;
    }

    public void Unsubscribe()
    {
        Status = EmailCampaignStatus.Unsubscribed;
    }
}
