using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class PurchaseOrderTrendsRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    /// <summary>Monthly, Quarterly, Half-Yearly, Yearly</summary>
    public string Period { get; set; } = "Monthly";

    /// <summary>Supplier, Item, ItemGroup</summary>
    public string BasedOn { get; set; } = "Supplier";

    /// <summary>None, Item, Supplier</summary>
    public string? GroupBy { get; set; }

    public Guid? SupplierId { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemGroup { get; set; }
    public bool IncludeClosedOrders { get; set; } = false;
}

public class PurchaseOrderTrendsPeriodColumnDto
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class PurchaseOrderTrendsRowDto
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

public class PurchaseOrderTrendsChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Datapoints { get; set; } = new();
    public string Currency { get; set; } = "MYR";
}

public class PurchaseOrderTrendsReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public string Period { get; set; } = "Monthly";
    public string BasedOn { get; set; } = "Supplier";
    public string? GroupBy { get; set; }
    public List<PurchaseOrderTrendsPeriodColumnDto> PeriodColumns { get; set; } = new();
    public List<PurchaseOrderTrendsRowDto> Rows { get; set; } = new();
    public PurchaseOrderTrendsRowDto TotalRow { get; set; } = new();
    public PurchaseOrderTrendsChartDto Chart { get; set; } = new();
}
