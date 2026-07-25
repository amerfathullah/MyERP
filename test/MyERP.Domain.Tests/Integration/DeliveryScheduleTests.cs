using System;
using System.Linq;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for Delivery Schedule generation (SO delivery planning).
/// Per ERPNext gotcha #108: SO has a dialog to create frequency-based split deliveries
/// with whole-number UOM enforcement.
/// </summary>
public class DeliveryScheduleTests
{
    private static DeliveryScheduleService CreateService() => new();

    // --- Monthly Schedule ---

    [Fact]
    public void Monthly_EvenSplit_4Months()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 100m,
            startDate: new DateTime(2026, 1, 15),
            endDate: new DateTime(2026, 4, 15),
            frequency: DeliveryFrequency.Monthly);

        entries.Count.ShouldBe(4);
        entries[0].ScheduledQty.ShouldBe(25m);
        entries[1].ScheduledQty.ShouldBe(25m);
        entries[2].ScheduledQty.ShouldBe(25m);
        entries[3].ScheduledQty.ShouldBe(25m);
        entries.Sum(e => e.ScheduledQty).ShouldBe(100m);
    }

    [Fact]
    public void Monthly_UnevenSplit_LastAbsorbsRemainder()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 100m,
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 3, 1), // 3 months
            frequency: DeliveryFrequency.Monthly);

        entries.Count.ShouldBe(3);
        // 100/3 = 33.333... → last absorbs remainder
        entries[0].ScheduledQty.ShouldBe(100m / 3m);
        entries[2].ScheduledQty.ShouldBe(100m - (100m / 3m) * 2); // Absorbs rounding
        entries.Sum(e => e.ScheduledQty).ShouldBe(100m);
    }

    // --- Weekly Schedule ---

    [Fact]
    public void Weekly_4Weeks()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 28m,
            startDate: new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 6, 22), // ~3 weeks span
            frequency: DeliveryFrequency.Weekly);

        entries.Count.ShouldBe(4); // Jun 1, 8, 15, 22
        entries[0].ScheduledQty.ShouldBe(7m);
        entries.Sum(e => e.ScheduledQty).ShouldBe(28m);
    }

    // --- Quarterly Schedule ---

    [Fact]
    public void Quarterly_FullYear()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 1200m,
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 12, 31),
            frequency: DeliveryFrequency.Quarterly);

        entries.Count.ShouldBe(4); // Q1, Q2, Q3, Q4
        entries[0].ScheduledQty.ShouldBe(300m);
        entries.Sum(e => e.ScheduledQty).ShouldBe(1200m);
    }

    // --- Whole Number UOM Enforcement ---

    [Fact]
    public void WholeNumber_FloorPerDelivery_LastAbsorbs()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 10m,
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 3, 1), // 3 deliveries
            frequency: DeliveryFrequency.Monthly,
            mustBeWholeNumber: true);

        entries.Count.ShouldBe(3);
        entries[0].ScheduledQty.ShouldBe(3m); // Floor(10/3) = 3
        entries[1].ScheduledQty.ShouldBe(3m);
        entries[2].ScheduledQty.ShouldBe(4m); // Last absorbs: 10 - 6 = 4
        entries.Sum(e => e.ScheduledQty).ShouldBe(10m);
    }

    // --- Entity Properties ---

    [Fact]
    public void DeliveryScheduleEntry_Defaults()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, 50m);

        entry.ScheduledQty.ShouldBe(50m);
        entry.DeliveredQty.ShouldBe(0m);
        entry.PendingQty.ShouldBe(50m);
        entry.IsFullyDelivered.ShouldBeFalse();
    }

    [Fact]
    public void RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, 100m);

        entry.RecordDelivery(40m);
        entry.DeliveredQty.ShouldBe(40m);
        entry.PendingQty.ShouldBe(60m);
        entry.IsFullyDelivered.ShouldBeFalse();

        entry.RecordDelivery(60m);
        entry.DeliveredQty.ShouldBe(100m);
        entry.PendingQty.ShouldBe(0m);
        entry.IsFullyDelivered.ShouldBeTrue();
    }

    [Fact]
    public void RecordDelivery_PendingNeverNegative()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, 10m);

        entry.RecordDelivery(15m); // Over-deliver
        entry.PendingQty.ShouldBe(0m); // Math.Max(0, ...) prevents negative
        entry.IsFullyDelivered.ShouldBeTrue();
    }

    // --- Date Generation ---

    [Fact]
    public void SingleDelivery_StartEqualsEnd()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 50m,
            startDate: new DateTime(2026, 3, 15),
            endDate: new DateTime(2026, 3, 15), // Same day
            frequency: DeliveryFrequency.Monthly);

        entries.Count.ShouldBe(1);
        entries[0].ScheduledQty.ShouldBe(50m);
        entries[0].ScheduledDate.ShouldBe(new DateTime(2026, 3, 15));
    }

    [Fact]
    public void ScheduleDates_AreCorrect_Monthly()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 30m,
            startDate: new DateTime(2026, 1, 31),
            endDate: new DateTime(2026, 4, 30),
            frequency: DeliveryFrequency.Monthly);

        entries.Count.ShouldBe(4);
        entries[0].ScheduledDate.ShouldBe(new DateTime(2026, 1, 31));
        entries[1].ScheduledDate.Month.ShouldBe(2); // Feb 28/29
        entries[2].ScheduledDate.Month.ShouldBe(3); // Mar 28 (from Feb 28 + 1 month)
    }

    // --- Yearly ---

    [Fact]
    public void Yearly_MultiYear()
    {
        var service = CreateService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            totalQty: 500m,
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2028, 1, 1),
            frequency: DeliveryFrequency.Yearly);

        entries.Count.ShouldBe(3); // 2026, 2027, 2028
        entries.Sum(e => e.ScheduledQty).ShouldBe(500m);
    }
}
