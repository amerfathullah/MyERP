using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Unit tests for Profit and Loss Statement Margin View per ERPNext PR #60012 / commits f180489d02, 63040a87d9.
/// - Base the margin view on TotalRevenue (100% base) rather than looking for a hardcoded account name.
/// - Handles numbered accounts ("4000 - Sales") and custom income roots without falling back to raw amounts.
/// </summary>
public class ProfitLossMarginViewTests
{
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly AccountBalanceService _balanceService;
    private readonly ReportingAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _salesAccountId = Guid.NewGuid();
    private readonly Guid _cogsAccountId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();

    public ProfitLossMarginViewTests()
    {
        _balanceService = new AccountBalanceService(_journalEntryRepo, _journalLineRepo, _closingBalanceRepo, _accountRepo);
        _appService = new ReportingAppService(
            _accountRepo,
            _journalLineRepo,
            _journalEntryRepo,
            _balanceService,
            _customerRepo,
            _supplierRepo,
            _companyRepo,
            _fiscalYearRepo);
    }

    [Fact]
    public async Task GetProfitLossAsync_WithNumberedAccounts_ComputesMarginsBasedOnTotalRevenue()
    {
        // Arrange: Revenue 150, COGS (Expense) 30 -> Net Profit 120
        var fromDate = new DateTime(2025, 1, 1);
        var toDate = new DateTime(2025, 1, 31);

        var salesAccount = new Account(_salesAccountId, _companyId, "4000", "4000 - Sales Revenue", AccountType.Revenue);
        var cogsAccount = new Account(_cogsAccountId, _companyId, "5000", "5000 - Cost of Goods Sold", AccountType.Expense);

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { salesAccount, cogsAccount });

        // Journal Entry for Sales: Debit Bank 150, Credit Sales 150
        var jeSale = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2025, 1, 10));
        jeSale.AddLine(_bankAccountId, 150m, isDebit: true);
        jeSale.AddLine(_salesAccountId, 150m, isDebit: false);
        jeSale.Post();

        // Journal Entry for COGS: Debit COGS 30, Credit Bank 30
        var jeCost = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2025, 1, 15));
        jeCost.AddLine(_cogsAccountId, 30m, isDebit: true);
        jeCost.AddLine(_bankAccountId, 30m, isDebit: false);
        jeCost.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { jeSale, jeCost });

        // Act
        var result = await _appService.GetProfitLossAsync(new ProfitLossRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate
        });

        // Assert
        result.TotalRevenue.ShouldBe(150m);
        result.TotalExpense.ShouldBe(30m);
        result.NetProfitOrLoss.ShouldBe(120m);

        // Revenue row margin: 150 / 150 * 100 = 100%
        result.RevenueRows.Count.ShouldBe(1);
        result.RevenueRows[0].MarginPercentage.ShouldBe(100m);

        // Expense row margin: 30 / 150 * 100 = 20%
        result.ExpenseRows.Count.ShouldBe(1);
        result.ExpenseRows[0].MarginPercentage.ShouldBe(20m);

        // Report summary margins
        result.TotalExpenseMarginPercentage.ShouldBe(20m);
        result.NetProfitMarginPercentage.ShouldBe(80m); // 120 / 150 * 100 = 80%
    }

    [Fact]
    public async Task GetProfitLossAsync_WithZeroRevenue_MarginsAreNull()
    {
        // Arrange: No revenue, only Expense 50
        var fromDate = new DateTime(2025, 1, 1);
        var toDate = new DateTime(2025, 1, 31);

        var cogsAccount = new Account(_cogsAccountId, _companyId, "5000", "5000 - Cost of Goods Sold", AccountType.Expense);

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { cogsAccount });

        var jeCost = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2025, 1, 15));
        jeCost.AddLine(_cogsAccountId, 50m, isDebit: true);
        jeCost.AddLine(_bankAccountId, 50m, isDebit: false);
        jeCost.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { jeCost });

        // Act
        var result = await _appService.GetProfitLossAsync(new ProfitLossRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate
        });

        // Assert
        result.TotalRevenue.ShouldBe(0m);
        result.TotalExpense.ShouldBe(50m);
        result.NetProfitOrLoss.ShouldBe(-50m);

        result.ExpenseRows[0].MarginPercentage.ShouldBeNull();
        result.TotalExpenseMarginPercentage.ShouldBeNull();
        result.NetProfitMarginPercentage.ShouldBeNull();
    }
}
