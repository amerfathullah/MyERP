using System;

namespace MyERP.Sales;

public class SalesRegisterLineDto
{
    public string VoucherType { get; set; } = "Sales Invoice";
    public Guid? InvoiceId { get; set; }
    public Guid? PaymentEntryId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public string? CustomerGroupName { get; set; }
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Outstanding { get; set; }
    public bool IsReturn { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public class ItemWiseSalesRegisterFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CustomerGroupId { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public Guid? ItemGroupId { get; set; }
}

public class ItemWiseSalesRegisterLineDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? CustomerGroupId { get; set; }
    public string? CustomerGroupName { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemGroup { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal StockQty { get; set; }
    public string StockUom { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal Total { get; set; }
    public bool IsReturn { get; set; }
    public string? SalesOrderNumber { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string Currency { get; set; } = "MYR";
}
