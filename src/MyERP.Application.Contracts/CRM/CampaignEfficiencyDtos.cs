using System;
using System.Collections.Generic;

namespace MyERP.CRM;

public class CampaignEfficiencyFilterDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CompanyId { get; set; }
    /// <summary>"UtmCampaign" or "CampaignName". Defaults to "UtmCampaign".</summary>
    public string BasedOn { get; set; } = "UtmCampaign";
}

public class CampaignEfficiencyRowDto
{
    public string Campaign { get; set; } = null!;
    public int LeadCount { get; set; }
    public int OppCount { get; set; }
    public int QuotCount { get; set; }
    public int OrderCount { get; set; }
    public decimal OrderValue { get; set; }
    public decimal OppLeadRate { get; set; }
    public decimal QuotLeadRate { get; set; }
    public decimal OrderQuotRate { get; set; }
}

public class CampaignEfficiencyReportDto
{
    public List<CampaignEfficiencyRowDto> Rows { get; set; } = new();
    public int TotalLeads { get; set; }
    public int TotalOpps { get; set; }
    public int TotalQuots { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalOrderValue { get; set; }
}
