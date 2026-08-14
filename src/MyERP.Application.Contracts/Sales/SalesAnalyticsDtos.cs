using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public enum AnalyticsGroupBy { Customer = 0, Item = 1, Territory = 2, SalesPerson = 3, ItemGroup = 4 }
public enum AnalyticsPeriodType { Monthly = 0, Quarterly = 1, Yearly = 2 }

public class SalesAnalyticsRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public AnalyticsGroupBy GroupBy { get; set; }
    public AnalyticsPeriodType PeriodType { get; set; }
    public string? ValueField { get; set; }
}

public class SalesAnalyticsReportDto
{
    public List<string> PeriodLabels { get; set; } = new();
    public List<SalesAnalyticsRowDto> Rows { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public List<decimal> PeriodTotals { get; set; } = new();
}

public class SalesAnalyticsRowDto
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public List<decimal> PeriodValues { get; set; } = new();
    public decimal Total { get; set; }
    public decimal Growth { get; set; }
}
