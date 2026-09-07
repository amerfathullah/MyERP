using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

public abstract class FgConversionAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetFgConversionDetailsAsync_ReturnsAlternativesAndAvailableQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var altRepo = GetRequiredService<IRepository<ItemAlternative, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "FG Conv Test Co 1"), autoSave: true);
            var prodItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-PROD-1", "Main FG", ItemType.Goods), autoSave: true);
            var altItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ALT-1", "Alternative FG", ItemType.Goods), autoSave: true);

            await altRepo.InsertAsync(new ItemAlternative(Guid.NewGuid(), company.Id, prodItem.Id, altItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-FG-001", prodItem.Id, Guid.NewGuid(), 10m)
            {
                ProducedQuantity = 8m
            };
            await woRepo.InsertAsync(wo, autoSave: true);

            var details = await mfgAppService.GetFgConversionDetailsAsync(wo.Id);

            details.AvailableQty.ShouldBe(8m);
            details.AlternativeItems.ShouldContain(a => a.ItemId == altItem.Id && a.ItemCode == "FG-ALT-1");
        });
    }

    [Fact]
    public async Task CreateFgConversionEntryAsync_Throws_WhenSettingDisabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var altRepo = GetRequiredService<IRepository<ItemAlternative, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var settingsRepo = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "FG Conv Test Co 2"), autoSave: true);
            var prodItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-PROD-2", "Main FG 2", ItemType.Goods), autoSave: true);
            var altItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ALT-2", "Alternative FG 2", ItemType.Goods), autoSave: true);

            await altRepo.InsertAsync(new ItemAlternative(Guid.NewGuid(), company.Id, prodItem.Id, altItem.Id), autoSave: true);

            var settings = new ManufacturingSettings(Guid.NewGuid(), company.Id)
            {
                AllowAlternativeFinishedGoods = false
            };
            await settingsRepo.InsertAsync(settings, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-FG-002", prodItem.Id, Guid.NewGuid(), 10m)
            {
                ProducedQuantity = 10m
            };
            await woRepo.InsertAsync(wo, autoSave: true);

            var input = new CreateFgConversionEntryDto
            {
                WorkOrderId = wo.Id,
                AlternativeItemId = altItem.Id,
                Quantity = 5m
            };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                mfgAppService.CreateFgConversionEntryAsync(input));
            ex.Data["detail"]!.ToString()!.ShouldContain("Allow Alternative Finished Goods");
        });
    }

    [Fact]
    public async Task CreateFgConversionEntryAsync_Succeeds_WhenSettingEnabled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var altRepo = GetRequiredService<IRepository<ItemAlternative, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var settingsRepo = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "FG Conv Test Co 3"), autoSave: true);
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series", "SE", "SE-"), autoSave: true);
            var fgWarehouse = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Warehouse"), autoSave: true);

            var prodItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-PROD-3", "Main FG 3", ItemType.Goods), autoSave: true);
            var altItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-ALT-3", "Alternative FG 3", ItemType.Goods), autoSave: true);

            await altRepo.InsertAsync(new ItemAlternative(Guid.NewGuid(), company.Id, prodItem.Id, altItem.Id), autoSave: true);

            var settings = new ManufacturingSettings(Guid.NewGuid(), company.Id)
            {
                AllowAlternativeFinishedGoods = true
            };
            await settingsRepo.InsertAsync(settings, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-FG-003", prodItem.Id, Guid.NewGuid(), 10m)
            {
                ProducedQuantity = 10m,
                FgWarehouseId = fgWarehouse.Id
            };
            await woRepo.InsertAsync(wo, autoSave: true);

            var input = new CreateFgConversionEntryDto
            {
                WorkOrderId = wo.Id,
                AlternativeItemId = altItem.Id,
                Quantity = 4m
            };

            var result = await mfgAppService.CreateFgConversionEntryAsync(input);

            result.StockEntryId.ShouldNotBe(Guid.Empty);
            result.EntryType.ShouldBe(StockEntryType.Repack.ToString());
            result.ItemCount.ShouldBe(2);

            var entry = await seRepo.GetAsync(result.StockEntryId, includeDetails: true);
            entry.IsFgConversion.ShouldBeTrue();
            entry.WorkOrderId.ShouldBe(wo.Id);
            entry.Items.ShouldContain(i => i.ItemId == prodItem.Id && i.Quantity == 4m && i.SourceWarehouseId == fgWarehouse.Id);
            entry.Items.ShouldContain(i => i.ItemId == altItem.Id && i.Quantity == 4m && i.TargetWarehouseId == fgWarehouse.Id && i.IsFinishedItem);
        });
    }

    [Fact]
    public async Task ProductionPlan_CloseAndReopenAppService_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP Test Co"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PP-ITEM", "Plan Item", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-PP", item.Id), autoSave: true);

            var plan = new ProductionPlan(Guid.NewGuid(), company.Id, "PLAN-2026-001", DateTime.UtcNow);
            plan.AddPlannedItem(new ProductionPlanItem(Guid.NewGuid(), plan.Id, item.Id, "Plan Item", bom.Id, 5m));
            plan.Submit();
            await planRepo.InsertAsync(plan, autoSave: true);

            var closed = await planAppService.CloseAsync(plan.Id);
            closed.Status.ShouldBe(ProductionPlanStatus.Closed);

            var reopened = await planAppService.ReopenAsync(plan.Id);
            reopened.Status.ShouldBe(ProductionPlanStatus.Submitted);
        });
    }
}
