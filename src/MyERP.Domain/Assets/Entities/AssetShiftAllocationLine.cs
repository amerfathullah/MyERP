using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

/// <summary>
/// One reassigned period within an Asset Shift Allocation.
/// Amount/AccumulatedDepreciation are populated as a result snapshot when the allocation is submitted.
/// </summary>
public class AssetShiftAllocationLine : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid AssetShiftAllocationId { get; set; }

    /// <summary>The existing depreciation schedule period being reassigned.</summary>
    public Guid ScheduleEntryId { get; set; }

    public Guid ShiftFactorId { get; set; }

    public DateTime ScheduleDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }

    protected AssetShiftAllocationLine() { }

    public AssetShiftAllocationLine(
        Guid id,
        Guid assetShiftAllocationId,
        Guid scheduleEntryId,
        Guid shiftFactorId,
        DateTime scheduleDate,
        Guid? tenantId = null)
        : base(id)
    {
        AssetShiftAllocationId = assetShiftAllocationId;
        ScheduleEntryId = scheduleEntryId;
        ShiftFactorId = shiftFactorId;
        ScheduleDate = scheduleDate;
        TenantId = tenantId;
    }
}
