using System;
using System.Collections.Generic;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class DeferredAccountingTests
{
    private static SalesInvoiceItem CreateDeferredItem(
        DateTime startDate, DateTime endDate, decimal amount = 12000m)
    {
        var item = new SalesInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Annual Support", 1, amount, 0);
        item.EnableDeferredRevenue = true;
        item.DeferredRevenueAccountId = Guid.NewGuid();
        item.ServiceStartDate = startDate;
        item.ServiceEndDate = endDate;
        return item;
    }

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
        var service = new DeferredAccountingService(null!, null!);
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12000m);

        var schedule = service.GenerateSchedule(item, new DateTime(2027, 1, 1));

        schedule.Count.ShouldBe(12);
    }

    [Fact]
    public void DeferredSchedule_MonthlyAmount_EvenDistribution()
    {
        var service = new DeferredAccountingService(null!, null!);
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12000m);

        var schedule = service.GenerateSchedule(item, new DateTime(2027, 1, 1));

        // 12000 / 12 = 1000 per month
        schedule[0].Amount.ShouldBe(1000m);
        schedule[5].Amount.ShouldBe(1000m);
    }

    [Fact]
    public void DeferredSchedule_FinalPeriod_AbsorbsRounding()
    {
        var service = new DeferredAccountingService(null!, null!);
        // 10000 / 3 = 3333.33... → last period absorbs remainder
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), 10000m);

        var schedule = service.GenerateSchedule(item, new DateTime(2026, 4, 1));

        var total = 0m;
        foreach (var entry in schedule)
            total += entry.Amount;

        // Total must equal exact item amount (no rounding loss)
        total.ShouldBe(10000m);
    }

    [Fact]
    public void DeferredSchedule_PostingDate_LastDayOfMonth()
    {
        var service = new DeferredAccountingService(null!, null!);
        var item = CreateDeferredItem(
            new DateTime(2026, 2, 1), new DateTime(2026, 4, 30), 3000m);

        var schedule = service.GenerateSchedule(item, new DateTime(2026, 5, 1));

        schedule[0].PostingDate.ShouldBe(new DateTime(2026, 2, 28));
        schedule[1].PostingDate.ShouldBe(new DateTime(2026, 3, 31));
        schedule[2].PostingDate.ShouldBe(new DateTime(2026, 4, 30));
    }

    [Fact]
    public void DeferredSchedule_SingleMonth_SingleEntry()
    {
        var service = new DeferredAccountingService(null!, null!);
        var item = CreateDeferredItem(
            new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 5000m);

        var schedule = service.GenerateSchedule(item, new DateTime(2026, 7, 1));

        schedule.Count.ShouldBe(1);
        schedule[0].Amount.ShouldBe(5000m);
    }

    [Fact]
    public void DeferredSchedule_NullDates_EmptySchedule()
    {
        var service = new DeferredAccountingService(null!, null!);
        var item = new SalesInvoiceItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "No dates", 1, 1000, 0);
        item.EnableDeferredRevenue = true;

        var schedule = service.GenerateSchedule(item, DateTime.Today);
        schedule.ShouldBeEmpty();
    }
}
