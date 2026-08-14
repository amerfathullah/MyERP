using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Review — auto-created by scheduler or manually from Quality Goals.
/// Per ERPNext: status roll-up (if any objective Failed -> status = Failed; if all Passed -> Passed; else Open).
/// </summary>
public class QualityReview : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid QualityGoalId { get; set; }

    /// <summary>Linked Procedure (optional).</summary>
    public Guid? ProcedureId { get; set; }

    /// <summary>Review period start date.</summary>
    public DateTime ReviewDate { get; set; }

    /// <summary>Actual measured overall value.</summary>
    public decimal? ActualValue { get; set; }

    /// <summary>Status: Open, Passed, Failed.</summary>
    public QualityReviewStatus Status { get; private set; } = QualityReviewStatus.Open;

    /// <summary>Notes about the review.</summary>
    public string? Notes { get; set; }

    /// <summary>Reviewed by user.</summary>
    public Guid? ReviewedByUserId { get; set; }

    private readonly List<QualityReviewObjective> _objectives = new();
    public IReadOnlyList<QualityReviewObjective> Objectives => _objectives.AsReadOnly();

    protected QualityReview() { }

    public QualityReview(Guid id, Guid qualityGoalId, DateTime reviewDate, Guid? tenantId = null)
        : base(id)
    {
        QualityGoalId = qualityGoalId;
        ReviewDate = reviewDate;
        TenantId = tenantId;
        Status = QualityReviewStatus.Open;
    }

    public void AddObjective(QualityReviewObjective objective)
    {
        _objectives.Add(objective);
        EvaluateStatus();
    }

    public void ClearObjectives()
    {
        _objectives.Clear();
        EvaluateStatus();
    }

    public void Pass(decimal? actualValue = null, string? notes = null)
    {
        ActualValue = actualValue ?? ActualValue;
        Status = QualityReviewStatus.Passed;
        if (notes != null) Notes = notes;
    }

    public void Fail(decimal? actualValue = null, string? notes = null)
    {
        ActualValue = actualValue ?? ActualValue;
        Status = QualityReviewStatus.Failed;
        if (notes != null) Notes = notes;
    }

    public void EvaluateStatus()
    {
        if (_objectives.Count == 0)
        {
            return;
        }

        if (_objectives.Any(o => o.Status == QualityReviewStatus.Failed))
        {
            Status = QualityReviewStatus.Failed;
        }
        else if (_objectives.All(o => o.Status == QualityReviewStatus.Passed))
        {
            Status = QualityReviewStatus.Passed;
        }
        else
        {
            Status = QualityReviewStatus.Open;
        }
    }
}

public class QualityReviewObjective : Entity<Guid>
{
    public Guid QualityReviewId { get; set; }
    public string Objective { get; set; } = null!;
    public decimal Target { get; set; }
    public decimal? Actual { get; set; }
    public string? Uom { get; set; }
    public QualityReviewStatus Status { get; set; } = QualityReviewStatus.Open;
    public string? Notes { get; set; }

    protected QualityReviewObjective() { }

    public QualityReviewObjective(Guid id, Guid qualityReviewId, string objective, decimal target, string? uom = null)
        : base(id)
    {
        QualityReviewId = qualityReviewId;
        Objective = objective;
        Target = target;
        Uom = uom;
        Status = QualityReviewStatus.Open;
    }
}
