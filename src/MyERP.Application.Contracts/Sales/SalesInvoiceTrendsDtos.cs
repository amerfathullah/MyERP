using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Sales;

public class SalesInvoiceTrendsRequestDto
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

    /// <summary>
    /// When true, includes credit notes / returns.
    /// </summary>
    public bool IncludeReturns { get; set; }
}

public class SalesInvoiceTrendsPeriodColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class SalesInvoiceTrendsRowDto
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

public class SalesInvoiceTrendsChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Datapoints { get; set; } = new();
    public string Currency { get; set; } = "MYR";
}

public class SalesInvoiceTrendsReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public string Period { get; set; } = "Monthly";
    public string BasedOn { get; set; } = "Customer";
    public string? GroupBy { get; set; }
    public List<SalesInvoiceTrendsPeriodColumnDto> PeriodColumns { get; set; } = new();
    public List<SalesInvoiceTrendsRowDto> Rows { get; set; } = new();
    public SalesInvoiceTrendsRowDto TotalRow { get; set; } = new();
    public SalesInvoiceTrendsChartDto Chart { get; set; } = new();
}
