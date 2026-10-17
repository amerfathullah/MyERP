using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class DowntimeAnalysisRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? WorkstationId { get; set; }
    public Guid? OperatorId { get; set; }
}

public class DowntimeAnalysisReportDto
{
    public List<DowntimeAnalysisRowDto> Rows { get; set; } = new();
    public int TotalEntries { get; set; }
    public decimal TotalDowntimeHours { get; set; }
    public decimal TotalDowntimeMinutes { get; set; }
    public List<DowntimeSummaryItemDto> WorkstationSummaries { get; set; } = new();
    public List<DowntimeSummaryItemDto> ReasonSummaries { get; set; } = new();
}

public class DowntimeAnalysisRowDto
{
    public Guid Id { get; set; }
    public Guid WorkstationId { get; set; }
    public string WorkstationName { get; set; } = string.Empty;
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal DowntimeMinutes { get; set; }
    public decimal DowntimeHours { get; set; }
    public string StopReason { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}

public class DowntimeSummaryItemDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public int Count { get; set; }
}
