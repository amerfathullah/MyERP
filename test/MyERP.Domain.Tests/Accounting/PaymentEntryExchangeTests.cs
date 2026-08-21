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

    // ═══════════════════════════════════════════════
    // ResolveExchangeGainLossPosting — party account + direction-aware gain/loss classification
    // Regression coverage for the 75th migration session's exchange-gain-loss fix: the raw
    // ExchangeGainLoss sign above is Receive-oriented ("higher payment rate = gain for
    // receivable"); for Pay it means the opposite (paid more = loss), and the offsetting JE leg
    // must hit the party account (PaidFrom for Receive, PaidTo for Pay), never PaidToAccountId
    // unconditionally — see PaymentEntryAppService.ResolveExchangeGainLossPosting's own doc
    // comment for the worked example this derives from.
    // ═══════════════════════════════════════════════

    [Fact]
    public void ResolveExchangeGainLossPosting_Receive_PositiveRaw_IsGain_UsesPaidFrom()
    {
        var paidFrom = Guid.NewGuid();
        var paidTo = Guid.NewGuid();

        var (partyAccountId, isGain) = MyERP.Accounting.PaymentEntryAppService
            .ResolveExchangeGainLossPosting(PaymentType.Receive, paidFrom, paidTo, rawGainLoss: 200m);

        isGain.ShouldBeTrue();
        partyAccountId.ShouldBe(paidFrom); // Receivable
    }

    [Fact]
    public void ResolveExchangeGainLossPosting_Receive_NegativeRaw_IsLoss_UsesPaidFrom()
    {
        var paidFrom = Guid.NewGuid();
        var paidTo = Guid.NewGuid();

        var (partyAccountId, isGain) = MyERP.Accounting.PaymentEntryAppService
            .ResolveExchangeGainLossPosting(PaymentType.Receive, paidFrom, paidTo, rawGainLoss: -200m);

        isGain.ShouldBeFalse();
        partyAccountId.ShouldBe(paidFrom); // Receivable
    }

    [Fact]
    public void ResolveExchangeGainLossPosting_Pay_PositiveRaw_IsLoss_UsesPaidTo()
    {
        // Pay: PaidFrom=Bank, PaidTo=Payable. A higher settlement rate means the company paid
        // MORE home-currency to clear the same foreign debt — a loss, not a gain, even though
        // the raw formula's sign is identical to the Receive case.
        var paidFrom = Guid.NewGuid(); // Bank
        var paidTo = Guid.NewGuid();   // Payable

        var (partyAccountId, isGain) = MyERP.Accounting.PaymentEntryAppService
            .ResolveExchangeGainLossPosting(PaymentType.Pay, paidFrom, paidTo, rawGainLoss: 200m);

        isGain.ShouldBeFalse();
        partyAccountId.ShouldBe(paidTo); // Payable
    }

    [Fact]
    public void ResolveExchangeGainLossPosting_Pay_NegativeRaw_IsGain_UsesPaidTo()
    {
        // Pay, rate dropped vs booking: paid LESS home-currency to clear the same foreign debt —
        // a gain.
        var paidFrom = Guid.NewGuid(); // Bank
        var paidTo = Guid.NewGuid();   // Payable

        var (partyAccountId, isGain) = MyERP.Accounting.PaymentEntryAppService
            .ResolveExchangeGainLossPosting(PaymentType.Pay, paidFrom, paidTo, rawGainLoss: -200m);

        isGain.ShouldBeTrue();
        partyAccountId.ShouldBe(paidTo); // Payable
    }
}
