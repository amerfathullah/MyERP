using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class SupplierPaymentSummaryReportDto
{
    public List<SupplierPaymentLineDto> Items { get; set; } = new();
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public int SupplierCount { get; set; }
}

public class SupplierPaymentLineDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public int InvoiceCount { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int OverdueCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal PaymentTimeliness { get; set; }
}
