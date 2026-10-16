using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Sales;

public class QuotationTrendsRequestDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public Guid? FiscalYearId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Periodic increment: "Monthly", "Quarterly", "HalfYearly", or "Yearly".
    /// Defaults to "Monthly".
    /// </summary>
    public string Period { get; set; } = "Monthly";

    /// <summary>
    /// Aggregation dimension: "Customer", "Item", or "ItemGroup".
    /// Defaults to "Customer".
    /// </summary>
    public string BasedOn { get; set; } = "Customer";

    /// <summary>
    /// Secondary detail dimension: "Customer", "Item", or null/empty.
    /// </summary>
    public string? GroupBy { get; set; }
}

public class QuotationTrendsPeriodColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class QuotationTrendsRowDto
{
    public string EntityId { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? SecondaryInfo { get; set; }
    public string? GroupByValue { get; set; }
    public Dictionary<string, decimal> PeriodQuantities { get; set; } = new();
    public Dictionary<string, decimal> PeriodAmounts { get; set; } = new();
    public decimal TotalQty { get; set; }
    public decimal TotalAmount { get; set; }
}

public class QuotationTrendsChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Datapoints { get; set; } = new();
    public string Currency { get; set; } = "MYR";
}

public class QuotationTrendsReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public string Period { get; set; } = "Monthly";
    public string BasedOn { get; set; } = "Customer";
    public string? GroupBy { get; set; }
    public List<QuotationTrendsPeriodColumnDto> PeriodColumns { get; set; } = new();
    public List<QuotationTrendsRowDto> Rows { get; set; } = new();
    public QuotationTrendsRowDto TotalRow { get; set; } = new();
    public QuotationTrendsChartDto Chart { get; set; } = new();
}
