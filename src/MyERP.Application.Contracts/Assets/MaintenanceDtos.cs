using System;
using Volo.Abp.Application.Dtos;
using MyERP.Maintenance;

namespace MyERP.Assets;

public class MaintenanceScheduleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Periodicity { get; set; } = null!;
    public int Status { get; set; }
    public MaintenanceScheduleDetailDto[] Details { get; set; } = [];
}

public class MaintenanceScheduleDetailDto
{
    public Guid Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class CreateMaintenanceScheduleDto
{
    public Guid CompanyId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Periodicity { get; set; } = "Quarterly";
}

// --- Maintenance Visit DTOs ---

public class MaintenanceVisitDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime VisitDate { get; set; }
    public string MaintenanceType { get; set; } = null!;
    public Guid? MaintenanceScheduleId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ContactId { get; set; }
    public MaintenanceVisitStatus CompletionStatus { get; set; }
    public MaintenanceVisitPurposeDto[] Purposes { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class MaintenanceVisitPurposeDto
{
    public Guid Id { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? SerialNoId { get; set; }
    public string WorkDone { get; set; } = null!;
    public string? WorkDetails { get; set; }
}

public class CreateMaintenanceVisitDto
{
    public Guid CompanyId { get; set; }
    public DateTime VisitDate { get; set; }
    public string MaintenanceType { get; set; } = "Scheduled";
    public Guid? MaintenanceScheduleId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? ContactId { get; set; }
    public CreateMaintenanceVisitPurposeDto[] Purposes { get; set; } = [];
}

public class CreateMaintenanceVisitPurposeDto
{
    public Guid? ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? SerialNoId { get; set; }
    public string WorkDone { get; set; } = null!;
    public string? WorkDetails { get; set; }
}

public class GetMaintenanceVisitListDto : PagedAndSortedResultRequestDto
{
    public MaintenanceVisitStatus? CompletionStatus { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public string? MaintenanceType { get; set; }
    public Guid? CustomerId { get; set; }
}
