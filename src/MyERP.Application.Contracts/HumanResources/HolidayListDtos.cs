using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class HolidayListDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public int Year { get; set; }
    public string? WeeklyOff { get; set; }
    public bool IsDefault { get; set; }
    public HolidayDto[] Holidays { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class HolidayDto
{
    public Guid Id { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;
    public bool IsWeeklyOff { get; set; }
}

public class CreateHolidayListDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public int Year { get; set; }
    public string? WeeklyOff { get; set; }
    public bool IsDefault { get; set; }
    public CreateHolidayDto[] Holidays { get; set; } = [];
}

public class CreateHolidayDto
{
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;
    public bool IsWeeklyOff { get; set; }
}
