using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Communication;

public class CommunicationMediumTimeslotDto : EntityDto<Guid>
{
    public Guid CommunicationMediumId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid EmployeeGroupId { get; set; }
}

public class CommunicationMediumDto : FullAuditedEntityDto<Guid>
{
    public CommunicationMediumType CommunicationMediumType { get; set; }
    public string? CommunicationChannel { get; set; }
    public Guid? CatchAllEmployeeGroupId { get; set; }
    public Guid? ProviderSupplierId { get; set; }
    public bool IsDisabled { get; set; }
    public List<CommunicationMediumTimeslotDto> Timeslots { get; set; } = new();
}

public class CreateUpdateCommunicationMediumTimeslotDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid EmployeeGroupId { get; set; }
}

public class CreateUpdateCommunicationMediumDto
{
    public CommunicationMediumType CommunicationMediumType { get; set; }

    [StringLength(CommunicationMediumConsts.MaxCommunicationChannelLength)]
    public string? CommunicationChannel { get; set; }

    public Guid? CatchAllEmployeeGroupId { get; set; }
    public Guid? ProviderSupplierId { get; set; }
    public bool IsDisabled { get; set; }
    public List<CreateUpdateCommunicationMediumTimeslotDto> Timeslots { get; set; } = new();
}

public class GetCommunicationMediumListDto : PagedAndSortedResultRequestDto
{
    public CommunicationMediumType? CommunicationMediumType { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}
