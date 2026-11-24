using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Projects.Entities;

public class ProjectTask : FullAuditedEntity<Guid>
{
    public Guid ProjectId { get; set; }
    public string TaskNumber { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public ProjectTaskStatus Status { get; private set; }
    public ProjectPriority Priority { get; set; }

    public Guid? ParentTaskId { get; set; }
    public Guid? TaskTypeId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsMilestone { get; set; }
    public decimal TaskWeight { get; set; } = 1;
    public decimal Progress { get; set; }

    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public decimal ExpectedHours { get; set; }
    public decimal ActualHours { get; set; }

    public Guid? AssignedUserId { get; set; }
    public string? Description { get; set; }

    // Dependencies (stored as IDs, resolved at application layer)
    public List<TaskDependency> Dependencies { get; set; } = new();

    protected ProjectTask() { }

    public ProjectTask(Guid id, Guid projectId, string taskNumber, string subject)
        : base(id)
    {
        ProjectId = projectId;
        TaskNumber = taskNumber;
        Subject = subject;
        Status = ProjectTaskStatus.Open;
        Priority = ProjectPriority.Medium;
    }

    /// <summary>
    /// Adds a task dependency. Validates self-reference at entity level.
    /// Full cycle detection requires TaskDependencyValidationService.
    /// </summary>
    public void AddDependency(Guid dependsOnTaskId)
    {
        if (dependsOnTaskId == Id)
            throw new BusinessException(MyERPDomainErrorCodes.CircularDependencyDetected)
                .WithData("taskId", Id);

        Dependencies.Add(new TaskDependency(Guid.NewGuid(), Id, dependsOnTaskId));
    }

    /// <summary>
    /// Sets default weight from Task Type if missing or when only type changes (per ERPNext PR #60635 / commit ba5e28ebee).
    /// </summary>
    public void SetDefaultWeight(decimal defaultWeight)
    {
        TaskWeight = defaultWeight;
    }

    /// <summary>
    /// Calculates default ExpectedEndDate if missing and ExpectedHours > 0 (per ERPNext PR #51224 / commit 820ccba9a4).
    /// </summary>
    public void SetDefaultEndDateIfMissing()
    {
        if (ExpectedStartDate.HasValue && ExpectedHours > 0 && !ExpectedEndDate.HasValue)
        {
            ExpectedEndDate = ExpectedStartDate.Value.AddHours((double)ExpectedHours);
        }
    }

    /// <summary>
    /// Returns the later of ExpectedEndDate and ActualEndDate, as a task that overruns still blocks its dependents.
    /// Per ERPNext PR #69135 (<c>task.py: get_schedule_end_date</c>).
    /// </summary>
    public DateTime? GetScheduleEndDate()
    {
        if (ExpectedEndDate.HasValue && ActualEndDate.HasValue)
            return ExpectedEndDate.Value >= ActualEndDate.Value ? ExpectedEndDate.Value : ActualEndDate.Value;
        return ExpectedEndDate ?? ActualEndDate;
    }

    /// <summary>
    /// Moves this task to start on the day after <paramref name="endDate"/>, preserving time-of-day
    /// and duration (works even when <see cref="ExpectedEndDate"/> is unset).
    /// Per ERPNext PR #69135 (<c>task.py: move_to_start_after</c>).
    /// </summary>
    public void MoveToStartAfter(DateTime endDate)
    {
        if (!ExpectedStartDate.HasValue) return;
        var startDate = ExpectedStartDate.Value;
        var newStartDate = endDate.Date.AddDays(1).Add(startDate.TimeOfDay);
        var shift = newStartDate - startDate;
        ExpectedStartDate = newStartDate;
        if (ExpectedEndDate.HasValue)
        {
            ExpectedEndDate = ExpectedEndDate.Value + shift;
        }
    }

    public void Start()
    {
        if (Status is not (ProjectTaskStatus.Open or ProjectTaskStatus.Overdue or ProjectTaskStatus.PendingReview))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectTaskStatus.Working;
        ActualStartDate ??= DateTime.UtcNow;
    }

    public void SendForReview()
    {
        if (Status is ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectTaskStatus.PendingReview;
        ActualStartDate ??= DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status is ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectTaskStatus.Completed;
        Progress = 100;
        ActualEndDate = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == ProjectTaskStatus.Completed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectTaskStatus.Cancelled;
    }

    public void Reopen()
    {
        if (Status is not (ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled or ProjectTaskStatus.PendingReview))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectTaskStatus.Open;
        Progress = 0;
        ActualEndDate = null;
    }

    public void MarkOverdue()
    {
        if (Status is ProjectTaskStatus.Open or ProjectTaskStatus.Working)
            Status = ProjectTaskStatus.Overdue;
    }

    /// <summary>
    /// Updates the task workflow status with validation.
    /// Supports Kanban board movements across lifecycle states.
    /// </summary>
    public void SetStatus(ProjectTaskStatus newStatus)
    {
        if (Status == newStatus) return;

        switch (newStatus)
        {
            case ProjectTaskStatus.Open:
                Reopen();
                break;
            case ProjectTaskStatus.Working:
                Start();
                break;
            case ProjectTaskStatus.PendingReview:
                SendForReview();
                break;
            case ProjectTaskStatus.Completed:
                Complete();
                break;
            case ProjectTaskStatus.Cancelled:
                Cancel();
                break;
            case ProjectTaskStatus.Overdue:
                MarkOverdue();
                break;
            default:
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        }
    }
}
