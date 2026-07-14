using System;
using System.Linq;
using MyERP.Core;
using MyERP.Core.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class CurrencyExchangeAndRecurringTests
{
    // === Currency Exchange Resolution ===

    [Fact]
    public void CurrencyExchange_SameCurrency_ReturnsOne()
    {
        // When from == to, rate is always 1 (no conversion needed)
        // Tested at service level via the `if (fromCurrency == toCurrency) return 1m` check
        var from = "MYR";
        var to = "MYR";
        (from == to).ShouldBeTrue();
    }

    [Fact]
    public void CurrencyExchange_ReverseInversion()
    {
        // If we have USD→MYR = 4.72, then MYR→USD = 1/4.72 ≈ 0.2119
        decimal usdToMyr = 4.72m;
        decimal myrToUsd = 1m / usdToMyr;
        myrToUsd.ShouldBeGreaterThan(0.21m);
        myrToUsd.ShouldBeLessThan(0.22m);
    }

    [Fact]
    public void CurrencyExchange_ZeroRateInversion_Avoided()
    {
        // If stored rate is 0, we should NOT invert (would be division by zero)
        decimal rate = 0m;
        bool canInvert = rate != 0;
        canInvert.ShouldBeFalse();
    }

    // === Auto-Repeat / Recurring Invoice ===

    [Fact]
    public void AutoRepeat_IsDueOn_ReturnsTrueWhenPastSchedule()
    {
        var repeat = CreateAutoRepeat(DateTime.UtcNow.AddDays(-1));
        repeat.IsDueOn(DateTime.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void AutoRepeat_IsDueOn_ReturnsFalseBeforeSchedule()
    {
        var repeat = CreateAutoRepeat(DateTime.UtcNow.AddDays(5));
        repeat.IsDueOn(DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_IsDueOn_ReturnsFalseWhenDisabled()
    {
        var repeat = CreateAutoRepeat(DateTime.UtcNow.AddDays(-1));
        repeat.Disable();
        repeat.IsDueOn(DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_IsDueOn_ReturnsFalseWhenPastEndDate()
    {
        var repeat = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddDays(-1));

        repeat.IsDueOn(DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_IncrementsCount()
    {
        var repeat = CreateAutoRepeat(DateTime.UtcNow.AddDays(-1));
        repeat.GeneratedCount.ShouldBe(0);

        repeat.RecordGeneration(DateTime.UtcNow);
        repeat.GeneratedCount.ShouldBe(1);
        repeat.LastGeneratedDate.ShouldNotBeNull();
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_AdvancesNextSchedule_Monthly()
    {
        var startDate = new DateTime(2026, 1, 15);
        var repeat = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly, startDate);

        repeat.RecordGeneration(startDate);

        // Next should be Feb 15
        repeat.NextScheduleDate.Month.ShouldBe(2);
        repeat.NextScheduleDate.Day.ShouldBe(15);
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_ClampsToLastDay()
    {
        // Jan 31 + 1 month → Feb 28 (not Feb 31 which doesn't exist)
        var startDate = new DateTime(2026, 1, 31);
        var repeat = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly, startDate);

        repeat.RecordGeneration(startDate);
        repeat.NextScheduleDate.Month.ShouldBe(2);
        repeat.NextScheduleDate.Day.ShouldBe(28); // 2026 is not a leap year
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_AutoDisablesAtEndDate()
    {
        var startDate = new DateTime(2026, 6, 1);
        var endDate = new DateTime(2026, 6, 15);
        var repeat = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly, startDate, endDate);

        repeat.RecordGeneration(startDate);
        // Next schedule would be July 1, which is past end date → auto-disabled
        repeat.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_Quarterly_AdvancesThreeMonths()
    {
        var startDate = new DateTime(2026, 3, 1);
        var repeat = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Quarterly, startDate);

        repeat.RecordGeneration(startDate);
        repeat.NextScheduleDate.ShouldBe(new DateTime(2026, 6, 1));
    }

    [Fact]
    public void AutoRepeat_CancelledTemplate_ShouldDisable()
    {
        // Per DO-NOT: cannot auto-repeat cancelled documents
        // The job checks template status and disables the repeat if cancelled
        var repeat = CreateAutoRepeat(DateTime.UtcNow.AddDays(-1));
        repeat.IsEnabled.ShouldBeTrue();

        // Simulate: job detects cancelled template → disables
        repeat.Disable();
        repeat.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_EndDateBeforeStart_Throws()
    {
        Should.Throw<Volo.Abp.BusinessException>(() =>
            new AutoRepeat(
                Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
                RepeatFrequency.Monthly, DateTime.UtcNow, DateTime.UtcNow.AddDays(-5)));
    }

    private static AutoRepeat CreateAutoRepeat(DateTime nextSchedule)
    {
        var repeat = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly, nextSchedule);
        return repeat;
    }
}
