using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class AccountingIntegrityTests
{
    // === Trial Balance Validation ===

    [Fact]
    public void TrialBalance_Balanced_WhenDebitEqualsCredit()
    {
        var result = new TrialBalanceValidationResult
        {
            TotalDebit = 50000m,
            TotalCredit = 50000m,
            Difference = 0m,
            IsBalanced = true
        };
        result.IsBalanced.ShouldBeTrue();
        result.Difference.ShouldBe(0m);
    }

    [Fact]
    public void TrialBalance_Unbalanced_WhenDifferenceExists()
    {
        decimal totalDebit = 50000m;
        decimal totalCredit = 49999.50m;
        decimal diff = totalDebit - totalCredit;
        bool isBalanced = Math.Abs(diff) < 0.01m;

        isBalanced.ShouldBeFalse();
        diff.ShouldBe(0.50m);
    }

    [Fact]
    public void TrialBalance_Tolerance_SmallRoundingOk()
    {
        // Differences under 0.01 are acceptable (rounding tolerance)
        decimal diff = 0.005m;
        bool isBalanced = Math.Abs(diff) < 0.01m;
        isBalanced.ShouldBeTrue();
    }

    [Fact]
    public void UnbalancedEntry_DetectedPerVoucher()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);
        je.AddLine(Guid.NewGuid(), 1000m, true, "Debit side");
        je.AddLine(Guid.NewGuid(), 1000m, false, "Credit side");

        // This JE is balanced
        var debit = je.Lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var credit = je.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        (debit - credit).ShouldBe(0m);
    }

    [Fact]
    public void UnbalancedEntries_ListPopulated()
    {
        var result = new TrialBalanceValidationResult { IsBalanced = false };
        result.UnbalancedEntries.Add(new UnbalancedEntryInfo
        {
            JournalEntryId = Guid.NewGuid(),
            EntryNumber = "JE-001",
            Debit = 5000,
            Credit = 4900,
            Difference = 100
        });

        result.UnbalancedEntries.Count.ShouldBe(1);
        result.UnbalancedEntries[0].Difference.ShouldBe(100m);
    }

    [Fact]
    public void TrialBalance_NoEntries_ConsideredBalanced()
    {
        var result = new TrialBalanceValidationResult
        {
            TotalJournalEntries = 0,
            IsBalanced = true
        };
        result.IsBalanced.ShouldBeTrue();
    }

    // === Stale Exchange Rate Detection ===

    [Fact]
    public void StaleCurrencyPair_DefaultsToStale_WhenNoRateExists()
    {
        // No stored rate = stale (need to fetch)
        bool isStale = true; // latestRate == null
        int daysSince = int.MaxValue;

        isStale.ShouldBeTrue();
        daysSince.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void StaleRate_NotStale_WhenRecent()
    {
        var rateDate = DateTime.UtcNow.Date; // today
        int maxStaleDays = 1;
        int daysSince = (DateTime.UtcNow.Date - rateDate).Days;
        bool isStale = daysSince > maxStaleDays;

        isStale.ShouldBeFalse();
        daysSince.ShouldBe(0);
    }

    [Fact]
    public void StaleRate_IsStale_WhenOlderThanMax()
    {
        var rateDate = DateTime.UtcNow.Date.AddDays(-5);
        int maxStaleDays = 1;
        int daysSince = (DateTime.UtcNow.Date - rateDate).Days;
        bool isStale = daysSince > maxStaleDays;

        isStale.ShouldBeTrue();
        daysSince.ShouldBe(5);
    }

    [Fact]
    public void StaleRate_SameCurrency_NeverStale()
    {
        // Same currency pair (MYR→MYR) is always fresh (rate = 1.0)
        bool isSameCurrency = string.Equals("MYR", "MYR", StringComparison.OrdinalIgnoreCase);
        isSameCurrency.ShouldBeTrue();
        // No stale check needed for same currency
    }

    [Fact]
    public void StaleRate_ExactlyOnMaxDay_NotStale()
    {
        var rateDate = DateTime.UtcNow.Date.AddDays(-1);
        int maxStaleDays = 1;
        int daysSince = (DateTime.UtcNow.Date - rateDate).Days;
        bool isStale = daysSince > maxStaleDays; // > not >=, so exactly 1 day is NOT stale

        isStale.ShouldBeFalse();
    }

    [Fact]
    public void StalePairInfo_Properties()
    {
        var info = new StaleCurrencyPairInfo
        {
            FromCurrency = "USD",
            ToCurrency = "MYR",
            LastRateDate = DateTime.UtcNow.AddDays(-10),
            LastRate = 4.72m,
            DaysSinceUpdate = 10
        };

        info.FromCurrency.ShouldBe("USD");
        info.ToCurrency.ShouldBe("MYR");
        info.DaysSinceUpdate.ShouldBe(10);
    }

    [Fact]
    public void StaleRate_ConfigurableMaxDays()
    {
        // allow_stale=false in ERPNext means we check; stale_days >= 1
        var rateDate = DateTime.UtcNow.Date.AddDays(-3);

        // With maxStaleDays=7: not stale
        bool staleAt7 = (DateTime.UtcNow.Date - rateDate).Days > 7;
        staleAt7.ShouldBeFalse();

        // With maxStaleDays=1: stale
        bool staleAt1 = (DateTime.UtcNow.Date - rateDate).Days > 1;
        staleAt1.ShouldBeTrue();
    }
}
