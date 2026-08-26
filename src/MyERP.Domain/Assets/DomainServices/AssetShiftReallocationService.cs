using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Assets.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Assets.DomainServices;

/// <summary>
/// Reallocates shift factors across an asset's unbooked depreciation periods.
/// Per ERPNext asset_shift_allocation.py: a booked (journaled) period's shift can never change.
/// Unlike ERPNext (which adds/removes trailing periods to absorb the shift), this conserves the
/// total remaining depreciable value by redistributing the delta across the OTHER unassigned
/// unbooked periods, weighted by their baseline amount — the period count never changes.
/// </summary>
public class AssetShiftReallocationService : DomainService
{
    /// <summary>
    /// Applies requested shift factors and rebalances the remaining unbooked schedule so the
    /// total (still unconsumed) depreciable value is unchanged.
    /// </summary>
    /// <param name="unbookedEntriesOrdered">Unbooked schedule entries for one asset+finance book, ordered by ScheduleDate.</param>
    /// <param name="factorById">Lookup of AssetShiftFactor.Id → Factor.</param>
    /// <param name="requestedFactorByEntryId">Which schedule entries are being reassigned, and to which shift factor.</param>
    /// <param name="accumulatedBeforeFirstUnbooked">AccumulatedDepreciation as of the last booked period (0 if none).</param>
    public void Reallocate(
        List<DepreciationScheduleEntry> unbookedEntriesOrdered,
        Dictionary<Guid, decimal> factorById,
        Dictionary<Guid, Guid> requestedFactorByEntryId,
        decimal accumulatedBeforeFirstUnbooked)
    {
        if (unbookedEntriesOrdered.Count == 0) return;

        var assignedIds = new HashSet<Guid>();
        var delta = 0m;

        foreach (var entry in unbookedEntriesOrdered)
        {
            if (!requestedFactorByEntryId.TryGetValue(entry.Id, out var shiftFactorId)) continue;
            if (!factorById.TryGetValue(shiftFactorId, out var factor)) continue;

            var before = entry.DepreciationAmount;
            entry.ApplyShift(shiftFactorId, factor);
            delta += entry.DepreciationAmount - before;
            assignedIds.Add(entry.Id);
        }

        if (delta != 0)
        {
            var unassigned = unbookedEntriesOrdered.Where(e => !assignedIds.Contains(e.Id)).ToList();
            if (unassigned.Count == 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AssetShiftInsufficientUnassignedPeriods)
                    .WithData("reason", "At least one unbooked period must remain unassigned to absorb the shift change.");
            }

            var weightSum = unassigned.Sum(e => e.BaseDepreciationAmount);
            var appliedAdjustment = 0m;

            for (var i = 0; i < unassigned.Count; i++)
            {
                var entry = unassigned[i];
                decimal adjustment;
                if (i == unassigned.Count - 1)
                {
                    // Last unassigned period absorbs the rounding remainder (conserves total exactly).
                    adjustment = -delta - appliedAdjustment;
                }
                else
                {
                    var weight = weightSum > 0 ? entry.BaseDepreciationAmount / weightSum : 1m / unassigned.Count;
                    adjustment = Math.Round(-delta * weight, 2);
                    appliedAdjustment += adjustment;
                }

                entry.DepreciationAmount = Math.Max(0, entry.DepreciationAmount + adjustment);
            }
        }

        var running = accumulatedBeforeFirstUnbooked;
        foreach (var entry in unbookedEntriesOrdered)
        {
            running += entry.DepreciationAmount;
            entry.AccumulatedDepreciation = running;
        }
    }
}
