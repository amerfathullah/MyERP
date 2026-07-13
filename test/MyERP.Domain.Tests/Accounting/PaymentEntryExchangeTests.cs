using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class PaymentEntryExchangeTests
{
    private static PaymentEntry CreatePE(decimal amount = 1000m, string currency = "USD")
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, amount, Guid.NewGuid(), Guid.NewGuid());
        pe.CurrencyCode = currency;
        return pe;
    }

    [Fact]
    public void ExchangeRate_DefaultsToOne()
    {
        var pe = CreatePE();
        pe.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void SourceExchangeRate_DefaultsToOne()
    {
        var pe = CreatePE();
        pe.SourceExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void BaseAmount_CalculatesCorrectly()
    {
        var pe = CreatePE(1000m, "USD");
        pe.ExchangeRate = 4.5m; // 1 USD = 4.5 MYR
        pe.BaseAmount.ShouldBe(4500m);
    }

    [Fact]
    public void ExchangeGainLoss_ZeroWhenRatesMatch()
    {
        var pe = CreatePE(1000m, "USD");
        pe.ExchangeRate = 4.5m;
        pe.SourceExchangeRate = 4.5m;
        pe.ExchangeGainLoss.ShouldBe(0m);
    }

    [Fact]
    public void ExchangeGainLoss_PositiveWhenPaymentRateHigher()
    {
        // Invoice at 4.3, payment at 4.5 → gain for receivable (customer pays more in base)
        var pe = CreatePE(1000m, "USD");
        pe.ExchangeRate = 4.5m;
        pe.SourceExchangeRate = 4.3m;
        pe.ExchangeGainLoss.ShouldBe(200m); // 1000 × (4.5 - 4.3)
    }

    [Fact]
    public void ExchangeGainLoss_NegativeWhenPaymentRateLower()
    {
        // Invoice at 4.5, payment at 4.3 → loss for receivable
        var pe = CreatePE(1000m, "USD");
        pe.ExchangeRate = 4.3m;
        pe.SourceExchangeRate = 4.5m;
        pe.ExchangeGainLoss.ShouldBe(-200m); // 1000 × (4.3 - 4.5)
    }

    [Fact]
    public void ExchangeGainLoss_MYRPayment_AlwaysZero()
    {
        // MYR payments have rate = 1 always
        var pe = CreatePE(5000m, "MYR");
        pe.ExchangeRate = 1m;
        pe.SourceExchangeRate = 1m;
        pe.ExchangeGainLoss.ShouldBe(0m);
    }
}
