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
    public int ClosedOpportunities { get; set; }

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

// =========================================================================
// Opportunity Summary by Sales Stage (ERPNext PR #59908 / commit f4feb24f70)
// =========================================================================

public static class OpportunitySummaryConsts
{
    /// <summary>
    /// Internal key for opportunities without a sales stage, kept distinct from any real stage name (even if named "Not Set").
    /// Per ERPNext PR #59908 / commit f4feb24f70.
    /// </summary>
    public const string NoSalesStageKey = "__no_sales_stage";
    public const string NotSetLabel = "Not Set";
}

public class GetOpportunitySummaryBySalesStageRequestDto
{
    public Guid? CompanyId { get; set; }
    /// <summary>"Opportunity Owner", "Source", "Opportunity Type". Default: "Opportunity Type"</summary>
    public string BasedOn { get; set; } = "Opportunity Type";
    /// <summary>"Number" or "Amount". Default: "Number"</summary>
    public string DataBasedOn { get; set; } = "Number";
    public OpportunityType? OpportunityType { get; set; }
    public string? OpportunitySource { get; set; }
    public List<OpportunityStatus>? Statuses { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class OpportunitySummaryColumnDto
{
    public string Label { get; set; } = null!;
    public string Fieldname { get; set; } = null!;
    public string Fieldtype { get; set; } = "Int"; // "Int" or "Currency" or "Data"
    public int Width { get; set; } = 150;
}

public class OpportunitySummaryRowDto
{
    public string Key { get; set; } = null!; // Dimension value (e.g. Type, Owner, Source)
    public Dictionary<string, decimal> StageValues { get; set; } = new();
}

public class OpportunitySummaryChartDatasetDto
{
    public string Name { get; set; } = null!;
    public List<decimal> Values { get; set; } = new();
}

public class OpportunitySummaryChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<OpportunitySummaryChartDatasetDto> Datasets { get; set; } = new();
    public string Type { get; set; } = "line";
}

public class OpportunitySummaryBySalesStageDto
{
    public List<OpportunitySummaryColumnDto> Columns { get; set; } = new();
    public List<OpportunitySummaryRowDto> Data { get; set; } = new();
    public OpportunitySummaryChartDto Chart { get; set; } = new();
}

// =========================================================================
// First Response Time for Opportunity (ERPNext PR #59922 / commit ab8a279282)
// =========================================================================

public class GetFirstResponseTimeReportRequestDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class FirstResponseTimeRowDto
{
    public DateTime CreationDate { get; set; }
    /// <summary>Average response time in seconds for opportunities created on this date.</summary>
    public double AvgResponseTimeSeconds { get; set; }
    /// <summary>Formatted duration (e.g., "1h 30m" or "45s").</summary>
    public string FormattedResponseTime { get; set; } = null!;
}

public class FirstResponseTimeReportDto
{
    public List<FirstResponseTimeRowDto> Data { get; set; } = new();
    public OpportunitySummaryChartDto Chart { get; set; } = new();
}

// =========================================================================
// Lost Opportunity Report (ERPNext PR #59918 / commit c541ac3f98)
// =========================================================================

public class GetLostOpportunityReportRequestDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Territory { get; set; }
    public string? LostReason { get; set; }
    public string? OpportunityFrom { get; set; } // "Lead" or "Customer"
    public Guid? PartyId { get; set; }
}

public class LostOpportunityRowDto
{
    public Guid Id { get; set; }
    public string OpportunityNumber { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string OpportunityFrom { get; set; } = null!;
    public Guid? PartyId { get; set; }
    public string? PartyName { get; set; }
    public string? CustomerName { get; set; }
    public string? OpportunityType { get; set; }
    /// <summary>
    /// All lost reasons associated with this opportunity.
    /// When filtering by a single lost reason, all reasons on the opportunity remain listed here.
    /// Per ERPNext PR #59918 / commit c541ac3f98.
    /// </summary>
    public string LostReasons { get; set; } = string.Empty;
    public string? SalesStage { get; set; }
    public string? Territory { get; set; }
    public decimal OpportunityAmount { get; set; }
    public DateTime CreationTime { get; set; }
}

public class LostOpportunityReportDto
{
    public List<LostOpportunityRowDto> Data { get; set; } = new();
}
