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

    public List<ProjectTask> Tasks { get; set; } = new();

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

    public void UpdateProgress()
    {
        if (Tasks.Count == 0)
        {
            PercentComplete = 0;
            return;
        }

        PercentComplete = PercentCompleteMethod switch
        {
            PercentCompleteMethod.TaskCompletion => CalculateByTaskCompletion(),
            PercentCompleteMethod.TaskProgress => CalculateByTaskProgress(),
            PercentCompleteMethod.TaskWeight => CalculateByTaskWeight(),
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

    private decimal CalculateByTaskCompletion()
    {
        var total = Tasks.Count;
        var completed = 0;
        foreach (var t in Tasks)
        {
            if (t.Status is ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled)
                completed++;
        }
        return total > 0 ? Math.Round((decimal)completed / total * 100, 1) : 0;
    }

    private decimal CalculateByTaskProgress()
    {
        var total = Tasks.Count;
        var sum = 0m;
        foreach (var t in Tasks)
            sum += t.Progress;
        return total > 0 ? Math.Round(sum / total, 1) : 0;
    }

    private decimal CalculateByTaskWeight()
    {
        var totalWeight = 0m;
        var weightedProgress = 0m;
        foreach (var t in Tasks)
        {
            var w = t.TaskWeight > 0 ? t.TaskWeight : 1;
            totalWeight += w;
            weightedProgress += t.Progress * w;
        }
        return totalWeight > 0 ? Math.Round(weightedProgress / totalWeight, 1) : 0;
    }
}
