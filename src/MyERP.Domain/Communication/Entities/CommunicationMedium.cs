using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Communication.Entities;

/// <summary>
/// Communication Medium — configures support/sales communication channels (Voice, Email, Chat),
/// telephony/email providers, and active operational timeslots routed to Employee Groups with a fallback catch-all.
/// Maps to ERPNext communication/doctype/communication_medium.
/// </summary>
public class CommunicationMedium : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public CommunicationMediumType CommunicationMediumType { get; set; }
    public string? CommunicationChannel { get; set; }
    public Guid? CatchAllEmployeeGroupId { get; set; }
    public Guid? ProviderSupplierId { get; set; }
    public bool IsDisabled { get; set; }

    private readonly List<CommunicationMediumTimeslot> _timeslots = new();
    public IReadOnlyList<CommunicationMediumTimeslot> Timeslots => _timeslots.AsReadOnly();

    protected CommunicationMedium() { }

    public CommunicationMedium(
        Guid id,
        CommunicationMediumType communicationMediumType,
        string? communicationChannel = null,
        Guid? catchAllEmployeeGroupId = null,
        Guid? providerSupplierId = null,
        bool isDisabled = false,
        Guid? tenantId = null) : base(id)
    {
        CommunicationMediumType = communicationMediumType;
        CommunicationChannel = communicationChannel;
        CatchAllEmployeeGroupId = catchAllEmployeeGroupId;
        ProviderSupplierId = providerSupplierId;
        IsDisabled = isDisabled;
        TenantId = tenantId;
    }

    public void AddTimeslot(DayOfWeek dayOfWeek, TimeSpan fromTime, TimeSpan toTime, Guid employeeGroupId)
    {
        if (fromTime >= toTime)
        {
            throw new BusinessException("MyERP:Comm:001", "FromTime must be earlier than ToTime.");
        }

        if (employeeGroupId == Guid.Empty)
        {
            throw new BusinessException("MyERP:Comm:002", "EmployeeGroupId is required for timeslot.");
        }

        _timeslots.Add(new CommunicationMediumTimeslot(Guid.NewGuid(), Id, dayOfWeek, fromTime, toTime, employeeGroupId));
    }

    public void ClearTimeslots()
    {
        _timeslots.Clear();
    }

    /// <summary>
    /// Resolves the handling employee group for incoming communication at the given day and time.
    /// Returns matching timeslot's EmployeeGroupId if within schedule, or CatchAllEmployeeGroupId fallback.
    /// </summary>
    public Guid? GetHandlingEmployeeGroup(DayOfWeek dayOfWeek, TimeSpan time)
    {
        var slot = _timeslots.FirstOrDefault(t => t.DayOfWeek == dayOfWeek && time >= t.FromTime && time <= t.ToTime);
        return slot?.EmployeeGroupId ?? CatchAllEmployeeGroupId;
    }
}

/// <summary>
/// Operating hours timeslot for a communication medium.
/// Maps to ERPNext communication/doctype/communication_medium_timeslot.
/// </summary>
public class CommunicationMediumTimeslot : Entity<Guid>
{
    public Guid CommunicationMediumId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan FromTime { get; set; }
    public TimeSpan ToTime { get; set; }
    public Guid EmployeeGroupId { get; set; }

    protected CommunicationMediumTimeslot() { }

    public CommunicationMediumTimeslot(
        Guid id,
        Guid communicationMediumId,
        DayOfWeek dayOfWeek,
        TimeSpan fromTime,
        TimeSpan toTime,
        Guid employeeGroupId) : base(id)
    {
        CommunicationMediumId = communicationMediumId;
        DayOfWeek = dayOfWeek;
        FromTime = fromTime;
        ToTime = toTime;
        EmployeeGroupId = employeeGroupId;
    }
}
