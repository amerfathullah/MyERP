using System;
using MyERP.Core.BackgroundJobs;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Core;

public class RecurringInvoiceDateShiftingTests
{
    [Fact]
    public void CalculateNextPeriod_Monthly_StandardDates()
    {
        var refFrom = new DateTime(2026, 1, 1);
        var refTo = new DateTime(2026, 1, 31);
        var postingDate = new DateTime(2026, 2, 1);

        var (newFrom, newTo) = RecurringInvoiceJob.CalculateNextPeriod(refFrom, refTo, postingDate, RepeatFrequency.Monthly);

        newFrom.ShouldBe(new DateTime(2026, 2, 1));
        newTo.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void CalculateNextPeriod_Monthly_EndOfMonthPreserved()
    {
        // 31 Jan -> end of Feb (28th in 2026)
        var refFrom = new DateTime(2026, 1, 1);
        var refTo = new DateTime(2026, 1, 31);
        var postingDate = new DateTime(2026, 2, 1);

        var (newFrom, newTo) = RecurringInvoiceJob.CalculateNextPeriod(refFrom, refTo, postingDate, RepeatFrequency.Monthly);

        newFrom.ShouldBe(new DateTime(2026, 2, 1));
        newTo.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void CalculateNextPeriod_Monthly_MidMonthPeriod()
    {
        var refFrom = new DateTime(2026, 1, 15);
        var refTo = new DateTime(2026, 2, 14);
        var postingDate = new DateTime(2026, 2, 15);

        var (newFrom, newTo) = RecurringInvoiceJob.CalculateNextPeriod(refFrom, refTo, postingDate, RepeatFrequency.Monthly);

        newFrom.ShouldBe(new DateTime(2026, 2, 15));
        newTo.ShouldBe(new DateTime(2026, 3, 14));
    }

    [Fact]
    public void CalculateNextPeriod_Daily_AdvancesByStep()
    {
        var refFrom = new DateTime(2026, 1, 1);
        var refTo = new DateTime(2026, 1, 1);
        var postingDate = new DateTime(2026, 1, 2);

        var (newFrom, newTo) = RecurringInvoiceJob.CalculateNextPeriod(refFrom, refTo, postingDate, RepeatFrequency.Daily);

        newFrom.ShouldBe(new DateTime(2026, 1, 2));
        newTo.ShouldBe(new DateTime(2026, 1, 2));
    }

    [Fact]
    public void ShiftDate_WhenDateEqualsRefToDate_ReturnsTargetToDate()
    {
        // PR #60256 rule: if date == reference_to_date, shifted date is strictly target to_date
        var refFrom = new DateTime(2026, 1, 1);
        var refTo = new DateTime(2026, 1, 31);
        var targetFrom = new DateTime(2026, 2, 1);
        var targetTo = new DateTime(2026, 2, 28);

        var shifted = RecurringInvoiceJob.ShiftDate(
            new DateTime(2026, 1, 31),
            refFrom, refTo,
            targetFrom, targetTo,
            RepeatFrequency.Monthly);

        shifted.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void ShiftDate_WhenDateInsidePeriod_ShiftsByMonthsAndClamps()
    {
        var refFrom = new DateTime(2026, 1, 1);
        var refTo = new DateTime(2026, 1, 31);
        var targetFrom = new DateTime(2026, 2, 1);
        var targetTo = new DateTime(2026, 2, 28);

        // Date inside period: Jan 15 -> Feb 15
        var shiftedMid = RecurringInvoiceJob.ShiftDate(
            new DateTime(2026, 1, 15),
            refFrom, refTo,
            targetFrom, targetTo,
            RepeatFrequency.Monthly);

        shiftedMid.ShouldBe(new DateTime(2026, 2, 15));

        // Date inside period near end: Jan 30 + 1 month would be Feb 28, clamped to targetTo
        var shiftedNearEnd = RecurringInvoiceJob.ShiftDate(
            new DateTime(2026, 1, 30),
            refFrom, refTo,
            targetFrom, targetTo,
            RepeatFrequency.Monthly);

        shiftedNearEnd.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void ShiftDate_DailyWeeklyFrequency_ShiftsByDays()
    {
        var refFrom = new DateTime(2026, 1, 1);
        var refTo = new DateTime(2026, 1, 7);
        var targetFrom = new DateTime(2026, 1, 8);
        var targetTo = new DateTime(2026, 1, 14);

        var shifted = RecurringInvoiceJob.ShiftDate(
            new DateTime(2026, 1, 3),
            refFrom, refTo,
            targetFrom, targetTo,
            RepeatFrequency.Weekly);

        shifted.ShouldBe(new DateTime(2026, 1, 10));
    }

    [Fact]
    public void ShiftDateByFrequency_Monthly_ShiftsMonthDifference()
    {
        var refPostingDate = new DateTime(2026, 1, 1);
        var newPostingDate = new DateTime(2026, 2, 1);
        var serviceDate = new DateTime(2026, 1, 15);

        var shifted = RecurringInvoiceJob.ShiftDateByFrequency(
            serviceDate, RepeatFrequency.Monthly, newPostingDate, refPostingDate);

        shifted.ShouldBe(new DateTime(2026, 2, 15));
    }

    [Fact]
    public void ShiftDate_OnRecurring_KeepsServiceDatesAfterPeriodEndOrdered()
    {
        // Per ERPNext PR #60296 (commit 1b709ff29a):
        // Auto Repeat moves period 31 Jan-27 Feb to 28 Feb-30 Mar
        var refFrom = new DateTime(2025, 1, 31);
        var refTo = new DateTime(2025, 2, 27);
        var targetFrom = new DateTime(2025, 2, 28);
        var targetTo = new DateTime(2025, 3, 30);

        var serviceStartDate = new DateTime(2025, 2, 27);
        var serviceEndDate = new DateTime(2025, 2, 28);

        var shiftedStart = RecurringInvoiceJob.ShiftDate(
            serviceStartDate, refFrom, refTo, targetFrom, targetTo, RepeatFrequency.Monthly);
        var shiftedEnd = RecurringInvoiceJob.ShiftDate(
            serviceEndDate, refFrom, refTo, targetFrom, targetTo, RepeatFrequency.Monthly);

        shiftedStart.ShouldBe(new DateTime(2025, 3, 30));
        shiftedEnd.ShouldBe(new DateTime(2025, 3, 31));
    }

    [Fact]
    public void ShiftDate_OnRecurring_KeepsServiceEndAtMonthEnd()
    {
        // Per ERPNext PR #60296 (commit 1b709ff29a):
        // Billing in advance: Jan invoice (01-31 Jan) covers service in Feb (01-28 Feb)
        var refFrom = new DateTime(2025, 1, 1);
        var refTo = new DateTime(2025, 1, 31);
        var targetFrom = new DateTime(2025, 2, 1);
        var targetTo = new DateTime(2025, 2, 28);

        var serviceStartDate = new DateTime(2025, 2, 1);
        var serviceEndDate = new DateTime(2025, 2, 28);

        var shiftedStart = RecurringInvoiceJob.ShiftDate(
            serviceStartDate, refFrom, refTo, targetFrom, targetTo, RepeatFrequency.Monthly);
        var shiftedEnd = RecurringInvoiceJob.ShiftDate(
            serviceEndDate, refFrom, refTo, targetFrom, targetTo, RepeatFrequency.Monthly);

        shiftedStart.ShouldBe(new DateTime(2025, 3, 1));
        shiftedEnd.ShouldBe(new DateTime(2025, 3, 31));
    }

    [Fact]
    public void ShiftItemServiceDates_OnRecurring_KeepsServiceDatesAfterPeriodEndOrdered()
    {
        // Per ERPNext PR #60328 (commit 287e8dab9c):
        // Auto Repeat moves period 30 Jan-26 Feb to 27 Feb-29 Mar.
        // Item service dates: 26 Feb - 27 Feb.
        // Start date moved to period end (29 Mar) passes end date (27 Mar),
        // so end date is adjusted to period end + 1 (30 Mar).
        var refFrom = new DateTime(2025, 1, 30);
        var refTo = new DateTime(2025, 2, 26);
        var targetFrom = new DateTime(2025, 2, 27);
        var targetTo = new DateTime(2025, 3, 29);

        var serviceStartDate = new DateTime(2025, 2, 26);
        var serviceEndDate = new DateTime(2025, 2, 27);

        var (shiftedStart, shiftedEnd) = RecurringInvoiceJob.ShiftItemServiceDates(
            serviceStartDate, serviceEndDate,
            refFrom, refTo,
            targetFrom, targetTo,
            RepeatFrequency.Monthly);

        shiftedStart.ShouldBe(new DateTime(2025, 3, 29));
        shiftedEnd.ShouldBe(new DateTime(2025, 3, 30));
    }

    [Fact]
    public void ShiftItemServiceDates_OnRecurring_KeepsShortServiceRangeAfterPeriodEnd()
    {
        // Per ERPNext PR #60328 (commit 287e8dab9c):
        // Auto Repeat builds full next month after half-month period: 01-15 Jan to 01-28 Feb.
        // Service range: 20-25 Jan (after reference period end).
        // Must stay 20-25 Feb, not pushed to 01 Mar.
        var refFrom = new DateTime(2025, 1, 1);
        var refTo = new DateTime(2025, 1, 15);
        var targetFrom = new DateTime(2025, 2, 1);
        var targetTo = new DateTime(2025, 2, 28);

        var serviceStartDate = new DateTime(2025, 1, 20);
        var serviceEndDate = new DateTime(2025, 1, 25);

        var (shiftedStart, shiftedEnd) = RecurringInvoiceJob.ShiftItemServiceDates(
            serviceStartDate, serviceEndDate,
            refFrom, refTo,
            targetFrom, targetTo,
            RepeatFrequency.Monthly);

        shiftedStart.ShouldBe(new DateTime(2025, 2, 20));
        shiftedEnd.ShouldBe(new DateTime(2025, 2, 25));
    }
}
