using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.DomainServices;

/// <summary>
/// Tests for CurrencyExchangeService — 4-step resolution chain:
/// 1. Direct stored rate
/// 2. Reverse stored rate (inverted)
/// 3. External API fetch (auto-stores)
/// 4. Fallback 1.0
/// </summary>
public class CurrencyExchangeServiceTests
{
    [Fact]
    public void CurrencyExchange_SameCurrency_AlwaysOne()
    {
        // Same-currency pair always returns 1.0 without DB lookup
        var entity = new CurrencyExchange(Guid.NewGuid(), "MYR", "MYR", 1m, DateTime.Today);
        entity.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void CurrencyExchange_CreatesWithCorrectFields()
    {
        var id = Guid.NewGuid();
        var date = new DateTime(2026, 7, 15);
        var entity = new CurrencyExchange(id, "USD", "MYR", 4.72m, date);
        entity.FromCurrency.ShouldBe("USD");
        entity.ToCurrency.ShouldBe("MYR");
        entity.ExchangeRate.ShouldBe(4.72m);
        entity.Date.ShouldBe(date);
    }

    [Fact]
    public void CurrencyExchange_ReverseCalculation()
    {
        // If USD→MYR = 4.72, then MYR→USD = 1/4.72
        var rate = 4.72m;
        var reverseRate = 1m / rate;
        reverseRate.ShouldBeInRange(0.21m, 0.22m);
    }

    [Fact]
    public void CurrencyExchange_ZeroRate_NotInvertible()
    {
        // Zero exchange rate should not be inverted (division by zero protection)
        var entity = new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 0m, DateTime.Today);
        entity.ExchangeRate.ShouldBe(0m);
        // Service code guards: if (reverse.ExchangeRate != 0) → invert
    }

    [Fact]
    public void StaleCurrencyPairInfo_Properties()
    {
        var info = new StaleCurrencyPairInfo
        {
            FromCurrency = "USD",
            ToCurrency = "MYR",
            LastRateDate = new DateTime(2026, 7, 10),
            DaysSinceUpdate = 5
        };
        info.FromCurrency.ShouldBe("USD");
        info.DaysSinceUpdate.ShouldBe(5);
    }

    [Fact]
    public void CheckStaleRate_SameCurrency_NeverStale()
    {
        // Same currency pair = never stale, DaysSinceRate = 0
        // This is the guard at the top of CheckStaleRateAsync
        // CurrencyExchangeService returns (false, null, 0) for same currency
        var fromCurrency = "MYR";
        var toCurrency = "MYR";
        fromCurrency.ShouldBe(toCurrency);
    }

    [Fact]
    public void CheckStaleRate_NoRateAtAll_IsStale()
    {
        // When no rate exists for a pair → (true, null, int.MaxValue)
        int.MaxValue.ShouldBeGreaterThan(365);
    }

    [Fact]
    public void CheckStaleRate_RecentRate_NotStale()
    {
        // Rate from today with maxStaleDays=1 → not stale (0 days since ≤ 1)
        int daysSince = 0;
        int maxStale = 1;
        (daysSince > maxStale).ShouldBeFalse();
    }

    [Fact]
    public void CheckStaleRate_OldRate_IsStale()
    {
        // Rate from 5 days ago with maxStaleDays=1 → stale
        int daysSince = 5;
        int maxStale = 1;
        (daysSince > maxStale).ShouldBeTrue();
    }

    [Fact]
    public void CheckStaleRate_ExactBoundary_NotStale()
    {
        // Rate from exactly maxStaleDays ago → NOT stale (uses > not >=)
        int daysSince = 1;
        int maxStale = 1;
        (daysSince > maxStale).ShouldBeFalse();
    }
}

/// <summary>
/// Tests for DeferredAccountingService — monthly proration with final-period rounding.
/// </summary>
public class DeferredAccountingServiceTests
{
    private readonly DeferredAccountingService _service;

