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
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public string? CustomerName { get; set; }
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
}
