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

public abstract class ConsolidatedFinancialStatementTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportingAppService _reportingAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<CurrencyExchange, Guid> _currencyExchangeRepository;

    protected ConsolidatedFinancialStatementTests()
    {
        _reportingAppService = GetRequiredService<IReportingAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
        _currencyExchangeRepository = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
    }

    [Fact]
    public async Task ConsolidatedStatements_Should_Compute_BS_PL_And_CashFlow_With_Accumulation_And_Fctr()
    {
        var holding = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "CFS Holdings Bhd")
            {
                CurrencyCode = "MYR",
                ReportingCurrency = "MYR",
                IsGroup = true,
            }, autoSave: true);

        var sub = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "CFS Singapore Pte Ltd")
            {
                CurrencyCode = "USD",
                ParentCompanyId = holding.Id,
                IsGroup = false,
            }, autoSave: true);

        // Historical tx rate Jan 15: 4.20; Closing rate Jun 30: 4.50
        await _currencyExchangeRepository.InsertAsync(
            new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.20m, new DateTime(2026, 1, 15)), autoSave: true);
        await _currencyExchangeRepository.InsertAsync(
            new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.50m, new DateTime(2026, 6, 30)), autoSave: true);

        var fyHold = await _fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), holding.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
        var fySub = await _fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), sub.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

        // Holding CoA (MYR)
        var holdCashGrp = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), holding.Id, "1000", "Current Assets", AccountType.Asset) { IsGroup = true }, autoSave: true);
        var holdCash = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), holding.Id, "1100", "Cash", AccountType.Asset)
            {
                ParentAccountId = holdCashGrp.Id,
                AccountSubType = AccountSubType.CashAccount,
            }, autoSave: true);
        var holdEquity = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), holding.Id, "3100", "Share Capital", AccountType.Equity), autoSave: true);
        var holdSales = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), holding.Id, "4100", "Sales", AccountType.Revenue), autoSave: true);

        // Subsidiary CoA (USD)
        var subCashGrp = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), sub.Id, "1000", "Current Assets", AccountType.Asset) { IsGroup = true }, autoSave: true);
        var subCash = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), sub.Id, "1100", "Cash", AccountType.Asset)
            {
                ParentAccountId = subCashGrp.Id,
                AccountSubType = AccountSubType.CashAccount,
            }, autoSave: true);
        var subAr = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), sub.Id, "1200", "Receivables", AccountType.Asset)
            {
                ParentAccountId = subCashGrp.Id,
                AccountSubType = AccountSubType.AccountsReceivable,
            }, autoSave: true);
        var subSales = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), sub.Id, "4100", "Sales", AccountType.Revenue), autoSave: true);
        var subRent = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), sub.Id, "5100", "Rent", AccountType.Expense), autoSave: true);

        // Holding JE: Cash 1000 Dr / Equity 1000 Cr; Cash 200 Dr / Sales 200 Cr (MYR)
        var jeHold = new JournalEntry(Guid.NewGuid(), holding.Id, fyHold.Id, new DateTime(2026, 2, 1));
        jeHold.AddLine(holdCash.Id, 1000m, isDebit: true);
        jeHold.AddLine(holdEquity.Id, 1000m, isDebit: false);
        jeHold.AddLine(holdCash.Id, 200m, isDebit: true);
        jeHold.AddLine(holdSales.Id, 200m, isDebit: false);
        jeHold.Post();
        await _journalEntryRepository.InsertAsync(jeHold, autoSave: true);

        // Sub JE (Jan 15 @ 4.20 tx rate, closing Jun 30 @ 4.50):
        // Cash 100 USD Dr, Receivables 50 USD Dr / Sales 150 USD Cr
        // Rent 30 USD Dr / Cash 30 USD Cr
        // Net Sub Cash = 70 USD (closing = 315 MYR), AR = 50 USD (closing = 225 MYR) => Sub Assets closing = 540 MYR
        // Sub Net P&L = 120 USD @ tx 4.20 = 504 MYR, closing 4.50 = 540 MYR => FX translation reserve = +36 MYR
        var jeSub = new JournalEntry(Guid.NewGuid(), sub.Id, fySub.Id, new DateTime(2026, 1, 15));
        jeSub.AddLine(subCash.Id, 100m, isDebit: true);
        jeSub.AddLine(subAr.Id, 50m, isDebit: true);
        jeSub.AddLine(subSales.Id, 150m, isDebit: false);
        jeSub.AddLine(subRent.Id, 30m, isDebit: true);
        jeSub.AddLine(subCash.Id, 30m, isDebit: false);
        jeSub.Post();
        await _journalEntryRepository.InsertAsync(jeSub, autoSave: true);

        // 1. Balance Sheet with PresentationCurrency = MYR & AccumulatedInGroupCompany = true
        var bsAcc = await _reportingAppService.GetConsolidatedFinancialStatementAsync(new ConsolidatedFinancialStatementRequestDto
        {
            CompanyId = holding.Id,
            ReportType = "BalanceSheet",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30),
            PresentationCurrency = "MYR",
            AccumulatedInGroupCompany = true,
        });

        bsAcc.CompanyColumns.Count.ShouldBe(2);
        var cashRow = bsAcc.Rows.Single(r => r.AccountCode == "1100");
        cashRow.CompanyAmounts[holding.Id].ShouldBe(1515m); // 1200 + 70*4.50
        cashRow.CompanyAmounts[sub.Id].ShouldBe(315m);
        cashRow.Total.ShouldBe(1515m); // No double-counting

        var grpRow = bsAcc.Rows.Single(r => r.AccountCode == "1000");
        grpRow.IsGroup.ShouldBeTrue();
        grpRow.Total.ShouldBe(1740m); // 1515 + 50*4.50 (225)

        var fctrBs = bsAcc.Rows.Single(r => r.AccountCode == "FCTR");
        fctrBs.CompanyAmounts[holding.Id].ShouldBe(36m); // 120 * (4.50 - 4.20)
        fctrBs.Total.ShouldBe(36m);

        var totalAssets = bsAcc.Summary.Single(s => s.Label == "Total Assets").Value;
        totalAssets.ShouldBe(1740m);

        // 2. Profit and Loss with AccumulatedInGroupCompany = false and default per-company currencies
        var plStandalone = await _reportingAppService.GetConsolidatedFinancialStatementAsync(new ConsolidatedFinancialStatementRequestDto
        {
            CompanyId = holding.Id,
            ReportType = "ProfitAndLoss",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30),
            AccumulatedInGroupCompany = false,
        });

        var salesRow = plStandalone.Rows.Single(r => r.AccountCode == "4100");
        salesRow.CompanyAmounts[holding.Id].ShouldBe(200m); // MYR
        salesRow.CompanyAmounts[sub.Id].ShouldBe(150m); // USD (column uses sub currency)
        salesRow.Total.ShouldBe(830m); // 200 + 150*4.20 = 830 MYR

        var netProfitRow = plStandalone.Rows.Single(r => r.AccountKey == "NET_PROFIT");
        netProfitRow.Total.ShouldBe(704m); // 200 + (150 - 30)*4.20 = 200 + 504

        // 3. Cash Flow Statement
        var cf = await _reportingAppService.GetConsolidatedFinancialStatementAsync(new ConsolidatedFinancialStatementRequestDto
        {
            CompanyId = holding.Id,
            ReportType = "CashFlow",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30),
            PresentationCurrency = "MYR",
            AccumulatedInGroupCompany = true,
        });

        var netCashSummary = cf.Summary.Single(s => s.Label == "Net Change in Cash").Value;
        cf.Rows.Any(r => r.AccountKey == "NET_CHANGE_IN_CASH").ShouldBeTrue();
        cf.Rows.Single(r => r.AccountName == "Foreign Currency Translation Reserve").Total.ShouldBe(36m);
        netCashSummary.ShouldBe(1515m);
    }
}
