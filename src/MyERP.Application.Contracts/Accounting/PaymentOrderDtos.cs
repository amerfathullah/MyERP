using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class PaymentOrderReferenceDto
{
    public Guid Id { get; set; }
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public decimal Amount { get; set; }
    public Guid? SupplierId { get; set; }
    public string? ModeOfPayment { get; set; }
    public Guid BankAccountId { get; set; }
    public string? PaymentReference { get; set; }
}

public class PaymentOrderDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? OrderNumber { get; set; }
    public PaymentOrderType PaymentOrderType { get; set; }
    public DateTime PostingDate { get; set; }
    public Guid? PartyId { get; set; }
    public Guid CompanyBankAccountId { get; set; }
    public int Status { get; set; }
    public Guid? AmendedFromId { get; set; }
    public List<PaymentOrderReferenceDto> References { get; set; } = new();
}

public class CreatePaymentOrderReferenceDto
{
    [Required] public string ReferenceType { get; set; } = null!;
    [Required] public Guid ReferenceId { get; set; }
    public decimal Amount { get; set; }
    public Guid? SupplierId { get; set; }
    [StringLength(PaymentOrderConsts.MaxModeOfPaymentLength)] public string? ModeOfPayment { get; set; }
    [Required] public Guid BankAccountId { get; set; }
    [StringLength(PaymentOrderConsts.MaxPaymentReferenceLength)] public string? PaymentReference { get; set; }
}

public class CreatePaymentOrderDto
{
    [Required] public Guid CompanyId { get; set; }
    public PaymentOrderType PaymentOrderType { get; set; }
    public DateTime PostingDate { get; set; } = DateTime.UtcNow;
    public Guid? PartyId { get; set; }
    [Required] public Guid CompanyBankAccountId { get; set; }
    public List<CreatePaymentOrderReferenceDto> References { get; set; } = new();
}

public class MakePaymentRecordsDto
{
    [Required] public Guid SupplierId { get; set; }
    public string? ModeOfPayment { get; set; }
}