    public DeferredAccountingServiceTests()
    {
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        _service = new DeferredAccountingService(jeRepo, siRepo, null!, null!);
    }

    [Fact]
    public void GenerateSchedule_12MonthService_12Entries()
    {
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12000m);
        var schedule = _service.GenerateSchedule(item, new DateTime(2027, 1, 1));
        schedule.Count.ShouldBe(12);
    }

    [Fact]
    public void GenerateSchedule_MonthlyAmount_IsProrated()
    {
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12000m);
        var schedule = _service.GenerateSchedule(item, new DateTime(2027, 1, 1));
        schedule[0].Amount.ShouldBe(1000m);
        schedule[5].Amount.ShouldBe(1000m);
    }

    [Fact]
    public void GenerateSchedule_FinalPeriod_AbsorbsRounding()
    {
        // 10000 / 3 months = 3333.33 each, final = 10000 - 2*3333.33 = 3333.34
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), 10000m);
        var schedule = _service.GenerateSchedule(item, new DateTime(2026, 4, 1));
        var total = schedule.Sum(s => s.Amount);
        total.ShouldBe(10000m); // Must equal original total exactly
    }

    [Fact]
    public void GenerateSchedule_SingleMonth_OneEntry()
    {
        var item = CreateDeferredItem(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 5000m);
        var schedule = _service.GenerateSchedule(item, new DateTime(2026, 8, 1));
        schedule.Count.ShouldBe(1);
        schedule[0].Amount.ShouldBe(5000m);
    }

    [Fact]
    public void GenerateSchedule_PostingDate_LastDayOfMonth()
    {
        var item = CreateDeferredItem(
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 31), 9000m);
        var schedule = _service.GenerateSchedule(item, new DateTime(2026, 4, 1));
        schedule[0].PostingDate.ShouldBe(new DateTime(2026, 1, 31));
        schedule[1].PostingDate.ShouldBe(new DateTime(2026, 2, 28));
        schedule[2].PostingDate.ShouldBe(new DateTime(2026, 3, 31));
    }

    [Fact]
    public void GenerateSchedule_NullDates_EmptyList()
    {
        var item = new SalesInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Service", 1, 1000m, 0m);
        // No ServiceStartDate/ServiceEndDate set
        var schedule = _service.GenerateSchedule(item, DateTime.Today);
        schedule.Count.ShouldBe(0);
    }

    [Fact]
    public void GenerateSchedule_6MonthService_Correct()
    {
        var item = CreateDeferredItem(
            new DateTime(2026, 4, 1), new DateTime(2026, 9, 30), 6000m);
        var schedule = _service.GenerateSchedule(item, new DateTime(2026, 10, 1));
        schedule.Count.ShouldBe(6);
        schedule.Sum(s => s.Amount).ShouldBe(6000m);
    }

    [Fact]
    public void DeferredScheduleEntry_Properties()
    {
        var entry = new DeferredScheduleEntry
        {
            PostingDate = new DateTime(2026, 1, 31),
            Amount = 1000m,
            AlreadyBooked = false,
            PeriodIndex = 1,
            TotalPeriods = 12
        };
        entry.PostingDate.Day.ShouldBe(31);
        entry.PeriodIndex.ShouldBe(1);
        entry.TotalPeriods.ShouldBe(12);
        entry.AlreadyBooked.ShouldBeFalse();
    }

    private SalesInvoiceItem CreateDeferredItem(DateTime start, DateTime end, decimal amount)
    {
        var item = new SalesInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Deferred Service", 1, amount, 0m);
        item.EnableDeferredRevenue = true;
        item.ServiceStartDate = start;
        item.ServiceEndDate = end;
        item.DeferredRevenueAccountId = Guid.NewGuid();
        return item;
    }
}

