using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

/// <summary>
/// Recognition schedule of a deferred row, per ERPNext deferred_revenue.py: one entry per month of
/// service, dated at the month end, the last one dated at the service end (or stop) date and
/// taking whatever is left.
/// </summary>
public class DeferredAccountingTests
{
    private static System.Collections.Generic.List<DeferredBookingPeriod> Schedule(
        DateTime start, DateTime end, decimal amount, string basedOn = "Days", DateTime? stop = null)
        => DeferredAccountingService.ProjectSchedule(start, end, stop, amount, amount, basedOn, end);

    [Fact]
    public void SalesInvoiceItem_DeferredFields_DefaultFalse()
    {
        var item = new SalesInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 5, 100, 0);
        item.EnableDeferredRevenue.ShouldBeFalse();
        item.DeferredRevenueAccountId.ShouldBeNull();
        item.ServiceStartDate.ShouldBeNull();
        item.ServiceEndDate.ShouldBeNull();
    }

    [Fact]
    public void DeferredSchedule_12MonthService_Generates12Entries()
    {
        Schedule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12000m).Count.ShouldBe(12);
    }

    [Fact]
    public void DeferredSchedule_Months_EvenDistribution()
    {
        var schedule = Schedule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12000m, "Months");

        // 12000 / 12 = 1000 per month, whatever the month's length
        schedule[0].Amount.ShouldBe(1000m);
        schedule[1].Amount.ShouldBe(1000m);
        schedule[5].Amount.ShouldBe(1000m);
    }

    [Fact]
    public void DeferredSchedule_Days_FollowsTheLengthOfEachMonth()
    {
        var schedule = Schedule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 36500m);

        schedule[0].Amount.ShouldBe(3100m); // January, 31 of 365 days
        schedule[1].Amount.ShouldBe(2800m); // February, 28 of 365 days
    }

    [Theory]
    [InlineData("Days")]
    [InlineData("Months")]
    public void DeferredSchedule_FinalPeriod_AbsorbsRounding(string basedOn)
    {
        var schedule = Schedule(new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), 10000m, basedOn);

        // Total must equal exact item amount (no rounding loss)
        schedule.Sum(entry => entry.Amount).ShouldBe(10000m);
        schedule.Last().IsLast.ShouldBeTrue();
    }

    [Fact]
    public void DeferredSchedule_PostingDate_LastDayOfMonth()
    {
        var schedule = Schedule(new DateTime(2026, 2, 1), new DateTime(2026, 4, 30), 3000m);

        schedule[0].End.ShouldBe(new DateTime(2026, 2, 28));
        schedule[1].End.ShouldBe(new DateTime(2026, 3, 31));
        schedule[2].End.ShouldBe(new DateTime(2026, 4, 30));
    }

    [Fact]
    public void DeferredSchedule_SingleMonth_SingleEntry()
    {
        var schedule = Schedule(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 5000m);

        schedule.Count.ShouldBe(1);
        schedule[0].Amount.ShouldBe(5000m);
    }

    [Fact]
    public void DeferredSchedule_SingleDayService_GeneratesSingleEntryWithExactPostingDate()
    {
        var sameDay = new DateTime(2026, 4, 15);
        var schedule = Schedule(sameDay, sameDay, 500m);

        schedule.Count.ShouldBe(1);
        schedule[0].Amount.ShouldBe(500m);
        schedule[0].End.ShouldBe(sameDay);
    }

    [Fact]
    public void DeferredSchedule_MidMonthServiceEnd_LastPeriodPostingDateIsServiceEndDate()
    {
        var schedule = Schedule(new DateTime(2026, 4, 1), new DateTime(2026, 6, 15), 3000m);

        schedule.Count.ShouldBe(3);
        schedule[0].End.ShouldBe(new DateTime(2026, 4, 30));
        schedule[1].End.ShouldBe(new DateTime(2026, 5, 31));
        // Per ERPNext deferred_revenue.py: last posting date is clamped to service end date
        schedule[2].End.ShouldBe(new DateTime(2026, 6, 15));
        schedule.Sum(entry => entry.Amount).ShouldBe(3000m);
    }

    [Fact]
    public void DeferredSchedule_MidMonthServiceStart_FirstPeriodStartsOnServiceStartDate()
    {
        var schedule = Schedule(new DateTime(2026, 4, 16), new DateTime(2026, 6, 30), 7600m);

        schedule[0].Start.ShouldBe(new DateTime(2026, 4, 16));
        schedule[0].End.ShouldBe(new DateTime(2026, 4, 30));
        schedule[0].Amount.ShouldBe(1500m); // 15 of 76 days
    }

    [Fact]
    public void DeferredSchedule_BookedOnlyUpToThePostingDate()
    {
        var schedule = DeferredAccountingService.ProjectSchedule(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), null, 12000m, 12000m, "Months", new DateTime(2026, 3, 31));

        schedule.Count.ShouldBe(3);
        schedule.ShouldAllBe(entry => !entry.IsLast);
    }
}
