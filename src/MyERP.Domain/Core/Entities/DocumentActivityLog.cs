using System;
using System.Collections.Generic;
using MyERP.Core;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Core.Entities;

/// <summary>
/// Document activity log — records key state transitions for audit purposes.
/// ABP AuditLog covers technical changes; this captures business-level events
/// (submitted, posted, cancelled, amended, converted, payment received, etc.)
/// </summary>
public class DocumentActivityLog : CreationAuditedEntity<Guid>
{
    /// <summary>Document type (e.g., "SalesInvoice", "PurchaseOrder").</summary>
    public string DocumentType { get; set; } = null!;

    /// <summary>Document ID.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Document number (for display without needing to join).</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Activity type: Submitted, Posted, Cancelled, Amended, Converted, PaymentReceived, etc.</summary>
    public string ActivityType { get; set; } = null!;

    /// <summary>Previous status (if applicable).</summary>
    public string? PreviousStatus { get; set; }

    /// <summary>New status after this activity.</summary>
    public string? NewStatus { get; set; }

    /// <summary>User who performed the action.</summary>
    public Guid? PerformedByUserId { get; set; }

    /// <summary>Additional details (JSON or plain text).</summary>
    public string? Details { get; set; }

    /// <summary>Company this activity belongs to.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Tenant ID for multi-tenancy.</summary>
    public Guid? TenantId { get; set; }

    protected DocumentActivityLog() { }

    public DocumentActivityLog(
        Guid id, string documentType, Guid documentId,
        string activityType, Guid companyId,
        string? documentNumber = null, string? previousStatus = null,
        string? newStatus = null, Guid? performedByUserId = null,
        string? details = null, Guid? tenantId = null)
        : base(id)
    {
        DocumentType = documentType;
        DocumentId = documentId;
        ActivityType = activityType;
        CompanyId = companyId;
        DocumentNumber = documentNumber;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        PerformedByUserId = performedByUserId;
        Details = details;
        TenantId = tenantId;
    }
}
