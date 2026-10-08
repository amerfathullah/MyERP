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

public abstract class ProductionPlanSubAssemblyWarehousePoolTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GroupSubAssemblyWarehouse_PoolsChildStock_NettingDeficit()
    {
        // ERPNext PR #60210: test_group_sub_assembly_warehouse_pools_child_stock
        // Child A has projected +300, Child B has projected -100.
        // Group warehouse pools stock: actual = 300, projected = 200.
        // FG needs 400 SF items. With skip_available = true:
        // SF PlannedQty = 400 - 200 = 200, AvailableQty = 300.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP SubAssembly Co 1"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PP-Series-1", "PP", "PP-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-POOL-1", "FG Pool 1", ItemType.Goods), autoSave: true);
            var sfItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SF-POOL-1", "SF Pool 1", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-POOL-1", "RM Pool 1", ItemType.Goods), autoSave: true);

            var sfBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SF-POOL-1", sfItem.Id) { Quantity = 1m, IsActive = true };
            sfBom.Items.Add(new BomItem(Guid.NewGuid(), sfBom.Id, rmItem.Id, "RM Pool 1", 1m, 10m));
            await bomRepo.InsertAsync(sfBom, autoSave: true);

            var fgBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-POOL-1", fgItem.Id) { Quantity = 1m, IsActive = true };
            var fgBomItem = new BomItem(Guid.NewGuid(), fgBom.Id, sfItem.Id, "SF Pool 1", 1m, 50m) { SubBomId = sfBom.Id };
            fgBom.Items.Add(fgBomItem);
            await bomRepo.InsertAsync(fgBom, autoSave: true);

            var groupWh = new Warehouse(Guid.NewGuid(), company.Id, "Sub Assembly Group WH") { IsGroup = true };
            await whRepo.InsertAsync(groupWh, autoSave: true);

            var childWhA = new Warehouse(Guid.NewGuid(), company.Id, "Pool Child WH A") { ParentWarehouseId = groupWh.Id, IsGroup = false };
            await whRepo.InsertAsync(childWhA, autoSave: true);

            var childWhB = new Warehouse(Guid.NewGuid(), company.Id, "Pool Child WH B") { ParentWarehouseId = groupWh.Id, IsGroup = false };
            await whRepo.InsertAsync(childWhB, autoSave: true);

            var binA = new Bin(Guid.NewGuid(), sfItem.Id, childWhA.Id) { ActualQty = 300m };
            await binRepo.InsertAsync(binA, autoSave: true);

            var binB = new Bin(Guid.NewGuid(), sfItem.Id, childWhB.Id) { ActualQty = 0m, ReservedQty = 100m };
            await binRepo.InsertAsync(binB, autoSave: true);

            var planDto = await planAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = DateTime.UtcNow,
                SubAssemblyWarehouseId = groupWh.Id,
                SkipAvailableSubAssemblyItem = true,
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = fgBom.Id,
                        PlannedQty = 400m
                    }
                }
            });

            var calculated = await planAppService.CalculateMaterialRequirementsAsync(planDto.Id);

            var sfRequirements = calculated.MaterialRequirements.Where(m => m.ItemId == sfItem.Id).ToList();
            sfRequirements.Count.ShouldBe(1);

            var sfRow = sfRequirements[0];
            sfRow.RequiredQty.ShouldBe(400m);
            sfRow.AvailableQty.ShouldBe(300m);
            sfRow.PlannedQty.ShouldBe(200m);
            sfRow.WarehouseId.ShouldBe(groupWh.Id);
            sfRow.ProcurementType.ShouldBe(SubAssemblyType.InHouseManufacturing);

            // Raw material needed only for the 200 units of SF that actually need to be produced
            var rmRequirements = calculated.MaterialRequirements.Where(m => m.ItemId == rmItem.Id).ToList();
            rmRequirements.Count.ShouldBe(1);
            rmRequirements[0].RequiredQty.ShouldBe(200m);
        });
    }

    [Fact]
    public async Task PooledSubAssemblyStock_ConsumedOnceAcrossBranches()
    {
        // ERPNext PR #60210: test_pooled_sub_assembly_stock_consumed_once_across_branches
        // Shared item stock pooled across Pool A (60) + Pool B (90) = 150.
        // Branch A needs 100 -> consumes 100, remaining pool = 50, row planned qty = 0.
        // Branch B needs 100 -> consumes remaining 50, row planned qty = 50.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP SubAssembly Co 2"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PP-Series-2", "PP", "PP-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-BRANCHES", "FG Branches", ItemType.Goods), autoSave: true);
            var branchAItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "BRANCH-A", "Branch A", ItemType.Goods), autoSave: true);
            var branchBItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "BRANCH-B", "Branch B", ItemType.Goods), autoSave: true);
            var sharedItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SHARED-ITEM", "Shared Item", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-LEAF", "RM Leaf", ItemType.Goods), autoSave: true);

            var sharedBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SHARED", sharedItem.Id) { Quantity = 1m, IsActive = true };
            sharedBom.Items.Add(new BomItem(Guid.NewGuid(), sharedBom.Id, rmItem.Id, "RM Leaf", 1m, 10m));
            await bomRepo.InsertAsync(sharedBom, autoSave: true);

            var branchABom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-BRANCH-A", branchAItem.Id) { Quantity = 1m, IsActive = true };
            branchABom.Items.Add(new BomItem(Guid.NewGuid(), branchABom.Id, sharedItem.Id, "Shared Item", 1m, 20m) { SubBomId = sharedBom.Id });
            await bomRepo.InsertAsync(branchABom, autoSave: true);

            var branchBBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-BRANCH-B", branchBItem.Id) { Quantity = 1m, IsActive = true };
            branchBBom.Items.Add(new BomItem(Guid.NewGuid(), branchBBom.Id, sharedItem.Id, "Shared Item", 1m, 20m) { SubBomId = sharedBom.Id });
            await bomRepo.InsertAsync(branchBBom, autoSave: true);

            var fgBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-BRANCHES", fgItem.Id) { Quantity = 1m, IsActive = true };
            fgBom.Items.Add(new BomItem(Guid.NewGuid(), fgBom.Id, branchAItem.Id, "Branch A", 1m, 30m) { SubBomId = branchABom.Id });
            fgBom.Items.Add(new BomItem(Guid.NewGuid(), fgBom.Id, branchBItem.Id, "Branch B", 1m, 30m) { SubBomId = branchBBom.Id });
            await bomRepo.InsertAsync(fgBom, autoSave: true);

            var groupWh = new Warehouse(Guid.NewGuid(), company.Id, "Shared Group WH") { IsGroup = true };
            await whRepo.InsertAsync(groupWh, autoSave: true);

            var poolWhA = new Warehouse(Guid.NewGuid(), company.Id, "Pool WH A") { ParentWarehouseId = groupWh.Id, IsGroup = false };
            await whRepo.InsertAsync(poolWhA, autoSave: true);

            var poolWhB = new Warehouse(Guid.NewGuid(), company.Id, "Pool WH B") { ParentWarehouseId = groupWh.Id, IsGroup = false };
            await whRepo.InsertAsync(poolWhB, autoSave: true);

            var binA = new Bin(Guid.NewGuid(), sharedItem.Id, poolWhA.Id) { ActualQty = 60m };
            await binRepo.InsertAsync(binA, autoSave: true);

            var binB = new Bin(Guid.NewGuid(), sharedItem.Id, poolWhB.Id) { ActualQty = 90m };
            await binRepo.InsertAsync(binB, autoSave: true);

            var planDto = await planAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = DateTime.UtcNow,
                SubAssemblyWarehouseId = groupWh.Id,
                SkipAvailableSubAssemblyItem = true,
                CombineItems = false,
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = fgBom.Id,
                        PlannedQty = 100m
                    }
                }
            });

            var calculated = await planAppService.CalculateMaterialRequirementsAsync(planDto.Id);

            var sharedRows = calculated.MaterialRequirements.Where(m => m.ItemId == sharedItem.Id).ToList();
            sharedRows.Count.ShouldBe(2);

            // First branch gets fully satisfied by the pool (100 from 150 -> 0 to produce)
            // Second branch gets partially satisfied by the remaining pool (50 from 50 -> 50 to produce)
            var plannedQuantities = sharedRows.Select(r => r.PlannedQty).ToList();
            plannedQuantities.ShouldBe(new[] { 0m, 50m });
        });
    }

    [Fact]
    public async Task SkipAvailableSubAssemblyItemFalse_PreservesFullRequiredQty()
    {
        // When SkipAvailableSubAssemblyItem = false, warehouse stock is not deducted
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP SubAssembly Co 3"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PP-Series-3", "PP", "PP-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-NOSKIP", "FG NoSkip", ItemType.Goods), autoSave: true);
            var sfItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SF-NOSKIP", "SF NoSkip", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-NOSKIP", "RM NoSkip", ItemType.Goods), autoSave: true);

            var sfBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SF-NOSKIP", sfItem.Id) { Quantity = 1m, IsActive = true };
            sfBom.Items.Add(new BomItem(Guid.NewGuid(), sfBom.Id, rmItem.Id, "RM NoSkip", 1m, 10m));
            await bomRepo.InsertAsync(sfBom, autoSave: true);

            var fgBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-NOSKIP", fgItem.Id) { Quantity = 1m, IsActive = true };
            fgBom.Items.Add(new BomItem(Guid.NewGuid(), fgBom.Id, sfItem.Id, "SF NoSkip", 1m, 20m) { SubBomId = sfBom.Id });
            await bomRepo.InsertAsync(fgBom, autoSave: true);

            var wh = new Warehouse(Guid.NewGuid(), company.Id, "Single Warehouse") { IsGroup = false };
            await whRepo.InsertAsync(wh, autoSave: true);

            var bin = new Bin(Guid.NewGuid(), sfItem.Id, wh.Id) { ActualQty = 100m };
            await binRepo.InsertAsync(bin, autoSave: true);

            var planDto = await planAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = DateTime.UtcNow,
                SubAssemblyWarehouseId = wh.Id,
                SkipAvailableSubAssemblyItem = false,
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = fgBom.Id,
                        PlannedQty = 50m
                    }
                }
            });

            var calculated = await planAppService.CalculateMaterialRequirementsAsync(planDto.Id);

            var sfRows = calculated.MaterialRequirements.Where(m => m.ItemId == sfItem.Id).ToList();
            sfRows.Count.ShouldBe(1);
            sfRows[0].RequiredQty.ShouldBe(50m);
            sfRows[0].PlannedQty.ShouldBe(50m); // Not deducted
            sfRows[0].AvailableQty.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task CreateProductionPlanAsync_SubAssemblyWarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Owner Co"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Other Co"), autoSave: true);

            var fg = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "FG-OWNER", "FG Owner", ItemType.Goods), autoSave: true);
            var bom = new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-OWNER", fg.Id) { Quantity = 1m, IsActive = true };
            await bomRepo.InsertAsync(bom, autoSave: true);

            var crossCoWh = new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Co WH") { IsGroup = false };
            await whRepo.InsertAsync(crossCoWh, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                planAppService.CreateAsync(new CreateProductionPlanDto
                {
                    CompanyId = ownerCompany.Id,
                    PostingDate = DateTime.UtcNow,
                    SubAssemblyWarehouseId = crossCoWh.Id,
                    Items = new List<CreateProductionPlanItemDto>
                    {
                        new()
                        {
                            ItemId = fg.Id,
                            ItemName = fg.ItemName,
                            BomId = bom.Id,
                            PlannedQty = 5m
                        }
                    }
                }));
        });
    }
}
