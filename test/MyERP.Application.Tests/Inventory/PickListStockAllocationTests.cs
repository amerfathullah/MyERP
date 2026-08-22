using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for PickListAppService.AllocateStockAsync (PickListManager's underlying
/// AllocateStockAsync/GetAvailableQtyForPickAsync), found while auditing the Angular side:
/// pick-list.service.ts's allocateStock had zero callers — the Pick List detail page had no way to
/// preview stock shortages before submitting, even though the backend already computed them
/// (including double-pick prevention against other active Pick Lists, per this domain service's own
/// "Skip double-pick prevention" DO-NOT comment). Added a "Check Availability" button; this test
/// covers the backend it now actually reaches — previously untested end to end.
/// </summary>
public abstract class PickListStockAllocationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task AllocateStockAsync_SufficientStock_NoShortage()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var pickListRepository = GetRequiredService<IRepository<PickList, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Pick Allocation Test Co 1"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PICK-1", "Pick Test Item 1", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Pick WH 1"), autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), item.Id, warehouse.Id) { ActualQty = 100m }, autoSave: true);

            var pickList = new PickList(Guid.NewGuid(), company.Id, "Delivery");
            pickList.AddItem(item.Id, warehouse.Id, qty: 30m);
            await pickListRepository.InsertAsync(pickList, autoSave: true);

            var result = await pickListAppService.AllocateStockAsync(pickList.Id);

            result.HasShortage.ShouldBeFalse();
            var allocation = result.Allocations.Single();
            allocation.AllocatedQty.ShouldBe(30m);
            allocation.ShortageQty.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task AllocateStockAsync_InsufficientStock_ReportsShortage()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var pickListRepository = GetRequiredService<IRepository<PickList, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Pick Allocation Test Co 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PICK-2", "Pick Test Item 2", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Pick WH 2"), autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), item.Id, warehouse.Id) { ActualQty = 10m }, autoSave: true);

            var pickList = new PickList(Guid.NewGuid(), company.Id, "Delivery");
            pickList.AddItem(item.Id, warehouse.Id, qty: 30m);
            await pickListRepository.InsertAsync(pickList, autoSave: true);

            var result = await pickListAppService.AllocateStockAsync(pickList.Id);

            result.HasShortage.ShouldBeTrue();
            var allocation = result.Allocations.Single();
            allocation.AllocatedQty.ShouldBe(10m);
            allocation.ShortageQty.ShouldBe(20m);
        });
    }

    [Fact]
    public async Task AllocateStockAsync_DeductsQtyAlreadyPickedByOtherSubmittedPickList()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();
            var pickListRepository = GetRequiredService<IRepository<PickList, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Pick Allocation Test Co 3"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PICK-3", "Pick Test Item 3", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Pick WH 3"), autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), item.Id, warehouse.Id) { ActualQty = 50m }, autoSave: true);

            // Another Pick List already Submitted (active) has claimed 40 of the 50 on hand.
            var otherPickList = new PickList(Guid.NewGuid(), company.Id, "Delivery");
            otherPickList.AddItem(item.Id, warehouse.Id, qty: 40m);
            otherPickList.Submit();
            await pickListRepository.InsertAsync(otherPickList, autoSave: true);

            var pickList = new PickList(Guid.NewGuid(), company.Id, "Delivery");
            pickList.AddItem(item.Id, warehouse.Id, qty: 30m);
            await pickListRepository.InsertAsync(pickList, autoSave: true);

            // Only 50 - 40 = 10 actually available for this second Pick List.
            var result = await pickListAppService.AllocateStockAsync(pickList.Id);

            result.HasShortage.ShouldBeTrue();
            var allocation = result.Allocations.Single();
            allocation.AllocatedQty.ShouldBe(10m);
            allocation.ShortageQty.ShouldBe(20m);
        });
    }
}
