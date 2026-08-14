using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class ItemSalesReportDto
{
    public List<ItemSalesLineDto> Items { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalQty { get; set; }
    public int UniqueItems { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class ItemSalesLineDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal TotalQty { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageRate { get; set; }
    public int InvoiceCount { get; set; }
}
