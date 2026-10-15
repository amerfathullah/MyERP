using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Unit tests for BudgetVarianceReportAppService per ERPNext PR #60001:
/// - Excludes Period Closing Vouchers from actuals so closed years still reflect real expenses.
/// - Respects company default / specific finance book filters.
/// - Supports filtering by BudgetAgainst dimension and CostCenterId/ProjectId.
/// - Computes variance and over-budget status correctly.
/// </summary>
public class BudgetVarianceReportAppServiceTests
{
    private readonly IRepository<Budget, Guid> _budgetRepo = Substitute.For<IRepository<Budget, Guid>>();
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _lineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();

    private readonly AccountBalanceService _balanceService;
    private readonly BudgetVarianceReportAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly FiscalYear _fiscalYear;

    public BudgetVarianceReportAppServiceTests()
    {
        _fiscalYear = new FiscalYear(
            _fiscalYearId,
            _companyId,
            "FY2026",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31));

        _fiscalYearRepo.GetAsync(_fiscalYearId).Returns(Task.FromResult(_fiscalYear));

        _balanceService = new AccountBalanceService(_journalRepo, _lineRepo, _closingRepo, _accountRepo);
        _appService = new BudgetVarianceReportAppService(
            _budgetRepo,
            _accountRepo,
            _fiscalYearRepo,
            _balanceService);
    }

    [Fact]
    public async Task GetReportAsync_SubmittedBudgets_CalculatesVarianceCorrectly()
    {
        var expenseAccountId = Guid.NewGuid();
        var expenseAccount = new Account(expenseAccountId, _companyId, "5100", "Office Supplies", AccountType.Expense);

        var budget = new Budget(Guid.NewGuid(), _companyId, _fiscalYearId, "Cost Center", Guid.NewGuid());
        budget.AddAccount(expenseAccountId, 10000m, "Office Supplies");
        budget.Submit();

        var budgets = new List<Budget> { budget };
        _budgetRepo.GetListAsync(Arg.Any<Expression<Func<Budget, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Budget, bool>>>().Compile();
                return Task.FromResult(budgets.Where(predicate).ToList());
            });

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(Task.FromResult(new List<Account> { expenseAccount }));

        // Post 6,000 expense
        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2026, 4, 15));
        je.AddLine(expenseAccountId, 6000m, true);
        je.AddLine(Guid.NewGuid(), 6000m, false);
        je.Validate();
        je.Post();

        var journals = new List<JournalEntry> { je };
        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntry, bool>>>().Compile();
                return Task.FromResult(journals.Where(predicate).ToList());
            });

        _lineRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntryLine, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntryLine, bool>>>().Compile();
                return Task.FromResult(journals.SelectMany(j => j.Lines).Where(predicate).ToList());
            });

        var request = new BudgetVarianceRequestDto
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.AccountId.ShouldBe(expenseAccountId);
        row.BudgetAmount.ShouldBe(10000m);
        row.ActualAmount.ShouldBe(6000m);
        row.Variance.ShouldBe(4000m);
        row.VariancePercent.ShouldBe(40.0m);
        row.IsOverBudget.ShouldBeFalse();
        report.TotalBudget.ShouldBe(10000m);
        report.TotalActual.ShouldBe(6000m);
        report.TotalVariance.ShouldBe(4000m);
        report.OverBudgetCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesPeriodClosingVouchers_FromActualExpense()
    {
        var expenseAccountId = Guid.NewGuid();
        var expenseAccount = new Account(expenseAccountId, _companyId, "5200", "Rent Expense", AccountType.Expense);

        var budget = new Budget(Guid.NewGuid(), _companyId, _fiscalYearId, "Cost Center", Guid.NewGuid());
        budget.AddAccount(expenseAccountId, 12000m, "Rent Expense");
        budget.Submit();

        var budgets = new List<Budget> { budget };
        _budgetRepo.GetListAsync(Arg.Any<Expression<Func<Budget, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Budget, bool>>>().Compile();
                return Task.FromResult(budgets.Where(predicate).ToList());
            });

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(Task.FromResult(new List<Account> { expenseAccount }));

        // 1. Regular expense: DR 12000
        var regularJe = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2026, 6, 1))
        {
            VoucherType = JournalEntryVoucherType.JournalEntry
        };
        regularJe.AddLine(expenseAccountId, 12000m, true);
        regularJe.AddLine(Guid.NewGuid(), 12000m, false);
        regularJe.Validate();
        regularJe.Post();

        // 2. Period closing voucher: CR 12000 at FY end (zeros out expense to retained earnings)
        var pcvJe = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2026, 12, 31))
        {
            VoucherType = JournalEntryVoucherType.PeriodClosing
        };
        pcvJe.AddLine(expenseAccountId, 12000m, false);
        pcvJe.AddLine(Guid.NewGuid(), 12000m, true);
        pcvJe.Validate();
        pcvJe.Post();

        var journals = new List<JournalEntry> { regularJe, pcvJe };
        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntry, bool>>>().Compile();
                return Task.FromResult(journals.Where(predicate).ToList());
            });

        _lineRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntryLine, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntryLine, bool>>>().Compile();
                return Task.FromResult(journals.SelectMany(j => j.Lines).Where(predicate).ToList());
            });

        var request = new BudgetVarianceRequestDto
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        // Must reflect DR 12000 actuals, NOT 0 (from PCV reversal)
        row.ActualAmount.ShouldBe(12000m);
        row.Variance.ShouldBe(0m);
        row.IsOverBudget.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportAsync_OverBudget_FlaggedCorrectly()
    {
        var expenseAccountId = Guid.NewGuid();
        var expenseAccount = new Account(expenseAccountId, _companyId, "5300", "Travel", AccountType.Expense);

        var budget = new Budget(Guid.NewGuid(), _companyId, _fiscalYearId, "Cost Center", Guid.NewGuid());
        budget.AddAccount(expenseAccountId, 5000m, "Travel");
        budget.Submit();

        var budgets = new List<Budget> { budget };
        _budgetRepo.GetListAsync(Arg.Any<Expression<Func<Budget, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Budget, bool>>>().Compile();
                return Task.FromResult(budgets.Where(predicate).ToList());
            });

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(Task.FromResult(new List<Account> { expenseAccount }));

        // Post 7,000 expense (over budget by 2,000)
        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2026, 8, 1));
        je.AddLine(expenseAccountId, 7000m, true);
        je.AddLine(Guid.NewGuid(), 7000m, false);
        je.Validate();
        je.Post();

        var journals = new List<JournalEntry> { je };
        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntry, bool>>>().Compile();
                return Task.FromResult(journals.Where(predicate).ToList());
            });

        _lineRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntryLine, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<JournalEntryLine, bool>>>().Compile();
                return Task.FromResult(journals.SelectMany(j => j.Lines).Where(predicate).ToList());
            });

        var request = new BudgetVarianceRequestDto
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.BudgetAmount.ShouldBe(5000m);
        row.ActualAmount.ShouldBe(7000m);
        row.Variance.ShouldBe(-2000m);
        row.VariancePercent.ShouldBe(-40.0m);
        row.IsOverBudget.ShouldBeTrue();
        report.OverBudgetCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetReportAsync_FiltersByCostCenter()
    {
        var costCenterA = Guid.NewGuid();
        var costCenterB = Guid.NewGuid();

        var accA = Guid.NewGuid();
        var accB = Guid.NewGuid();
        var accountA = new Account(accA, _companyId, "5001", "Ops Expense", AccountType.Expense);
        var accountB = new Account(accB, _companyId, "5002", "Sales Expense", AccountType.Expense);

        var budgetA = new Budget(Guid.NewGuid(), _companyId, _fiscalYearId, "Cost Center", costCenterA);
        budgetA.AddAccount(accA, 4000m);
        budgetA.Submit();

        var budgetB = new Budget(Guid.NewGuid(), _companyId, _fiscalYearId, "Cost Center", costCenterB);
        budgetB.AddAccount(accB, 8000m);
        budgetB.Submit();

        var budgets = new List<Budget> { budgetA, budgetB };
        _budgetRepo.GetListAsync(Arg.Any<Expression<Func<Budget, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Budget, bool>>>().Compile();
                return Task.FromResult(budgets.Where(predicate).ToList());
            });

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Account, bool>>>().Compile();
                return Task.FromResult(new List<Account> { accountA, accountB }.Where(predicate).ToList());
            });

        _journalRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry>()));

        // Filter for Cost Center A only
        var request = new BudgetVarianceRequestDto
        {
            CompanyId = _companyId,
            FiscalYearId = _fiscalYearId,
            CostCenterId = costCenterA
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        report.Rows[0].AccountId.ShouldBe(accA);
        report.TotalBudget.ShouldBe(4000m);
    }
}
