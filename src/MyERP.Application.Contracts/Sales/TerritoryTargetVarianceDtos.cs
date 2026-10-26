using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GetTerritoryTargetVarianceInput
{
    public Guid CompanyId { get; set; }

    /// <summary>"Sales Invoice" or "Sales Order"</summary>
    public string DocType { get; set; } = "Sales Invoice";

    /// <summary>"Amount" or "Quantity"</summary>
    public string TargetOn { get; set; } = "Amount";

    public Guid? FiscalYearId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? TerritoryId { get; set; }
    public Guid? ItemGroupId { get; set; }
}

public class TerritoryTargetVarianceRowDto
{
    public Guid TerritoryId { get; set; }
    public string TerritoryName { get; set; } = string.Empty;
    public Guid? ItemGroupId { get; set; }
    public string ItemGroupName { get; set; } = string.Empty;
    public decimal Target { get; set; }
    public decimal Achieved { get; set; }
    public decimal Variance { get; set; }
    public decimal AchievementPercentage { get; set; }
}

public class TerritoryTargetVarianceReportDto
{
    public string Currency { get; set; } = "MYR";
    public string DocType { get; set; } = "Sales Invoice";
    public string TargetOn { get; set; } = "Amount";
    public string? FiscalYearName { get; set; }
    public List<TerritoryTargetVarianceRowDto> Rows { get; set; } = new();
    public decimal TotalTarget { get; set; }
    public decimal TotalAchieved { get; set; }
    public decimal TotalVariance { get; set; }
    public decimal OverallAchievementPercentage { get; set; }
}
