using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class InactiveSalesItemsFilterDto
{
    public Guid? CompanyId { get; set; }
    public string DocType { get; set; } = "Sales Order";
    public int DaysSinceLastOrder { get; set; } = 30;
    public Guid? ItemGroupId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? TerritoryId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class InactiveSalesItemDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? ItemGroup { get; set; }
    public string? Territory { get; set; }
    public string? CustomerName { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public decimal? Quantity { get; set; }
    public int? DaysSinceLastOrder { get; set; }
}

public class InactiveSalesItemsReportDto
{
    public List<InactiveSalesItemDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public string DocType { get; set; } = "Sales Order";
    public int DaysSinceLastOrderThreshold { get; set; }
}
