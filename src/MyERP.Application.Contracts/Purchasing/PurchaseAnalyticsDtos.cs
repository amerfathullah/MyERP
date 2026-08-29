using System;
using System.Collections.Generic;
using MyERP.Sales;

namespace MyERP.Purchasing;

public class PurchaseAnalyticsRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public AnalyticsGroupBy GroupBy { get; set; }
    public AnalyticsPeriodType PeriodType { get; set; }
    public string? ValueField { get; set; }
    public List<string>? EntityIds { get; set; }
}

public class PurchaseAnalyticsReportDto
{
    public List<string> PeriodLabels { get; set; } = new();
    public List<PurchaseAnalyticsRowDto> Rows { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public List<decimal> PeriodTotals { get; set; } = new();
}

public class PurchaseAnalyticsRowDto
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public List<decimal> PeriodValues { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Growth { get; set; }
}
