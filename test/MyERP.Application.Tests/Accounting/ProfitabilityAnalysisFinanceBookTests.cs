using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Unit tests for Profitability Analysis and Finance Book filtering per ERPNext PR #60003 / commits 9ba66b3b3f, 41818912bc.
/// - Leaves out entries of other finance books from profitability analysis and P&L drilldowns.
/// - Allows filtering by specific finance book (including bookless entries) and company default finance book.
/// - Guards against selecting a different book while requesting default book entries.
/// - Supports tree accumulation into parent cost centers and project-based dimensions.
/// </summary>
public class ProfitabilityAnalysisFinanceBookTests
{
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<FinanceBook, Guid> _financeBookRepo = Substitute.For<IRepository<FinanceBook, Guid>>();
    private readonly IRepository<CostCenter, Guid> _costCenterRepo = Substitute.For<IRepository<CostCenter, Guid>>();
    private readonly IRepository<Project, Guid> _projectRepo = Substitute.For<IRepository<Project, Guid>>();

    private readonly AccountBalanceService _balanceService;
    private readonly ReportingAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _incomeAccountId = Guid.NewGuid();
    private readonly Guid _expenseAccountId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Account _incomeAccount;
    private readonly Account _expenseAccount;
    private readonly Account _bankAccount;

    public ProfitabilityAnalysisFinanceBookTests()
    {
        _incomeAccount = new Account(_incomeAccountId, _companyId, "4100", "Sales Revenue", AccountType.Revenue);
        _expenseAccount = new Account(_expenseAccountId, _companyId, "5100", "Cost of Goods Sold", AccountType.Expense);
        _bankAccount = new Account(_bankAccountId, _companyId, "1210", "Bank Account", AccountType.Asset)
        {
            AccountSubType = AccountSubType.BankAccount
        };

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Account, bool>>>().Compile();
                return Task.FromResult(new List<Account> { _incomeAccount, _expenseAccount, _bankAccount }.Where(predicate).ToList());
            });

        _balanceService = new AccountBalanceService(_journalEntryRepo, _journalLineRepo, _closingBalanceRepo, _accountRepo);
        _appService = new ReportingAppService(
            _accountRepo,
            _journalLineRepo,
            _journalEntryRepo,
            _balanceService,
            _customerRepo,
            _supplierRepo,
            _companyRepo,
            _fiscalYearRepo,
            financeBookRepository: _financeBookRepo,
            costCenterRepository: _costCenterRepo,
            projectRepository: _projectRepo);
    }

    [Fact]
    public async Task GetProfitabilityAnalysisAsync_IncomeExpenseAndGrossProfit_PerCostCenter()
    {
        var ccId = Guid.NewGuid();
        var costCenter = new CostCenter(ccId, _companyId, "Main Operations");

        _costCenterRepo.GetListAsync(Arg.Any<Expression<Func<CostCenter, bool>>>())
            .Returns(Task.FromResult(new List<CostCenter> { costCenter }));

        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 12, 31);

        var je = CreateJournalEntry(fromDate);
        // Income: CR 10000
        var lineIncome = new JournalEntryLine(Guid.NewGuid(), je.Id, _incomeAccountId, 10000m, false)
        {
            CostCenterId = ccId
        };
        // Expense: DR 4000
        var lineExpense = new JournalEntryLine(Guid.NewGuid(), je.Id, _expenseAccountId, 4000m, true)
        {
            CostCenterId = ccId
        };
        je.AddLine(lineIncome);
        je.AddLine(lineExpense);
        je.AddLine(_bankAccountId, 6000m, true); // Balancing: DR 6000
        je.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { je }));

        var report = await _appService.GetProfitabilityAnalysisAsync(new ProfitabilityAnalysisRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            BasedOn = "Cost Center"
        });

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.DimensionId.ShouldBe(ccId);
        row.Income.ShouldBe(10000m);
        row.Expense.ShouldBe(4000m);
        row.GrossProfitOrLoss.ShouldBe(6000m);

        report.TotalIncome.ShouldBe(10000m);
        report.TotalExpense.ShouldBe(4000m);
        report.TotalGrossProfitOrLoss.ShouldBe(6000m);
    }

    [Fact]
    public async Task GetProfitabilityAnalysisAsync_ParentCostCenterAccumulatesChildren()
    {
        var parentId = Guid.NewGuid();
        var child1Id = Guid.NewGuid();
        var child2Id = Guid.NewGuid();

        var parentCc = new CostCenter(parentId, _companyId, "Parent Department", isGroup: true);
        var child1Cc = new CostCenter(child1Id, _companyId, "Child Division 1", parentId: parentId);
        var child2Cc = new CostCenter(child2Id, _companyId, "Child Division 2", parentId: parentId);

        _costCenterRepo.GetListAsync(Arg.Any<Expression<Func<CostCenter, bool>>>())
            .Returns(Task.FromResult(new List<CostCenter> { parentCc, child1Cc, child2Cc }));

        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 12, 31);

        var je = CreateJournalEntry(fromDate);
        // Child 1 income: CR 10000
        var line1 = new JournalEntryLine(Guid.NewGuid(), je.Id, _incomeAccountId, 10000m, false)
        {
            CostCenterId = child1Id
        };
        // Child 2 expense: DR 3000
        var line2 = new JournalEntryLine(Guid.NewGuid(), je.Id, _expenseAccountId, 3000m, true)
        {
            CostCenterId = child2Id
        };
        je.AddLine(line1);
        je.AddLine(line2);
        je.AddLine(_bankAccountId, 7000m, true); // Balancing: DR 7000
        je.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { je }));

        var report = await _appService.GetProfitabilityAnalysisAsync(new ProfitabilityAnalysisRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            BasedOn = "Cost Center"
        });

        var parentRow = report.Rows.Single(r => r.DimensionId == parentId);
        parentRow.Income.ShouldBe(10000m);
        parentRow.Expense.ShouldBe(3000m);
        parentRow.GrossProfitOrLoss.ShouldBe(7000m);

        // Total row must sum direct bookings without double-counting parent group
        report.TotalIncome.ShouldBe(10000m);
        report.TotalExpense.ShouldBe(3000m);
        report.TotalGrossProfitOrLoss.ShouldBe(7000m);
    }

    [Fact]
    public async Task GetProfitabilityAnalysisAsync_EntriesOfOtherFinanceBooksAreLeftOut()
    {
        // Per ERPNext PR #60003:
        // Entries booked only in an alternative book (e.g. tax-book depreciation) must not pollute main figures.
        var ccId = Guid.NewGuid();
        var costCenter = new CostCenter(ccId, _companyId, "Finance Book Branch");

        _costCenterRepo.GetListAsync(Arg.Any<Expression<Func<CostCenter, bool>>>())
            .Returns(Task.FromResult(new List<CostCenter> { costCenter }));

        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 12, 31);

        var je = CreateJournalEntry(fromDate);
        // Bookless expense: 300
        var lineStandard = new JournalEntryLine(Guid.NewGuid(), je.Id, _expenseAccountId, 300m, true)
        {
            CostCenterId = ccId,
            FinanceBook = null
        };
        // Tax book expense: 500
        var lineTaxBook = new JournalEntryLine(Guid.NewGuid(), je.Id, _expenseAccountId, 500m, true)
        {
            CostCenterId = ccId,
            FinanceBook = "Tax Depreciation Book"
        };
        je.AddLine(lineStandard);
        je.AddLine(lineTaxBook);
        je.AddLine(_bankAccountId, 800m, false); // Balancing: CR 800
        je.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { je }));

        // 1. Without finance_book filter: only bookless entries included (expense = 300, 500 left out!)
        var reportWithoutBook = await _appService.GetProfitabilityAnalysisAsync(new ProfitabilityAnalysisRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            BasedOn = "Cost Center"
        });

        reportWithoutBook.Rows[0].Expense.ShouldBe(300m);

        // 2. With finance_book = "Tax Depreciation Book": includes both bookless and tax book (300 + 500 = 800)
        var reportWithBook = await _appService.GetProfitabilityAnalysisAsync(new ProfitabilityAnalysisRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            BasedOn = "Cost Center",
            FinanceBook = "Tax Depreciation Book"
        });

        reportWithBook.Rows[0].Expense.ShouldBe(800m);
    }

    [Fact]
    public async Task GetProfitabilityAnalysisAsync_WithIncludeDefaultBookEntries_ThrowsIfDifferentBookSelected()
    {
        var defaultBook = new FinanceBook(Guid.NewGuid(), _companyId, "Primary IFRS Book") { IsDefault = true };
        _financeBookRepo.GetListAsync(Arg.Any<Expression<Func<FinanceBook, bool>>>())
            .Returns(Task.FromResult(new List<FinanceBook> { defaultBook }));

        var ex = await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await _appService.GetProfitabilityAnalysisAsync(new ProfitabilityAnalysisRequestDto
            {
                CompanyId = _companyId,
                FromDate = new DateTime(2026, 1, 1),
                ToDate = new DateTime(2026, 12, 31),
                FinanceBook = "Alternative Tax Book",
                IncludeDefaultBookEntries = true
            });
        });

        ex.Message.ShouldContain("To use a different finance book, please uncheck 'Include Default FB Entries'");
    }

    [Fact]
    public async Task GetProfitLossAsync_WithFinanceBookFilter_FiltersEntriesForDrilldownConsistency()
    {
        // Per ERPNext PR #41818912bc:
        // Opening P&L Statement from a profitability analysis row must preserve finance book filter.
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 12, 31);

        var je = CreateJournalEntry(fromDate);
        var lineRev = new JournalEntryLine(Guid.NewGuid(), je.Id, _incomeAccountId, 1000m, false)
        {
            FinanceBook = null
        };
        var lineExpStandard = new JournalEntryLine(Guid.NewGuid(), je.Id, _expenseAccountId, 200m, true)
        {
            FinanceBook = null
        };
        var lineExpTaxBook = new JournalEntryLine(Guid.NewGuid(), je.Id, _expenseAccountId, 150m, true)
        {
            FinanceBook = "Tax Book"
        };
        je.AddLine(lineRev);
        je.AddLine(lineExpStandard);
        je.AddLine(lineExpTaxBook);
        je.AddLine(_bankAccountId, 650m, true); // Balancing: DR 650 (DR 200 + DR 150 + DR 650 = CR 1000)
        je.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { je }));

        // Standard P&L (without finance book filter) -> leaves out "Tax Book" expense
        var plStandard = await _appService.GetProfitLossAsync(new ProfitLossRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate
        });

        plStandard.TotalRevenue.ShouldBe(1000m);
        plStandard.TotalExpense.ShouldBe(200m);
        plStandard.NetProfitOrLoss.ShouldBe(800m);

        // Filtered P&L for "Tax Book" -> includes standard + tax book expense (200 + 150 = 350)
        var plTax = await _appService.GetProfitLossAsync(new ProfitLossRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            FinanceBook = "Tax Book"
        });

        plTax.TotalRevenue.ShouldBe(1000m);
        plTax.TotalExpense.ShouldBe(350m);
        plTax.NetProfitOrLoss.ShouldBe(650m);
    }

    private JournalEntry CreateJournalEntry(DateTime postingDate)
    {
        return new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), postingDate);
    }
}
