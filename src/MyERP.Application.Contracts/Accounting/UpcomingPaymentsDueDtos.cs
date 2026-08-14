using System;
using System.Collections.Generic;

namespace MyERP.Accounting;

public class UpcomingPaymentDueDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime DueDate { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? CurrencyCode { get; set; }
    public int DaysUntilDue { get; set; }
    public string WeekLabel { get; set; } = null!;
    public bool IsOverdue { get; set; }
}

public class UpcomingPaymentsDueReportDto
{
    public decimal TotalDueThisWeek { get; set; }
    public decimal TotalDueNextWeek { get; set; }
    public decimal TotalDueNext30Days { get; set; }
    public decimal TotalOverdue { get; set; }
    public int InvoiceCount { get; set; }
    public int SupplierCount { get; set; }
    public List<UpcomingPaymentDueDto> Invoices { get; set; } = [];
}

public class GetUpcomingPaymentsDueInput
{
    public Guid CompanyId { get; set; }
    public int DaysAhead { get; set; } = 30;
    public Guid? SupplierId { get; set; }
}
