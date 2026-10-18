using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class ExponentialSmoothingForecastingFilterDto
{
    public Guid? CompanyId { get; set; }
    public string BasedOnDocument { get; set; } = "Sales Order"; // "Sales Order", "Delivery Note", "Sales Invoice"
    public string BasedOnField { get; set; } = "Qty"; // "Qty", "Amount"
    public string Periodicity { get; set; } = "Monthly"; // "Monthly", "Quarterly", "Half-Yearly", "Yearly"
    public DateTime FromDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime ToDate { get; set; } = DateTime.UtcNow.Date.AddMonths(3);
    public int NoOfYears { get; set; } = 1;
    public decimal SmoothingConstant { get; set; } = 0.5m; // alpha 0.01 to 1.0
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? SearchTerm { get; set; }
}

public class ForecastingPeriodDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool IsForecast { get; set; }
}

public class ExponentialSmoothingForecastingRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal AverageHistoricalDemand { get; set; }
    public Dictionary<string, decimal> PeriodValues { get; set; } = new();
    public decimal TotalDemand { get; set; }
    public decimal TotalForecast { get; set; }
}

public class ExponentialSmoothingForecastingDto
{
    public List<ForecastingPeriodDto> Periods { get; set; } = new();
    public decimal TotalDemandPastData { get; set; }
    public decimal TotalForecastPastData { get; set; }
    public decimal TotalForecastFutureData { get; set; }
    public List<ExponentialSmoothingForecastingRowDto> Rows { get; set; } = new();
    public ExponentialSmoothingForecastingRowDto? TotalRow { get; set; }
}
