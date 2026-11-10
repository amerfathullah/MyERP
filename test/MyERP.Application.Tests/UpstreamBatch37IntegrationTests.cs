using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch37IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // === 1) PR #65210 / commit e8c9d1948f: Set each Pick List row's Stock UOM from its Item ===

    [Fact]
    public async Task ConvertSalesOrderToPickList_SetsStockUomOnPickListRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var plRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var docConversionService = GetRequiredService<IDocumentConversionAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Pick Uom Co B37") { CurrencyCode = "MYR" },
                autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SO Series", "SalesOrder", "SO-B37-"),
                autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PL Series", "PickList", "PL-B37-"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Customer B37"),
                autoSave: true);

            var wh = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Main WH B37") { WarehouseType = WarehouseType.Standard },
                autoSave: true);

            // Component A with Stock UOM = "Kg"
            var compA = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-A-B37", "Component A", ItemType.Goods)
                {
                    MaintainStock = true,
                    Uom = "Kg"
                },
                autoSave: true);

            // Component B with Stock UOM = "Meter"
            var compB = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B-B37", "Component B", ItemType.Goods)
                {
                    MaintainStock = true,
                    Uom = "Meter"
                },
                autoSave: true);

            // Parent Bundle
            var parentItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-B37", "Combo Bundle B37", ItemType.Goods)
                {
                    MaintainStock = false,
                    Uom = "Unit"
                },
                autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), parentItem.Id)
            {
                ItemName = "Combo Bundle B37"
            };
            bundle.AddItem(compA.Id, 2m, "Component A", "Kg");
            bundle.AddItem(compB.Id, 3m, "Component B", "Meter");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Standard Item C with Stock UOM = "Litre"
            var standardItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "STD-C-B37", "Standard Item C", ItemType.Goods)
                {
                    MaintainStock = true,
                    Uom = "Litre"
                },
                autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-B37-001", DateTime.UtcNow);
            so.AddItem(parentItem.Id, "Combo Bundle B37", 1m, 100m, 0m, uom: "Unit");
            so.Items[0].WarehouseId = wh.Id;
            so.AddItem(standardItem.Id, "Standard Item C", 4m, 25m, 0m, uom: "Litre");
            so.Items[1].WarehouseId = wh.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Convert to Pick List
            var pickListId = await docConversionService.ConvertSalesOrderToPickListAsync(so.Id);
            var pickList = await plRepo.GetAsync(pickListId, includeDetails: true);

            // Verify bundle component rows get their Stock UOM from Item
            var rowA = pickList.Items.FirstOrDefault(i => i.ItemId == compA.Id);
            rowA.ShouldNotBeNull();
            rowA.Uom.ShouldBe("Kg");
            rowA.StockUom.ShouldBe("Kg");
            rowA.Qty.ShouldBe(2m);

            var rowB = pickList.Items.FirstOrDefault(i => i.ItemId == compB.Id);
            rowB.ShouldNotBeNull();
            rowB.Uom.ShouldBe("Meter");
            rowB.StockUom.ShouldBe("Meter");
            rowB.Qty.ShouldBe(3m);

            // Verify standard item row gets its Stock UOM from Item
            var rowC = pickList.Items.FirstOrDefault(i => i.ItemId == standardItem.Id);
            rowC.ShouldNotBeNull();
            rowC.Uom.ShouldBe("Litre");
            rowC.StockUom.ShouldBe("Litre");
            rowC.Qty.ShouldBe(4m);
        });
    }

    // === 2) PR #65211 / commit 173ca23551: Count disabled batches in stock-level batch queries ===

    [Fact]
    public async Task GetAvailableBatches_ForStockLevels_IncludesDisabledBatches()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var batchAppService = GetRequiredService<IBatchAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Batch Parity Co B37") { CurrencyCode = "MYR" },
                autoSave: true);

            var wh = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores WH B37") { WarehouseType = WarehouseType.Standard },
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BATCH-ITEM-B37", "Batch Tracked Item", ItemType.Goods)
                {
                    MaintainStock = true,
                    HasBatchNo = true,
                    Uom = "Unit"
                },
                autoSave: true);

            var batch = await batchRepo.InsertAsync(
                new Batch(Guid.NewGuid(), item.Id, "BATCH-B37-001")
                {
                    IsDisabled = false,
                    ExpiryDate = DateTime.UtcNow.AddMonths(6)
                },
                autoSave: true);

            // Create inward SLE with this batch
            await sleRepo.InsertAsync(
                new StockLedgerEntry(
                    Guid.NewGuid(),
                    company.Id,
                    item.Id,
                    wh.Id,
                    DateTime.UtcNow,
                    quantityChange: 15m,
                    valuationRate: 50m,
                    balanceQuantity: 15m,
                    balanceValue: 750m)
                {
                    BatchId = batch.Id,
                    VoucherType = "PurchaseReceipt",
                    VoucherId = Guid.NewGuid()
                },
                autoSave: true);

            // Query when enabled: normal picking finds the batch
            var activeBatches = await batchAppService.GetAvailableBatchesAsync(new GetAvailableBatchesDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = wh.Id,
                ForStockLevels = false
            });

            activeBatches.Count.ShouldBe(1);
            activeBatches[0].BatchId.ShouldBe(batch.Id);
            activeBatches[0].AvailableQuantity.ShouldBe(15m);

            // Disable the batch
            var batchToDisable = await batchRepo.GetAsync(batch.Id);
            batchToDisable.IsDisabled = true;
            await batchRepo.UpdateAsync(batchToDisable, autoSave: true);

            // Normal issue/picking query (ForStockLevels = false): disabled batch must be EXCLUDED
            var pickingBatches = await batchAppService.GetAvailableBatchesAsync(new GetAvailableBatchesDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = wh.Id,
                ForStockLevels = false
            });
            pickingBatches.Count.ShouldBe(0);

            // Stock level query (ForStockLevels = true, per PR #65211): disabled batch must be INCLUDED
            var stockLevelBatches = await batchAppService.GetAvailableBatchesAsync(new GetAvailableBatchesDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = wh.Id,
                ForStockLevels = true
            });
            stockLevelBatches.Count.ShouldBe(1);
            stockLevelBatches[0].BatchId.ShouldBe(batch.Id);
            stockLevelBatches[0].AvailableQuantity.ShouldBe(15m);
        });
    }
}
