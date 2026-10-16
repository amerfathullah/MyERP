using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class JobCardSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<Guid>? WorkOrderIds { get; set; }
    public List<Guid>? ProductionItemIds { get; set; }
    public Guid? WorkstationId { get; set; }
    public Guid? OperationId { get; set; }
    public JobCardStatus? Status { get; set; }
}

public class JobCardSummaryDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<JobCardSummaryRowDto> Rows { get; set; } = new();
    public JobCardSummaryChartDto Chart { get; set; } = new();
    public int TotalJobCards { get; set; }
    public decimal TotalCompletedQty { get; set; }
    public decimal TotalTimeInMins { get; set; }
}

public class JobCardSummaryRowDto
{
    public Guid Id { get; set; }
    public DateTime PostingDate { get; set; }
    public string Status { get; set; } = null!;
    public JobCardStatus StatusCode { get; set; }
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public Guid ProductionItemId { get; set; }
    public string ProductionItemCode { get; set; } = null!;
    public string ProductionItemName { get; set; } = null!;
    public Guid? WorkstationId { get; set; }
    public string? WorkstationName { get; set; }
    public Guid? OperationId { get; set; }
    public string? OperationName { get; set; }
    public decimal TotalCompletedQty { get; set; }
    public DateTime? FromTime { get; set; }
    public DateTime? ToTime { get; set; }
    public decimal TotalTimeInMins { get; set; }
}

public class JobCardSummaryChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<JobCardChartDatasetDto> Datasets { get; set; } = new();
}

public class JobCardChartDatasetDto
{
    public string Name { get; set; } = null!;
    public List<int> Values { get; set; } = new();
}
