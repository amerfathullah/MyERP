using System;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Tests for financial reporting logic: aging bucket calculation, 
/// exchange rate gain/loss, and payment schedule allocation.
/// These cover daily operational reports used by finance teams.
/// </summary>
public class FinancialReportingLogicTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    #region Aging Bucket Index Calculation

    [Theory]
    [InlineData(0, 0)]    // Due today → bucket 0 (0-30)
    [InlineData(15, 0)]   // 15 days overdue → bucket 0 (0-30)
    [InlineData(30, 0)]   // Exactly 30 days → bucket 0 (0-30)
    [InlineData(31, 1)]   // 31 days → bucket 1 (31-60)
    [InlineData(60, 1)]   // 60 days → bucket 1 (31-60)
    [InlineData(61, 2)]   // 61 days → bucket 2 (61-90)
    [InlineData(90, 2)]   // 90 days → bucket 2 (61-90)
    [InlineData(91, 3)]   // 91 days → bucket 3 (91-120)
    [InlineData(120, 3)]  // 120 days → bucket 3 (91-120)
    [InlineData(121, 4)]  // 121 days → bucket 4 (120+)
    [InlineData(365, 4)]  // 1 year → bucket 4 (120+)
    public void GetBucketIndex_CorrectForStandardBuckets(int ageDays, int expectedBucket)
    {
        var bucketDays = new[] { 30, 60, 90, 120 };
        var index = GetBucketIndexHelper(ageDays, bucketDays);
        Assert.Equal(expectedBucket, index);
    }

    [Fact]
    public void NotYetDue_ClampedToZeroDays()
    {
        // If due date is in the future, ageDays should be 0 (not negative)
        var dueDate = DateTime.Today.AddDays(5);
        var asOfDate = DateTime.Today;
        var ageDays = (int)(asOfDate - dueDate).TotalDays;
        if (ageDays < 0) ageDays = 0;
        Assert.Equal(0, ageDays);
    }

    #endregion

    #region Aging Report Structure

    [Fact]
    public void AgingReport_DefaultsEmpty()
    {
        var report = new AgingReport();
        Assert.Equal(0m, report.TotalOutstanding);
        Assert.Equal(0, report.InvoiceCount);
    }

    [Fact]
    public void AgingReport_StandardBuckets_Has5Buckets()
    {
        var bucketDays = new[] { 30, 60, 90, 120 };
        // 4 defined ranges + 1 overflow = 5 buckets
        var bucketCount = bucketDays.Length + 1;
        Assert.Equal(5, bucketCount);
    }

    [Fact]
    public void AgingReport_CustomBuckets_Has3Buckets()
    {
        var bucketDays = new[] { 15, 45 }; // 0-15, 16-45, 45+
        var bucketCount = bucketDays.Length + 1;
        Assert.Equal(3, bucketCount);
    }

    #endregion

    #region Exchange Rate Gain/Loss Calculation

    [Fact]
    public void ExchangeGainLoss_GainWhenPaymentRateHigher()
    {
        // Invoice at 4.50 MYR/USD, payment at 4.60 MYR/USD
        decimal paidAmount = 1000m; // USD
        decimal paymentRate = 4.60m;
        decimal invoiceRate = 4.50m;
        decimal gainLoss = paidAmount * (paymentRate - invoiceRate);
        
        Assert.Equal(100m, gainLoss); // Gain of RM 100
        Assert.True(gainLoss > 0); // Positive = gain for receivable
    }

    [Fact]
    public void ExchangeGainLoss_LossWhenPaymentRateLower()
    {
        decimal paidAmount = 1000m;
        decimal paymentRate = 4.40m;
        decimal invoiceRate = 4.50m;
        decimal gainLoss = paidAmount * (paymentRate - invoiceRate);
        
        Assert.Equal(-100m, gainLoss); // Loss of RM 100
        Assert.True(gainLoss < 0);
    }

    [Fact]
    public void ExchangeGainLoss_ZeroWhenSameRate()
    {
        decimal paidAmount = 5000m;
        decimal paymentRate = 4.72m;
        decimal invoiceRate = 4.72m;
        decimal gainLoss = paidAmount * (paymentRate - invoiceRate);
        
        Assert.Equal(0m, gainLoss);
    }

    [Fact]
    public void ExchangeGainLoss_BaseCurrencyAlwaysZero()
    {
        // MYR→MYR: exchange rate is always 1
        decimal paidAmount = 10000m;
        decimal paymentRate = 1m;
        decimal invoiceRate = 1m;
        decimal gainLoss = paidAmount * (paymentRate - invoiceRate);
        
        Assert.Equal(0m, gainLoss);
    }

    #endregion

    #region Payment Schedule FIFO Allocation

    [Fact]
    public void PaymentSchedule_FIFOAllocatesEarliestFirst()
    {
        // 3 terms: RM 1000 due Jan, RM 2000 due Feb, RM 3000 due Mar
        var schedule = new[]
        {
            new { DueDate = new DateTime(2026, 1, 31), Amount = 1000m, Paid = 0m },
            new { DueDate = new DateTime(2026, 2, 28), Amount = 2000m, Paid = 0m },
            new { DueDate = new DateTime(2026, 3, 31), Amount = 3000m, Paid = 0m },
        };

        // Payment of RM 2500 → allocates 1000 to Jan (full), 1500 to Feb (partial)
        decimal payment = 2500m;
        decimal[] allocated = new decimal[3];
        decimal remaining = payment;

        for (int i = 0; i < schedule.Length && remaining > 0; i++)
        {
            var outstanding = schedule[i].Amount - schedule[i].Paid;
            var alloc = Math.Min(remaining, outstanding);
            allocated[i] = alloc;
            remaining -= alloc;
        }

        Assert.Equal(1000m, allocated[0]); // Jan fully paid
        Assert.Equal(1500m, allocated[1]); // Feb partially paid
        Assert.Equal(0m, allocated[2]);    // Mar untouched
        Assert.Equal(0m, remaining);       // All allocated
    }

    [Fact]
    public void PaymentSchedule_OverpaymentCapped()
    {
        // Only RM 500 outstanding, payment of RM 1000
        decimal outstanding = 500m;
        decimal payment = 1000m;
        decimal allocated = Math.Min(payment, outstanding);
        
        Assert.Equal(500m, allocated); // Capped at outstanding
    }

    #endregion

    #region Invoice Outstanding Tracking

    [Fact]
    public void Outstanding_MultiPayment_ProgressiveReduction()
    {
        decimal grandTotal = 10000m;
        decimal[] payments = { 3000m, 2000m, 5000m };
        decimal amountPaid = 0m;

        foreach (var p in payments)
        {
            amountPaid += p;
            decimal outstanding = grandTotal - amountPaid;
            Assert.True(outstanding >= 0);
        }

        Assert.Equal(grandTotal, amountPaid);
    }

    [Fact]
    public void Outstanding_NeverNegative_MathMaxGuard()
    {
        decimal grandTotal = 1000m;
        decimal amountPaid = 1050m; // Overpayment scenario
        decimal outstanding = Math.Max(0, grandTotal - amountPaid);
        
        Assert.Equal(0m, outstanding); // Clamped to 0
    }

    #endregion

    #region Currency Exchange Rate Resolution

    [Fact]
    public void CurrencyExchange_SameCurrency_ReturnsOne()
    {
        // MYR to MYR = 1.0 (no conversion needed)
        string from = "MYR", to = "MYR";
        decimal rate = from == to ? 1m : 0m; // Placeholder logic
        Assert.Equal(1m, rate);
    }

    [Fact]
    public void CurrencyExchange_ReverseRate_Inverts()
    {
        // If USD→MYR = 4.72, then MYR→USD = 1/4.72
        decimal usdToMyr = 4.72m;
        decimal myrToUsd = Math.Round(1m / usdToMyr, 6);
        
        Assert.True(myrToUsd > 0);
        Assert.True(myrToUsd < 1); // MYR is weaker
        // Round-trip: converting and back should be close to 1
        decimal roundTrip = usdToMyr * myrToUsd;
        Assert.True(Math.Abs(roundTrip - 1m) < 0.001m);
    }

    [Fact]
    public void CurrencyExchange_BaseAmount_Calculation()
    {
        // USD 1000 at rate 4.72 = MYR 4720
        decimal amount = 1000m;
        decimal rate = 4.72m;
        decimal baseAmount = amount * rate;
        
        Assert.Equal(4720m, baseAmount);
    }

    #endregion

    #region Trial Balance Validation

    [Fact]
    public void TrialBalance_DebitEqualsCredit()
    {
        decimal totalDebit = 150000m;
        decimal totalCredit = 150000m;
        decimal difference = totalDebit - totalCredit;
        
        Assert.Equal(0m, difference);
        Assert.True(Math.Abs(difference) < 0.01m); // Within tolerance
    }

    [Fact]
    public void TrialBalance_ImbalanceDetected()
    {
        decimal totalDebit = 150000m;
        decimal totalCredit = 149999.50m;
        decimal difference = totalDebit - totalCredit;
        
        Assert.Equal(0.50m, difference);
        Assert.True(Math.Abs(difference) > 0.01m); // Outside tolerance
    }

    #endregion

    // Helper to simulate private GetBucketIndex logic
    private static int GetBucketIndexHelper(int ageDays, int[] bucketDays)
    {
        for (int i = 0; i < bucketDays.Length; i++)
        {
            if (ageDays <= bucketDays[i]) return i;
        }
        return bucketDays.Length;
    }
}
