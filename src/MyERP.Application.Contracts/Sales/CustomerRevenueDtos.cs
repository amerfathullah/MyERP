using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class CustomerRevenueReportDto
{
    public List<CustomerRevenueLineDto> Items { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int CustomerCount { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class CustomerRevenueLineDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
}
