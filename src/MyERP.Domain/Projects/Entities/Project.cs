using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

public class Project : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string ProjectNumber { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public ProjectStatus Status { get; private set; }
    public ProjectPriority Priority { get; set; }
    public PercentCompleteMethod PercentCompleteMethod { get; set; }
    public decimal PercentComplete { get; private set; }

    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? ProjectTypeId { get; set; }

    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // Costing
    public decimal EstimatedCost { get; set; }
    public decimal TotalCostingAmount { get; set; }
    public decimal TotalBillingAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }

    public string? Notes { get; set; }
    public string? CostCenter { get; set; }

    public List<ProjectTask> Tasks { get; private set; } = new();

    /// <summary>Team members assigned to this project, with per-member access flags.</summary>
    public ICollection<ProjectUser> Users { get; private set; } = new List<ProjectUser>();

    protected Project() { }

    public Project(Guid id, Guid companyId, string projectNumber, string projectName, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ProjectNumber = projectNumber;
        ProjectName = projectName;
        Status = ProjectStatus.Open;
        Priority = ProjectPriority.Medium;
        PercentCompleteMethod = PercentCompleteMethod.TaskCompletion;
        TenantId = tenantId;
    }

    public void UpdateProgress() => UpdateProgress(Tasks);

    /// <summary>
    /// Recalculates PercentComplete from an explicit task list rather than the <see cref="Tasks"/>
    /// navigation. Callers that just inserted/deleted a task in the same unit of work should query
    /// tasks fresh via the task repository and pass them here — the <see cref="Tasks"/> AutoInclude
    /// navigation has been observed (confirmed on both SQLite and PostgreSQL) to still reflect the
    /// pre-mutation state immediately after a same-request task delete, even though a direct
    /// repository query for the same tasks correctly reflects it.
    /// </summary>
    public void UpdateProgress(IReadOnlyList<ProjectTask> currentTasks)
    {
        if (currentTasks.Count == 0)
        {
            PercentComplete = 0;
            return;
        }

        PercentComplete = PercentCompleteMethod switch
        {
            PercentCompleteMethod.TaskCompletion => CalculateByTaskCompletion(currentTasks),
            PercentCompleteMethod.TaskProgress => CalculateByTaskProgress(currentTasks),
            PercentCompleteMethod.TaskWeight => CalculateByTaskWeight(currentTasks),
            _ => PercentComplete, // Manual — don't change
        };

        if (PercentComplete >= 100 && Status == ProjectStatus.Open)
        {
            Status = ProjectStatus.Completed;
        }
    }

    public void Complete()
    {
        Status = ProjectStatus.Completed;
        PercentComplete = 100;
        ActualEndDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Manually set percent complete (only allowed when PercentCompleteMethod == Manual).
    /// Per ERPNext PR #57274: must be between 0 and 100; auto-sets to 100 when Completed.
    /// </summary>
    public void SetPercentComplete(decimal value)
    {
        if (PercentCompleteMethod != PercentCompleteMethod.Manual)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Percent complete can only be set manually when method is Manual");

        if (Status == ProjectStatus.Completed)
        {
            PercentComplete = 100;
            return;
        }

        if (value < 0 || value > 100)
            throw new BusinessException("MyERP:13003")
                .WithData("value", value);

        PercentComplete = value;
    }

    public void Cancel()
    {
        if (Status == ProjectStatus.Completed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectStatus.Cancelled;
    }

    public void Reopen()
    {
        if (Status != ProjectStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectStatus.Open;
    }

    public decimal GrossMargin =>
        TotalBilledAmount - TotalCostingAmount;

    private static decimal CalculateByTaskCompletion(IReadOnlyList<ProjectTask> tasks)
    {
        var total = tasks.Count;
        var completed = 0;
        foreach (var t in tasks)
        {
            if (t.Status is ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled)
                completed++;
        }
        return total > 0 ? Math.Round((decimal)completed / total * 100, 1) : 0;
    }

    private static decimal CalculateByTaskProgress(IReadOnlyList<ProjectTask> tasks)
    {
        var total = tasks.Count;
        var sum = 0m;
        foreach (var t in tasks)
            sum += t.Progress;
        return total > 0 ? Math.Round(sum / total, 1) : 0;
    }

    private static decimal CalculateByTaskWeight(IReadOnlyList<ProjectTask> tasks)
    {
        var totalWeight = 0m;
        var weightedProgress = 0m;
        foreach (var t in tasks)
        {
            var w = t.TaskWeight > 0 ? t.TaskWeight : 1;
            totalWeight += w;
            weightedProgress += t.Progress * w;
        }
        return totalWeight > 0 ? Math.Round(weightedProgress / totalWeight, 1) : 0;
    }
}
