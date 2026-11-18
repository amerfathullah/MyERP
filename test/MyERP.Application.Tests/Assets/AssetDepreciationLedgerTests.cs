using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// EF Core integration tests for Asset Depreciation Ledger report
/// per ERPNext accounts/report/asset_depreciation_ledger.
/// </summary>
public abstract class AssetDepreciationLedgerTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetAssetDepreciationLedgerAsync_ComputesRunningAccumulatedAndCarryingValues()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var bookRepo = GetRequiredService<IRepository<FinanceBook, Guid>>();
            var catRepo = GetRequiredService<IRepository<AssetCategory, Guid>>();
            var accRepo = GetRequiredService<IRepository<Account, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
            var reportingService = GetRequiredService<IAssetReportingAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Asset Ledger Co") { CurrencyCode = "MYR" }, autoSave: true);
            var fy = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY-2026-ADL",
                    new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            var defaultBook = await bookRepo.InsertAsync(
                new FinanceBook(Guid.NewGuid(), company.Id, "Corporate Book") { IsDefault = true }, autoSave: true);
            var taxBook = await bookRepo.InsertAsync(
                new FinanceBook(Guid.NewGuid(), company.Id, "Tax Book") { IsDefault = false }, autoSave: true);

            var category = await catRepo.InsertAsync(
                new AssetCategory(Guid.NewGuid(), "IT Equipment"), autoSave: true);

            var depExpGl = await accRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5300-ADL", "Depreciation Expense", AccountType.Expense), autoSave: true);
            var accDepGl = await accRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1590-ADL", "Accumulated Depreciation", AccountType.Asset), autoSave: true);

            var jePrior = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 8, 31)) { EntryNumber = "JE-DEP-08" };
            jePrior.AddLine(depExpGl.Id, 100m, true);
            jePrior.AddLine(accDepGl.Id, 100m, false);
            jePrior.Post();
            await jeRepo.InsertAsync(jePrior, autoSave: true);

            var jeOct = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 10, 31)) { EntryNumber = "JE-DEP-10" };
            jeOct.AddLine(depExpGl.Id, 100m, true);
            jeOct.AddLine(accDepGl.Id, 100m, false);
            jeOct.Post();
            await jeRepo.InsertAsync(jeOct, autoSave: true);

            var jeNov = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 11, 30)) { EntryNumber = "JE-DEP-11" };
            jeNov.AddLine(depExpGl.Id, 100m, true);
            jeNov.AddLine(accDepGl.Id, 100m, false);
            jeNov.Post();
            await jeRepo.InsertAsync(jeNov, autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-ADL-01", "Workstation Pro",
                new DateTime(2026, 1, 1), 1200m)
            {
                AssetCategoryId = category.Id,
                OpeningAccumulatedDepreciation = 200m,
            };

            var priorEntry = new DepreciationScheduleEntry(Guid.NewGuid(), asset.Id, new DateTime(2026, 8, 31), 100m, 300m)
            {
                FinanceBookId = defaultBook.Id
            };
            priorEntry.Book(jePrior.Id);
            asset.DepreciationSchedule.Add(priorEntry);

            var octEntry = new DepreciationScheduleEntry(Guid.NewGuid(), asset.Id, new DateTime(2026, 10, 31), 100m, 400m)
            {
                FinanceBookId = defaultBook.Id
            };
            octEntry.Book(jeOct.Id);
            asset.DepreciationSchedule.Add(octEntry);

            var novEntry = new DepreciationScheduleEntry(Guid.NewGuid(), asset.Id, new DateTime(2026, 11, 30), 100m, 500m)
            {
                FinanceBookId = defaultBook.Id
            };
            novEntry.Book(jeNov.Id);
            asset.DepreciationSchedule.Add(novEntry);

            // Unbooked future entry -> excluded
            asset.DepreciationSchedule.Add(new DepreciationScheduleEntry(Guid.NewGuid(), asset.Id, new DateTime(2026, 11, 15), 99m, 599m)
            {
                FinanceBookId = defaultBook.Id
            });

            // Tax book booked entry -> excluded when IncludeDefaultBookAssets=true
            var taxEntry = new DepreciationScheduleEntry(Guid.NewGuid(), asset.Id, new DateTime(2026, 10, 15), 250m, 250m)
            {
                FinanceBookId = taxBook.Id
            };
            taxEntry.Book(jeOct.Id);
            asset.DepreciationSchedule.Add(taxEntry);

            asset.Submit();
            await assetRepo.InsertAsync(asset, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                reportingService.GetAssetDepreciationLedgerAsync(new AssetDepreciationLedgerRequestDto
                {
                    CompanyId = company.Id,
                    FromDate = new DateTime(2026, 12, 1),
                    ToDate = new DateTime(2026, 10, 1),
                }));

            var report = await reportingService.GetAssetDepreciationLedgerAsync(new AssetDepreciationLedgerRequestDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 10, 1),
                ToDate = new DateTime(2026, 11, 30),
                IncludeDefaultBookAssets = true,
            });

            report.CurrencyCode.ShouldBe("MYR");
            report.TotalDepreciationAmount.ShouldBe(200m);
            report.Rows.Count.ShouldBe(2);

            report.Rows[0].AssetNumber.ShouldBe("AST-ADL-01");
            report.Rows[0].AssetCategoryName.ShouldBe("IT Equipment");
            report.Rows[0].DepreciationDate.ShouldBe(new DateTime(2026, 10, 31));
            report.Rows[0].DepreciationAmount.ShouldBe(100m);
            report.Rows[0].AccumulatedDepreciationAmount.ShouldBe(400m);
            report.Rows[0].ValueAfterDepreciation.ShouldBe(800m);
            report.Rows[0].DepreciationEntryNumber.ShouldBe("JE-DEP-10");

            report.Rows[1].DepreciationDate.ShouldBe(new DateTime(2026, 11, 30));
            report.Rows[1].DepreciationAmount.ShouldBe(100m);
            report.Rows[1].AccumulatedDepreciationAmount.ShouldBe(500m);
            report.Rows[1].ValueAfterDepreciation.ShouldBe(700m);
            report.Rows[1].DepreciationEntryNumber.ShouldBe("JE-DEP-11");
        });
    }
}
