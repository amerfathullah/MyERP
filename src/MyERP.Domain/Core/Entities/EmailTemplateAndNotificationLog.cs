using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Email Template — reusable email body/subject for automated notifications.
/// Used by: dunning, auto-repeat, delivery dispatch, RFQ to suppliers, 
/// payment reminders, approval notifications, statement of accounts.
/// 
/// Templates support variable substitution (e.g., {{ customer_name }}, {{ invoice_number }}).
/// Per security rules: templates are rendered in a sandboxed context.
/// </summary>
public class EmailTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Template name (unique identifier for lookup).</summary>
    public string Name { get; set; } = null!;

    /// <summary>Email subject line (supports variable substitution).</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Email body (HTML, supports variable substitution).</summary>
    public string Body { get; set; } = null!;

    /// <summary>Document type this template is associated with (e.g., "SalesInvoice").</summary>
    public string? DocumentType { get; set; }

    /// <summary>Whether this template is enabled for use.</summary>
    public bool IsEnabled { get; set; } = true;

    protected EmailTemplate() { }

    public EmailTemplate(Guid id, string name, string subject, string body, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        Subject = Check.NotNullOrWhiteSpace(subject, nameof(subject));
        Body = Check.NotNullOrWhiteSpace(body, nameof(body));
        TenantId = tenantId;
    }

    /// <summary>
    /// Render the subject with variable substitution.
    /// Variables are in the format {{ variable_name }}.
    /// </summary>
    public string RenderSubject(Dictionary<string, string> variables)
    {
        return RenderTemplate(Subject, variables);
    }

    /// <summary>
    /// Render the body with variable substitution.
    /// </summary>
    public string RenderBody(Dictionary<string, string> variables)
    {
        return RenderTemplate(Body, variables);
    }

    private static string RenderTemplate(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty);
            // Also handle without spaces: {{key}}
            result = result.Replace($"{{{{ {key} }}}}", value ?? string.Empty);
        }
        return result;
    }
}

/// <summary>
/// Notification Log — records each notification sent (email, in-app, or push).
/// Provides audit trail and retry capability for failed notifications.
/// </summary>
public class NotificationLog : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Channel: Email, InApp, Push.</summary>
    public NotificationChannel Channel { get; set; }

    /// <summary>Subject line.</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Rendered message body.</summary>
    public string? Body { get; set; }

    /// <summary>Recipient (email address or user ID).</summary>
    public string Recipient { get; set; } = null!;

    /// <summary>Sender (email address or system).</summary>
    public string? Sender { get; set; }

    /// <summary>Document type that triggered this notification.</summary>
    public string? DocumentType { get; set; }

    /// <summary>Document ID that triggered this notification.</summary>
    public Guid? DocumentId { get; set; }

    /// <summary>Template used (if applicable).</summary>
    public Guid? EmailTemplateId { get; set; }

    /// <summary>Delivery status.</summary>
    public NotificationStatus Status { get; set; } = NotificationStatus.Queued;

    /// <summary>Error message if delivery failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>When the notification was sent.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>Number of retry attempts.</summary>
    public int RetryCount { get; set; }

    protected NotificationLog() { }

    public NotificationLog(Guid id, NotificationChannel channel, string subject,
        string recipient, Guid? tenantId = null) : base(id)
    {
        Channel = channel;
        Subject = subject;
        Recipient = recipient;
        TenantId = tenantId;
    }

    /// <summary>Mark as sent successfully.</summary>
    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    /// <summary>Mark as failed with error.</summary>
    public void MarkFailed(string error)
    {
        Status = NotificationStatus.Failed;
        ErrorMessage = error;
        RetryCount++;
    }

    /// <summary>Queue for retry.</summary>
    public void QueueRetry()
    {
        if (RetryCount >= 3)
        {
            Status = NotificationStatus.PermanentlyFailed;
            return;
        }
        Status = NotificationStatus.Queued;
    }
}

public enum NotificationChannel
{
    Email = 0,
    InApp = 1,
    Push = 2
}

public enum NotificationStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
    PermanentlyFailed = 3
}
