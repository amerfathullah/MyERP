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
    public decimal TotalConsumedMaterialCost { get; set; }
    public decimal GrossMargin { get; set; }
    public string? Notes { get; set; }
    public Guid? CostCenterId { get; set; }
    public bool CollectProgress { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
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

    public bool CollectProgress { get; set; }
    [StringLength(255)]
    public string? Subject { get; set; }
    [StringLength(4000)]
    public string? Message { get; set; }

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

    public bool CollectProgress { get; set; }
    [StringLength(255)]
    public string? Subject { get; set; }
    [StringLength(4000)]
    public string? Message { get; set; }
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
    public Guid? TaskTypeId { get; set; }
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
    public Guid? TaskTypeId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsMilestone { get; set; }
    public decimal? TaskWeight { get; set; }
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
    public Guid? TaskTypeId { get; set; }
    public bool IsGroup { get; set; }
    public bool IsMilestone { get; set; }
    public decimal? TaskWeight { get; set; }
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
    public string? Title { get; set; }
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
    public decimal BillingHours { get; set; }
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
    public string? Title { get; set; }
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

// --- Project Summary & Delayed Tasks Summary Reports (ERPNext projects/report) ---
public class ProjectSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public ProjectStatus? Status { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public bool? IsActive { get; set; }
}

public class ProjectSummaryRowDto
{
    public Guid ProjectId { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectTypeName { get; set; }
    public ProjectStatus Status { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public decimal PercentComplete { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
}

public class ProjectSummaryReportDto
{
    public int TotalProjects { get; set; }
    public int OpenProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public List<ProjectSummaryRowDto> Rows { get; set; } = new();
}

public class DelayedTasksSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? ProjectId { get; set; }
    public ProjectPriority? Priority { get; set; }
    public ProjectTaskStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class DelayedTaskSummaryRowDto
{
    public Guid TaskId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public ProjectTaskStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public decimal Progress { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public int DelayInDays { get; set; }
}

public class DelayedTasksSummaryReportDto
{
    public int TotalTasks { get; set; }
    public int DelayedCount { get; set; }
    public int OnTrackCount { get; set; }
    public List<DelayedTaskSummaryRowDto> Rows { get; set; } = new();
}

// --- Daily Timesheet Summary & Timesheet Billing Summary Reports ---
public class DailyTimesheetSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? ProjectId { get; set; }
}

public class DailyTimesheetSummaryRowDto
{
    public Guid TimesheetId { get; set; }
    public string TimesheetLabel { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal Hours { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public string? TaskNumber { get; set; }
    public string? TaskSubject { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectNumber { get; set; }
    public string? ProjectName { get; set; }
    public TimesheetStatus Status { get; set; }
}

public class DailyTimesheetSummaryReportDto
{
    public decimal TotalHours { get; set; }
    public int EntryCount { get; set; }
    public List<DailyTimesheetSummaryRowDto> Rows { get; set; } = new();
}

public class TimesheetBillingSummaryRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool IncludeDraftTimesheets { get; set; }
}

public class TimesheetBillingSummaryRowDto
{
    public DateTime Date { get; set; }
    public Guid TimesheetId { get; set; }
    public string TimesheetLabel { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? ProjectNumber { get; set; }
    public string? ProjectName { get; set; }
    public decimal WorkingHours { get; set; }
    public decimal BillingHours { get; set; }
    public decimal BillingAmount { get; set; }
}

public class TimesheetBillingSummaryReportDto
{
    public string CurrencyCode { get; set; } = "MYR";
    public decimal TotalWorkingHours { get; set; }
    public decimal TotalBillingHours { get; set; }
    public decimal TotalBillingAmount { get; set; }
    public List<TimesheetBillingSummaryRowDto> Rows { get; set; } = new();
}

// --- Project-wise Stock Tracking Report (ERPNext projects/report/project_wise_stock_tracking) ---
public class ProjectWiseStockTrackingRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? ProjectId { get; set; }
    public ProjectStatus? Status { get; set; }
}

public class ProjectWiseStockTrackingRowDto
{
    public Guid ProjectId { get; set; }
    public string ProjectNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal CostOfPurchasedItems { get; set; }
    public decimal CostOfIssuedItems { get; set; }
    public decimal CostOfDeliveredItems { get; set; }
    public decimal EstimatedCost { get; set; }
    public DateTime? ExpectedStartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }
}

public class ProjectWiseStockTrackingReportDto
{
    public string CurrencyCode { get; set; } = "MYR";
    public decimal TotalPurchasedCost { get; set; }
    public decimal TotalIssuedCost { get; set; }
    public decimal TotalDeliveredCost { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public List<ProjectWiseStockTrackingRowDto> Rows { get; set; } = new();
}
