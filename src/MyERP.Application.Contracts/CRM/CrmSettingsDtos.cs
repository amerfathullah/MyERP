using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.CRM;

public class CrmSettingsDto : FullAuditedEntityDto<Guid>
{
    public string CampaignNamingBy { get; set; } = "Campaign Name";
    public bool AllowLeadDuplicationBasedOnEmails { get; set; }
    public bool AutoCreationOfContact { get; set; }
    public int CloseOpportunityAfterDays { get; set; }
    public bool EnableOpportunityCreationFromContactUs { get; set; }
    public int DefaultQuotationValidityDays { get; set; }
    public bool CarryForwardCommunicationAndComments { get; set; }
    public bool UpdateTimestampOnNewCommunication { get; set; }
}

public class UpdateCrmSettingsDto
{
    [StringLength(CrmSettingsConsts.MaxCampaignNamingLength)]
    public string CampaignNamingBy { get; set; } = "Campaign Name";

    public bool AllowLeadDuplicationBasedOnEmails { get; set; }

    public bool AutoCreationOfContact { get; set; } = true;

    public int CloseOpportunityAfterDays { get; set; } = 15;

    public bool EnableOpportunityCreationFromContactUs { get; set; }

    public int DefaultQuotationValidityDays { get; set; } = 30;

    public bool CarryForwardCommunicationAndComments { get; set; }

    public bool UpdateTimestampOnNewCommunication { get; set; }
}
