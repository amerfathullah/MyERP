using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class PaymentTermsStatusForSalesOrderRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
    public bool? OnlyImmediateUpcomingTerm { get; set; }
}

public class PaymentTermsStatusForSalesOrderReportDto
{
    public Guid CompanyId { get; set; }
    public List<PaymentTermsStatusForSalesOrderRowDto> Rows { get; set; } = new();
    public int TotalSalesOrders { get; set; }
    public int TotalPaymentTerms { get; set; }
    public decimal TotalPaymentAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalOutstandingAmount { get; set; }
    public int CompletedTermsCount { get; set; }
    public int OverdueTermsCount { get; set; }
}

public class PaymentTermsStatusForSalesOrderRowDto
{
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public string PaymentTermName { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Invoices { get; set; } = string.Empty;
    public string Status { get; set; } = null!;
    public string Currency { get; set; } = "MYR";
}
