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

    public void Start()
    {
        if (Status is not (ProjectTaskStatus.Open or ProjectTaskStatus.Overdue))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = ProjectTaskStatus.Working;
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
        if (Status is not (ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled))
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
}
