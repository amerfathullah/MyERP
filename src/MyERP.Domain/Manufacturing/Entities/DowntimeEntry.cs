using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Downtime Entry — records unplanned/planned stoppage of a workstation for OEE tracking.
/// Downtime (minutes) is derived from FromTime/ToTime.
/// Maps to ERPNext manufacturing/doctype/downtime_entry.
/// </summary>
public class DowntimeEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid WorkstationId { get; set; }
    public Guid OperatorId { get; set; }

    public DateTime FromTime { get; private set; }
    public DateTime ToTime { get; private set; }

    /// <summary>Downtime duration in minutes, derived from FromTime/ToTime.</summary>
    public decimal DowntimeMinutes { get; private set; }

    public string StopReason { get; set; } = null!;
    public string? Remarks { get; set; }

    protected DowntimeEntry() { }

    public DowntimeEntry(Guid id, Guid companyId, Guid workstationId, Guid operatorId,
        DateTime fromTime, DateTime toTime, string stopReason, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        WorkstationId = workstationId;
        OperatorId = operatorId;
        StopReason = Check.NotNullOrWhiteSpace(stopReason, nameof(stopReason), 100);
        TenantId = tenantId;
        SetTimeRange(fromTime, toTime);
    }

    public void SetTimeRange(DateTime fromTime, DateTime toTime)
    {
        if (toTime < fromTime)
            throw new BusinessException(MyERPDomainErrorCodes.DowntimeEntryToTimeBeforeFromTime);

        FromTime = fromTime;
        ToTime = toTime;
        DowntimeMinutes = (decimal)(toTime - fromTime).TotalMinutes;
    }
}
