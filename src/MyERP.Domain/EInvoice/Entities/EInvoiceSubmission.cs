using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.EInvoice.Entities;

/// <summary>
/// Tracks a submission to LHDN MyInvois API.
/// Migrated from myinvois cancel_doc.py / get_status.py concepts.
/// </summary>
public class EInvoiceSubmission : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>LHDN submission UID returned on submit.</summary>
    public string? SubmissionUid { get; set; }

    /// <summary>Document UUID assigned by LHDN on acceptance.</summary>
    public string? DocumentUuid { get; set; }

    /// <summary>Long ID for document retrieval.</summary>
    public string? LongId { get; set; }

    /// <summary>Source document type: "SalesInvoice" or "PurchaseInvoice".</summary>
    public string SourceDocumentType { get; set; } = null!;

    /// <summary>Source document ID in MyERP.</summary>
    public Guid SourceDocumentId { get; set; }

    /// <summary>LHDN document type code (01=Invoice, 02=CreditNote, etc.).</summary>
    public string DocumentTypeCode { get; set; } = "01";

    /// <summary>Current status from LHDN.</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Rejection/cancellation reason.</summary>
    public string? Reason { get; set; }

    /// <summary>QR code URL returned by LHDN.</summary>
    public string? QrCodeUrl { get; set; }

    /// <summary>Full JSON response from LHDN for audit.</summary>
    public string? RawResponse { get; set; }

    /// <summary>Submission timestamp.</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Validation timestamp from LHDN.</summary>
    public DateTime? ValidatedAt { get; set; }

    /// <summary>Cancellation timestamp.</summary>
    public DateTime? CancelledAt { get; set; }

    protected EInvoiceSubmission() { }

    public EInvoiceSubmission(Guid id, Guid companyId, string sourceDocumentType, Guid sourceDocumentId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SourceDocumentType = sourceDocumentType;
        SourceDocumentId = sourceDocumentId;
        TenantId = tenantId;
    }

    public void MarkAccepted(string submissionUid, string documentUuid, string? longId, string? qrCodeUrl, string? rawResponse)
    {
        SubmissionUid = submissionUid;
        DocumentUuid = documentUuid;
        LongId = longId;
        QrCodeUrl = qrCodeUrl;
        RawResponse = rawResponse;
        SubmittedAt = DateTime.UtcNow;
        Status = "Valid";
        ValidatedAt = DateTime.UtcNow;
    }

    public void MarkRejected(string reason, string? rawResponse)
    {
        Reason = reason;
        RawResponse = rawResponse;
        Status = "Invalid";
    }

    public void MarkCancelled(string reason)
    {
        Reason = reason;
        Status = "Cancelled";
        CancelledAt = DateTime.UtcNow;
    }
}
