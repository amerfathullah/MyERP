using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.EInvoice;

public class EInvoiceSubmissionDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string? SubmissionUid { get; set; }
    public string? DocumentUuid { get; set; }
    public string? LongId { get; set; }
    public string SourceDocumentType { get; set; } = null!;
    public Guid SourceDocumentId { get; set; }
    public string DocumentTypeCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Reason { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class SubmitEInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(50)] public string SourceDocumentType { get; set; } = null!;
    [Required] public Guid SourceDocumentId { get; set; }
    [StringLength(5)] public string DocumentTypeCode { get; set; } = "01";
}

public class CancelEInvoiceDto
{
    [Required] public Guid SubmissionId { get; set; }
    [Required][StringLength(500)] public string Reason { get; set; } = null!;
}
