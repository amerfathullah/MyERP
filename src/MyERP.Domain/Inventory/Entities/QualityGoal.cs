using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

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

    /// <summary>Linked Procedure (optional).</summary>
    public Guid? ProcedureId { get; set; }

    /// <summary>Weekday for weekly frequency (e.g., Monday).</summary>
    public string? Weekday { get; set; }

    /// <summary>Day of month for monthly frequency (1-31).</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>Whether this goal is actively being tracked.</summary>
    public bool IsEnabled { get; set; } = true;

    private readonly List<QualityGoalObjective> _objectives = new();
    public IReadOnlyList<QualityGoalObjective> Objectives => _objectives.AsReadOnly();

    protected QualityGoal() { }

    public QualityGoal(Guid id, string name, string frequency, decimal targetValue, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: QualityManagementConsts.MaxNameLength);
        Frequency = Check.NotNullOrWhiteSpace(frequency, nameof(frequency), maxLength: QualityManagementConsts.MaxFrequencyLength);
        TargetValue = targetValue;
        TenantId = tenantId;
    }

    public void AddObjective(QualityGoalObjective objective)
    {
        _objectives.Add(objective);
    }

    public void ClearObjectives()
    {
        _objectives.Clear();
    }
}

public class QualityGoalObjective : Entity<Guid>
{
    public Guid QualityGoalId { get; set; }
    public string Objective { get; set; } = null!;
    public decimal Target { get; set; }
    public string? Uom { get; set; }

    protected QualityGoalObjective() { }

    public QualityGoalObjective(Guid id, Guid qualityGoalId, string objective, decimal target, string? uom = null)
        : base(id)
    {
        QualityGoalId = qualityGoalId;
        Objective = Check.NotNullOrWhiteSpace(objective, nameof(objective), maxLength: QualityManagementConsts.MaxNameLength);
        Target = target;
        Uom = uom;
    }
}
