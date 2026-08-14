using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Accounting;

public class GetOutstandingForBatchDto
{
    public Guid CompanyId { get; set; }
    public string PartyType { get; set; } = "Supplier";
    public Guid PartyId { get; set; }
}

public class BatchPaymentInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string InvoiceType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Outstanding { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
}

public class CreateBatchPaymentDto
{
    [Required]
    public Guid CompanyId { get; set; }

    public PaymentType PaymentType { get; set; } = PaymentType.Pay;
    public string PartyType { get; set; } = "Supplier";

    [Required]
    public Guid PaidFromAccountId { get; set; }

    [Required]
    public Guid PaidToAccountId { get; set; }

    public Guid? ModeOfPaymentId { get; set; }
    public DateTime? PostingDate { get; set; }
    public bool GroupByParty { get; set; } = true;

    [Required]
    public List<BatchPaymentItemDto> Items { get; set; } = new();
}

public class BatchPaymentItemDto
{
    public Guid PartyId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceType { get; set; } = "PurchaseInvoice";
    public decimal TotalAmount { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
}

public class BatchPaymentResultDto
{
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<Guid> CreatedPaymentEntryIds { get; set; } = new();
}

public class ValidatePayableInvoicesDto
{
    [Required]
    public List<Guid> InvoiceIds { get; set; } = new();
}

public class PayableInvoicePartitionDto
{
    public List<PayableInvoiceInfoDto> Payable { get; set; } = new();
    public List<ExcludedInvoiceDto> Excluded { get; set; } = new();
    public decimal TotalPayable { get; set; }
    public int PaymentEntryCount { get; set; }
}

public class PayableInvoiceInfoDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Guid PartyAccountId { get; set; }
    public decimal Outstanding { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
}

public class ExcludedInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Reason { get; set; } = null!;
}
