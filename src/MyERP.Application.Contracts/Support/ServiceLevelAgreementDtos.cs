using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Support;

public class ServiceLevelAgreementDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? HolidayListId { get; set; }
    public int ResolutionTimeHours { get; set; }
    public int ResponseTimeHours { get; set; }
    public bool IsDefault { get; set; }
    public bool ApplyOnResolution { get; set; }
    public bool IsActive { get; set; }
    public List<ServiceLevelPriorityDto> Priorities { get; set; } = new();
    public List<ServiceDayDto> ServiceDays { get; set; } = new();
}

public class ServiceLevelPriorityDto
{
    public Guid Id { get; set; }
    public string PriorityName { get; set; } = null!;
    public decimal ResponseTimeHours { get; set; }
    public decimal ResolutionTimeHours { get; set; }
    public bool IsDefault { get; set; }
}

public class ServiceDayDto
{
    public Guid Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class CreateServiceLevelAgreementDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(ServiceLevelAgreementConsts.MaxNameLength)] public string Name { get; set; } = null!;
    [StringLength(ServiceLevelAgreementConsts.MaxEntityTypeLength)] public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? HolidayListId { get; set; }
    public int ResolutionTimeHours { get; set; }
    public int ResponseTimeHours { get; set; }
    public bool IsDefault { get; set; }
    public bool ApplyOnResolution { get; set; } = true;
    public List<CreateServiceLevelPriorityDto> Priorities { get; set; } = new();
    public List<CreateServiceDayDto> ServiceDays { get; set; } = new();
}

public class CreateServiceLevelPriorityDto
{
    [Required][StringLength(ServiceLevelPriorityConsts.MaxPriorityNameLength)] public string PriorityName { get; set; } = null!;
    public decimal ResponseTimeHours { get; set; }
    public decimal ResolutionTimeHours { get; set; }
    public bool IsDefault { get; set; }
}

public class CreateServiceDayDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

public class GetServiceLevelAgreementListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
}
