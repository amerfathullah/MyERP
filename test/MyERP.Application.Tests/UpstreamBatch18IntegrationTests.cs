using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 18 upstream ERPNext parity:
/// 1) Product Bundle decomposition with ordered component ratio retention (PR #65005 / commits ea7e451bcf, 710a59f031).
/// </summary>
public abstract class UpstreamBatch18IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ProductBundleDecompositionService_DecomposesBundle_PreservingOrderedRatio_710a59f031()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var decompositionService = GetRequiredService<ProductBundleDecompositionService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 18 Bundle Co"), autoSave: true);
            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B18", "Parent Bundle", ItemType.Goods) { MaintainStock = false }, autoSave: true);
            var componentItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B18", "Component Item", ItemType.Goods) { MaintainStock = true }, autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(componentItem.Id, 1m, "Component Item", "Box");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Ordered ratio was 10 Nos per Box (stored in componentStockQtyPerUnit)
            var orderedRatios = new Dictionary<Guid, decimal>
            {
                [componentItem.Id] = 10m
            };

            var decomposed = await decompositionService.DecomposeAsync(
                itemId: bundleItem.Id,
                transactionQty: 3m,
                parentRate: 250m,
                componentStockQtyPerUnit: orderedRatios);

            decomposed.Count.ShouldBe(1);
            decomposed[0].ComponentItemId.ShouldBe(componentItem.Id);
            decomposed[0].Qty.ShouldBe(30m); // 3 * 10 = 30
        });
    }

    [Fact]
    public async Task ProductBundleDecompositionService_DecomposesReturn_PreservingOrderedRatio_ea7e451bcf()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var decompositionService = GetRequiredService<ProductBundleDecompositionService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 18 Bundle Co 2"), autoSave: true);
            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B18-2", "Parent Bundle 2", ItemType.Goods) { MaintainStock = false }, autoSave: true);
            var componentItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B18-2", "Component Item 2", ItemType.Goods) { MaintainStock = true }, autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(componentItem.Id, 1m, "Component Item 2", "Box");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var orderedRatios = new Dictionary<Guid, decimal>
            {
                [componentItem.Id] = 10m
            };

            var transactionItems = new List<BundleTransactionItem>
            {
                new BundleTransactionItem(
                    ItemId: bundleItem.Id,
                    Qty: -2m,
                    Rate: 250m,
                    ComponentStockQtyPerUnit: orderedRatios)
            };

            var result = await decompositionService.DecomposeTransactionItemsAsync(transactionItems, isReturn: true);
            result.PackedItems.Count.ShouldBe(1);
            result.PackedItems[0].ComponentItemId.ShouldBe(componentItem.Id);
            result.PackedItems[0].Qty.ShouldBe(-20m);
        });
    }
}
