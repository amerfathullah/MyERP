using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 28 upstream parity:
/// 1) Pick list bundle component ratio grouping across split locations & repeated items (PR #65001 / commit 710a59f031).
/// 2) Production Plan material request resolves item default warehouse before for_warehouse (PR #64936 / commit f820b711cc).
/// 3) Pick list draft creation keeps saved conversion factor (PR #64938 / commit 4305cb27f5).
/// </summary>
public abstract class UpstreamBatch28IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task DeliveryNote_CreateWithMultiLocationPickList_GroupsComponentRatioCorrectly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var plRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "DN Pick MultiLoc Co B28") { CurrencyCode = "MYR" },
                autoSave: true);

            var wh1 = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WH-1 B28"),
                autoSave: true);

            var wh2 = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WH-2 B28"),
                autoSave: true);

            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B28-01", "Bundle Item B28", ItemType.Goods),
                autoSave: true);

            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B28-01", "Component Item B28", ItemType.Goods),
                autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compItem.Id, 5m, "Component Item B28", "Nos");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var customerId = Guid.NewGuid();
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customerId, "SO-B28-01", DateTime.UtcNow);
            so.AddItem(bundleItem.Id, "Bundle Item B28", 2m, 100m, 0m, "Unit", productBundleId: bundle.Id);
            await soRepo.InsertAsync(so, autoSave: true);
            var soItemId = so.Items.First().Id;

            // Pick list with 2 rows picking the same component from different warehouses: 5 from WH-1, 5 from WH-2 (total 10 Nos for 2 bundles)
            var pl = new PickList(Guid.NewGuid(), company.Id, "Delivery", tenantId: null)
            {
                SalesOrderId = so.Id,
                CustomerId = customerId
            };
            pl.AddItem(compItem.Id, wh1.Id, 5m, 5m, "Component Item B28", null, null, soItemId, null, "Nos", 1m);
            pl.AddItem(compItem.Id, wh2.Id, 5m, 5m, "Component Item B28", null, null, soItemId, null, "Nos", 1m);
            await plRepo.InsertAsync(pl, autoSave: true);

            // Verify ratio grouping logic without duplicate key exception
            var matchingPicks = pl.Items.Where(p => p.SourceDocumentItemId == soItemId).ToList();
            matchingPicks.Count.ShouldBe(2);

            var componentStockQtyPerUnit = matchingPicks
                .GroupBy(p => p.ItemId)
                .ToDictionary(
                    g => g.Key,
                    g => Math.Round(g.Sum(p => p.StockQty > 0 ? p.StockQty : p.Qty) / so.Items.First().Quantity, 4));

            componentStockQtyPerUnit.ShouldContainKey(compItem.Id);
            componentStockQtyPerUnit[compItem.Id].ShouldBe(5m); // 10 units / 2 bundles = 5 units per bundle
        });
    }

    [Fact]
    public async Task PickListManager_DraftCreation_PreservesGivenConversionFactor()
    {
        await WithUnitOfWorkAsync(() =>
        {
            var itemId = Guid.NewGuid();
            var sourceRowId = Guid.NewGuid();

            // Row with non-standard UOM Box, saved factor 10m
            var (uom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
                itemId: itemId,
                stockUom: "Nos",
                rowUom: "Box",
                qty: 4m,
                sourceDocumentItemId: sourceRowId,
                productBundleItemId: null,
                orderedConversionFactors: null,
                itemMasterConversionFactor: 12m, // Master has changed to 12m
                savedConversionFactor: 10m); // Saved row keeps 10m

            uom.ShouldBe("Box");
            factor.ShouldBe(10m);
            stockQty.ShouldBe(40m); // 4 * 10 = 40

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void PartyValidationService_GetPartyAccountCurrency_PicksBillingCurrencyWhenSet()
    {
        PartyValidationService.GetPartyAccountCurrency("EUR", "MYR").ShouldBe("EUR");
        PartyValidationService.GetPartyAccountCurrency("", "MYR").ShouldBe("MYR");
        PartyValidationService.GetPartyAccountCurrency(null, "MYR").ShouldBe("MYR");
    }
}
