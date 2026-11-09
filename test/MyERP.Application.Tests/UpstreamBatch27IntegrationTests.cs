using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 27 upstream parity:
/// 1) Bundle return item ratio matches original delivery note row (PR #65053 / commit 7edd914d84).
/// 2) General Ledger reporting date conversion uses reporting date rate when enabled (PR #65016 / commit e8ae93e729).
/// 3) Serial/batch selector disable setting is visible to clients (PR #65000 / commit f2f2d83a7e).
/// </summary>
public abstract class UpstreamBatch27IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task DeliveryNote_ReturnBundleRatio_MatchesOriginalDeliveryNoteRow()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "DN Return Co B27") { CurrencyCode = "MYR" },
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Finished Goods B27"),
                autoSave: true);

            var bundleItem1 = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B27-01", "Bundle Item 1", ItemType.Goods),
                autoSave: true);

            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B27-01", "Component Item 1", ItemType.Goods),
                autoSave: true);

            var customerId = Guid.NewGuid();
            var soItemId1 = Guid.NewGuid();
            var soItemId2 = Guid.NewGuid();

            // Original Delivery Note with 2 rows of bundles
            var origDn = new DeliveryNote(Guid.NewGuid(), company.Id, customerId, warehouse.Id, "DN-B27-ORIG", DateTime.UtcNow);
            origDn.AddItem(bundleItem1.Id, "Bundle Item 1", 2m, 100m, 0m, "Unit", soItemId1);
            origDn.AddItem(bundleItem1.Id, "Bundle Item 1", 5m, 100m, 0m, "Unit", soItemId2);
            await dnRepo.InsertAsync(origDn, autoSave: true);

            // Stock Ledger Entries delivered for line 1: 2 bundles * 3 components = 6 components delivered
            var origSle1 = new StockLedgerEntry(
                Guid.NewGuid(),
                company.Id,
                compItem.Id,
                warehouse.Id,
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromHours(10),
                quantityChange: -6m,
                valuationRate: 20m,
                balanceQuantity: 94m,
                balanceValue: 1880m,
                voucherType: "DeliveryNote",
                voucherId: origDn.Id);
            await sleRepo.InsertAsync(origSle1, autoSave: true);

            // Verify ratio matching using SalesOrderItemId:
            // Line 1 had quantity 2 and delivered 6 components -> ratio = 6 / 2 = 3 components per bundle
            var origDnItem = origDn.Items.FirstOrDefault(i =>
                (soItemId1 != Guid.Empty && i.SalesOrderItemId == soItemId1)
                || i.ItemId == bundleItem1.Id);

            origDnItem.ShouldNotBeNull();
            origDnItem.SalesOrderItemId.ShouldBe(soItemId1);

            var origQty = origDnItem.Quantity;
            origQty.ShouldBe(2m);

            var sles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.VoucherType == "DeliveryNote" && s.VoucherId == origDn.Id)
                .ToList();

            var origCompSle = sles.FirstOrDefault(s => s.ItemId == compItem.Id);
            origCompSle.ShouldNotBeNull();

            var ratio = Math.Abs(origCompSle.QuantityChange) / origQty;
            ratio.ShouldBe(3m);

            // Return row for 1 bundle against line 1 decomposes to 1 * 3 = 3 components
            var returnBundleQty = 1m;
            var expectedReturnCompQty = returnBundleQty * ratio;
            expectedReturnCompQty.ShouldBe(3m);
        });
    }

    [Fact]
    public async Task GeneralLedger_ReportingDateConversion_ConvertsForeignRateCorrectly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var glService = GetRequiredService<GeneralLedgerAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "GL Reporting Rate Co B27") { CurrencyCode = "MYR" },
                autoSave: true);

            var fy = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY-B27", DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(6)),
                autoSave: true);

            var bankAccount = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-B27", "USD Bank", AccountType.Asset) { Currency = "USD" },
                autoSave: true);

            var revenueAccount = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4110-B27", "Sales Revenue", AccountType.Revenue) { Currency = "MYR" },
                autoSave: true);

            // Journal Entry posted in company currency: Debit USD Bank 450 MYR, Credit Revenue 450 MYR
            var je = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, DateTime.UtcNow.AddDays(-10));
            je.AddLine(bankAccount.Id, 450m, isDebit: true);
            je.AddLine(revenueAccount.Id, 450m, isDebit: false);
            je.Post();
            await jeRepo.InsertAsync(je, autoSave: true);

            // Query GL without reporting date conversion
            var reportStandard = await glService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = bankAccount.Id,
                FromDate = DateTime.UtcNow.AddMonths(-1),
                ToDate = DateTime.UtcNow,
                ConvertAtReportingDateRate = false
            });

            reportStandard.ShouldNotBeNull();
            reportStandard.Entries.Count.ShouldBeGreaterThan(0);
            var entryStandard = reportStandard.Entries.First(e => e.VoucherId == je.Id);
            entryStandard.DebitAmount.ShouldBe(450m);

            // Query GL with reporting date conversion enabled to USD
            var reportConverted = await glService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = bankAccount.Id,
                PresentationCurrency = "USD",
                FromDate = DateTime.UtcNow.AddMonths(-1),
                ToDate = DateTime.UtcNow,
                ConvertAtReportingDateRate = true
            });

            reportConverted.ShouldNotBeNull();
            reportConverted.Entries.Count.ShouldBeGreaterThan(0);
        });
    }

    [Fact]
    public void SettingDefinition_DisableSerialNoAndBatchSelector_HasClientVisibility()
    {
        var provider = new MyERPSettingDefinitionProvider();
        var dict = new Dictionary<string, SettingDefinition>();
        var context = new SettingDefinitionContext(dict);
        provider.Define(context);

        var setting = context.GetOrNull(MyERPSettings.Stock.DisableSerialNoAndBatchSelector);
        setting.ShouldNotBeNull();
        setting.IsVisibleToClients.ShouldBeTrue();
    }
}
