using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class WorkOrderMaterialReturnTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateMaterialReturn_Excludes_MaterialConsumption()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Return Test Co 1"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series 1", "SE", "SE1-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-RET-1", "Finished Good 1", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-RET-1", "Raw Material 1", ItemType.Goods), autoSave: true);

            var storesWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stores Wh 1"), autoSave: true);
            var wipWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP Wh 1"), autoSave: true);

            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-RET-1", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-RET-001", fgItem.Id, bom.Id, quantity: 10m)
            {
                SourceWarehouseId = storesWh.Id,
                WipWarehouseId = wipWh.Id,
            };
            var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Material 1", requiredQuantity: 10m)
            {
                SourceWarehouseId = storesWh.Id,
                TransferredQuantity = 10m,
                ConsumedQuantity = 6m, // 6 consumed via Material Consumption for Manufacture (ERPNext PR #59310)
            };
            wo.RequiredItems.Add(woItem);
            wo.Submit();
            wo.Start();
            await woRepo.InsertAsync(wo, autoSave: true);

            var result = await mfgAppService.CreateMaterialReturnForManufactureAsync(wo.Id);

            result.ShouldNotBeNull();
            result.ItemCount.ShouldBe(1);

            var returnEntry = await seRepo.GetAsync(result.StockEntryId);
            returnEntry.IsReturn.ShouldBeTrue();
            returnEntry.EntryType.ShouldBe(StockEntryType.MaterialTransferForManufacture);
            returnEntry.Items.Count.ShouldBe(1);

            var item = returnEntry.Items.First();
            item.ItemId.ShouldBe(rmItem.Id);
            item.Quantity.ShouldBe(4m); // 10 transferred - 6 consumed = 4 returnable
            item.SourceWarehouseId.ShouldBe(wipWh.Id);
            item.TargetWarehouseId.ShouldBe(storesWh.Id);
        });
    }

    [Fact]
    public async Task CreateMaterialReturn_Excludes_ManufactureConsumption()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Return Test Co 2"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series 2", "SE", "SE2-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-RET-2", "Finished Good 2", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-RET-2", "Raw Material 2", ItemType.Goods), autoSave: true);

            var storesWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stores Wh 2"), autoSave: true);
            var wipWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP Wh 2"), autoSave: true);

            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-RET-2", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-RET-002", fgItem.Id, bom.Id, quantity: 10m)
            {
                SourceWarehouseId = storesWh.Id,
                WipWarehouseId = wipWh.Id,
            };
            var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Material 2", requiredQuantity: 10m)
            {
                SourceWarehouseId = storesWh.Id,
                TransferredQuantity = 10m,
                ConsumedQuantity = 7m, // 7 consumed during Manufacture
            };
            wo.RequiredItems.Add(woItem);
            wo.Submit();
            wo.Start();
            await woRepo.InsertAsync(wo, autoSave: true);

            var result = await mfgAppService.CreateMaterialReturnForManufactureAsync(wo.Id);

            var returnEntry = await seRepo.GetAsync(result.StockEntryId);
            returnEntry.Items.First().Quantity.ShouldBe(3m); // 10 - 7 = 3 returnable
        });
    }

    [Fact]
    public async Task CreateMaterialReturn_Throws_WhenAllMaterialsConsumed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Return Test Co 3"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series 3", "SE", "SE3-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-RET-3", "Finished Good 3", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-RET-3", "Raw Material 3", ItemType.Goods), autoSave: true);

            var storesWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stores Wh 3"), autoSave: true);
            var wipWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP Wh 3"), autoSave: true);

            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-RET-3", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-RET-003", fgItem.Id, bom.Id, quantity: 10m)
            {
                SourceWarehouseId = storesWh.Id,
                WipWarehouseId = wipWh.Id,
            };
            var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Material 3", requiredQuantity: 10m)
            {
                SourceWarehouseId = storesWh.Id,
                TransferredQuantity = 10m,
                ConsumedQuantity = 10m, // All 10 consumed
            };
            wo.RequiredItems.Add(woItem);
            wo.Submit();
            wo.Start();
            await woRepo.InsertAsync(wo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() => mfgAppService.CreateMaterialReturnForManufactureAsync(wo.Id));
        });
    }

    [Fact]
    public async Task StockEntryAppService_PostAndCancel_MaterialConsumption_UpdatesWorkOrderConsumedQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var accountRepo = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
            var fiscalYearRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var stockEntryAppService = GetRequiredService<IStockEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Return Test Co 4"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series 4", "StockEntry", "SE4-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series 4b", "SE", "SE4b-"), autoSave: true);
            await fiscalYearRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY-RET4", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            var stockAccount = await accountRepo.InsertAsync(
                new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "9RET401", "Stock Account", AccountType.Asset), autoSave: true);
            var adjAccount = await accountRepo.InsertAsync(
                new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "9RET402", "Stock Adj", AccountType.Equity), autoSave: true);
            var wipAccount = await accountRepo.InsertAsync(
                new MyERP.Accounting.Entities.Account(Guid.NewGuid(), company.Id, "9RET403", "WIP Account", AccountType.Asset), autoSave: true);

            company.DefaultInventoryAccountId = stockAccount.Id;
            company.DefaultStockAdjustmentAccountId = adjAccount.Id;
            company.DefaultWipAccountId = wipAccount.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-RET-4", "Finished Good 4", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-RET-4", "Raw Material 4", ItemType.Goods), autoSave: true);

            var wipWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP Wh 4"), autoSave: true);
            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-RET-4", fgItem.Id), autoSave: true);

            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, rmItem.Id, wipWh.Id,
                DateTime.Today.AddDays(-1), quantityChange: 100m, valuationRate: 10m,
                balanceQuantity: 100m, balanceValue: 1000m)
            {
                StockQueue = "[[100,10]]",
            }, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-RET-004", fgItem.Id, bom.Id, quantity: 10m)
            {
                WipWarehouseId = wipWh.Id,
            };
            var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Raw Material 4", requiredQuantity: 10m)
            {
                TransferredQuantity = 10m,
                ConsumedQuantity = 0m,
            };
            wo.RequiredItems.Add(woItem);
            wo.Submit();
            wo.Start();
            await woRepo.InsertAsync(wo, autoSave: true);

            var se = await stockEntryAppService.CreateAsync(new CreateStockEntryDto
            {
                CompanyId = company.Id,
                EntryType = StockEntryType.MaterialConsumptionForManufacture,
                PostingDate = DateTime.Today,
                WorkOrderId = wo.Id,
                Items = new List<CreateStockEntryItemDto>
                {
                    new CreateStockEntryItemDto
                    {
                        ItemId = rmItem.Id,
                        Quantity = 6m,
                        SourceWarehouseId = wipWh.Id,
                    }
                }
            });

            await stockEntryAppService.SubmitAsync(se.Id);
            await stockEntryAppService.PostAsync(se.Id);

            var updatedWo = await woRepo.GetAsync(wo.Id, includeDetails: true);
            updatedWo.RequiredItems.First().ConsumedQuantity.ShouldBe(6m);

            await stockEntryAppService.CancelAsync(se.Id);

            var cancelledWo = await woRepo.GetAsync(wo.Id, includeDetails: true);
            cancelledWo.RequiredItems.First().ConsumedQuantity.ShouldBe(0m);
        });
    }
}
