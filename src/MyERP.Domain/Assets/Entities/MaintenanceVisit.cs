using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Maintenance Visit — record of an actual maintenance visit performed.
/// Can be linked to a Maintenance Schedule or standalone.
/// Tracks completion status, work done, and breakdown resolution.
/// </summary>
public class MaintenanceVisit : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public DateTime VisitDate { get; set; }

    /// <summary>Type: Scheduled, Unscheduled, Breakdown.</summary>
    public string MaintenanceType { get; set; } = "Scheduled";

    /// <summary>Linked maintenance schedule (if this is a scheduled visit).</summary>
    public Guid? MaintenanceScheduleId { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? ContactId { get; set; }

    /// <summary>Overall completion status.</summary>
    public MaintenanceVisitStatus CompletionStatus { get; private set; } = MaintenanceVisitStatus.Open;

    private readonly List<MaintenanceVisitPurpose> _purposes = new();
    public IReadOnlyList<MaintenanceVisitPurpose> Purposes => _purposes.AsReadOnly();

    protected MaintenanceVisit() { }

    public MaintenanceVisit(Guid id, Guid companyId, DateTime visitDate, string maintenanceType, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        VisitDate = visitDate;
        MaintenanceType = maintenanceType;
        TenantId = tenantId;
    }

    public void AddPurpose(MaintenanceVisitPurpose purpose)
    {
        _purposes.Add(purpose);
    }

    public void Complete()
    {
        if (CompletionStatus == MaintenanceVisitStatus.Completed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        CompletionStatus = MaintenanceVisitStatus.Completed;
    }

    public void PartiallyComplete()
    {
        CompletionStatus = MaintenanceVisitStatus.PartiallyCompleted;
    }

    public void Cancel()
    {
        if (CompletionStatus == MaintenanceVisitStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        CompletionStatus = MaintenanceVisitStatus.Cancelled;
    }
}

/// <summary>Purpose/work item within a maintenance visit.</summary>
public class MaintenanceVisitPurpose : Entity<Guid>
{
    public Guid MaintenanceVisitId { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? SerialNoId { get; set; }
    public string WorkDone { get; set; } = null!;
    public string? WorkDetails { get; set; }

    protected MaintenanceVisitPurpose() { }
    public MaintenanceVisitPurpose(Guid id, Guid visitId, string workDone)
        : base(id)
    {
        MaintenanceVisitId = visitId;
        WorkDone = workDone;
    }
}

public enum MaintenanceVisitStatus
{
    Open = 0,
    PartiallyCompleted = 1,
    Completed = 2,
    Cancelled = 3,
}
