using System;
using System.Linq;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class ExchangeRateRevaluationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _glAccountId = Guid.NewGuid();

    private ExchangeRateRevaluation CreateERR(decimal roundingAllowance = 0m)
    {
        return new ExchangeRateRevaluation(
            Guid.NewGuid(), _companyId, new DateTime(2026, 6, 30),
            _glAccountId, roundingAllowance);
    }

    [Fact]
    public void ERR_DefaultState()
    {
        var err = CreateERR();
        Assert.Equal(ExchangeRateRevaluationStatus.Draft, err.Status);
        Assert.Equal(0m, err.TotalGainLoss);
        Assert.Empty(err.Entries);
        Assert.Null(err.RevaluationJournalEntryId);
        Assert.Null(err.ZeroBalanceJournalEntryId);
    }

    [Fact]
    public void ERR_RoundingAllowance_ValidRange()
    {
        var err = CreateERR(0.5m);
        Assert.Equal(0.5m, err.RoundingLossAllowance);
    }

    [Fact]
    public void ERR_RoundingAllowance_InvalidRange_Throws()
    {
        Assert.Throws<BusinessException>(() => CreateERR(1.0m)); // >= 1 invalid
        Assert.Throws<BusinessException>(() => CreateERR(-0.1m)); // < 0 invalid
    }

    [Fact]
    public void ERR_RoundingAllowance_Zero_Valid()
    {
        var err = CreateERR(0m);
        Assert.Equal(0m, err.RoundingLossAllowance);
    }

    [Fact]
    public void ERR_AddEntry_CalculatesGainLoss()
    {
        var err = CreateERR();
        var accountId = Guid.NewGuid();

        // USD account: 10,000 USD at old rate 4.2 = 42,000 MYR
        // New rate 4.5 → new balance 45,000 MYR → gain = 3,000
        err.AddEntry(accountId, "USD", 10_000m, 42_000m, 4.5m);

        Assert.Single(err.Entries);
        var entry = err.Entries.First();
        Assert.Equal(10_000m, entry.BalanceInAccountCurrency);
        Assert.Equal(42_000m, entry.CurrentBalanceInCompanyCurrency);
        Assert.Equal(4.5m, entry.NewExchangeRate);
        Assert.Equal(45_000m, entry.NewBalanceInCompanyCurrency);
        Assert.Equal(3_000m, entry.GainLoss);
    }

    [Fact]
    public void ERR_AddEntry_CalculatesLoss()
    {
        var err = CreateERR();
        var accountId = Guid.NewGuid();

        // USD account: 10,000 USD at old rate 4.5 = 45,000 MYR
        // New rate 4.2 → new balance 42,000 MYR → loss = -3,000
        err.AddEntry(accountId, "USD", 10_000m, 45_000m, 4.2m);

        var entry = err.Entries.First();
        Assert.Equal(-3_000m, entry.GainLoss);
    }

    [Fact]
    public void ERR_AddPartyEntry_WithPartyDetails()
    {
        var err = CreateERR();
        var accountId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        err.AddPartyEntry(accountId, "USD", "Customer", customerId,
            5_000m, 21_000m, 4.5m);

        var entry = err.Entries.First();
        Assert.Equal("Customer", entry.PartyType);
        Assert.Equal(customerId, entry.PartyId);
        Assert.Equal(1_500m, entry.GainLoss); // 5000*4.5 - 21000 = 1500
    }

    [Fact]
    public void ERR_Submit_RemovesZeroGainEntries()
    {
        var err = CreateERR();
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();

        err.AddEntry(accountId1, "USD", 10_000m, 42_000m, 4.5m); // Gain = 3000
        err.AddEntry(accountId2, "EUR", 5_000m, 25_000m, 5.0m);  // Gain = 0

        err.Submit();

        Assert.Single(err.Entries); // Zero-gain entry removed
        Assert.Equal(3_000m, err.TotalGainLoss);
    }

    [Fact]
    public void ERR_Submit_SetsStatus()
    {
        var err = CreateERR();
        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 42_000m, 4.5m);

        err.Submit();

        Assert.Equal(ExchangeRateRevaluationStatus.Submitted, err.Status);
    }

    [Fact]
    public void ERR_Submit_NoEntries_Throws()
    {
        var err = CreateERR();
        // Only zero-gain entries
        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 45_000m, 4.5m); // exactly 0

        var ex = Assert.Throws<BusinessException>(() => err.Submit());
        Assert.Equal("MyERP:02018", ex.Code);
    }

    [Fact]
    public void ERR_Submit_FromNonDraft_Throws()
    {
        var err = CreateERR();
        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 42_000m, 4.5m);
        err.Submit();

        Assert.Throws<BusinessException>(() => err.Submit()); // Already submitted
    }

    [Fact]
    public void ERR_Cancel_FromSubmitted()
    {
        var err = CreateERR();
        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 42_000m, 4.5m);
        err.Submit();
        err.Cancel();

        Assert.Equal(ExchangeRateRevaluationStatus.Cancelled, err.Status);
    }

    [Fact]
    public void ERR_Cancel_FromDraft_Throws()
    {
        var err = CreateERR();
        Assert.Throws<BusinessException>(() => err.Cancel());
    }

    [Fact]
    public void ERR_AddEntry_AfterSubmit_Throws()
    {
        var err = CreateERR();
        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 42_000m, 4.5m);
        err.Submit();

        Assert.Throws<BusinessException>(() =>
            err.AddEntry(Guid.NewGuid(), "EUR", 5_000m, 25_000m, 5.5m));
    }

    [Fact]
    public void ERR_TotalGainLoss_MixedEntries()
    {
        var err = CreateERR();

        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 42_000m, 4.5m); // +3000 gain
        err.AddEntry(Guid.NewGuid(), "EUR", 5_000m, 27_000m, 5.0m);  // -2000 loss
        err.AddEntry(Guid.NewGuid(), "GBP", 3_000m, 18_000m, 6.5m);  // +1500 gain

        err.Submit();

        Assert.Equal(2_500m, err.TotalGainLoss); // 3000 - 2000 + 1500
    }

    [Fact]
    public void ERR_MultiplePartyEntries_SameAccount()
    {
        var err = CreateERR();
        var receivableId = Guid.NewGuid();
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        err.AddPartyEntry(receivableId, "USD", "Customer", customer1,
            5_000m, 21_000m, 4.5m); // 22500 - 21000 = +1500
        err.AddPartyEntry(receivableId, "USD", "Customer", customer2,
            3_000m, 13_500m, 4.5m); // 13500 - 13500 = 0

        err.Submit();

        // Only customer1 entry remains (customer2 is zero)
        Assert.Single(err.Entries);
        Assert.Equal(1_500m, err.TotalGainLoss);
    }

    [Fact]
    public void ERR_Entry_DefaultFields()
    {
        var entry = new ExchangeRateRevaluationEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "USD",
            10_000m, 42_000m, 4.5m, 45_000m, 3_000m);

        Assert.Null(entry.PartyType);
        Assert.Null(entry.PartyId);
    }

    [Fact]
    public void ERR_OnlyBalanceSheet_NoP_and_L()
    {
        // Per DO-NOT: "Implement exchange rate revaluation on P&L accounts"
        // This is enforced by the service (GetEligibleAccountsAsync filters by AccountType)
        // The entity itself doesn't enforce this — it's a service-level business rule
        var err = CreateERR();
        // Entity accepts any account — validation is at service level
        err.AddEntry(Guid.NewGuid(), "USD", 10_000m, 42_000m, 4.5m);
        Assert.Single(err.Entries);
    }

    [Fact]
    public void ERR_JournalEntryIds_DefaultNull()
    {
        var err = CreateERR();
        Assert.Null(err.RevaluationJournalEntryId);
        Assert.Null(err.ZeroBalanceJournalEntryId);
    }

    [Fact]
    public void ERR_JournalEntryIds_CanBeSet()
    {
        var err = CreateERR();
        var jeId = Guid.NewGuid();
        err.RevaluationJournalEntryId = jeId;
        Assert.Equal(jeId, err.RevaluationJournalEntryId);
    }
}
