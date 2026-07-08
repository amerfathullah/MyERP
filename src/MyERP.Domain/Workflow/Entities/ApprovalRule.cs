using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Workflow.Entities;

/// <summary>
/// Defines a configurable approval rule: when a document of a given type 
/// requires approval and who can approve it.
/// Supports multi-level approval chains via Level property.
/// </summary>
public class ApprovalRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Document type this rule applies to (e.g., "SalesInvoice", "PurchaseOrder").</summary>
    public string DocumentType { get; private set; } = null!;

    /// <summary>Descriptive name for this approval rule.</summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Approval level in the chain. Level 1 must approve before Level 2, etc.
    /// Multiple rules can exist at the same level (any one approver suffices).
    /// </summary>
    public int Level { get; set; } = 1;

    /// <summary>The role that can approve at this level (ABP role name).</summary>
    public string? ApproverRoleName { get; set; }

    /// <summary>Specific user who can approve (if not role-based).</summary>
    public Guid? ApproverUserId { get; set; }

    /// <summary>
    /// Optional condition expression evaluated against document properties.
    /// E.g., "Amount > 10000" — only triggers this rule when condition is met.
    /// If null, rule always applies to the document type.
    /// </summary>
    public string? ConditionExpression { get; set; }

    /// <summary>Minimum amount threshold that triggers this rule. Null = no threshold.</summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>Company scope. Null = applies to all companies.</summary>
    public Guid? CompanyId { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Description { get; set; }

    protected ApprovalRule() { }

    public ApprovalRule(Guid id, string documentType, string name, int level, Guid? tenantId = null) : base(id)
    {
        DocumentType = documentType;
        Name = name;
        Level = level;
        TenantId = tenantId;
    }
}
