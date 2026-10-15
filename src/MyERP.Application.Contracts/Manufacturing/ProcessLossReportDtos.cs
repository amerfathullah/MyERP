using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class ProcessLossReportRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WorkOrderId { get; set; }
}

public class ProcessLossReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<ProcessLossReportRowDto> Rows { get; set; } = new();
    public decimal TotalProducedQty { get; set; }
    public decimal TotalProcessLossQty { get; set; }
    public decimal TotalProcessLossValue { get; set; }
    public decimal TotalFgValue { get; set; }
    public decimal TotalRmValue { get; set; }
}

public class ProcessLossReportRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public Guid ProductionItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal QtyToManufacture { get; set; }
    public decimal ProducedQty { get; set; }
    public decimal OutputQty { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal TotalProcessLossValue { get; set; }
    public decimal TotalFgValue { get; set; }
    public decimal TotalRmValue { get; set; }
}
