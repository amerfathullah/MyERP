using System;
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
    [StringLength(PaymentEntryConsts.MaxReferenceNumberLength)] public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid? AgainstInvoiceId { get; set; }
    public string? AgainstInvoiceType { get; set; }
}
