using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class GrossAndNetProfitReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportingAppService _reportingAppService;
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;

    protected GrossAndNetProfitReportTests()
    {
        _reportingAppService = GetRequiredService<IReportingAppService>();
        _accountAppService = GetRequiredService<IAccountAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
    }

    [Fact]
    public async Task GrossAndNetProfit_Should_Split_Gross_And_NonGross_Trees_And_Compute_Both_Profits()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "GNP Co"), autoSave: true);
        var fy = await _fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

        var cash = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1100", "Cash", AccountType.Asset), autoSave: true);

        var incomeGroup = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "4000", "Income", AccountType.Revenue) { IsGroup = true }, autoSave: true);
        var directSales = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            CompanyId = company.Id,
            AccountCode = "4100",
            AccountName = "Direct Sales",
            AccountType = AccountType.Revenue,
            ParentAccountId = incomeGroup.Id,
            IncludeInGross = true,
        });
        var interestIncome = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            CompanyId = company.Id,
            AccountCode = "4900",
            AccountName = "Interest Income",
            AccountType = AccountType.Revenue,
            ParentAccountId = incomeGroup.Id,
            IncludeInGross = false,
        });

        var expenseGroup = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "5000", "Expenses", AccountType.Expense) { IsGroup = true }, autoSave: true);
        var cogs = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            CompanyId = company.Id,
            AccountCode = "5100",
            AccountName = "Cost of Goods Sold",
            AccountType = AccountType.Expense,
            ParentAccountId = expenseGroup.Id,
            IncludeInGross = true,
        });
        var adminRent = await _accountAppService.CreateAsync(new CreateUpdateAccountDto
        {
            CompanyId = company.Id,
            AccountCode = "5200",
            AccountName = "Office Rent",
            AccountType = AccountType.Expense,
            ParentAccountId = expenseGroup.Id,
            IncludeInGross = false,
        });

        // Jan: Direct Sales 1000, COGS 400; Feb: Interest Income 200, Office Rent 150
        var jeJan = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 1, 15));
        jeJan.AddLine(cash.Id, 1000m, isDebit: true);
        jeJan.AddLine(directSales.Id, 1000m, isDebit: false);
        jeJan.AddLine(cogs.Id, 400m, isDebit: true);
        jeJan.AddLine(cash.Id, 400m, isDebit: false);
        jeJan.Post();
        await _journalEntryRepository.InsertAsync(jeJan, autoSave: true);

        var jeFeb = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 2, 10));
        jeFeb.AddLine(cash.Id, 200m, isDebit: true);
        jeFeb.AddLine(interestIncome.Id, 200m, isDebit: false);
        jeFeb.AddLine(adminRent.Id, 150m, isDebit: true);
        jeFeb.AddLine(cash.Id, 150m, isDebit: false);
        jeFeb.Post();
        await _journalEntryRepository.InsertAsync(jeFeb, autoSave: true);

        var report = await _reportingAppService.GetGrossAndNetProfitAsync(new GrossAndNetProfitRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 2, 28),
            Periodicity = "Monthly",
            AccumulatedValues = true,
        });

        report.NothingIncludedInGross.ShouldBeFalse();
        report.PeriodLabels.Length.ShouldBe(2);
        report.GrossIncomeTotal.ShouldBe(1000m);
        report.GrossExpenseTotal.ShouldBe(400m);
        report.GrossProfit.ShouldBe(600m);
        report.NonGrossIncomeTotal.ShouldBe(200m);
        report.NonGrossExpenseTotal.ShouldBe(150m);
        report.NetProfit.ShouldBe(650m);

        // Accumulated: Feb period column on Gross Profit is still 600, Net Profit is 650 (600 Jan + 50 Feb)
        var netRow = report.Rows.Single(r => r.RowKind == "NetProfit");
        netRow.PeriodAmounts[0].ShouldBe(600m);
        netRow.PeriodAmounts[1].ShouldBe(650m);
        netRow.Total.ShouldBe(650m);

        // Group totals in Gross section only reflect IncludeInGross == true child (1000, not 1200)
        var grossIncomeGroupRow = report.Rows.SkipWhile(r => r.RowKind != "SectionHeader")
            .First(r => r.AccountId == incomeGroup.Id);
        grossIncomeGroupRow.Total.ShouldBe(1000m);
    }

    [Fact]
    public async Task GrossAndNetProfit_Should_Report_NothingIncludedInGross_And_Still_Show_NetProfit()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "No Gross Co"), autoSave: true);
        var fy = await _fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

        var cash = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1100", "Cash", AccountType.Asset), autoSave: true);
        var rev = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "4100", "Service Fee", AccountType.Revenue) { IncludeInGross = false }, autoSave: true);
        var exp = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "5100", "Utilities", AccountType.Expense) { IncludeInGross = false }, autoSave: true);

        var je = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 3, 5));
        je.AddLine(cash.Id, 500m, isDebit: true);
        je.AddLine(rev.Id, 500m, isDebit: false);
        je.AddLine(exp.Id, 125m, isDebit: true);
        je.AddLine(cash.Id, 125m, isDebit: false);
        je.Post();
        await _journalEntryRepository.InsertAsync(je, autoSave: true);

        var report = await _reportingAppService.GetGrossAndNetProfitAsync(new GrossAndNetProfitRequestDto
        {
            CompanyId = company.Id,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 3, 31),
        });

        report.NothingIncludedInGross.ShouldBeTrue();
        report.GrossProfit.ShouldBe(0m);
        report.NetProfit.ShouldBe(375m);
        report.Rows.ShouldContain(r => r.RowKind == "EmptyNote");
        report.Rows.ShouldNotContain(r => r.RowKind == "GrossProfit");
    }
}
