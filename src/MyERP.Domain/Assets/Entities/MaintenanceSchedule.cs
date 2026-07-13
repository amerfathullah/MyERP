using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// Maintenance Schedule — planned preventive maintenance calendar for assets/serial numbers.
/// Auto-generates visit dates based on periodicity (Monthly, Quarterly, etc.).
/// </summary>
public class MaintenanceSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Asset or item being maintained.</summary>
    public Guid? AssetId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? SerialNoId { get; set; }

    /// <summary>Customer whose asset is being maintained (for warranty/AMC).</summary>
    public Guid? CustomerId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Frequency: Monthly, Quarterly, Half Yearly, Yearly.</summary>
    public string Periodicity { get; set; } = "Quarterly";

    /// <summary>Assigned maintenance person.</summary>
    public Guid? SalesPersonId { get; set; }

    public MaintenanceScheduleStatus Status { get; private set; } = MaintenanceScheduleStatus.Draft;

    private readonly List<MaintenanceScheduleDetail> _details = new();
    public IReadOnlyList<MaintenanceScheduleDetail> Details => _details.AsReadOnly();

    protected MaintenanceSchedule() { }

    public MaintenanceSchedule(Guid id, Guid companyId, DateTime startDate, DateTime endDate,
        string periodicity, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        StartDate = startDate;
        EndDate = endDate;
        Periodicity = periodicity;
        TenantId = tenantId;
    }

    public void AddDetail(MaintenanceScheduleDetail detail)
    {
        _details.Add(detail);
    }

    public void Submit()
    {
        if (Status != MaintenanceScheduleStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = MaintenanceScheduleStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == MaintenanceScheduleStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = MaintenanceScheduleStatus.Cancelled;
    }
}

/// <summary>Individual scheduled maintenance visit date.</summary>
public class MaintenanceScheduleDetail : Entity<Guid>
{
    public Guid MaintenanceScheduleId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }

    protected MaintenanceScheduleDetail() { }
    public MaintenanceScheduleDetail(Guid id, Guid scheduleId, DateTime scheduledDate)
        : base(id)
    {
        MaintenanceScheduleId = scheduleId;
        ScheduledDate = scheduledDate;
    }
}

public enum MaintenanceScheduleStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2,
}
