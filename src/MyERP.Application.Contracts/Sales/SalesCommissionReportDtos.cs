using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class SalesCommissionReportDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCommission { get; set; }
    public int InvoiceCount { get; set; }
    public int SalesPersonCount { get; set; }
    public List<SalesPersonCommissionRowDto> Rows { get; set; } = new();
}

public class SalesPersonCommissionRowDto
{
    public Guid SalesPersonId { get; set; }
    public string SalesPersonName { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public decimal TotalAllocatedAmount { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal CommissionRate { get; set; }
}
