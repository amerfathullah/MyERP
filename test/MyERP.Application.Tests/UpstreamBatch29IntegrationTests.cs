using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 29 upstream parity:
/// 1) Product bundle demand netting on partial delivery in MRP (PR #64317 / commit 007b7f3162).
/// 2) Whole-pack PO calculation with float noise guard (PR #64310 / commit 11ea4f022a).
/// 3) Item Price converted from Standard Selling Rate rounds to currency precision (PR #64855 / commit 1853ef83ea).
/// </summary>
public abstract class UpstreamBatch29IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ProductionPlanningReport_SalesOrderBundle_NetsDeliveredComponents()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var reportService = GetRequiredService<ProductionPlanningReportAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "MRP Bundle Netting Co B29") { CurrencyCode = "MYR" },
                autoSave: true);

            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B29-01", "Bundle Item B29", ItemType.Goods),
                autoSave: true);

            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B29-01", "Component Item B29", ItemType.Goods),
                autoSave: true);

            // Bundle: 1 Bundle contains 4 Components
            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItem.Id, 4m, "Component Item B29", "Unit");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var customerId = Guid.NewGuid();
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customerId, "SO-B29-01", DateTime.UtcNow);
            so.AddItem(bundleItem.Id, "Bundle Item B29", 5m, 200m, 0m, "Unit", productBundleId: bundle.Id);

            // Simulate 2 bundles delivered (out of 5 ordered -> 3 pending)
            var soItem = so.Items.First();
            soItem.DeliveredQty = 2m;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Generate Production Planning Report based on Sales Orders
            var report = await reportService.GetReportAsync(new ProductionPlanningReportFilterDto
            {
                CompanyId = company.Id,
                BasedOn = "SalesOrder"
            });

            report.ShouldNotBeNull();
            var bundleRow = report.Rows.FirstOrDefault(r => r.ProductionItemCode == bundleItem.ItemCode && r.RawMaterialCode == compItem.ItemCode);
            bundleRow.ShouldNotBeNull();

            // Total 4 * 3 = 12 units required for 3 pending bundles (PR #64317 / commit 007b7f3162)
            bundleRow.QtyToManufacture.ShouldBe(3m);
            bundleRow.RequiredQty.ShouldBe(12m);
        });
    }

    [Fact]
    public void UomConversionService_CalculateWholePackPurchaseOrderQty_HandlesExactMultiple()
    {
        var qty = UomConversionService.CalculateWholePackPurchaseOrderQty(
            requiredStockQty: 0.3m,
            minOrderQty: 0m,
            conversionFactor: 0.1m,
            mustBeWholeNumber: true);

        qty.ShouldBe(3m);
    }

    [Fact]
    public void ItemPriceAutoInsertService_StandardSellingRate_RoundsPrecision()
    {
        var usdPrice = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 150m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "USD",
            exchangeRate: 4.5m);

        usdPrice.ShouldNotBeNull();
        usdPrice.Value.ShouldBe(33.33m); // 150 / 4.5 = 33.3333... -> 33.33

        var jpyPrice = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 150m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "JPY",
            exchangeRate: 0.03m);

        jpyPrice.ShouldNotBeNull();
        jpyPrice.Value.ShouldBe(5000m); // 150 / 0.03 = 5000
    }
}
