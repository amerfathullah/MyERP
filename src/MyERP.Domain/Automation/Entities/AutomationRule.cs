using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Automation.Entities;

/// <summary>
/// Configurable automation rule that triggers actions on business events.
/// Replaces ERPNext hooks.py pattern with a data-driven approach.
/// Example: "When SalesInvoice is Posted, Submit to LHDN automatically"
/// </summary>
public class AutomationRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Human-readable name for the rule.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Optional description of what this rule does.</summary>
    public string? Description { get; set; }

    /// <summary>The event that triggers this rule.</summary>
    public AutomationTrigger Trigger { get; set; }

    /// <summary>Document type this rule applies to (e.g., "SalesInvoice", "PurchaseOrder"). Null = all.</summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Optional condition expression (simple field comparison).
    /// E.g., "GrandTotal > 5000" or "CurrencyCode == MYR"
    /// Null = always fire when trigger matches.
    /// </summary>
    public string? ConditionExpression { get; set; }

    /// <summary>The action to execute when triggered.</summary>
    public AutomationAction Action { get; set; }

    /// <summary>
    /// JSON configuration for the action.
    /// For SendNotification: { "subject": "...", "severity": 1 }
    /// For SendEmail: { "to": "{{CustomerEmail}}", "template": "invoice-submitted" }
    /// For SubmitToLhdn: { }
    /// For UpdateField: { "field": "Status", "value": "Approved" }
    /// </summary>
    public string? ActionConfig { get; set; }

    /// <summary>Company scope. Null = applies to all companies.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Whether this rule is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Execution order when multiple rules match the same trigger.</summary>
    public int Priority { get; set; }

    protected AutomationRule() { }

    public AutomationRule(Guid id, string name, AutomationTrigger trigger, AutomationAction action, Guid? tenantId = null)
        : base(id)
    {
        Name = name;
        Trigger = trigger;
        Action = action;
        TenantId = tenantId;
    }
}
