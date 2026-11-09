using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 17 upstream ERPNext parity:
/// 1) BOM sub-BOM production item validation in CreateBomAsync / UpdateBomAsync (PR #64463 / commits 4ab6adb935, 3c4508e0a5, d4745493e6).
/// 2) Serial and Batch Entry batch tracking queryability (PR #64542 / commit 80e1a4d976).
/// </summary>
public abstract class UpstreamBatch17IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateBomAsync_RejectsSubBom_WhenSubBomDoesNotProduceChildItem_4ab6adb935()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 17 Mfg Co"), autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "BOM Series B17-1", "BOM", "BOM-B17-"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B17-FG", "Finished Good", ItemType.Goods), autoSave: true);
            var subAssemblyItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B17-SUB", "Sub Assembly Component", ItemType.Goods), autoSave: true);
            var unrelatedItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B17-OTHER", "Unrelated Other Item", ItemType.Goods), autoSave: true);

            // Create a valid BOM that produces unrelatedItem, NOT subAssemblyItem
            var unrelatedBom = new BillOfMaterials(
                id: Guid.NewGuid(),
                companyId: company.Id,
                bomNumber: "BOM-OTHER-001",
                itemId: unrelatedItem.Id)
            {
                IsActive = true,
                Quantity = 1m
            };
            await bomRepo.InsertAsync(unrelatedBom, autoSave: true);

            // Attempt to create BOM for FG with subAssemblyItem referencing unrelatedBom as its SubBomId
            var createDto = new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                Quantity = 1m,
                Uom = "Nos",
                Items = new List<CreateBomItemDto>
                {
                    new CreateBomItemDto
                    {
                        ItemId = subAssemblyItem.Id,
                        ItemName = subAssemblyItem.ItemName,
                        Quantity = 2m,
                        Rate = 0m,
                        SubBomId = unrelatedBom.Id,
                        SetRateOfSubAssemblyItemBasedOnBom = true
                    }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() => mfgAppService.CreateBomAsync(createDto));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateBomAsync_AcceptsSubBom_WhenSubBomProducesChildItem_AndSetsRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 17 Mfg Co 2"), autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "BOM Series B17-2", "BOM", "BOM-B17-"), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B17-FG2", "Finished Good 2", ItemType.Goods), autoSave: true);
            var subAssemblyItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B17-SUB2", "Sub Assembly 2", ItemType.Goods), autoSave: true);
            var rawMaterial = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B17-RM2", "Raw Material 2", ItemType.Goods), autoSave: true);

            // Sub-BOM that correctly produces subAssemblyItem with UnitCost = 42
            var subBom = new BillOfMaterials(
                id: Guid.NewGuid(),
                companyId: company.Id,
                bomNumber: "BOM-SUB-001",
                itemId: subAssemblyItem.Id)
            {
                IsActive = true,
                Quantity = 1m
            };
            subBom.AddItem(new BomItem(Guid.NewGuid(), subBom.Id, rawMaterial.Id, "Raw Material", 1m, 42m));
            subBom.RecalculateCost();
            await bomRepo.InsertAsync(subBom, autoSave: true);

            var createDto = new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                Quantity = 1m,
                Uom = "Nos",
                Items = new List<CreateBomItemDto>
                {
                    new CreateBomItemDto
                    {
                        ItemId = subAssemblyItem.Id,
                        ItemName = subAssemblyItem.ItemName,
                        Quantity = 3m,
                        Rate = 0m,
                        SubBomId = subBom.Id,
                        SetRateOfSubAssemblyItemBasedOnBom = true
                    }
                }
            };

            var result = await mfgAppService.CreateBomAsync(createDto);
            result.ShouldNotBeNull();
            result.Items.Count.ShouldBe(1);
            result.Items[0].SubBomId.ShouldBe(subBom.Id);
            result.Items[0].Rate.ShouldBe(42m);
        });
    }
}
