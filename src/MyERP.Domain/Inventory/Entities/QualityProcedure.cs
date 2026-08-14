using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Procedure — tree-structured process document.
/// Per ERPNext: NestedSet tree (nsm_parent_field = "parent_quality_procedure").
/// Each procedure can have child steps forming a hierarchical SOP.
/// Per DO-NOT: "Allow child quality procedure to belong to multiple parent procedures (one-parent-only constraint)."
/// </summary>
public class QualityProcedure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Human-readable procedure name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Parent procedure (tree structure). Null = root-level procedure.</summary>
    public Guid? ParentQualityProcedureId { get; set; }

    /// <summary>Whether this procedure is a group/folder (has children) vs leaf.</summary>
    public bool IsGroup { get; set; }

    /// <summary>NestedSet left boundary.</summary>
    public int Lft { get; set; }

    /// <summary>NestedSet right boundary.</summary>
    public int Rgt { get; set; }

    /// <summary>Detailed procedure description/steps.</summary>
    public string? Description { get; set; }

    /// <summary>Process owner full name.</summary>
    public string? ProcessOwner { get; set; }

    /// <summary>Sequence for ordering steps within a parent.</summary>
    public int Sequence { get; set; }

    private readonly List<QualityProcedureStep> _steps = new();
    public IReadOnlyList<QualityProcedureStep> Steps => _steps.AsReadOnly();

    protected QualityProcedure() { }

    public QualityProcedure(Guid id, string name, Guid? parentId = null, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: QualityManagementConsts.MaxNameLength);
        ParentQualityProcedureId = parentId;
        TenantId = tenantId;
    }

    public void AddStep(QualityProcedureStep step)
    {
        _steps.Add(step);
    }

    public void ClearSteps()
    {
        _steps.Clear();
    }

    public void SetParent(Guid? parentId)
    {
        ParentQualityProcedureId = parentId;
    }
}

/// <summary>
/// A step within a Quality Procedure — describes one action or check to perform.
/// </summary>
public class QualityProcedureStep : Entity<Guid>
{
    public Guid QualityProcedureId { get; set; }

    /// <summary>Step description — what to do.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Sequence within the procedure.</summary>
    public int Sequence { get; set; }

    /// <summary>Reference to a child Quality Procedure (for sub-procedure linking).</summary>
    public Guid? ChildProcedureId { get; set; }

    protected QualityProcedureStep() { }

    public QualityProcedureStep(Guid id, Guid procedureId, string description, int sequence, Guid? childProcedureId = null)
        : base(id)
    {
        QualityProcedureId = procedureId;
        Description = Check.NotNullOrWhiteSpace(description, nameof(description), maxLength: QualityManagementConsts.MaxDescriptionLength);
        Sequence = sequence;
        ChildProcedureId = childProcedureId;
    }
}
