using System;
using System.Collections.Generic;
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

public class BatchSubmitEInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public string SourceDocumentType { get; set; } = "SalesInvoice";
    [Required] public List<Guid> DocumentIds { get; set; } = new();
}

public class BatchSubmitResultDto
{
    public int TotalRequested { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<BatchSubmitItemResult> Results { get; set; } = new();
}

public class BatchSubmitItemResult
{
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = null!;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LhdnUuid { get; set; }
    public string? Status { get; set; }
}

public class ConsolidateInvoicesDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public List<Guid> InvoiceIds { get; set; } = new();
}
