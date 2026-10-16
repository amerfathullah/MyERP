using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class CostOfPoorQualityReportRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? OperationId { get; set; }
    public Guid? WorkstationId { get; set; }
    public Guid? ProductionItemId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class CostOfPoorQualityReportDto
{
    public Guid CompanyId { get; set; }
    public List<CostOfPoorQualityReportRowDto> Rows { get; set; } = new();
    public int TotalJobCards { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal TotalOperatingCost { get; set; }
}

public class CostOfPoorQualityReportRowDto
{
    public Guid JobCardId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public Guid ProductionItemId { get; set; }
    public string ProductionItemCode { get; set; } = null!;
    public string ProductionItemName { get; set; } = null!;
    public Guid OperationId { get; set; }
    public string OperationName { get; set; } = null!;
    public Guid? WorkstationId { get; set; }
    public string? WorkstationName { get; set; }
    public decimal HourRate { get; set; }
    public decimal TotalTimeInMins { get; set; }
    public decimal OperatingCost { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
