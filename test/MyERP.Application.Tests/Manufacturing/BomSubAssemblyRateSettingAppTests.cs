using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class BomSubAssemblyRateSettingAppTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateBomAsync_WithSubBom_WhenSettingIsTrue_AndRateZero_ResolvesSubBomRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "BOM Test Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "BOM Series", "BOM", "BOM-.####"), autoSave: true);

            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-001", "Raw Material 1", ItemType.Goods), autoSave: true);
            var subAssemblyItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SA-001", "Sub Assembly 1", ItemType.Goods), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-001", "Finished Good 1", ItemType.Goods), autoSave: true);

            // Create child BOM for sub-assembly
            var childBom = await manufacturingAppService.CreateBomAsync(new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = subAssemblyItem.Id,
                Quantity = 1,
                Items = new List<CreateBomItemDto>
                {
                    new()
                    {
                        ItemId = rmItem.Id,
                        ItemName = rmItem.ItemName,
                        Quantity = 2,
                        Rate = 125, // Total material cost = 250
                    }
                }
            });

            childBom.TotalCost.ShouldBe(250);

            // Create parent BOM referencing child BOM with SetRateOfSubAssemblyItemBasedOnBom = true and Rate = 0
            var parentBom = await manufacturingAppService.CreateBomAsync(new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                Quantity = 1,
                Items = new List<CreateBomItemDto>
                {
                    new()
                    {
                        ItemId = subAssemblyItem.Id,
                        ItemName = subAssemblyItem.ItemName,
                        Quantity = 3,
                        Rate = 0, // Auto-resolved
                        SubBomId = childBom.Id,
                        SetRateOfSubAssemblyItemBasedOnBom = true
                    }
                }
            });

            var parentRow = parentBom.Items.ShouldHaveSingleItem();
            parentRow.Rate.ShouldBe(250);
            parentRow.Amount.ShouldBe(750); // 3 * 250
            parentRow.SetRateOfSubAssemblyItemBasedOnBom.ShouldBeTrue();
            parentRow.SubBomId.ShouldBe(childBom.Id);
            parentBom.TotalCost.ShouldBe(750);
        });
    }

    [Fact]
    public async Task CreateBomAsync_WithSubBom_WhenSettingIsFalse_PreservesSpecifiedRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "BOM Test Co 2"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "BOM Series", "BOM", "BOM-.####"), autoSave: true);

            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-002", "Raw Material 2", ItemType.Goods), autoSave: true);
            var subAssemblyItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SA-002", "Sub Assembly 2", ItemType.Goods), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-002", "Finished Good 2", ItemType.Goods), autoSave: true);

            // Create child BOM (cost = 500)
            var childBom = await manufacturingAppService.CreateBomAsync(new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = subAssemblyItem.Id,
                Quantity = 1,
                Items = new List<CreateBomItemDto>
                {
                    new()
                    {
                        ItemId = rmItem.Id,
                        ItemName = rmItem.ItemName,
                        Quantity = 1,
                        Rate = 500,
                    }
                }
            });

            // Create parent BOM with SetRateOfSubAssemblyItemBasedOnBom = false and custom rate 80
            var parentBom = await manufacturingAppService.CreateBomAsync(new CreateBomDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                Quantity = 1,
                Items = new List<CreateBomItemDto>
                {
                    new()
                    {
                        ItemId = subAssemblyItem.Id,
                        ItemName = subAssemblyItem.ItemName,
                        Quantity = 2,
                        Rate = 80, // Custom rate preserved
                        SubBomId = childBom.Id,
                        SetRateOfSubAssemblyItemBasedOnBom = false
                    }
                }
            });

            var parentRow = parentBom.Items.ShouldHaveSingleItem();
            parentRow.Rate.ShouldBe(80); // Preserved custom rate
            parentRow.Amount.ShouldBe(160); // 2 * 80
            parentRow.SetRateOfSubAssemblyItemBasedOnBom.ShouldBeFalse();
            parentRow.SubBomId.ShouldBe(childBom.Id);
            parentBom.TotalCost.ShouldBe(160);
        });
    }
}
