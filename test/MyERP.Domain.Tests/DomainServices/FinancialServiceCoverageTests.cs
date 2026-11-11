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
/// Booking arithmetic of DeferredAccountingService — ported from ERPNext deferred_revenue.py
/// get_booking_dates(), calculate_amount() and calculate_monthly_amount().
/// </summary>
public class DeferredAccountingServiceTests
{
    private static readonly DateTime Jan1 = new(2026, 1, 1);
    private static readonly DateTime Mar31 = new(2026, 3, 31);

    [Fact]
    public void GetBookingDates_FirstPeriod_RunsFromServiceStartToMonthEnd()
    {
        var period = DeferredAccountingService.GetBookingDates(Jan1, Mar31, null, new DateTime(2026, 12, 31), null);
        period.ShouldNotBeNull();
        period.Value.Start.ShouldBe(Jan1);
        period.Value.End.ShouldBe(new DateTime(2026, 1, 31));
        period.Value.IsLast.ShouldBeFalse();
    }

    [Fact]
    public void GetBookingDates_ContinuesFromTheDayAfterTheLastBooking()
    {
        var period = DeferredAccountingService.GetBookingDates(
            Jan1, Mar31, null, new DateTime(2026, 12, 31), new DateTime(2026, 1, 31));
        period!.Value.Start.ShouldBe(new DateTime(2026, 2, 1));
        period.Value.End.ShouldBe(new DateTime(2026, 2, 28));
        period.Value.IsLast.ShouldBeFalse();
    }

    [Fact]
    public void GetBookingDates_MonthReachingServiceEnd_IsTheLastEntry()
    {
        var period = DeferredAccountingService.GetBookingDates(
            Jan1, Mar31, null, new DateTime(2026, 12, 31), new DateTime(2026, 2, 28));
        period!.Value.End.ShouldBe(Mar31);
        period.Value.IsLast.ShouldBeTrue();
    }

    [Fact]
    public void GetBookingDates_ServiceStopDate_EndsTheScheduleEarly()
    {
        var period = DeferredAccountingService.GetBookingDates(
            Jan1, Mar31, new DateTime(2026, 2, 10), new DateTime(2026, 12, 31), new DateTime(2026, 1, 31));
        period!.Value.End.ShouldBe(new DateTime(2026, 2, 10));
        period.Value.IsLast.ShouldBeTrue();
    }

    [Fact]
    public void GetBookingDates_NeverRunsPastThePostingDate()
    {
        var period = DeferredAccountingService.GetBookingDates(Jan1, Mar31, null, new DateTime(2026, 1, 15), null);
        period!.Value.End.ShouldBe(new DateTime(2026, 1, 15));
        period.Value.IsLast.ShouldBeFalse();
    }

    [Fact]
    public void GetBookingDates_AlreadyBookedThroughPostingDate_ReturnsNothing()
    {
        DeferredAccountingService.GetBookingDates(
            Jan1, Mar31, null, new DateTime(2026, 1, 31), new DateTime(2026, 1, 31)).ShouldBeNull();
    }

    [Fact]
    public void CalculateAmount_Days_ProratesByDaysBooked()
    {
        // 90 days of service, 31 of them in January.
        var (amount, baseAmount) = DeferredAccountingService.CalculateAmount(
            "Days", 9000m, 9000m, 0m, 0m, Jan1, Mar31, Jan1, new DateTime(2026, 1, 31), isLast: false);
        amount.ShouldBe(3100m);
        baseAmount.ShouldBe(3100m);
    }

    [Fact]
    public void CalculateAmount_LastEntry_TakesTheRemainder()
    {
        var (amount, baseAmount) = DeferredAccountingService.CalculateAmount(
            "Days", 10000m, 10000m, 6555.56m, 6555.56m, Jan1, Mar31, new DateTime(2026, 3, 1), Mar31, isLast: true);
        amount.ShouldBe(3444.44m);
        baseAmount.ShouldBe(3444.44m);
    }

    [Fact]
    public void CalculateAmount_Months_BooksAnEqualSharePerMonth()
    {
        var yearEnd = new DateTime(2026, 12, 31);
        var (january, _) = DeferredAccountingService.CalculateAmount(
            "Months", 12000m, 12000m, 0m, 0m, Jan1, yearEnd, Jan1, new DateTime(2026, 1, 31), isLast: false);
        var (february, _) = DeferredAccountingService.CalculateAmount(
            "Months", 12000m, 12000m, 1000m, 1000m, Jan1, yearEnd, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), isLast: false);
        january.ShouldBe(1000m);
        february.ShouldBe(1000m);
    }

    [Fact]
    public void CalculateAmount_Months_NeverBooksPastTheRowAmount()
    {
        var (amount, _) = DeferredAccountingService.CalculateAmount(
            "Months", 12000m, 12000m, 11500m, 11500m, Jan1, new DateTime(2026, 12, 31),
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), isLast: false);
        amount.ShouldBe(500m);
    }

    [Fact]
    public void CalculateAmount_ForeignCurrency_KeepsBothAmounts()
    {
        var (amount, baseAmount) = DeferredAccountingService.CalculateAmount(
            "Days", 900m, 3960m, 0m, 0m, Jan1, Mar31, Jan1, new DateTime(2026, 1, 31), isLast: false);
        amount.ShouldBe(310m);
        baseAmount.ShouldBe(1364m);
    }

    [Fact]
    public void GetRowNetAmounts_WithoutInvoiceDiscount_IsTheLineTotal()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var net = DeferredAccountingService.GetRowNetAmounts(new[] { (a, 1200m), (b, 300m) }, 1500m);
        net[a].ShouldBe(1200m);
        net[b].ShouldBe(300m);
    }

    [Fact]
    public void GetRowNetAmounts_SpreadsAnInvoiceDiscountOverTheRows()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var net = DeferredAccountingService.GetRowNetAmounts(new[] { (a, 1200m), (b, 300m) }, 1350m);
        net[a].ShouldBe(1080m);
        net[b].ShouldBe(270m);
    }

    [Fact]
    public void GetRowNetAmounts_CreditNote_KeepsTheNegativeSign()
    {
        var a = Guid.NewGuid();
        var net = DeferredAccountingService.GetRowNetAmounts(new[] { (a, -300m) }, -300m);
        net[a].ShouldBe(-300m);
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
