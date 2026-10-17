using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class StockAnalyticsRequestDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Periodic frequency: "Monthly", "Quarterly", "HalfYearly", or "Yearly".
    /// Defaults to "Monthly".
    /// </summary>
    public string Range { get; set; } = "Monthly";

    /// <summary>
    /// Aggregation metric: "Quantity" or "Value".
    /// Defaults to "Quantity".
    /// </summary>
    public string ValueOrQuantity { get; set; } = "Quantity";

    public Guid? WarehouseId { get; set; }

    public Guid? ItemGroupId { get; set; }

    public string? SearchTerm { get; set; }
}

public class StockAnalyticsPeriodColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class StockAnalyticsRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemGroup { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Uom { get; set; }
    public decimal OpeningBalance { get; set; }
    public Dictionary<string, decimal> PeriodBalances { get; set; } = new();
    public decimal ClosingBalance { get; set; }
}

public class StockAnalyticsChartDto
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Datapoints { get; set; } = new();
    public string Unit { get; set; } = "Qty";
}

public class StockAnalyticsReportDto
{
    public string CompanyCurrency { get; set; } = "MYR";
    public string Range { get; set; } = "Monthly";
    public string ValueOrQuantity { get; set; } = "Quantity";
    public List<StockAnalyticsPeriodColumnDto> PeriodColumns { get; set; } = new();
    public List<StockAnalyticsRowDto> Rows { get; set; } = new();
    public StockAnalyticsRowDto TotalRow { get; set; } = new();
    public StockAnalyticsChartDto Chart { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalClosingQuantity { get; set; }
    public decimal TotalClosingValue { get; set; }
}
