using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class ConsolidatedTrialBalanceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportingAppService _reportingAppService;
    private readonly ICompanyAppService _companyAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<CurrencyExchange, Guid> _currencyExchangeRepository;

    protected ConsolidatedTrialBalanceTests()
    {
        _reportingAppService = GetRequiredService<IReportingAppService>();
        _companyAppService = GetRequiredService<ICompanyAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
        _currencyExchangeRepository = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
    }

    [Fact]
    public async Task CompanyHierarchy_Should_Validate_Group_And_Circular_Parents()
    {
        var nonGroup = await _companyAppService.CreateAsync(new CreateUpdateCompanyDto
        {
            Name = "Leaf Co Valid",
            CurrencyCode = "MYR",
            IsGroup = false,
        });

        var groupEx = await Should.ThrowAsync<BusinessException>(() =>
            _companyAppService.CreateAsync(new CreateUpdateCompanyDto
            {
                Name = "Invalid Child Co",
                CurrencyCode = "MYR",
                ParentCompanyId = nonGroup.Id,
            }));
        groupEx.Data["detail"]!.ToString()!.ShouldContain("Group Company");

        var holding = await _companyAppService.CreateAsync(new CreateUpdateCompanyDto
        {
            Name = "Holding Co Valid",
            CurrencyCode = "MYR",
            IsGroup = true,
        });

        var sub = await _companyAppService.CreateAsync(new CreateUpdateCompanyDto
        {
            Name = "Sub Co Valid",
            CurrencyCode = "USD",
            ParentCompanyId = holding.Id,
            IsGroup = false,
        });
        sub.ParentCompanyId.ShouldBe(holding.Id);

        var unmarkEx = await Should.ThrowAsync<BusinessException>(() =>
            _companyAppService.UpdateAsync(holding.Id, new CreateUpdateCompanyDto
            {
                Name = holding.Name,
                CurrencyCode = holding.CurrencyCode,
                IsGroup = false,
            }));
        unmarkEx.Data["detail"]!.ToString()!.ShouldContain("subsidiary");
    }

    [Fact]
    public async Task ConsolidatedTrialBalance_Should_Translate_Subsidiaries_Merge_Accounts_And_Emit_Fctr()
    {
        var asOf = new DateTime(2026, 6, 30);

        var holding = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "APEX Holdings Bhd")
            {
                CurrencyCode = "MYR",
                ReportingCurrency = "MYR",
                IsGroup = true,
            }, autoSave: true);

        var usdSub = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "APEX US LLC")
            {
                CurrencyCode = "USD",
                ParentCompanyId = holding.Id,
                IsGroup = false,
            }, autoSave: true);

        await _currencyExchangeRepository.InsertAsync(
            new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 1.335m, new DateTime(2026, 6, 30)), autoSave: true);

        var fyHold = await _fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), holding.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
        var fySub = await _fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), usdSub.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

        // Holding accounts (MYR): Cash 100 Dr / Equity 100 Cr
        var holdCash = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), holding.Id, "1100", "Cash", AccountType.Asset), autoSave: true);
        var holdEquity = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), holding.Id, "3100", "Share Capital", AccountType.Equity), autoSave: true);

        var jeHold = new JournalEntry(Guid.NewGuid(), holding.Id, fyHold.Id, new DateTime(2026, 6, 15));
        jeHold.AddLine(holdCash.Id, 100m, isDebit: true);
        jeHold.AddLine(holdEquity.Id, 100m, isDebit: false);
        jeHold.Post();
        await _journalEntryRepository.InsertAsync(jeHold, autoSave: true);

        // Subsidiary accounts (USD) at rate 1.335:
        // Cash (1100) 1.00 USD -> 1.34 MYR, Receivables (1200) 1.00 USD -> 1.34 MYR, Revenue (4100) 2.00 USD -> 2.67 MYR
        // Rounding diff = +0.01 MYR -> FCTR row emitted for 0.01 Credit so TotalDebit == TotalCredit
        var subCash = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), usdSub.Id, "1100", "Cash", AccountType.Asset), autoSave: true);
        var subAr = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), usdSub.Id, "1200", "Receivables", AccountType.Asset), autoSave: true);
        var subRev = await _accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), usdSub.Id, "4100", "Sales Revenue", AccountType.Revenue), autoSave: true);

        var jeSub = new JournalEntry(Guid.NewGuid(), usdSub.Id, fySub.Id, new DateTime(2026, 6, 20));
        jeSub.AddLine(subCash.Id, 1.00m, isDebit: true);
        jeSub.AddLine(subAr.Id, 1.00m, isDebit: true);
        jeSub.AddLine(subRev.Id, 2.00m, isDebit: false);
        jeSub.Post();
        await _journalEntryRepository.InsertAsync(jeSub, autoSave: true);

        // Single-company TB does not include subsidiary
        var single = await _reportingAppService.GetTrialBalanceAsync(new TrialBalanceRequestDto
        {
            CompanyId = holding.Id,
            AsOfDate = asOf,
            IncludeSubsidiaries = false,
        });
        single.IsConsolidated.ShouldBeFalse();
        single.ConsolidatedCompanyCount.ShouldBe(1);
        single.TotalDebit.ShouldBe(100m);

        // Consolidated TB expands subtree, translates USD -> MYR, merges 1100 Cash, and balances via FCTR
        var consolidated = await _reportingAppService.GetTrialBalanceAsync(new TrialBalanceRequestDto
        {
            CompanyId = holding.Id,
            AsOfDate = asOf,
            IncludeSubsidiaries = true,
        });

        consolidated.IsConsolidated.ShouldBeTrue();
        consolidated.ConsolidatedCompanyCount.ShouldBe(2);
        consolidated.CurrencyCode.ShouldBe("MYR");

        var cashRow = consolidated.Rows.Single(r => r.AccountCode == "1100");
        cashRow.Debit.ShouldBe(101.34m);

        var arRow = consolidated.Rows.Single(r => r.AccountCode == "1200");
        arRow.Debit.ShouldBe(1.34m);

        var revRow = consolidated.Rows.Single(r => r.AccountCode == "4100");
        revRow.Credit.ShouldBe(2.67m);

        var fctrRow = consolidated.Rows.Single(r => r.AccountCode == "FCTR");
        fctrRow.Credit.ShouldBe(0.01m);

        consolidated.TotalDebit.ShouldBe(consolidated.TotalCredit);
        consolidated.TotalDebit.ShouldBe(102.68m);
    }
}
