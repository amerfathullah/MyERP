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

public class CandidatePaymentRequestDto
{
    public Guid Id { get; set; }
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = null!;
    public string? PartyName { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public Guid? BankAccountId { get; set; }
}

public class CandidatePaymentEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public string PaymentType { get; set; } = null!;
    public Guid? PartyId { get; set; }
    public string? PartyType { get; set; }
    public string? PartyName { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public string? ModeOfPayment { get; set; }
    public Guid? PaidToBankAccountId { get; set; }
    public Guid? PaidFromBankAccountId { get; set; }
}

public class GenerateBankFileInput
{
    public string? FileFormat { get; set; } // "CSV", "TXT"
}

public class BankPaymentFileResultDto
{
    public string FileName { get; set; } = null!;
    public string FileContent { get; set; } = null!;
    public string MimeType { get; set; } = "text/csv";
    public int TotalRecords { get; set; }
    public decimal TotalAmount { get; set; }
}

public class PaymentOrderSummaryDto
{
    public Guid PaymentOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public int Status { get; set; }
    public int TotalReferences { get; set; }
    public decimal TotalAmount { get; set; }
    public int DistinctSuppliersCount { get; set; }
    public Dictionary<string, decimal> AmountByModeOfPayment { get; set; } = new();
}
