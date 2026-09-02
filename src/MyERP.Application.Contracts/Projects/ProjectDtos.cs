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
    public Guid? CostCenterId { get; set; }
    public int TaskCount { get; set; }
    public List<ProjectUserDto> Users { get; set; } = new();
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
    public Guid? CostCenterId { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public decimal EstimatedCost { get; set; }

    [StringLength(ProjectConsts.MaxNoteLength)]
    public string? Notes { get; set; }

    /// <summary>When set, clones the template's tasks (and their dependency edges) onto the new project.</summary>
    public Guid? ProjectTemplateId { get; set; }
}

public class UpdateProjectDto
{
    [Required]
    [StringLength(ProjectConsts.MaxProjectNameLength)]
    public string ProjectName { get; set; } = null!;

    public ProjectPriority Priority { get; set; }
    public PercentCompleteMethod PercentCompleteMethod { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CostCenterId { get; set; }
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

// === Project User DTOs ===

public class ProjectUserDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public bool ViewAttachments { get; set; }
    public bool HideTimesheets { get; set; }
}

public class AddProjectUserDto
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public bool ViewAttachments { get; set; }
    public bool HideTimesheets { get; set; }
}

public class UpdateProjectUserDto
{
    public bool ViewAttachments { get; set; }
    public bool HideTimesheets { get; set; }
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

// === Timesheet DTOs ===

public class TimesheetDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public TimesheetStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalBillableHours { get; set; }
    public decimal TotalBillingAmount { get; set; }
    public decimal TotalCostingAmount { get; set; }
    public string? Note { get; set; }
    public List<TimesheetDetailDto> Details { get; set; } = new();
}

public class TimesheetDetailDto : EntityDto<Guid>
{
    public Guid TimesheetId { get; set; }
    public string ActivityType { get; set; } = null!;
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal Hours { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public bool IsBillable { get; set; }
    public decimal BillingRate { get; set; }
    public decimal BillingAmount { get; set; }
    public decimal CostingRate { get; set; }
    public decimal CostingAmount { get; set; }
    public Guid? SalesInvoiceId { get; set; }
    public string? Description { get; set; }
}

public class CreateTimesheetDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    [Required] public DateTime StartDate { get; set; }
    [Required] public DateTime EndDate { get; set; }
    public string? Note { get; set; }
    public List<CreateTimesheetDetailDto> Details { get; set; } = new();
}

public class CreateTimesheetDetailDto
{
    [Required] public string ActivityType { get; set; } = null!;
    [Required] public DateTime FromTime { get; set; }
    [Required] public DateTime ToTime { get; set; }
    [Required] public decimal Hours { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public bool IsBillable { get; set; }
    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
    public string? Description { get; set; }
}

public class GetTimesheetListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? EmployeeId { get; set; }
    public TimesheetStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Filter { get; set; }
}

public class CreateTimesheetInvoiceDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid CustomerId { get; set; }
    public Guid? ProjectId { get; set; }
}

public class TimesheetBillingResultDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public int DetailCount { get; set; }
}

public class UnbilledTimesheetSummaryDto
{
    public string ActivityType { get; set; } = null!;
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public int EntryCount { get; set; }
}

// === Activity Type & Cost DTOs ===

public class ActivityTypeDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public decimal DefaultBillingRate { get; set; }
    public decimal DefaultCostingRate { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateActivityTypeDto
{
    [Required]
    public string Name { get; set; } = null!;
    public decimal DefaultBillingRate { get; set; }
    public decimal DefaultCostingRate { get; set; }
}

public class UpdateActivityTypeDto
{
    public decimal DefaultBillingRate { get; set; }
    public decimal DefaultCostingRate { get; set; }
    public bool IsEnabled { get; set; }
}

public class SetActivityCostDto
{
    [Required]
    public Guid EmployeeId { get; set; }
    [Required]
    public Guid ActivityTypeId { get; set; }
    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
}
