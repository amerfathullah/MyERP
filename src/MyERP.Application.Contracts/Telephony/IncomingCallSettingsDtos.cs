using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Telephony;

public class IncomingCallHandlingScheduleDto : EntityDto<Guid>
{
    public Guid IncomingCallSettingsId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid EmployeeGroupId { get; set; }
}

public class IncomingCallSettingsDto : FullAuditedEntityDto<Guid>
{
    public CallRoutingMode CallRouting { get; set; }
    public string? GreetingMessage { get; set; }
    public string? AgentBusyMessage { get; set; }
    public string? AgentUnavailableMessage { get; set; }
    public List<IncomingCallHandlingScheduleDto> Schedules { get; set; } = new();
}

public class CreateUpdateIncomingCallScheduleDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid EmployeeGroupId { get; set; }
}

public class UpdateIncomingCallSettingsDto
{
    public CallRoutingMode CallRouting { get; set; }

    [StringLength(TelephonyConsts.MaxMessageLength)]
    public string? GreetingMessage { get; set; }

    [StringLength(TelephonyConsts.MaxMessageLength)]
    public string? AgentBusyMessage { get; set; }

    [StringLength(TelephonyConsts.MaxMessageLength)]
    public string? AgentUnavailableMessage { get; set; }

    public List<CreateUpdateIncomingCallScheduleDto> Schedules { get; set; } = new();
}
