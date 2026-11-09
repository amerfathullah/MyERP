using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 14 upstream ERPNext parity:
/// 1) Sales Partner GetPublishedPartnersAsync returns only published partners and sanitizes internal fields (PR #64708 / commit ec0e080942).
/// 2) General Ledger GetReportAsync converts foreign-currency accounts at reporting date rate when enabled (PR #65016 / commit e8ae93e729).
/// </summary>
public abstract class UpstreamBatch14IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SalesPartnerAppService_GetPublishedPartnersAsync_ReturnsOnlyPublishedPartners_ExcludesInternalFields_ec0e080942()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var partnerRepo = GetRequiredService<IRepository<SalesPartner, Guid>>();
            var partnerService = GetRequiredService<ISalesPartnerAppService>();

            // 1. Published and enabled partner
            var publishedPartner = new SalesPartner(Guid.NewGuid(), "Alpha Reseller", PartnerType.Reseller, 15m)
            {
                Website = "https://alpha.example.com",
                ShowInWebsite = true,
                LogoUrl = "https://alpha.example.com/logo.png",
                Introduction = "Premier reseller in APAC region.",
                ReferralCode = "SECRET-REF-01"
            };
            publishedPartner.AddTarget(null, 100m, 50000m);
            await partnerRepo.InsertAsync(publishedPartner, autoSave: true);

            // 2. Enabled but NOT published partner
            var unpublishedPartner = new SalesPartner(Guid.NewGuid(), "Beta Internal Partner", PartnerType.Distributor, 20m)
            {
                Website = "https://beta.example.com",
                ShowInWebsite = false,
                ReferralCode = "SECRET-REF-02"
            };
            await partnerRepo.InsertAsync(unpublishedPartner, autoSave: true);

            // 3. Published but DISABLED partner
            var disabledPartner = new SalesPartner(Guid.NewGuid(), "Gamma Inactive Partner", PartnerType.Agent, 10m)
            {
                Website = "https://gamma.example.com",
                ShowInWebsite = true,
                IsEnabled = false,
                ReferralCode = "SECRET-REF-03"
            };
            await partnerRepo.InsertAsync(disabledPartner, autoSave: true);

            // Query public website partners
            var publicResult = await partnerService.GetPublishedPartnersAsync(new PagedAndSortedResultRequestDto
            {
                MaxResultCount = 20,
                SkipCount = 0
            });

            // Must only contain the active published partner
            publicResult.Items.ShouldContain(p => p.Name == "Alpha Reseller");
            publicResult.Items.ShouldNotContain(p => p.Name == "Beta Internal Partner");
            publicResult.Items.ShouldNotContain(p => p.Name == "Gamma Inactive Partner");

            var alphaDto = publicResult.Items.First(p => p.Name == "Alpha Reseller");
            alphaDto.Website.ShouldBe("https://alpha.example.com");
            alphaDto.LogoUrl.ShouldBe("https://alpha.example.com/logo.png");
            alphaDto.Introduction.ShouldBe("Premier reseller in APAC region.");

            // Verify internal list API still returns all partners with sensitive data
            var internalResult = await partnerService.GetListAsync(new GetSalesPartnerListDto
            {
                MaxResultCount = 20,
                SkipCount = 0
            });

            var internalAlpha = internalResult.Items.First(p => p.Name == "Alpha Reseller");
            internalAlpha.CommissionRate.ShouldBe(15m);
            internalAlpha.ReferralCode.ShouldBe("SECRET-REF-01");
            internalAlpha.Targets.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task GeneralLedgerAppService_ConvertAtReportingDateRate_RetranslatesForeignCurrencyAccount_e8ae93e729()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var journalRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var exchangeRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var glService = GetRequiredService<IGeneralLedgerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "GL Rep Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            var fy = await fyRepo.InsertAsync(new FiscalYear(
                Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            // Foreign bank account in USD
            var usdAccount = await accountRepo.InsertAsync(new Account(
                Guid.NewGuid(), company.Id, "1110-USD", "USD Bank Account", AccountType.Asset)
            {
                AccountSubType = AccountSubType.BankAccount,
                Currency = "USD",
                IsGroup = false
            }, autoSave: true);

            // Local cash account in MYR
            var cashAccount = await accountRepo.InsertAsync(new Account(
                Guid.NewGuid(), company.Id, "1100-MYR", "MYR Cash Account", AccountType.Asset)
            {
                AccountSubType = AccountSubType.CashAccount,
                Currency = "MYR",
                IsGroup = false
            }, autoSave: true);

            var toDate = new DateTime(2026, 6, 30);
            var postingDate = new DateTime(2026, 6, 20);

            // Journal entry: Debit USD Account 800 MYR (= 200 USD at booking rate 4.0), Credit Cash 800 MYR
            var jv = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, postingDate)
            {
                EntryNumber = "JV-USD-01"
            };
            jv.AddLine(usdAccount.Id, 800m, isDebit: true, description: "Deposit USD");
            jv.Lines.First(l => l.AccountId == usdAccount.Id).AccountCurrency = "USD";
            jv.Lines.First(l => l.AccountId == usdAccount.Id).AmountInAccountCurrency = 200m;

            jv.AddLine(cashAccount.Id, 800m, isDebit: false, description: "Cash outlay");
            jv.Post();
            await journalRepo.InsertAsync(jv, autoSave: true);

            // Reporting date exchange rate: 5.0 MYR per USD on toDate
            await exchangeRepo.InsertAsync(new CurrencyExchange(
                Guid.NewGuid(), "USD", "MYR", 5.0m, toDate), autoSave: true);

            // 1. Report without ConvertAtReportingDateRate (uses historical base amount)
            var baseReport = await glService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = postingDate.AddDays(-1),
                ToDate = toDate,
                ConvertAtReportingDateRate = false
            });

            baseReport.Entries.Count.ShouldBe(1);
            baseReport.Entries[0].DebitAmount.ShouldBe(800m); // In company currency MYR

            // 2. Report with ConvertAtReportingDateRate = true (converts at 5.0 on toDate: 800 / 5.0 = 160 USD)
            var retranslatedReport = await glService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = postingDate.AddDays(-1),
                ToDate = toDate,
                ConvertAtReportingDateRate = true
            });

            retranslatedReport.Entries.Count.ShouldBe(1);
            retranslatedReport.Entries[0].DebitAmount.ShouldBe(160m); // 800 / 5.0 = 160 USD
            retranslatedReport.PresentationCurrency.ShouldBe("USD");
            retranslatedReport.TotalDebit.ShouldBe(160m);
        });
    }
}
