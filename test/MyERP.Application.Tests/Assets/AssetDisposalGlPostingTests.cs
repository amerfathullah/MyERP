using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Positive-path regression coverage for AssetLifecycleManager.PostDisposalJournalEntryAsync and
/// PostValueAdjustmentJournalEntryAsync, neither of which ever had one (only the failure paths —
/// no category, no accounts-for-company — were tested, via NSubstitute mocks in
/// AssetLifecycleManagerTests). Both silently threw their "account missing" BusinessException even
/// with fully valid category-account configuration, because AssetCategory.Accounts was never
/// eager-loaded by the plain FindAsync() both methods used — found and fixed while building the
/// same fix for AssetCapitalizationPostingService (round 120). A mocked unit test can't catch this
/// class of bug (the mock just returns whatever's configured); only a real EFCore-backed
/// integration test, exercised through the actual AppServices, proves the eager-load actually
/// works.
/// </summary>
public abstract class AssetDisposalGlPostingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ScrapAsync_PostsDisposalJournalEntry_WithValidCategoryAccounts()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var categoryRepository = GetRequiredService<IRepository<AssetCategory, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var assetAppService = GetRequiredService<IAssetAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Disposal GL Test Co"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-DISP", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Asset Series Disp", "Asset", "ASTDISP-"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "JE Series Disp", "JE", "JEDISP-"), autoSave: true);

        var fixedAssetAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "15DISP", "Test Fixed Asset", AccountType.Asset), autoSave: true);
        var disposalAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "16DISP", "Test Disposal", AccountType.Revenue), autoSave: true);

        var category = new AssetCategory(Guid.NewGuid(), "Test Category Disp");
        category.AddAccount(Guid.NewGuid(), company.Id, fixedAssetAccount.Id);
        await categoryRepository.InsertAsync(category, autoSave: true);

        company.DisposalAccountId = disposalAccount.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        var created = await assetAppService.CreateAsync(new CreateAssetDto
        {
            AssetName = "Disposal Test Asset",
            CompanyId = company.Id,
            AssetCategoryId = category.Id,
            PurchaseDate = DateTime.Today,
            PurchaseAmount = 1000m,
        });
        await assetAppService.SubmitAsync(created.Id);

        // No accumulated depreciation configured on the category — scrapping at full book value
        // means the whole 1000 is a loss, avoiding the need for an AccumulatedDepreciationAccountId.
        await assetAppService.ScrapAsync(created.Id, DateTime.Today);

        var journals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = journals.Items.Single(j => j.ReferenceType == "Asset" && j.ReferenceId == created.Id);

        journal.Lines.ShouldContain(l => l.AccountId == fixedAssetAccount.Id && !l.IsDebit && l.Amount == 1000m);
        journal.Lines.ShouldContain(l => l.AccountId == disposalAccount.Id && l.IsDebit && l.Amount == 1000m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    [Fact]
    public async Task AssetValueAdjustment_SubmitAsync_PostsJournalEntry_WithValidCategoryAccounts()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var categoryRepository = GetRequiredService<IRepository<AssetCategory, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var assetAppService = GetRequiredService<IAssetAppService>();
        var adjustmentAppService = GetRequiredService<IAssetValueAdjustmentAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AVA GL Test Co 2"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-AVA2", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Asset Series AVA2", "Asset", "ASTAVA2-"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "JE Series AVA2", "JE", "JEAVA2-"), autoSave: true);

        var fixedAssetAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "15AVA2", "Test Fixed Asset", AccountType.Asset), autoSave: true);
        var reserveAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "17AVA2", "Test Revaluation Reserve", AccountType.Equity), autoSave: true);

        var category = new AssetCategory(Guid.NewGuid(), "Test Category AVA2");
        category.AddAccount(Guid.NewGuid(), company.Id, fixedAssetAccount.Id);
        await categoryRepository.InsertAsync(category, autoSave: true);

        var created = await assetAppService.CreateAsync(new CreateAssetDto
        {
            AssetName = "AVA Test Asset 2",
            CompanyId = company.Id,
            AssetCategoryId = category.Id,
            PurchaseDate = DateTime.Today,
            PurchaseAmount = 500m,
        });
        await assetAppService.SubmitAsync(created.Id);

        var adjustment = await adjustmentAppService.CreateAsync(new CreateUpdateAssetValueAdjustmentDto
        {
            AssetId = created.Id,
            CompanyId = company.Id,
            Date = DateTime.Today,
            CurrentAssetValue = 500m,
            NewAssetValue = 700m,
            DifferenceAccountId = reserveAccount.Id,
        });

        await adjustmentAppService.SubmitAsync(adjustment.Id);

        var journals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = journals.Items.Single(j => j.ReferenceType == "AssetValueAdjustment" && j.ReferenceId == adjustment.Id);

        journal.Lines.ShouldContain(l => l.AccountId == fixedAssetAccount.Id && l.IsDebit && l.Amount == 200m);
        journal.Lines.ShouldContain(l => l.AccountId == reserveAccount.Id && !l.IsDebit && l.Amount == 200m);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }
}
