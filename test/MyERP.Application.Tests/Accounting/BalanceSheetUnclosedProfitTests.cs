using System;
using System.Collections.Generic;
using System.Linq;
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
/// Unit tests for Balance Sheet Unclosed Fiscal Years Profit / Loss calculation.
/// Per ERPNext PR #60004 / commits 89012ba339, 509e2f1581:
/// - Split net profit into unclosed prior fiscal years vs current year provisional profit.
/// - Prior profit shown in "Unclosed Fiscal Years Profit / Loss (Credit)" row.
/// - Current profit shown in "Provisional Profit / Loss (Credit)" row.
/// - Even when current loss offsets prior profit, both rows are preserved and displayed.
/// </summary>
public class BalanceSheetUnclosedProfitTests
{
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
    private readonly AccountBalanceService _balanceService;
    private readonly ReportingAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fyId2025 = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _capitalAccountId = Guid.NewGuid();
    private readonly Guid _salesAccountId = Guid.NewGuid();
    private readonly Guid _expenseAccountId = Guid.NewGuid();

    public BalanceSheetUnclosedProfitTests()
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
    public async Task GetBalanceSheetAsync_WithUnclosedPriorFiscalYear_SplitsUnclosedProfitAndProvisionalProfit()
    {
        // Arrange
        var asOfDate = new DateTime(2025, 6, 30);
        var currentFy = new FiscalYear(_fyId2025, _companyId, "2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        _fiscalYearRepo.GetListAsync(Arg.Any<Expression<Func<FiscalYear, bool>>>())
            .Returns(new List<FiscalYear> { currentFy });

        var bankAccount = new Account(_bankAccountId, _companyId, "1110", "Bank", AccountType.Asset);
        var capitalAccount = new Account(_capitalAccountId, _companyId, "3110", "Share Capital", AccountType.Equity);

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { bankAccount, capitalAccount });

        // Prior entry in 2024 (prior unclosed FY): Debit Bank 10,000, Credit Capital 4,000, Credit Revenue 6,000 -> Prior Profit = 6,000
        var jePrior = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2024, 6, 1));
        jePrior.AddLine(_bankAccountId, 10000m, isDebit: true);
        jePrior.AddLine(_capitalAccountId, 4000m, isDebit: false);
        jePrior.AddLine(_salesAccountId, 6000m, isDebit: false);
        jePrior.Post();

        // Current entry in 2025: Debit Bank 2,000 -> Current Provisional Profit = 2,000
        var jeCurrent = new JournalEntry(Guid.NewGuid(), _companyId, _fyId2025, new DateTime(2025, 3, 1));
        jeCurrent.AddLine(_bankAccountId, 2000m, isDebit: true);
        jeCurrent.AddLine(_capitalAccountId, 2000m, isDebit: true); // unbalance simulated via direct JE or balanced line
        // To keep JE valid per double entry:
        jeCurrent.ClearLines();
        jeCurrent.AddLine(_bankAccountId, 2000m, isDebit: true);
        jeCurrent.AddLine(_salesAccountId, 2000m, isDebit: false); // Sales is Revenue account (not on balance sheet rows)
        jeCurrent.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { jePrior, jeCurrent });

        // Act
        var result = await _appService.GetBalanceSheetAsync(new BalanceSheetRequestDto
        {
            CompanyId = _companyId,
            AsOfDate = asOfDate
        });

        // Assert
        result.TotalAssets.ShouldBe(12000m);
        result.TotalLiabilities.ShouldBe(0m);
        result.UnclosedFiscalYearsProfitLoss.ShouldBe(6000m);
        result.ProvisionalProfitLoss.ShouldBe(2000m);
        result.TotalEquity.ShouldBe(12000m); // 4000 Capital + 6000 Unclosed Profit + 2000 Provisional Profit

        var unclosedRow = result.EquityRows.FirstOrDefault(r => r.AccountName == "Unclosed Fiscal Years Profit / Loss (Credit)");
        unclosedRow.ShouldNotBeNull();
        unclosedRow.Amount.ShouldBe(6000m);

        var provisionalRow = result.EquityRows.FirstOrDefault(r => r.AccountName == "Provisional Profit / Loss (Credit)");
        provisionalRow.ShouldNotBeNull();
        provisionalRow.Amount.ShouldBe(2000m);
    }

    [Fact]
    public async Task GetBalanceSheetAsync_WithPriorProfitAndCurrentLoss_BothRowsRenderedPerCommit509e2f1581()
    {
        // Arrange
        var asOfDate = new DateTime(2025, 6, 30);
        var currentFy = new FiscalYear(_fyId2025, _companyId, "2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        _fiscalYearRepo.GetListAsync(Arg.Any<Expression<Func<FiscalYear, bool>>>())
            .Returns(new List<FiscalYear> { currentFy });

        var bankAccount = new Account(_bankAccountId, _companyId, "1110", "Bank", AccountType.Asset);
        var capitalAccount = new Account(_capitalAccountId, _companyId, "3110", "Share Capital", AccountType.Equity);

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { bankAccount, capitalAccount });

        // Prior entry in 2024: Debit Bank 10,000, Credit Capital 2,000 (Credit Revenue 8,000) -> Prior profit = 8,000
        var jePrior = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), new DateTime(2024, 11, 15));
        jePrior.AddLine(_bankAccountId, 10000m, isDebit: true);
        jePrior.AddLine(_capitalAccountId, 2000m, isDebit: false);
        jePrior.AddLine(_salesAccountId, 8000m, isDebit: false);
        jePrior.Post();

        // Current entry in 2025: Debit Expense 3,000, Credit Bank 3,000 -> Current loss = -3,000
        var jeCurrent = new JournalEntry(Guid.NewGuid(), _companyId, _fyId2025, new DateTime(2025, 2, 10));
        jeCurrent.AddLine(_expenseAccountId, 3000m, isDebit: true);
        jeCurrent.AddLine(_bankAccountId, 3000m, isDebit: false);
        jeCurrent.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { jePrior, jeCurrent });

        // Act
        var result = await _appService.GetBalanceSheetAsync(new BalanceSheetRequestDto
        {
            CompanyId = _companyId,
            AsOfDate = asOfDate
        });

        // Assert:
        // Total Bank (Asset): 10,000 - 3,000 = 7,000
        // Capital (Equity): 2,000
        // Total Net Profit = 7,000 - 0 - 2,000 = 5,000
        // Prior Profit = 8,000
        // Current Provisional Profit = 5,000 - 8,000 = -3,000 (Loss)
        result.TotalAssets.ShouldBe(7000m);
        result.UnclosedFiscalYearsProfitLoss.ShouldBe(8000m);
        result.ProvisionalProfitLoss.ShouldBe(-3000m);
        result.TotalEquity.ShouldBe(7000m); // 2000 Capital + 8000 Prior - 3000 Current

        var unclosedRow = result.EquityRows.FirstOrDefault(r => r.AccountName == "Unclosed Fiscal Years Profit / Loss (Credit)");
        unclosedRow.ShouldNotBeNull();
        unclosedRow.Amount.ShouldBe(8000m);

        var provisionalRow = result.EquityRows.FirstOrDefault(r => r.AccountName == "Provisional Profit / Loss (Credit)");
        provisionalRow.ShouldNotBeNull();
        provisionalRow.Amount.ShouldBe(-3000m);
    }

    [Fact]
    public async Task GetBalanceSheetAsync_WithNoPriorUnclosedYears_OnlyProvisionalProfitRowAdded()
    {
        // Arrange
        var asOfDate = new DateTime(2025, 6, 30);
        var currentFy = new FiscalYear(_fyId2025, _companyId, "2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        _fiscalYearRepo.GetListAsync(Arg.Any<Expression<Func<FiscalYear, bool>>>())
            .Returns(new List<FiscalYear> { currentFy });

        var bankAccount = new Account(_bankAccountId, _companyId, "1110", "Bank", AccountType.Asset);
        var capitalAccount = new Account(_capitalAccountId, _companyId, "3110", "Share Capital", AccountType.Equity);

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { bankAccount, capitalAccount });

        // Only current year entry in 2025
        var jeCurrent = new JournalEntry(Guid.NewGuid(), _companyId, _fyId2025, new DateTime(2025, 4, 1));
        jeCurrent.AddLine(_bankAccountId, 5000m, isDebit: true);
        jeCurrent.AddLine(_salesAccountId, 5000m, isDebit: false);
        jeCurrent.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { jeCurrent });

        // Act
        var result = await _appService.GetBalanceSheetAsync(new BalanceSheetRequestDto
        {
            CompanyId = _companyId,
            AsOfDate = asOfDate
        });

        // Assert
        result.UnclosedFiscalYearsProfitLoss.ShouldBe(0m);
        result.ProvisionalProfitLoss.ShouldBe(5000m);
        result.TotalAssets.ShouldBe(5000m);
        result.TotalEquity.ShouldBe(5000m);

        result.EquityRows.ShouldNotContain(r => r.AccountName == "Unclosed Fiscal Years Profit / Loss (Credit)");
        var provisionalRow = result.EquityRows.FirstOrDefault(r => r.AccountName == "Provisional Profit / Loss (Credit)");
        provisionalRow.ShouldNotBeNull();
        provisionalRow.Amount.ShouldBe(5000m);
    }
}
