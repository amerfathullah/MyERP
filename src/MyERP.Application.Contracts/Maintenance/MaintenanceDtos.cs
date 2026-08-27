using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Maintenance;

// ─── Maintenance Schedule DTOs ───

public class MaintenanceScheduleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ScheduleNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? SalesOrderId { get; set; }
    public string? SalesOrderNumber { get; set; }
    public int Status { get; set; }
    public List<MaintenanceScheduleItemDto> Items { get; set; } = new();
    public List<MaintenanceScheduleDetailDto> ScheduleDetails { get; set; } = new();
}

public class MaintenanceScheduleItemDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? SerialNoId { get; set; }
    public Guid? SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int NoOfVisits { get; set; }
    public int Periodicity { get; set; }
}

public class MaintenanceScheduleDetailDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public Guid? SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public int Status { get; set; }
}

public class CreateMaintenanceScheduleDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    public Guid? SalesOrderId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? AddressId { get; set; }

    [Required]
    public List<CreateMaintenanceScheduleItemDto> Items { get; set; } = new();
}

public class CreateMaintenanceScheduleItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    public Guid? SerialNoId { get; set; }
    public Guid? SalesPersonId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Range(1, 365)]
    public int NoOfVisits { get; set; } = 1;

    public MaintenancePeriodicity Periodicity { get; set; } = MaintenancePeriodicity.Monthly;
}

public class GetMaintenanceScheduleListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CustomerId { get; set; }
    public int? Status { get; set; }
}

// ─── Maintenance Visit DTOs ───

public class MaintenanceVisitDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string VisitNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int MaintenanceType { get; set; }
    public DateTime VisitDate { get; set; }
    public int CompletionStatus { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public string? MaintenanceScheduleNumber { get; set; }
    public Guid? WarrantyClaimId { get; set; }
    public string? WarrantyClaimNumber { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsCancelled { get; set; }
    public List<MaintenanceVisitPurposeDto> Purposes { get; set; } = new();
}

public class MaintenanceVisitPurposeDto : EntityDto<Guid>
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? SerialNoId { get; set; }
    public string? WorkDone { get; set; }
    public int Status { get; set; }
    public Guid? ServicePersonId { get; set; }
    public string? ServicePersonName { get; set; }
}

public class CreateMaintenanceVisitDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    public Guid? ContactId { get; set; }
    public Guid? AddressId { get; set; }

    [Required]
    public DateTime VisitDate { get; set; }

    /// <summary>0=Scheduled, 1=Unscheduled, 2=Breakdown</summary>
    public int MaintenanceType { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public Guid? MaintenanceScheduleDetailId { get; set; }
    public Guid? WarrantyClaimId { get; set; }

    [Required]
    public List<CreateMaintenanceVisitPurposeDto> Purposes { get; set; } = new();
}

public class CreateMaintenanceVisitPurposeDto
{
    [Required]
    public Guid ItemId { get; set; }

    public Guid? SerialNoId { get; set; }
    public Guid? ServicePersonId { get; set; }

    [StringLength(MaintenanceConsts.MaxWorkDoneLength)]
    public string? WorkDone { get; set; }

    /// <summary>0=Pending, 1=PartiallyCompleted, 2=FullyCompleted</summary>
    public int Status { get; set; }
}

public class GetMaintenanceVisitListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public int? MaintenanceType { get; set; }
}

public class MakeMaintenanceVisitInput
{
    public Guid? ScheduleDetailId { get; set; }
    public DateTime? VisitDate { get; set; }
    public int? MaintenanceType { get; set; } // 0=Scheduled, 1=Unscheduled, 2=Breakdown
}

public class MaintenanceScheduleSummaryDto
{
    public Guid ScheduleId { get; set; }
    public int TotalVisits { get; set; }
    public int CompletedVisits { get; set; }
    public int PendingVisits { get; set; }
    public decimal CompletionPercentage { get; set; }
    public DateTime? NextScheduledDate { get; set; }
}
