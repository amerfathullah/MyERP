using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class GrossProfitReportDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitPercentage { get; set; }
    public List<GrossProfitLineDto> Items { get; set; } = new();
}

public class GrossProfitLineDto
{
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    /// <summary>
    /// Item name per ERPNext PR #58631 (commit 467f54162f).
    /// </summary>
    public string? ItemName { get; set; }
    public string? ItemGroup { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal Quantity { get; set; }
    public decimal SellingRate { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitPercentage { get; set; }
}

public class GrossProfitRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>
    /// Grouping dimension: "Invoice" (default), "Item", or "Customer".
    /// </summary>
    public string GroupBy { get; set; } = "Invoice";
    public Guid? CustomerId { get; set; }
    public Guid? ItemId { get; set; }
}
