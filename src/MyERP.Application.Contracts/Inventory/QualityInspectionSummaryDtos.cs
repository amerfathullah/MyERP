using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class QualityInspectionSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public InspectionStatus? Status { get; set; }
    public Guid? ItemId { get; set; }
    public InspectionType? InspectionType { get; set; }
    public Guid? InspectedById { get; set; }
}

public class QualityInspectionSummaryReportDto
{
    public List<QualityInspectionSummaryRowDto> Rows { get; set; } = new();
    public int TotalInspections { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int DraftCount { get; set; }
    public decimal AcceptanceRate { get; set; }
    public List<QualityInspectionStatusSummaryDto> StatusSummaries { get; set; } = new();
    public List<QualityInspectionItemSummaryDto> ItemSummaries { get; set; } = new();
}

public class QualityInspectionSummaryRowDto
{
    public Guid Id { get; set; }
    public string? InspectionNumber { get; set; }
    public DateTime InspectionDate { get; set; }
    public InspectionStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal SampleSize { get; set; }
    public InspectionType InspectionType { get; set; }
    public string InspectionTypeName { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid? InspectedById { get; set; }
    public string InspectedByName { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}

public class QualityInspectionStatusSummaryDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class QualityInspectionItemSummaryDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int TotalInspected { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
}
