using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 26 upstream parity:
/// 1) Pick list bundle with repeated component item keeps undelivered bundles pending (PR #65051 / commit fe42da0c74, cfa681da8c).
/// 2) Enforce warehouse access in batch quantity calculations (PR #64314 / commit cbc40ba340, 34e791febb, 55fd06a510).
/// 3) Item search index folds diacritics, kana, and character width (PR #65020 / commit 77c5fd1722, 88fabb805e).
/// </summary>
public abstract class UpstreamBatch26IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task PickListManager_BundleWithRepeatedComponent_KeepsUndeliveredBundlesPending()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "PickList Bundle Co B26") { CurrencyCode = "MYR" },
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores B26"),
                autoSave: true);

            var component = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B26-01", "Repeated Component B26", ItemType.Goods),
                autoSave: true);

            var soItemId = Guid.NewGuid();

            // Bundle requires: 1 unit + 2 units of Component = 3 units total per bundle
            var requiredStockQtyMap = new Dictionary<Guid, decimal>
            {
                [component.Id] = 3m
            };

            // Pick list picks 9 units (for 3 bundles)
            var pickItems = new List<PickListItem>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), component.Id, warehouse.Id, qty: 9m, stockQty: 9m, itemName: component.ItemName, batchId: null)
                {
                    SourceDocumentItemId = soItemId,
                    DeliveredQty = 0m
                }
            };

            // 1) Full bundle count
            var pickedBundles = PickListManager.ComputePickedQtyForBundle(
                pickItems, soItemId, requiredStockQtyMap, pendingOnly: false);

            pickedBundles.ShouldBe(3);

            // 2) Deliver 1 bundle (3 units delivered)
            pickItems[0].DeliveredQty = 3m;

            // 3) Pending undelivered bundles count
            var pendingBundles = PickListManager.ComputePickedQtyForBundle(
                pickItems, soItemId, requiredStockQtyMap, pendingOnly: true);

            pendingBundles.ShouldBe(2); // 6 units remaining / 3 = 2 bundles pending
        });
    }

    [Fact]
    public async Task BatchWarehousePermissions_RestrictsBatchesToAccessibleWarehouses()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Batch Perm Co B26") { CurrencyCode = "MYR" },
                autoSave: true);

            var whAllowed1 = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Allowed WH 1"),
                autoSave: true);

            var whAllowed2 = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Allowed WH 2"),
                autoSave: true);

            var whRestricted = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Restricted WH"),
                autoSave: true);

            var permittedWarehouseIds = new HashSet<Guid> { whAllowed1.Id, whAllowed2.Id };

            // Requesting allowed warehouse passes
            Should.NotThrow(() =>
                BatchWarehousePermissionValidator.ValidateWarehouseAccess(whAllowed1.Id, permittedWarehouseIds));

            // Requesting restricted warehouse throws
            var ex = Should.Throw<BusinessException>(() =>
                BatchWarehousePermissionValidator.ValidateWarehouseAccess(whRestricted.Id, permittedWarehouseIds));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.WarehouseAccessRestricted);

            // Filtering list of balances returns only allowed warehouses
            var balances = new List<(Guid WarehouseId, decimal Qty)>
            {
                (whAllowed1.Id, 100m),
                (whAllowed2.Id, 50m),
                (whRestricted.Id, 500m)
            };

            var filtered = BatchWarehousePermissionValidator.FilterByPermittedWarehouses(
                balances, b => b.WarehouseId, permittedWarehouseIds);

            filtered.Count.ShouldBe(2);
            filtered.ShouldNotContain(b => b.WarehouseId == whRestricted.Id);
        });
    }

    [Fact]
    public async Task ItemSearchNormalization_MatchesAcrossDiacriticsAndCharacterWidths()
    {
        await WithUnitOfWorkAsync(() =>
        {
            // Accent insensitive
            var itemTitle = "ZZ-CRÈME-7782";
            var query1 = "creme-7782";
            var query2 = "crême";

            var foldedTitle = ItemSearchNormalizationService.FoldText(itemTitle);
            var foldedQuery1 = ItemSearchNormalizationService.FoldText(query1);
            var foldedQuery2 = ItemSearchNormalizationService.FoldText(query2);

            foldedTitle.ToUpperInvariant().ShouldContain(foldedQuery1.ToUpperInvariant());
            foldedTitle.ToUpperInvariant().ShouldContain(foldedQuery2.ToUpperInvariant());

            // Width and kana insensitive
            var fullWidthQuery = "ＡＢＣ";
            var standardQuery = "ABC";
            ItemSearchNormalizationService.FoldText(fullWidthQuery)
                .ShouldBe(ItemSearchNormalizationService.FoldText(standardQuery));

            var katakana = "テスト";
            var hiragana = "てすと";
            ItemSearchNormalizationService.FoldText(katakana)
                .ShouldBe(ItemSearchNormalizationService.FoldText(hiragana));

            return Task.CompletedTask;
        });
    }
}
