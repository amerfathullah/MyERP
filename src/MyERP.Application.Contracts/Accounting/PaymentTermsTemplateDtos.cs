using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class PaymentTermsTemplateDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public PaymentTermDto[] Terms { get; set; } = [];
}

public class PaymentTermDto
{
    public Guid Id { get; set; }
    public decimal InvoicePortion { get; set; }
    public int CreditDays { get; set; }
    public string? Description { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
}

public class CreateUpdatePaymentTermsTemplateDto
{
    public string Name { get; set; } = null!;
    public CreatePaymentTermDto[] Terms { get; set; } = [];
}

public class CreatePaymentTermDto
{
    public decimal InvoicePortion { get; set; }
    public int CreditDays { get; set; }
    public string? Description { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
}
