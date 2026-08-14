using System;
using System.Collections.Generic;

namespace MyERP.CRM;

public class SalesPipelineDashboardDto
{
    // Leads
    public int TotalLeads { get; set; }
    public int ActiveLeads { get; set; }
    public int QualifiedLeads { get; set; }
    public int LostLeads { get; set; }

    // Opportunities
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public decimal OpenOpportunitiesAmount { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int WonOpportunities { get; set; }
    public decimal WonAmount { get; set; }
    public int LostOpportunities { get; set; }

    // Stage breakdown
    public List<PipelineStageDto> StageBreakdown { get; set; } = new();

    // Quotations
    public int TotalQuotations { get; set; }
    public int OpenQuotations { get; set; }
    public decimal OpenQuotationsAmount { get; set; }
    public int ConvertedQuotations { get; set; }

    // Orders (result)
    public int OrdersThisMonth { get; set; }
    public decimal OrdersThisMonthAmount { get; set; }

    // Conversion rates (%)
    public decimal LeadToOpportunityRate { get; set; }
    public decimal OpportunityToQuotationRate { get; set; }
    public decimal QuotationToOrderRate { get; set; }
}

public class PipelineStageDto
{
    public string StageName { get; set; } = null!;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal WeightedAmount { get; set; }
    public int AvgProbability { get; set; }
}

public class PipelineOpportunityDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string SalesStage { get; set; } = null!;
    public decimal Amount { get; set; }
    public int Probability { get; set; }
    public decimal WeightedAmount { get; set; }
    public DateTime? ExpectedClosingDate { get; set; }
    public string? ContactName { get; set; }
    public int DaysOpen { get; set; }
}
