using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.BackgroundJobs;
using MyERP.Core.Entities;
using MyERP.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for DepreciationSchedulerJob, which never had a test. Found while auditing
/// for other instances of round-120's missing-eager-load bug class: the job's own query correctly
/// identified candidate assets via a SQL-translated `DepreciationSchedule.Any(...)` filter, but
/// without an explicit WithDetailsAsync/Include, the materialized Asset objects' own
/// DepreciationSchedule/DepreciationDetails collections came back empty — so unbookedEntries was
/// always empty and the job posted nothing for any asset, ever, despite NightlyProcessingWorker
/// enqueuing it and it correctly finding candidates.
/// </summary>
public abstract class DepreciationSchedulerJobTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ExecuteAsync_PostsDueDepreciationEntry()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var assetAppService = GetRequiredService<IAssetAppService>();
        var job = GetRequiredService<DepreciationSchedulerJob>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Depr Scheduler Test Co"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-DEPR", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Asset Series Depr", "Asset", "ASTDEPR-"), autoSave: true);

        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "18DEPR", "Test Depreciation Expense", AccountType.Expense), autoSave: true);
        var accumDepAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "19DEPR", "Test Accumulated Depreciation", AccountType.Asset), autoSave: true);

        company.DepreciationExpenseAccountId = expenseAccount.Id;
        company.AccumulatedDepreciationAccountId = accumDepAccount.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        // Available for use 13 months ago with a 12-month useful life and annual frequency —
        // exactly one schedule entry, due today.
        var created = await assetAppService.CreateAsync(new CreateAssetDto
        {
            AssetName = "Depreciation Test Asset",
            CompanyId = company.Id,
            PurchaseDate = DateTime.Today.AddMonths(-13),
            PurchaseAmount = 1200m,
            CalculateDepreciation = true,
            UsefulLifeMonths = 12,
            FrequencyMonths = 12,
            AvailableForUseDate = DateTime.Today.AddMonths(-13),
        });
        await assetAppService.SubmitAsync(created.Id);

        // Raw job call (not an AppService, carries no [UnitOfWork] of its own) needs an ambient
        // unit of work to keep the same DbContext alive across its sequence of repository calls.
        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new DepreciationSchedulerArgs { CompanyId = company.Id, TenantId = company.TenantId });
        });

        var reloaded = await assetAppService.GetAsync(created.Id);
        reloaded.ValueAfterDepreciation.ShouldBeLessThan(1200m);

        var journals = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        var journal = journals.Items.SingleOrDefault(j => j.Lines.Any(l => l.AccountId == accumDepAccount.Id));

        journal.ShouldNotBeNull();
        journal!.Lines.ShouldContain(l => l.AccountId == expenseAccount.Id && l.IsDebit);
        journal.Lines.ShouldContain(l => l.AccountId == accumDepAccount.Id && !l.IsDebit);
        journal.TotalDebit.ShouldBe(journal.TotalCredit);
    }

    /// <summary>
    /// Regression coverage for AssetAppService.CancelAsync, which used plain GetAsync(includeDetails:
    /// true) — a no-op for DepreciationSchedule/DepreciationDetails, same bug class as above. Booked
    /// depreciation JEs were never reversed on cancel, and Asset.Cancel()'s own booked-entry guard
    /// (checking the same always-empty collection) let the cancel through anyway.
    /// </summary>
    [Fact]
    public async Task CancelAsync_ReversesBookedDepreciationJournalEntry()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var journalAppService = GetRequiredService<IJournalEntryAppService>();
        var assetAppService = GetRequiredService<IAssetAppService>();
        var job = GetRequiredService<DepreciationSchedulerJob>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Depr Cancel Test Co"), autoSave: true);
        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY-DEPRCXL", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
            autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Asset Series DeprCxl", "Asset", "ASTDEPRCXL-"), autoSave: true);

        var expenseAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "18DEPRCXL", "Test Depreciation Expense", AccountType.Expense), autoSave: true);
        var accumDepAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "19DEPRCXL", "Test Accumulated Depreciation", AccountType.Asset), autoSave: true);

        company.DepreciationExpenseAccountId = expenseAccount.Id;
        company.AccumulatedDepreciationAccountId = accumDepAccount.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        var created = await assetAppService.CreateAsync(new CreateAssetDto
        {
            AssetName = "Depreciation Cancel Test Asset",
            CompanyId = company.Id,
            PurchaseDate = DateTime.Today.AddMonths(-13),
            PurchaseAmount = 1200m,
            CalculateDepreciation = true,
            UsefulLifeMonths = 12,
            FrequencyMonths = 12,
            AvailableForUseDate = DateTime.Today.AddMonths(-13),
        });
        await assetAppService.SubmitAsync(created.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            await job.ExecuteAsync(new DepreciationSchedulerArgs { CompanyId = company.Id, TenantId = company.TenantId });
        });

        var journalsBeforeCancel = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        journalsBeforeCancel.Items.Count.ShouldBe(1); // the depreciation entry posted above

        await assetAppService.CancelAsync(created.Id);

        var reloaded = await assetAppService.GetAsync(created.Id);
        reloaded.Status.ShouldBe(AssetStatus.Cancelled);

        var journalsAfterCancel = await journalAppService.GetListAsync(new CompanyFilteredPagedRequestDto { CompanyId = company.Id, MaxResultCount = 100 });
        journalsAfterCancel.Items.Count.ShouldBeGreaterThan(1); // original + reversal
    }
}
