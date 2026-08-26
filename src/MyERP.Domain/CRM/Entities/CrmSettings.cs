using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.CRM.Entities;

/// <summary>
/// CRM Settings — global behavior and automation configuration for CRM.
/// Maps to ERPNext crm/doctype/crm_settings.
/// </summary>
public class CrmSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string CampaignNamingBy { get; set; } = "Campaign Name";
    public bool AllowLeadDuplicationBasedOnEmails { get; set; }
    public bool AutoCreationOfContact { get; set; } = true;
    public int CloseOpportunityAfterDays { get; set; } = 15;
    public bool EnableOpportunityCreationFromContactUs { get; set; }
    public int DefaultQuotationValidityDays { get; set; } = 30;
    public bool CarryForwardCommunicationAndComments { get; set; }
    public bool UpdateTimestampOnNewCommunication { get; set; }

    protected CrmSettings() { }

    public CrmSettings(
        Guid id,
        string campaignNamingBy = "Campaign Name",
        bool allowLeadDuplicationBasedOnEmails = false,
        bool autoCreationOfContact = true,
        int closeOpportunityAfterDays = 15,
        bool enableOpportunityCreationFromContactUs = false,
        int defaultQuotationValidityDays = 30,
        bool carryForwardCommunicationAndComments = false,
        bool updateTimestampOnNewCommunication = false,
        Guid? tenantId = null)
        : base(id)
    {
        CampaignNamingBy = campaignNamingBy;
        AllowLeadDuplicationBasedOnEmails = allowLeadDuplicationBasedOnEmails;
        AutoCreationOfContact = autoCreationOfContact;
        CloseOpportunityAfterDays = closeOpportunityAfterDays;
        EnableOpportunityCreationFromContactUs = enableOpportunityCreationFromContactUs;
        DefaultQuotationValidityDays = defaultQuotationValidityDays;
        CarryForwardCommunicationAndComments = carryForwardCommunicationAndComments;
        UpdateTimestampOnNewCommunication = updateTimestampOnNewCommunication;
        TenantId = tenantId;
    }
}
