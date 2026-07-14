using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

public class AccountBalanceServiceTests
{
    private readonly IRepository<JournalEntry, Guid> _journalRepo;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepo;
    private readonly IRepository<AccountClosingBalance, Guid> _closingRepo;
    private readonly IRepository<Account, Guid> _accountRepo;
    private readonly AccountBalanceService _service;

    public AccountBalanceServiceTests()
    {
        _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        _lineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
        _closingRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
        _accountRepo = Substitute.For<IRepository<Account, Guid>>();
        _service = new AccountBalanceService(_journalRepo, _lineRepo, _closingRepo, _accountRepo);
    }

    #region AccountBalanceResult Tests

    [Fact]
    public void AccountBalanceResult_DebitBalance_PositiveBalance()
    {
        var result = new AccountBalanceResult(10000m, 3000m);
        result.Balance.ShouldBe(7000m);
        result.IsDebitBalance.ShouldBeTrue();
        result.IsCreditBalance.ShouldBeFalse();
    }

    [Fact]
    public void AccountBalanceResult_CreditBalance_NegativeBalance()
    {
        var result = new AccountBalanceResult(2000m, 8000m);
        result.Balance.ShouldBe(-6000m);
        result.IsDebitBalance.ShouldBeFalse();
        result.IsCreditBalance.ShouldBeTrue();
    }

    [Fact]
    public void AccountBalanceResult_ZeroBalance()
    {
        var result = new AccountBalanceResult(5000m, 5000m);
        result.Balance.ShouldBe(0m);
        result.IsDebitBalance.ShouldBeFalse();
        result.IsCreditBalance.ShouldBeFalse();
    }

    [Fact]
    public void AccountBalanceResult_ClosingBalance_AbsoluteValue()
    {
        var debitResult = new AccountBalanceResult(10000m, 3000m);
        debitResult.ClosingBalance.ShouldBe(7000m);

        var creditResult = new AccountBalanceResult(2000m, 8000m);
        creditResult.ClosingBalance.ShouldBe(6000m);
    }

    [Fact]
    public void AccountBalanceResult_Zero_StaticFactory()
    {
        var zero = AccountBalanceResult.Zero;
        zero.Debit.ShouldBe(0m);
        zero.Credit.ShouldBe(0m);
        zero.Balance.ShouldBe(0m);
    }

    [Fact]
    public void AccountBalanceResult_Add_CombinesTwoResults()
    {
        var a = new AccountBalanceResult(1000m, 500m);
        var b = new AccountBalanceResult(2000m, 800m);
        var combined = a.Add(b);

        combined.Debit.ShouldBe(3000m);
        combined.Credit.ShouldBe(1300m);
        combined.Balance.ShouldBe(1700m);
    }

    #endregion

    #region GetBalanceOn Tests

    [Fact]
    public async Task GetBalanceOn_NoData_ReturnsZero()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        _closingRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountClosingBalance, bool>>>())
            .Returns(new List<AccountClosingBalance>());

        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry>());

        var result = await _service.GetBalanceOnAsync(companyId, accountId, DateTime.Today);

        result.Debit.ShouldBe(0m);
        result.Credit.ShouldBe(0m);
        result.Balance.ShouldBe(0m);
    }

    [Fact]
    public async Task GetBalanceOn_WithClosingBalance_UsesAsOpening()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        // Closing balance from June: DR 5000, CR 2000
        var closingDate = new DateTime(2026, 6, 30);
        var closing = new AccountClosingBalance(
            Guid.NewGuid(), companyId, accountId, closingDate, "2026-06", 5000m, 2000m);

        _closingRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<AccountClosingBalance, bool>>>())
            .Returns(new List<AccountClosingBalance> { closing });

        // No GL entries after closing date
        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry>());

        var result = await _service.GetBalanceOnAsync(companyId, accountId, new DateTime(2026, 7, 15));

        result.Debit.ShouldBe(5000m);
        result.Credit.ShouldBe(2000m);
        result.Balance.ShouldBe(3000m);
    }

    [Fact]
    public async Task GetPartyBalance_NoEntries_ReturnsZero()
    {
        var companyId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry>());

        var result = await _service.GetPartyBalanceAsync(companyId, "Customer", partyId, DateTime.Today);

        result.ShouldBe(AccountBalanceResult.Zero);
    }

    [Fact]
    public async Task GetTrialBalance_NoJournals_ReturnsEmpty()
    {
        var companyId = Guid.NewGuid();

        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry>());

        var result = await _service.GetTrialBalanceAsync(companyId, DateTime.Today);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTrialBalance_WithJournals_AggregatesByAccount()
    {
        var companyId = Guid.NewGuid();
        var receivableId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();
        var jeId = Guid.NewGuid();

        var journal = new JournalEntry(jeId, companyId, Guid.NewGuid(), DateTime.Today);
        journal.AddLine(receivableId, 1060m, true);
        journal.AddLine(revenueId, 1000m, false);
        journal.AddLine(Guid.NewGuid(), 60m, false); // Tax
        journal.Validate();
        journal.Post();

        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { journal });

        _lineRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntryLine, bool>>>())
            .Returns(journal.Lines.ToList());

        var result = await _service.GetTrialBalanceAsync(companyId, DateTime.Today);

        result.Count.ShouldBe(3);
        result[receivableId].Debit.ShouldBe(1060m);
        result[receivableId].Credit.ShouldBe(0m);
        result[revenueId].Credit.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetPeriodBalance_OnlyCountsEntriesInRange()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        // No journals in range
        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry>());

        var result = await _service.GetPeriodBalanceAsync(
            companyId, accountId,
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));

        result.ShouldBe(AccountBalanceResult.Zero);
    }

    [Fact]
    public async Task GetMultiCurrencyBalance_NoEntries_ReturnsBothZero()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        _journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry>());

        var (accountCurrency, companyCurrency) = await _service.GetMultiCurrencyBalanceAsync(
            companyId, accountId, DateTime.Today);

        accountCurrency.ShouldBe(0m);
        companyCurrency.ShouldBe(0m);
    }

    #endregion
}
