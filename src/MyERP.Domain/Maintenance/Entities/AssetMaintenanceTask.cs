using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Maintenance.Entities;

public class AssetMaintenanceTask : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetMaintenanceId { get; set; }

    public string MaintenanceTask { get; set; } = null!;
    public MaintenancePeriodicity Periodicity { get; set; } = MaintenancePeriodicity.Monthly;

    public string? MaintenanceType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public DateTime? NextDueDate { get; set; }
    public DateTime? LastCompletionDate { get; set; }

    public AssetMaintenanceStatus MaintenanceStatus { get; set; } = AssetMaintenanceStatus.Planned;

    public Guid? AssignToEmployeeId { get; set; }
    public string? AssignTo { get; set; }
    public string? AssignToName { get; set; }
    public bool CertificateRequired { get; set; }
    public string? Description { get; set; }
    public string? CertificateNo { get; set; }

    protected AssetMaintenanceTask() { }

    public AssetMaintenanceTask(
        Guid id,
        Guid assetMaintenanceId,
        string maintenanceTask,
        MaintenancePeriodicity periodicity,
        DateTime startDate,
        DateTime? nextDueDate = null,
        DateTime? endDate = null)
        : base(id)
    {
        AssetMaintenanceId = assetMaintenanceId;
        MaintenanceTask = Check.NotNullOrWhiteSpace(maintenanceTask, nameof(maintenanceTask), AssetMaintenanceConsts.MaxTaskNameLength);
        Periodicity = periodicity;
        StartDate = startDate;
        EndDate = endDate;
        NextDueDate = nextDueDate ?? CalculateNextDueDate(periodicity, startDate, endDate);
        MaintenanceStatus = AssetMaintenanceStatus.Planned;
    }

    /// <summary>
    /// Calculates next due date respecting periodicity, start/end dates, and last completion date.
    /// Per ERPNext PR #59998 / commit ea529f57d2 & 4375048459.
    /// </summary>
    public static DateTime? CalculateNextDueDate(
        MaintenancePeriodicity periodicity,
        DateTime startDate,
        DateTime? endDate = null,
        DateTime? lastCompletionDate = null)
    {
        var fromDate = lastCompletionDate ?? startDate;
        DateTime next = periodicity switch
        {
            MaintenancePeriodicity.Daily => fromDate.AddDays(1),
            MaintenancePeriodicity.Weekly => fromDate.AddDays(7),
            MaintenancePeriodicity.Monthly => fromDate.AddMonths(1),
            MaintenancePeriodicity.Quarterly => fromDate.AddMonths(3),
            MaintenancePeriodicity.HalfYearly => fromDate.AddMonths(6),
            MaintenancePeriodicity.Yearly => fromDate.AddYears(1),
            MaintenancePeriodicity.TwoYearly => fromDate.AddYears(2),
            MaintenancePeriodicity.ThreeYearly => fromDate.AddYears(3),
            MaintenancePeriodicity.Random => fromDate,
            _ => fromDate.AddMonths(1),
        };

        if (endDate.HasValue)
        {
            if (startDate.Date >= endDate.Value.Date ||
                (lastCompletionDate.HasValue && lastCompletionDate.Value.Date >= endDate.Value.Date) ||
                next.Date > endDate.Value.Date)
            {
                return null;
            }
        }

        return next;
    }

    /// <summary>
    /// Updates maintenance task status based on next due date relative to as-of date.
    /// Per ERPNext PR #59998 / commit 9ecd9425fb.
    /// </summary>
    public void SetStatus(DateTime asOfDate)
    {
        if (MaintenanceStatus == AssetMaintenanceStatus.Cancelled)
            return;

        if (NextDueDate.HasValue && NextDueDate.Value.Date < asOfDate.Date)
        {
            MaintenanceStatus = AssetMaintenanceStatus.Overdue;
        }
        else if (MaintenanceStatus == AssetMaintenanceStatus.Overdue)
        {
            MaintenanceStatus = AssetMaintenanceStatus.Planned;
        }
    }

    public void UpdateOnCompletion(DateTime completionDate)
    {
        LastCompletionDate = completionDate;
        if (Periodicity != MaintenancePeriodicity.Random)
        {
            NextDueDate = CalculateNextDueDate(Periodicity, StartDate, EndDate, completionDate);
        }
        if (NextDueDate.HasValue)
        {
            SetStatus(DateTime.UtcNow.Date);
        }
    }
}
