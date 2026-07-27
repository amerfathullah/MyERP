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
/// Per DO-NOT: "Allow child quality procedure to belong to multiple parent procedures
/// (one-parent-only constraint)."
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

    /// <summary>Sequence for ordering steps within a parent.</summary>
    public int Sequence { get; set; }

    private readonly List<QualityProcedureStep> _steps = new();
    public IReadOnlyList<QualityProcedureStep> Steps => _steps.AsReadOnly();

    protected QualityProcedure() { }

    public QualityProcedure(Guid id, string name, Guid? parentId = null, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 200);
        ParentQualityProcedureId = parentId;
        TenantId = tenantId;
    }

    public void AddStep(QualityProcedureStep step)
    {
        _steps.Add(step);
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

    public QualityProcedureStep(Guid id, Guid procedureId, string description, int sequence)
        : base(id)
    {
        QualityProcedureId = procedureId;
        Description = description;
        Sequence = sequence;
    }
}

/// <summary>
/// Quality Goal — trackable metrics with auto-review generation.
/// Per ERPNext: scheduler Daily/Weekly/Monthly/Quarterly creates QualityReview.
/// </summary>
public class QualityGoal : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public string? Goal { get; set; }

    /// <summary>How frequently to create quality reviews (Daily/Weekly/Monthly/Quarterly).</summary>
    public string Frequency { get; set; } = "Monthly";

    /// <summary>Target value for measurement.</summary>
    public decimal TargetValue { get; set; }

    /// <summary>Unit of measurement (e.g., %, units, hours).</summary>
    public string? Uom { get; set; }

    /// <summary>Who is responsible for this quality goal.</summary>
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>Whether this goal is actively being tracked.</summary>
    public bool IsEnabled { get; set; } = true;

    protected QualityGoal() { }

    public QualityGoal(Guid id, string name, string frequency, decimal targetValue, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 200);
        Frequency = frequency;
        TargetValue = targetValue;
        TenantId = tenantId;
    }
}

/// <summary>
/// Quality Review — auto-created by scheduler from Quality Goals.
/// Per ERPNext: status roll-up (any Failed objective → parent = Failed).
/// </summary>
public class QualityReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid QualityGoalId { get; set; }

    /// <summary>Review period start date.</summary>
    public DateTime ReviewDate { get; set; }

    /// <summary>Actual measured value.</summary>
    public decimal? ActualValue { get; set; }

    /// <summary>Status: Open, Passed, Failed.</summary>
    public QualityReviewStatus Status { get; set; } = QualityReviewStatus.Open;

    /// <summary>Notes about the review.</summary>
    public string? Notes { get; set; }

    /// <summary>Reviewed by user.</summary>
    public Guid? ReviewedByUserId { get; set; }

    protected QualityReview() { }

    public QualityReview(Guid id, Guid qualityGoalId, DateTime reviewDate, Guid? tenantId = null)
        : base(id)
    {
        QualityGoalId = qualityGoalId;
        ReviewDate = reviewDate;
        TenantId = tenantId;
    }

    public void Pass(decimal actualValue, string? notes = null)
    {
        ActualValue = actualValue;
        Status = QualityReviewStatus.Passed;
        Notes = notes;
    }

    public void Fail(decimal actualValue, string? notes = null)
    {
        ActualValue = actualValue;
        Status = QualityReviewStatus.Failed;
        Notes = notes;
    }
}

public enum QualityReviewStatus
{
    Open = 0,
    Passed = 1,
    Failed = 2,
}
