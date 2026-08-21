using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Accounting;

public class ReconcilePaymentDto
{
    [Required]
    public string PartyType { get; set; } = null!; // "Customer" or "Supplier"

    [Required]
    public Guid PartyId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public List<ReconcileAllocationDto> Allocations { get; set; } = new();
}

public class ReconcileAllocationDto
{
    [Required]
    public Guid PaymentVoucherId { get; set; }

    [Required]
    public string PaymentVoucherType { get; set; } = "PaymentEntry";

    [Required]
    public Guid InvoiceVoucherId { get; set; }

    [Required]
    public string InvoiceVoucherType { get; set; } = "SalesInvoice";

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AllocatedAmount { get; set; }
}

public class UnreconcileDto
{
    [Required]
    public string PaymentVoucherType { get; set; } = null!;

    [Required]
    public Guid PaymentVoucherId { get; set; }

    [Required]
    public string InvoiceVoucherType { get; set; } = null!;

    [Required]
    public Guid InvoiceVoucherId { get; set; }
}

public class OutstandingInvoiceDto
{
    public Guid VoucherId { get; set; }
    public string VoucherType { get; set; } = null!;
    public decimal Outstanding { get; set; }
}

public class UnreconciledPaymentDto
{
    public Guid VoucherId { get; set; }
    public string VoucherType { get; set; } = null!;
    public string? DocumentNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
}
