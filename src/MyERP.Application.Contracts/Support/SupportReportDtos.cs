using System;
using System.Collections.Generic;

namespace MyERP.Support;

public class SupportReportFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>Grouping dimension: "Customer", "Assigned To", "Issue Type", "Issue Priority".</summary>
    public string BasedOn { get; set; } = "Customer";
    /// <summary>Periodicity for Issue Analytics: "Weekly", "Monthly", "Quarterly", "Yearly".</summary>
    public string Range { get; set; } = "Monthly";
    public IssueStatus? Status { get; set; }
    public string? Priority { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? AssignedToId { get; set; }
}

public class IssueSummaryRowDto
{
    public string DimensionKey { get; set; } = string.Empty;
    public string DimensionLabel { get; set; } = string.Empty;
    public int OpenCount { get; set; }
    public int RepliedCount { get; set; }
    public int OnHoldCount { get; set; }
    public int ClosedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalIssues { get; set; }
    public int SlaFailedCount { get; set; }
    public int SlaFulfilledCount { get; set; }
    public int FirstResponseDueCount { get; set; }
    public int ResolutionDueCount { get; set; }
    public int PausedCount { get; set; }
    public decimal AvgFirstResponseTimeHours { get; set; }
    public decimal AvgHoldTimeHours { get; set; }
    public decimal AvgResolutionTimeHours { get; set; }
}

public class IssueSummaryReportDto
{
    public string BasedOn { get; set; } = "Customer";
    public List<IssueSummaryRowDto> Rows { get; set; } = new();
    public int TotalIssues { get; set; }
    public int TotalOpen { get; set; }
    public int TotalClosed { get; set; }
    public int TotalSlaFulfilled { get; set; }
    public int TotalSlaFailed { get; set; }
}

public class IssueAnalyticsRowDto
{
    public string DimensionKey { get; set; } = string.Empty;
    public string DimensionLabel { get; set; } = string.Empty;
    public int[] PeriodCounts { get; set; } = Array.Empty<int>();
    public int Total { get; set; }
}

public class IssueAnalyticsReportDto
{
    public string BasedOn { get; set; } = "Customer";
    public string Range { get; set; } = "Monthly";
    public string[] PeriodLabels { get; set; } = Array.Empty<string>();
    public List<IssueAnalyticsRowDto> Rows { get; set; } = new();
    public int[] PeriodTotals { get; set; } = Array.Empty<int>();
    public int GrandTotal { get; set; }
}

public class FirstResponseTimeRowDto
{
    public DateTime Date { get; set; }
    public int IssueCount { get; set; }
    public decimal AvgFirstResponseTimeHours { get; set; }
}

public class FirstResponseTimeReportDto
{
    public List<FirstResponseTimeRowDto> Rows { get; set; } = new();
    public int TotalIssues { get; set; }
    public decimal OverallAverageHours { get; set; }
}

public class SupportHourDistributionRowDto
{
    public DateTime Date { get; set; }
    public int[] SlotCounts { get; set; } = Array.Empty<int>();
    public int Total { get; set; }
}

public class SupportHourDistributionReportDto
{
    public string[] TimeSlotLabels { get; set; } = Array.Empty<string>();
    public List<SupportHourDistributionRowDto> Rows { get; set; } = new();
    public int[] SlotTotals { get; set; } = Array.Empty<int>();
    public int GrandTotal { get; set; }
}
