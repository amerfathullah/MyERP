using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class WorkOrderSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string BasedOn { get; set; } = "Creation Date"; // "Planned Date", "Actual Date", "Creation Date"
    public List<Guid>? WorkOrderIds { get; set; }
    public List<Guid>? ProductionItemIds { get; set; }
    public List<Guid>? SalesOrderIds { get; set; }
    public WorkOrderStatus? Status { get; set; }
    public int Age { get; set; } = 0;
    public string ChartsBasedOn { get; set; } = "Quantity"; // "Status", "Age", "Quantity"
}

public class WorkOrderSummaryDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<WorkOrderSummaryRowDto> Rows { get; set; } = new();
    public WorkOrderSummaryChartDto Chart { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public decimal TotalProduceQty { get; set; }
    public decimal TotalProducedQty { get; set; }
}

public class WorkOrderSummaryRowDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public WorkOrderStatus StatusCode { get; set; }
    public Guid ProductionItemId { get; set; }
    public string ProductionItemCode { get; set; } = null!;
    public string ProductionItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal ProducedQty { get; set; }
    public string? ActualFinishedGoods { get; set; }
    public Guid? SalesOrderId { get; set; }
    public string? SalesOrderNumber { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public int Age { get; set; }
    public decimal? LeadTime { get; set; }
}

public class WorkOrderSummaryChartDto
{
    public string ChartType { get; set; } = "donut"; // "donut" or "bar"
    public List<string> Labels { get; set; } = new();
    public List<WorkOrderChartDatasetDto> Datasets { get; set; } = new();
}

public class WorkOrderChartDatasetDto
{
    public string Name { get; set; } = null!;
    public List<decimal> Values { get; set; } = new();
}
