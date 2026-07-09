using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Projects;

// === Project DTOs ===

public class ProjectDto : AuditedEntityDto<Guid>
{
    public string ProjectNumber { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public ProjectStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public PercentCompleteMethod PercentCompleteMethod { get; set; }
    public decimal PercentComplete { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal TotalCostingAmount { get; set; }
    public decimal TotalBillingAmount { get; set; }
    public decimal TotalBilledAmount { get; set; }
    public decimal GrossMargin { get; set; }
    public string? Notes { get; set; }
    public int TaskCount { get; set; }
}

public class CreateProjectDto
{
    [Required]
    [StringLength(ProjectConsts.MaxProjectNameLength)]
    public string ProjectName { get; set; } = null!;

    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public PercentCompleteMethod PercentCompleteMethod { get; set; } = PercentCompleteMethod.TaskCompletion;

    [Required]
    public Guid CompanyId { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public decimal EstimatedCost { get; set; }

    [StringLength(ProjectConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class UpdateProjectDto
{
    [Required]
    [StringLength(ProjectConsts.MaxProjectNameLength)]
    public string ProjectName { get; set; } = null!;

    public ProjectPriority Priority { get; set; }
    public PercentCompleteMethod PercentCompleteMethod { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public decimal EstimatedCost { get; set; }

    [StringLength(ProjectConsts.MaxNoteLength)]
    public string? Notes { get; set; }
}

public class GetProjectListDto : PagedAndSortedResultRequestDto
{
    public ProjectStatus? Status { get; set; }
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
}

// === Task DTOs ===

public class ProjectTaskDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string TaskNumber { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public ProjectTaskStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public Guid? ParentTaskId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsMilestone { get; set; }
    public decimal TaskWeight { get; set; }
    public decimal Progress { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public decimal ExpectedHours { get; set; }
    public decimal ActualHours { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? Description { get; set; }
}

public class CreateProjectTaskDto
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [StringLength(ProjectTaskConsts.MaxSubjectLength)]
    public string Subject { get; set; } = null!;

    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public Guid? ParentTaskId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsMilestone { get; set; }
    public decimal TaskWeight { get; set; } = 1;
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public decimal ExpectedHours { get; set; }
    public Guid? AssignedUserId { get; set; }

    [StringLength(ProjectTaskConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public class UpdateProjectTaskDto
{
    [Required]
    [StringLength(ProjectTaskConsts.MaxSubjectLength)]
    public string Subject { get; set; } = null!;

    public ProjectPriority Priority { get; set; }
    public Guid? ParentTaskId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsMilestone { get; set; }
    public decimal TaskWeight { get; set; }
    public decimal Progress { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public decimal ExpectedHours { get; set; }
    public Guid? AssignedUserId { get; set; }

    [StringLength(ProjectTaskConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}
