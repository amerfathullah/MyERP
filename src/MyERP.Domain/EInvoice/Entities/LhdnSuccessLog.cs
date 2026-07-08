using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.EInvoice.Entities;

/// <summary>
/// Tracks successful LHDN e-Invoice submissions with full response data.
/// Migrated from myinvois lhdn_success_log doctype.
/// Provides permanent audit trail of all LHDN interactions.
/// </summary>
public class LhdnSuccessLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Reference to the EInvoiceSubmission.</summary>
    public Guid SubmissionId { get; set; }

    /// <summary>LHDN document UUID returned on acceptance.</summary>
    public string DocumentUuid { get; set; } = null!;

    /// <summary>LHDN long ID for public access.</summary>
    public string? LongId { get; set; }

    /// <summary>Source document type (SalesInvoice, PurchaseInvoice).</summary>
    public string SourceDocumentType { get; set; } = null!;

    /// <summary>Source document ID.</summary>
    public Guid SourceDocumentId { get; set; }

    /// <summary>Source document number (e.g., INV-2026-001).</summary>
    public string? SourceDocumentNumber { get; set; }

    /// <summary>Document type code (01=Invoice, 02=CreditNote, etc.).</summary>
    public string DocumentTypeCode { get; set; } = "01";

    /// <summary>Submission timestamp from LHDN response.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Validation timestamp from LHDN.</summary>
    public DateTime? ValidatedAt { get; set; }

    /// <summary>Full JSON response from LHDN API for audit.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>QR code URL for the validated document.</summary>
    public string? QrCodeUrl { get; set; }

    /// <summary>Total amount submitted.</summary>
    public decimal GrandTotal { get; set; }

    /// <summary>Currency code.</summary>
    public string CurrencyCode { get; set; } = "MYR";

    protected LhdnSuccessLog() { }

    public LhdnSuccessLog(Guid id, Guid companyId, Guid submissionId, string documentUuid,
        string sourceDocumentType, Guid sourceDocumentId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SubmissionId = submissionId;
        DocumentUuid = documentUuid;
        SourceDocumentType = sourceDocumentType;
        SourceDocumentId = sourceDocumentId;
        TenantId = tenantId;
        SubmittedAt = DateTime.UtcNow;
    }
}
