using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Purchasing;

public class PurchaseInvoiceTrendsRequestDto
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
    /// Aggregation dimension: "Supplier", "Item", or "ItemGroup".
    /// Defaults to "Supplier".
    /// </summary>
    public string BasedOn { get; set; } = "Supplier";

    /// <summary>
    /// Secondary detail dimension: "Supplier", "Item", or null/empty.
    /// </summary>
    public string? GroupBy { get; set; }

    /// <summary>
    /// When true, includes debit notes / returns.
    /// </summary>
    public bool IncludeReturns { get; set; }
}

public class PurchaseInvoiceTrendsPeriodColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class PurchaseInvoiceTrendsRowDto
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

public class PurchaseInvoiceTrendsChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Datapoints { get; set; } = new();
    public string Currency { get; set; } = "MYR";
}

public class PurchaseInvoiceTrendsReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public string Period { get; set; } = "Monthly";
    public string BasedOn { get; set; } = "Supplier";
    public string? GroupBy { get; set; }
    public List<PurchaseInvoiceTrendsPeriodColumnDto> PeriodColumns { get; set; } = new();
    public List<PurchaseInvoiceTrendsRowDto> Rows { get; set; } = new();
    public PurchaseInvoiceTrendsRowDto TotalRow { get; set; } = new();
    public PurchaseInvoiceTrendsChartDto Chart { get; set; } = new();
}
