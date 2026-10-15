using System;
using System.Collections.Generic;

namespace MyERP.Sales;

public class PosRegisterRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? PosProfileId { get; set; }
    public Guid? CashierId { get; set; }
    public Guid? CustomerId { get; set; }
    public bool? IsReturn { get; set; }
    public string? ModeOfPayment { get; set; }
    /// <summary>
    /// Group by: "POS Profile", "Cashier", "Customer", "Payment Method", or null/empty for flat list.
    /// </summary>
    public string? GroupBy { get; set; }
}

public class PosRegisterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<PosRegisterRowDto> Rows { get; set; } = new();
    public decimal TotalGrandTotal { get; set; }
    public decimal TotalPaidAmount { get; set; }
}

public class PosRegisterRowDto
{
    public DateTime? PostingDate { get; set; }
    public string? InvoiceType { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? PosProfileId { get; set; }
    public string? PosProfileName { get; set; }
    public Guid? CashierId { get; set; }
    public string? CashierName { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string? ModeOfPayment { get; set; }
    public bool IsReturn { get; set; }
    public bool IsSubtotal { get; set; }
    public string? GroupKey { get; set; }
}
