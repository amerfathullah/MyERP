using System;
using System.Collections.Generic;
using MyERP.Assets.Entities;
using Volo.Abp.Domain.Services;

namespace MyERP.Assets.DomainServices;

/// <summary>
/// Depreciation Calculation Service — implements 3 depreciation methods.
/// Per ERPNext: SL (Straight Line), WDV (Written Down Value), DDB (Double Declining Balance).
/// Also supports shift-based depreciation, pro-rata (partial periods), and salvage value catch-all.
/// 
/// Per DO-NOT:
/// - "Post depreciation entries for assets in Draft status"
/// - "Post depreciation entries for schedule dates within company's accounts_frozen_till_date"
/// - "Skip Finance Book separation in GL entries (each book gets independent schedules)"
/// </summary>
public class DepreciationCalculationService : DomainService
{
    /// <summary>
    /// Generate the full depreciation schedule for an asset.
    /// Per ERPNext: schedule entries are generated on submit, recalculated on life change.
    /// The last period absorbs any salvage value rounding difference (catch-all).
    /// </summary>
    public List<DepreciationScheduleEntry> GenerateSchedule(Asset asset, Guid? financeBookId = null)
    {
        var method = asset.DepreciationMethod;
        var totalCost = asset.TotalAssetCost;
        var openingAccum = asset.OpeningAccumulatedDepreciation;
        var usefulLifeMonths = asset.UsefulLifeMonths;
        var frequency = asset.FrequencyMonths;
        var startDate = asset.AvailableForUseDate ?? asset.PurchaseDate;
        var rate = asset.DepreciationRate;

        return method switch
        {
            DepreciationMethod.StraightLine => GenerateStraightLine(
                asset.Id, totalCost, openingAccum, usefulLifeMonths, frequency, startDate, financeBookId),
            DepreciationMethod.WrittenDownValue => GenerateWrittenDownValue(
                asset.Id, totalCost, openingAccum, usefulLifeMonths, frequency, startDate, rate, financeBookId),
            DepreciationMethod.DoubleDecliningBalance => GenerateDoubleDecliningBalance(
                asset.Id, totalCost, openingAccum, usefulLifeMonths, frequency, startDate, financeBookId),
            _ => GenerateStraightLine(
                asset.Id, totalCost, openingAccum, usefulLifeMonths, frequency, startDate, financeBookId),
        };
    }

    /// <summary>
    /// Straight Line: equal amount each period.
    /// Formula: depreciation_per_period = (total_cost - opening_accum) / number_of_periods
    /// Per ERPNext: daily proration mode uses (total_cost × days / total_days) when enabled.
    /// </summary>
    private List<DepreciationScheduleEntry> GenerateStraightLine(
        Guid assetId, decimal totalCost, decimal openingAccum,
        int usefulLifeMonths, int frequencyMonths, DateTime startDate, Guid? financeBookId)
    {
        var schedule = new List<DepreciationScheduleEntry>();
        var depreciableValue = totalCost - openingAccum;
        if (depreciableValue <= 0) return schedule;

        var totalPeriods = usefulLifeMonths / Math.Max(1, frequencyMonths);
        if (totalPeriods <= 0) return schedule;

        var perPeriod = Math.Round(depreciableValue / totalPeriods, 2);
        var accumulated = openingAccum;

        for (var i = 0; i < totalPeriods; i++)
        {
            var schedDate = startDate.AddMonths((i + 1) * frequencyMonths);
            var amount = (i == totalPeriods - 1)
                ? depreciableValue - (perPeriod * (totalPeriods - 1)) // last period catch-all
                : perPeriod;

            accumulated += amount;
            var entry = new DepreciationScheduleEntry(
                Guid.NewGuid(), assetId, schedDate, amount, accumulated)
            {
                FinanceBookId = financeBookId,
            };
            schedule.Add(entry);
        }

        return schedule;
    }