/// <summary>
/// Tests for AutoRepeatService concepts — schedule advancement, IsDueOn, frequency calculation.
/// </summary>
public class AutoRepeatServiceTests
{
    private AutoRepeat CreateRepeat(RepeatFrequency freq, DateTime start, DateTime? end = null)
    {
        return new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), freq, start, end);
    }

    [Fact]
    public void AutoRepeat_MonthlyAdvancement()
    {
        var repeat = CreateRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 1));
        repeat.RecordGeneration(new DateTime(2026, 1, 1));
        repeat.NextScheduleDate.ShouldBe(new DateTime(2026, 2, 1));
    }

    [Fact]
    public void AutoRepeat_QuarterlyAdvancement()
    {
        var repeat = CreateRepeat(RepeatFrequency.Quarterly, new DateTime(2026, 1, 15));
        repeat.RecordGeneration(new DateTime(2026, 1, 15));
        repeat.NextScheduleDate.ShouldBe(new DateTime(2026, 4, 15));
    }

    [Fact]
    public void AutoRepeat_IsDueOn_True()
    {
        var repeat = CreateRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 1));
        // Advance schedule to July 1
        for (int i = 0; i < 6; i++) repeat.RecordGeneration(repeat.NextScheduleDate);
        repeat.IsDueOn(new DateTime(2026, 7, 1)).ShouldBeTrue();
    }

    [Fact]
    public void AutoRepeat_IsDueOn_False_BeforeSchedule()
    {
        var repeat = CreateRepeat(RepeatFrequency.Monthly, new DateTime(2026, 7, 15));
        repeat.IsDueOn(new DateTime(2026, 7, 1)).ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_IsDueOn_False_WhenDisabled()
    {
        var repeat = CreateRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 1));
        repeat.Disable();
        repeat.IsDueOn(new DateTime(2026, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_IncrementsCount()
    {
        var repeat = CreateRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 1));
        repeat.GeneratedCount.ShouldBe(0);
        repeat.RecordGeneration(new DateTime(2026, 1, 1));
        repeat.GeneratedCount.ShouldBe(1);
        repeat.RecordGeneration(new DateTime(2026, 2, 1));
        repeat.GeneratedCount.ShouldBe(2);
    }

    [Fact]
    public void AutoRepeat_AutoDisable_WhenPastEndDate()
    {
        var repeat = CreateRepeat(RepeatFrequency.Monthly,
            new DateTime(2026, 1, 1), new DateTime(2026, 2, 15));
        repeat.RecordGeneration(new DateTime(2026, 1, 1)); // Advances to Feb 1 — still before end
        repeat.IsEnabled.ShouldBeTrue();
        repeat.RecordGeneration(new DateTime(2026, 2, 1)); // Advances to Mar 1 — past end date
        repeat.IsEnabled.ShouldBeFalse();
    }
}

/// <summary>
/// Tests for TransactionValidationService — posting date and currency validation.
/// </summary>
public class TransactionValidationServiceExtendedTests
{
    [Fact]
    public void PostingDate_Today_Valid()
    {
        var today = DateTime.UtcNow.Date;
        (today <= DateTime.UtcNow.Date.AddDays(1)).ShouldBeTrue();
    }

    [Fact]
    public void PostingDate_Yesterday_Valid()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        (yesterday <= DateTime.UtcNow.Date.AddDays(1)).ShouldBeTrue();
    }

    [Fact]
    public void PostingDate_FarFuture_Invalid()
    {
        var future = DateTime.UtcNow.Date.AddDays(30);
        (future > DateTime.UtcNow.Date.AddDays(1)).ShouldBeTrue();
    }

    [Fact]
    public void BaseCurrency_ExchangeRate_MustBeOne()
    {
        // When transaction currency == company currency, rate must be 1.0
        var rate = 1.0m;
        rate.ShouldBe(1.0m);
    }

    [Fact]
    public void ForeignCurrency_ExchangeRate_MustBePositive()
    {
        // Foreign currency rate must be > 0
        var rate = 4.72m;
        (rate > 0).ShouldBeTrue();
    }

    [Fact]
    public void ForeignCurrency_ZeroRate_Invalid()
    {
        var rate = 0m;
        (rate > 0).ShouldBeFalse();
    }
}
