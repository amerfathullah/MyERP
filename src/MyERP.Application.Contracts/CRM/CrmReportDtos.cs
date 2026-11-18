using System;
using System.Collections.Generic;

namespace MyERP.CRM;

public class CrmReportDateFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class LeadDetailsFilterDto : CrmReportDateFilterDto
{
    public LeadStatus? Status { get; set; }
    public LeadSource? Source { get; set; }
    public string? Country { get; set; }
    public Guid? AssignedUserId { get; set; }
}

public class LeadDetailsRowDto
{
    public Guid LeadId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? UtmSource { get; set; }
    public string? Email { get; set; }
    public string? MobileNo { get; set; }
    public string? Phone { get; set; }
    public string? LeadOwner { get; set; }
    public string? OrganizationName { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Industry { get; set; }
    public DateTime CreationTime { get; set; }
}

public class LeadDetailsReportDto
{
    public List<LeadDetailsRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
}

public class LeadConversionTimeRowDto
{
    public Guid LeadId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Interactions { get; set; }
    public decimal DurationInDays { get; set; }
    public int SupportTickets { get; set; }
    public DateTime LeadCreatedOn { get; set; }
    public DateTime ConvertedOn { get; set; }
}

public class LeadConversionTimeReportDto
{
    public List<LeadConversionTimeRowDto> Rows { get; set; } = new();
    public int TotalConverted { get; set; }
    public decimal AvgInteractions { get; set; }
    public decimal AvgDurationInDays { get; set; }
    public decimal AvgSupportTickets { get; set; }
}

public class ProspectsEngagedFilterDto
{
    public Guid CompanyId { get; set; }
    public int LeadAgeDays { get; set; } = 60;
    public int MinInteractions { get; set; } = 1;
    public Guid? LeadId { get; set; }
}

public class ProspectsEngagedRowDto
{
    public Guid LeadId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string LeadName { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public string ReferenceName { get; set; } = string.Empty;
    public int InteractionCount { get; set; }
    public string LastCommunication { get; set; } = string.Empty;
    public DateTime LastCommunicationDate { get; set; }
}

public class ProspectsEngagedReportDto
{
    public List<ProspectsEngagedRowDto> Rows { get; set; } = new();
    public int TotalCount { get; set; }
}

public class SalesPipelineAnalyticsFilterDto : CrmReportDateFilterDto
{
    /// <summary>"Sales Stage" or "Owner".</summary>
    public string PipelineBy { get; set; } = "Sales Stage";
    /// <summary>"Number" or "Amount".</summary>
    public string BasedOn { get; set; } = "Amount";
    /// <summary>"Monthly" or "Quarterly".</summary>
    public string Range { get; set; } = "Monthly";
    public OpportunityType? OpportunityType { get; set; }
    public OpportunityStatus? Status { get; set; }
}

public class SalesPipelineAnalyticsRowDto
{
    public string GroupKey { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public decimal[] PeriodValues { get; set; } = Array.Empty<decimal>();
    public decimal Total { get; set; }
}

public class SalesPipelineAnalyticsReportDto
{
    public string PipelineBy { get; set; } = "Sales Stage";
    public string BasedOn { get; set; } = "Amount";
    public string Range { get; set; } = "Monthly";
    public string[] PeriodLabels { get; set; } = Array.Empty<string>();
    public List<SalesPipelineAnalyticsRowDto> Rows { get; set; } = new();
    public decimal[] PeriodTotals { get; set; } = Array.Empty<decimal>();
    public decimal GrandTotal { get; set; }
}
