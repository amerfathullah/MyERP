using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Assets.Entities;

public class DepreciationScheduleEntry : FullAuditedEntity<Guid>
{
    public Guid AssetId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public bool IsBooked { get; set; }
    public Guid? JournalEntryId { get; set; }

    /// <summary>
    /// Finance Book this schedule belongs to (null = default/primary book).
    /// Enables per-book depreciation: e.g., straight-line for tax, WDV for management.
    /// Per DO-NOT: "Skip Finance Book separation in GL entries"
    /// </summary>
    public Guid? FinanceBookId { get; set; }

    /// <summary>
    /// Shift factor currently applied to this period (null = single-shift / no shift adjustment).
    /// Set via Asset Shift Allocation. Per ERPNext: only unbooked periods may have their shift changed.
    /// </summary>
    public Guid? ShiftFactorId { get; set; }

    /// <summary>
    /// The period's depreciation amount before any shift factor was applied.
    /// Preserved so shift reallocation can always recompute from the original baseline.
    /// </summary>
    public decimal BaseDepreciationAmount { get; set; }

    protected DepreciationScheduleEntry() { }

    public DepreciationScheduleEntry(Guid id, Guid assetId, DateTime scheduleDate,
        decimal depreciationAmount, decimal accumulatedDepreciation)
        : base(id)
    {
        AssetId = assetId;
        ScheduleDate = scheduleDate;
        DepreciationAmount = depreciationAmount;
        BaseDepreciationAmount = depreciationAmount;
        AccumulatedDepreciation = accumulatedDepreciation;
    }

    public void Book(Guid journalEntryId)
    {
        IsBooked = true;
        JournalEntryId = journalEntryId;
    }

    /// <summary>Applies a shift factor, recomputing this period's amount from its baseline.</summary>
    public void ApplyShift(Guid? shiftFactorId, decimal factor)
    {
        ShiftFactorId = shiftFactorId;
        DepreciationAmount = Math.Round(BaseDepreciationAmount * factor, 2);
    }
}
