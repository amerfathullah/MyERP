using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class PaymentRequestDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string PaymentRequestType { get; set; } = null!;
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = null!;
    public string? PartyName { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public int Status { get; set; }
    public Guid? PaymentEntryId { get; set; }
}

public class CreatePaymentRequestDto
{
    public Guid CompanyId { get; set; }
    public string PaymentRequestType { get; set; } = "Inward";
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Customer";
    public string? PartyName { get; set; }
    public decimal GrandTotal { get; set; }
    public string Currency { get; set; } = "MYR";
    public string? EmailTo { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
}

public class ResendPaymentEmailResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? SentTo { get; set; }
}

public class PaymentRequestSummaryDto
{
    public Guid Id { get; set; }
    public string PaymentRequestType { get; set; } = null!;
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public string PartyType { get; set; } = null!;
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public int Status { get; set; }
    public string StatusName { get; set; } = null!;
    public string? PaymentUrl { get; set; }
    public string? PaymentGateway { get; set; }
    public Guid? PaymentEntryId { get; set; }
    public bool CanPay { get; set; }
    public bool CanResendEmail { get; set; }
    public bool CanCancel { get; set; }
}
