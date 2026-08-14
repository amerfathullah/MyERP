using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Quality Meeting — management reviews, quality committee meetings with agendas and minutes.
/// </summary>
public class QualityMeeting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public DateTime MeetingDate { get; set; }
    public string? Chairperson { get; set; }
    public string? Attendees { get; set; }
    public QualityMeetingStatus Status { get; private set; } = QualityMeetingStatus.Open;

    private readonly List<QualityMeetingAgenda> _agendas = new();
    public IReadOnlyList<QualityMeetingAgenda> Agendas => _agendas.AsReadOnly();

    private readonly List<QualityMeetingMinutes> _minutes = new();
    public IReadOnlyList<QualityMeetingMinutes> Minutes => _minutes.AsReadOnly();

    protected QualityMeeting() { }

    public QualityMeeting(Guid id, Guid companyId, DateTime meetingDate, string? chairperson = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        MeetingDate = meetingDate;
        Chairperson = chairperson;
        Status = QualityMeetingStatus.Open;
        TenantId = tenantId;
    }

    public void AddAgenda(QualityMeetingAgenda agenda)
    {
        _agendas.Add(agenda);
    }

    public void ClearAgendas()
    {
        _agendas.Clear();
    }

    public void AddMinute(QualityMeetingMinutes minute)
    {
        _minutes.Add(minute);
    }

    public void ClearMinutes()
    {
        _minutes.Clear();
    }

    public void Close()
    {
        Status = QualityMeetingStatus.Closed;
    }

    public void Reopen()
    {
        Status = QualityMeetingStatus.Open;
    }
}

public class QualityMeetingAgenda : Entity<Guid>
{
    public Guid QualityMeetingId { get; set; }
    public string Agenda { get; set; } = null!;

    protected QualityMeetingAgenda() { }

    public QualityMeetingAgenda(Guid id, Guid meetingId, string agenda)
        : base(id)
    {
        QualityMeetingId = meetingId;
        Agenda = Check.NotNullOrWhiteSpace(agenda, nameof(agenda), maxLength: QualityManagementConsts.MaxDescriptionLength);
    }
}

public class QualityMeetingMinutes : Entity<Guid>
{
    public Guid QualityMeetingId { get; set; }
    public string Discussion { get; set; } = null!;
    public string? ActionPlan { get; set; }
    public Guid? AssignedUserId { get; set; }

    protected QualityMeetingMinutes() { }

    public QualityMeetingMinutes(Guid id, Guid meetingId, string discussion, string? actionPlan = null, Guid? assignedUserId = null)
        : base(id)
    {
        QualityMeetingId = meetingId;
        Discussion = Check.NotNullOrWhiteSpace(discussion, nameof(discussion), maxLength: QualityManagementConsts.MaxMinutesLength);
        ActionPlan = actionPlan;
        AssignedUserId = assignedUserId;
    }
}
