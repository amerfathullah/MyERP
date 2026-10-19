using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Parity tests for ERPNext PR #61266 / commits 7ead363243, cfaa6eaa04:
/// - Balances respect finance books: unbooked entries are common; alternative finance books
///   do NOT bleed into the unbooked balance or other finance books.
/// - Bank reconciliation statement excludes journal entries and lines belonging to
///   non-empty/alternative finance books.
/// </summary>
public class FinanceBookBalancesAndReconciliationTests
{
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<FinanceBook, Guid> _financeBookRepo = Substitute.For<IRepository<FinanceBook, Guid>>();

    private readonly AccountBalanceService _balanceService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();

    public FinanceBookBalancesAndReconciliationTests()
    {
        _balanceService = new AccountBalanceService(
            _journalRepo,
            _journalLineRepo,
            _closingBalanceRepo,
            _accountRepo);
    }

    [Fact]
    public async Task Balances_RespectFinanceBooks_NoBleedAcrossBooks()
    {
        var fiscalYearId = Guid.NewGuid();
        var asOfDate = new DateTime(2026, 6, 1);

        var je1 = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, asOfDate);
        var cashAccountId = Guid.NewGuid();

        // 4 lines matching ERPNext test_balances_respect_finance_books:
        // None: 100
        // "": 200
        // "Default Book": 300
        // "Alternative Book": 400
        var lineNone = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 100m, true) { FinanceBook = null };
        var lineEmpty = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 200m, true) { FinanceBook = "" };
        var lineDefault = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 300m, true) { FinanceBook = "Default Book" };
        var lineAlt = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 400m, true) { FinanceBook = "Alternative Book" };
        var lineCredit = new JournalEntryLine(Guid.NewGuid(), je1.Id, cashAccountId, 1000m, false) { FinanceBook = null };

        je1.AddLine(lineNone);
        je1.AddLine(lineEmpty);
        je1.AddLine(lineDefault);
        je1.AddLine(lineAlt);
        je1.AddLine(lineCredit);
        je1.Post();

        var allLines = new List<JournalEntryLine> { lineNone, lineEmpty, lineDefault, lineAlt, lineCredit };

        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { je1 }));

        _journalLineRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntryLine, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntryLine, bool>>>().Compile();
                return Task.FromResult(allLines.Where(predicate).ToList());
            });

        _closingBalanceRepo.GetListAsync(Arg.Any<Expression<Func<AccountClosingBalance, bool>>>())
            .Returns(Task.FromResult(new List<AccountClosingBalance>()));

        // 1. Query with no finance book (null) -> only unbooked entries (100 + 200 = 300)
        var balNull = await _balanceService.GetBalanceOnAsync(_companyId, _bankAccountId, asOfDate, financeBook: null);
        balNull.Debit.ShouldBe(300m);
        balNull.Balance.ShouldBe(300m);

        // 2. Query with empty finance book ("") -> only unbooked entries (300)
        var balEmpty = await _balanceService.GetBalanceOnAsync(_companyId, _bankAccountId, asOfDate, financeBook: "");
        balEmpty.Debit.ShouldBe(300m);

        // 3. Query with "Default Book" -> 300 common + 300 Default Book = 600
        var balDefault = await _balanceService.GetBalanceOnAsync(_companyId, _bankAccountId, asOfDate, financeBook: "Default Book");
        balDefault.Debit.ShouldBe(600m);

        // 4. Query with "Alternative Book" -> 300 common + 400 Alternative Book = 700 (Default Book does NOT bleed)
        var balAlt = await _balanceService.GetBalanceOnAsync(_companyId, _bankAccountId, asOfDate, financeBook: "Alternative Book");
        balAlt.Debit.ShouldBe(700m);

        // 5. Trial balance with no finance book -> only unbooked entries (300)
        var tbNull = await _balanceService.GetTrialBalanceAsync(_companyId, asOfDate, financeBook: null);
        tbNull[_bankAccountId].Debit.ShouldBe(300m);

        // 6. Trial balance with "Alternative Book" -> 700 (300 common + 400 Alternative)
        var tbAlt = await _balanceService.GetTrialBalanceAsync(_companyId, asOfDate, financeBook: "Alternative Book");
        tbAlt[_bankAccountId].Debit.ShouldBe(700m);
    }

    [Fact]
    public async Task PeriodBalances_RespectFinanceBooks()
    {
        var fiscalYearId = Guid.NewGuid();
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 12, 31);

        var je1 = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, new DateTime(2026, 3, 1));
        var cashAccountId = Guid.NewGuid();

        var lineNone = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 100m, true) { FinanceBook = null };
        var lineDefault = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 300m, true) { FinanceBook = "Default Book" };
        var lineAlt = new JournalEntryLine(Guid.NewGuid(), je1.Id, _bankAccountId, 400m, true) { FinanceBook = "Alternative Book" };
        var lineCredit = new JournalEntryLine(Guid.NewGuid(), je1.Id, cashAccountId, 800m, false) { FinanceBook = null };

        je1.AddLine(lineNone);
        je1.AddLine(lineDefault);
        je1.AddLine(lineAlt);
        je1.AddLine(lineCredit);
        je1.Post();

        var allLines = new List<JournalEntryLine> { lineNone, lineDefault, lineAlt, lineCredit };

        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { je1 }));

        _journalLineRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntryLine, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntryLine, bool>>>().Compile();
                return Task.FromResult(allLines.Where(predicate).ToList());
            });

        // 1. Period balance without finance book -> only unbooked line (100)
        var periodNull = await _balanceService.GetPeriodBalanceAsync(_companyId, _bankAccountId, fromDate, toDate, financeBook: null);
        periodNull.Debit.ShouldBe(100m);

        // 2. Period balance with "Alternative Book" -> 100 common + 400 Alternative = 500
        var periodAlt = await _balanceService.GetPeriodBalanceAsync(_companyId, _bankAccountId, fromDate, toDate, financeBook: "Alternative Book");
        periodAlt.Debit.ShouldBe(500m);
    }

    [Fact]
    public async Task ClosingBalances_RespectFinanceBooks()
    {
        var asOfDate = new DateTime(2026, 6, 1);
        var closingDate = new DateTime(2026, 5, 31);

        // Closing balance booked specifically to "Alternative Book"
        var cbAlt = new AccountClosingBalance(
            Guid.NewGuid(), _companyId, _bankAccountId, closingDate, "2026-05",
            debit: 500m, credit: 0m, financeBook: "Alternative Book");

        // Closing balance with no finance book (common)
        var cbCommon = new AccountClosingBalance(
            Guid.NewGuid(), _companyId, _bankAccountId, closingDate, "2026-05",
            debit: 200m, credit: 0m, financeBook: null);

        var allBalances = new List<AccountClosingBalance> { cbAlt, cbCommon };

        _closingBalanceRepo.GetListAsync(Arg.Any<Expression<Func<AccountClosingBalance, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<AccountClosingBalance, bool>>>().Compile();
                return Task.FromResult(allBalances.Where(predicate).ToList());
            });

        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry>()));

        // Query common balance -> picks up cbCommon (200), NOT cbAlt (500)
        var balCommon = await _balanceService.GetBalanceOnAsync(_companyId, _bankAccountId, asOfDate, financeBook: null);
        balCommon.Debit.ShouldBe(200m);

        // Query "Alternative Book" -> picks up cbAlt (500)
        var balAlt = await _balanceService.GetBalanceOnAsync(_companyId, _bankAccountId, asOfDate, financeBook: "Alternative Book");
        balAlt.Debit.ShouldBe(500m);
    }
}
