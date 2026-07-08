using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Notification.Entities;

/// <summary>
/// Persistent notification record for in-app notification center.
/// Uses ABP's notification infrastructure for delivery, but stores
/// custom notification data for the MyERP UI notification panel.
/// </summary>
public class AppNotification : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Target user for this notification.</summary>
    public Guid UserId { get; set; }

    /// <summary>Notification subject/title.</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Notification body (supports markdown).</summary>
    public string? Body { get; set; }

    /// <summary>Severity level for visual styling.</summary>
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>Whether the user has read this notification.</summary>
    public bool IsRead { get; set; }

    /// <summary>When the user marked it as read.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Optional link to navigate to (e.g., "/sales/invoices/abc-123").</summary>
    public string? ActionUrl { get; set; }

    /// <summary>Source document type for context (e.g., "SalesInvoice").</summary>
    public string? SourceDocumentType { get; set; }

    /// <summary>Source document ID for linking.</summary>
    public Guid? SourceDocumentId { get; set; }

    protected AppNotification() { }

    public AppNotification(Guid id, Guid userId, string subject, Guid? tenantId = null) : base(id)
    {
        UserId = userId;
        Subject = subject;
        TenantId = tenantId;
    }

    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
