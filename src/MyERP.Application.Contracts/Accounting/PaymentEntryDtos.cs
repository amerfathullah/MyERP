using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class PaymentEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? PaymentNumber { get; set; }
    public string PaymentType { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public string? ModeOfPayment { get; set; }
    public decimal PaidAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ReferenceNumber { get; set; }
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    public string? PartyName { get; set; }
}

public class CreatePaymentEntryDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public PaymentType PaymentType { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    [Required][Range(0.01, double.MaxValue)] public decimal PaidAmount { get; set; }
    [Required] public Guid PaidFromAccountId { get; set; }
    [Required] public Guid PaidToAccountId { get; set; }
    [StringLength(PaymentEntryConsts.MaxModeOfPaymentLength)] public string? ModeOfPayment { get; set; }
    public string? PartyType { get; set; }
    public Guid? PartyId { get; set; }
    /// <summary>Cost center for departmental GL reporting. Propagated to exchange gain/loss JE.</summary>
    public Guid? CostCenterId { get; set; }
    /// <summary>Project for project-wise reporting. Propagated to exchange gain/loss JE.</summary>
    public Guid? ProjectId { get; set; }
    [StringLength(PaymentEntryConsts.MaxReferenceNumberLength)] public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    /// <summary>Legacy single-invoice allocation (backwards compatible).</summary>
    public Guid? AgainstInvoiceId { get; set; }
    public string? AgainstInvoiceType { get; set; }

    /// <summary>Multi-invoice allocation (used when paying multiple invoices in one PE).
    /// Takes precedence over AgainstInvoiceId when populated.</summary>
    public List<PaymentReferenceDto>? References { get; set; }

    /// <summary>Against order for advance payments.</summary>
    public Guid? AgainstOrderId { get; set; }
    public string? AgainstOrderType { get; set; }

    /// <summary>Exchange rate for multi-currency payments.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>
    /// Payment currency code (e.g., "USD"). When different from company currency,
    /// the AppService auto-resolves the exchange rate from CurrencyExchangeService.
    /// Null/empty = same as company currency (no conversion needed).
    /// </summary>
    public string? PaymentCurrency { get; set; }
}

/// <summary>Individual allocation of a payment against an invoice or order.</summary>
public class PaymentReferenceDto
{
    [Required] public string ReferenceType { get; set; } = null!;
    [Required] public Guid ReferenceId { get; set; }
    [Required][Range(0.01, double.MaxValue)] public decimal AllocatedAmount { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
}

/// <summary>Outstanding invoice available for payment allocation.</summary>
public class OutstandingInvoiceForPaymentDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal Outstanding { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string InvoiceType { get; set; } = null!;
    public int DaysOverdue { get; set; }
    public bool IsOverdue { get; set; }
}

/// <summary>Outstanding order available for advance payment allocation.</summary>
public class OutstandingOrderForPaymentDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AdvancePaid { get; set; }
    public decimal PendingAdvance { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string OrderType { get; set; } = null!;
    public string? PartyName { get; set; }
}

/// <summary>Combined response for PE allocation: invoices + orders for a party.</summary>
public class PartyOutstandingDto
{
    public List<OutstandingInvoiceForPaymentDto> Invoices { get; set; } = [];
    public List<OutstandingOrderForPaymentDto> Orders { get; set; } = [];
    public decimal TotalInvoiceOutstanding { get; set; }
    public decimal TotalOrderPending { get; set; }
}
