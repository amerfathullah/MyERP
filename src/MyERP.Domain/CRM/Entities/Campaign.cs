using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// Campaign — a named marketing campaign with a sequence of email touchpoints
/// (CampaignEmailSchedule) that Email Campaigns are scheduled against.
/// Maps to ERPNext crm/doctype/campaign.
/// </summary>
public class Campaign : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string CampaignName { get; set; } = null!;
    public string? Description { get; set; }

    private readonly List<CampaignEmailSchedule> _emailSchedules = new();
    public IReadOnlyList<CampaignEmailSchedule> EmailSchedules => _emailSchedules.AsReadOnly();

    protected Campaign() { }

    public Campaign(Guid id, string campaignName, Guid? tenantId = null) : base(id)
    {
        CampaignName = Check.NotNullOrWhiteSpace(campaignName, nameof(campaignName), CampaignConsts.MaxCampaignNameLength);
        TenantId = tenantId;
    }

    public void AddEmailSchedule(CampaignEmailSchedule schedule)
    {
        _emailSchedules.Add(schedule);
    }

    /// <summary>Days from an Email Campaign's start date until the last scheduled touchpoint fires.</summary>
    public int MaxSendAfterDays()
    {
        var max = 0;
        foreach (var s in _emailSchedules)
        {
            if (s.SendAfterDays > max) max = s.SendAfterDays;
        }
        return max;
    }
}

/// <summary>One templated email touchpoint within a Campaign, fired N days after the campaign starts.</summary>
public class CampaignEmailSchedule : Entity<Guid>
{
    public Guid CampaignId { get; set; }
    public Guid EmailTemplateId { get; set; }
    public int SendAfterDays { get; set; }

    protected CampaignEmailSchedule() { }

    public CampaignEmailSchedule(Guid id, Guid campaignId, Guid emailTemplateId, int sendAfterDays)
        : base(id)
    {
        CampaignId = campaignId;
        EmailTemplateId = emailTemplateId;
        SendAfterDays = sendAfterDays;
    }
}