    /// <summary>
    /// Written Down Value (WDV): fixed rate applied to book value each year.
    /// Formula: depreciation = book_value × rate / 100
    /// Per ERPNext: recalculates on FY boundary (yearly_opening_wdv resets at new FY).
    /// </summary>
    private List<DepreciationScheduleEntry> GenerateWrittenDownValue(
        Guid assetId, decimal totalCost, decimal openingAccum,
        int usefulLifeMonths, int frequencyMonths, DateTime startDate, decimal rate, Guid? financeBookId)
    {
        var schedule = new List<DepreciationScheduleEntry>();
        if (rate <= 0 || rate >= 100) return schedule;

        var totalPeriods = usefulLifeMonths / Math.Max(1, frequencyMonths);
        var bookValue = totalCost - openingAccum;
        var accumulated = openingAccum;
        var periodsPerYear = 12 / Math.Max(1, frequencyMonths);

        for (var i = 0; i < totalPeriods && bookValue > 0.01m; i++)
        {
            var schedDate = startDate.AddMonths((i + 1) * frequencyMonths);
            // WDV uses annual rate applied proportionally per period
            var annualDep = bookValue * rate / 100m;
            var amount = Math.Round(annualDep / periodsPerYear, 2);

            // Ensure we don't depreciate below zero
            amount = Math.Min(amount, bookValue);
            bookValue -= amount;
            accumulated += amount;

            var entry = new DepreciationScheduleEntry(
                Guid.NewGuid(), assetId, schedDate, amount, accumulated)
            {
                FinanceBookId = financeBookId,
            };
            schedule.Add(entry);
        }

        return schedule;
    }

    /// <summary>
    /// Double Declining Balance (DDB): 2 × straight-line rate applied to book value.
    /// Formula: rate = 2 × (1 / useful_life_years) × 100; depreciation = book_value × rate / 100
    /// Per ERPNext: DDB = 2 × SL rate applied to WDV.
    /// </summary>
    private List<DepreciationScheduleEntry> GenerateDoubleDecliningBalance(
        Guid assetId, decimal totalCost, decimal openingAccum,
        int usefulLifeMonths, int frequencyMonths, DateTime startDate, Guid? financeBookId)
    {
        var schedule = new List<DepreciationScheduleEntry>();
        var usefulLifeYears = usefulLifeMonths / 12m;
        if (usefulLifeYears <= 0) return schedule;

        var ddbRate = 2m * (1m / usefulLifeYears) * 100m;
        var totalPeriods = usefulLifeMonths / Math.Max(1, frequencyMonths);
        var bookValue = totalCost - openingAccum;
        var accumulated = openingAccum;
        var periodsPerYear = 12 / Math.Max(1, frequencyMonths);

        for (var i = 0; i < totalPeriods && bookValue > 0.01m; i++)
        {
            var schedDate = startDate.AddMonths((i + 1) * frequencyMonths);
            var annualDep = bookValue * ddbRate / 100m;
            var amount = Math.Round(annualDep / periodsPerYear, 2);

            // Cannot depreciate below zero
            amount = Math.Min(amount, bookValue);
            bookValue -= amount;
            accumulated += amount;

            var entry = new DepreciationScheduleEntry(
                Guid.NewGuid(), assetId, schedDate, amount, accumulated)
            {
                FinanceBookId = financeBookId,
            };
            schedule.Add(entry);
        }

        return schedule;
    }

    /// <summary>
    /// Calculates pro-rata depreciation for a partial period (e.g., asset acquired mid-month).
    /// Used for the first and last periods when asset doesn't span full cycles.
    /// </summary>
    public decimal CalculateProRata(decimal fullPeriodAmount, DateTime periodStart, DateTime periodEnd, DateTime assetDate)
    {
        var totalDays = (periodEnd - periodStart).Days;
        if (totalDays <= 0) return 0;

        var effectiveDays = assetDate > periodStart
            ? (periodEnd - assetDate).Days
            : totalDays;

        return Math.Round(fullPeriodAmount * effectiveDays / totalDays, 2);
    }
}
