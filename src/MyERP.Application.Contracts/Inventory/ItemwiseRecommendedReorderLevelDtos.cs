using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class ItemwiseRecommendedReorderLevelFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? Brand { get; set; }
    public string? SearchText { get; set; }
}

public class ItemwiseRecommendedReorderLevelRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroup { get; set; }
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public decimal SafetyStock { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal ConsumedQty { get; set; }
    public decimal DeliveredQty { get; set; }
    public decimal TotalOutgoing { get; set; }
    public decimal AvgDailyOutgoing { get; set; }
    public decimal CurrentReorderLevel { get; set; }
    public decimal RecommendedReorderLevel { get; set; }
    public decimal Variance { get; set; }
    public string Status { get; set; } = "Adequate"; // "UnderConfigured", "OverConfigured", "Adequate"
}

public class ItemwiseRecommendedReorderLevelReportDto
{
    public List<ItemwiseRecommendedReorderLevelRowDto> Rows { get; set; } = new();
    public int TotalItems { get; set; }
    public int UnderConfiguredCount { get; set; }
    public int OverConfiguredCount { get; set; }
    public decimal TotalAvgDailyOutgoing { get; set; }
}
