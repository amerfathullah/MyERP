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

public class GetItemWiseSalesHistoryDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? CustomerId { get; set; }
}

public class ItemWiseSalesHistoryReportDto
{
    public List<ItemWiseSalesHistoryRowDto> Rows { get; set; } = new();
    public decimal TotalQty { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalDeliveredQty { get; set; }
    public decimal TotalBilledAmount { get; set; }
}

public class ItemWiseSalesHistoryRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemGroup { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerGroup { get; set; } = string.Empty;
    public decimal DeliveredQuantity { get; set; }
    public decimal BilledAmount { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
}
