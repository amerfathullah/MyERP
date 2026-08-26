using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Telephony.Entities;

/// <summary>
/// Incoming Call Settings — singleton/per-tenant incoming call routing configuration, IVR greeting,
/// and weekly agent group schedule.
/// Maps to ERPNext telephony/doctype/incoming_call_settings.
/// </summary>
public class IncomingCallSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public CallRoutingMode CallRouting { get; set; } = CallRoutingMode.Sequential;
    public string? GreetingMessage { get; set; }
    public string? AgentBusyMessage { get; set; }
    public string? AgentUnavailableMessage { get; set; }

    private readonly List<IncomingCallHandlingSchedule> _schedules = new();
    public IReadOnlyList<IncomingCallHandlingSchedule> Schedules => _schedules.AsReadOnly();

    protected IncomingCallSettings() { }

    public IncomingCallSettings(
        Guid id,
        CallRoutingMode callRouting = CallRoutingMode.Sequential,
        string? greetingMessage = null,
        string? agentBusyMessage = null,
        string? agentUnavailableMessage = null,
        Guid? tenantId = null)
        : base(id)
    {
        CallRouting = callRouting;
        GreetingMessage = greetingMessage;
        AgentBusyMessage = agentBusyMessage;
        AgentUnavailableMessage = agentUnavailableMessage;
        TenantId = tenantId;
    }

    public void AddSchedule(DayOfWeek dayOfWeek, TimeSpan fromTime, TimeSpan toTime, Guid employeeGroupId)
    {
        if (fromTime >= toTime)
        {
            throw new BusinessException("MyERP:Telephony:002", "FromTime must be earlier than ToTime in call schedule.");
        }

        if (employeeGroupId == Guid.Empty)
        {
            throw new BusinessException("MyERP:Telephony:003", "EmployeeGroupId is required.");
        }

        _schedules.Add(new IncomingCallHandlingSchedule(Guid.NewGuid(), Id, dayOfWeek, fromTime, toTime, employeeGroupId));
    }

    public void ClearSchedules()
    {
        _schedules.Clear();
    }

    public Guid? GetActiveEmployeeGroup(DayOfWeek dayOfWeek, TimeSpan time)
    {
        var match = _schedules.FirstOrDefault(s => s.DayOfWeek == dayOfWeek && time >= s.FromTime && time <= s.ToTime);
        return match?.EmployeeGroupId;
    }
}

/// <summary>
/// Child schedule row for incoming call routing.
/// Maps to ERPNext telephony/doctype/incoming_call_handling_schedule.
/// </summary>
public class IncomingCallHandlingSchedule : Entity<Guid>
{
    public Guid IncomingCallSettingsId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid EmployeeGroupId { get; set; }

    protected IncomingCallHandlingSchedule() { }

    public IncomingCallHandlingSchedule(
        Guid id,
        Guid incomingCallSettingsId,
        DayOfWeek dayOfWeek,
        TimeSpan fromTime,
        TimeSpan toTime,
        Guid employeeGroupId)
        : base(id)
    {
        IncomingCallSettingsId = incomingCallSettingsId;
        DayOfWeek = dayOfWeek;
        FromTime = fromTime;
        ToTime = toTime;
        EmployeeGroupId = employeeGroupId;
    }
}
