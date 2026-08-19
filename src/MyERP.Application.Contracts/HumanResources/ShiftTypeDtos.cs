using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class ShiftTypeDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Guid? HolidayListId { get; set; }
}

public class CreateShiftTypeDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Guid? HolidayListId { get; set; }
}
