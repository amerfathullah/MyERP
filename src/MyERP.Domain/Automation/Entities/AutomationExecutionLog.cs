using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Automation.Entities;

/// <summary>
/// Execution log for automation rules — tracks when a rule fired and its result.
/// Provides auditability for automated actions.
/// </summary>
public class AutomationExecutionLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid AutomationRuleId { get; set; }

    /// <summary>Document that triggered the rule.</summary>
    public Guid? SourceDocumentId { get; set; }

    /// <summary>Document type that triggered the rule.</summary>
    public string? SourceDocumentType { get; set; }

    /// <summary>Whether execution succeeded.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Error message if execution failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Duration in milliseconds.</summary>
    public int ExecutionDurationMs { get; set; }

    protected AutomationExecutionLog() { }

    public AutomationExecutionLog(Guid id, Guid automationRuleId, Guid? tenantId = null) : base(id)
    {
        AutomationRuleId = automationRuleId;
        TenantId = tenantId;
    }
}
