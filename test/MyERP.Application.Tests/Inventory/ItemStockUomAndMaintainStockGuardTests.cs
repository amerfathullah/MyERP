using System;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for ERPNext PR #60318 / commit 56d058f26c:
/// Validates that stock UOM and MaintainStock cannot be changed once transactions
/// or active documents (SLE, Bin, SO, PO, MR, WO, BOM) exist.
/// </summary>
public abstract class ItemStockUomAndMaintainStockGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task UpdateAsync_WhenChangingStockUomWithStockLedgerEntry_ThrowsCannotChangeStockUom()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemAppService = GetRequiredService<IItemAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "UOM Test Co 1"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WH-UOM-1"), autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-UOM-001", "Item UOM 001", ItemType.Goods)
                {
                    Uom = "Unit",
                    MaintainStock = true
                }, autoSave: true);

            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(),
                company.Id,
                item.Id,
                warehouse.Id,
                DateTime.UtcNow,
                10,
                100,
                10,
                1000), autoSave: true);

            var updateDto = new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Uom = "Kg", // Changed stock UOM
                MaintainStock = item.MaintainStock,
                IsActive = true
            };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                itemAppService.UpdateAsync(item.Id, updateDto));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotChangeStockUom);
        });
    }

    [Fact]
    public async Task UpdateAsync_WhenChangingStockUomWithBinQty_ThrowsCannotChangeStockUom()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemAppService = GetRequiredService<IItemAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "UOM Test Co 2"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WH-UOM-2"), autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-UOM-002", "Item UOM 002", ItemType.Goods)
                {
                    Uom = "Unit",
                    MaintainStock = true
                }, autoSave: true);

            var bin = new Bin(Guid.NewGuid(), item.Id, warehouse.Id)
            {
                ActualQty = 5,
                StockValue = 50
            };
            await binRepo.InsertAsync(bin, autoSave: true);

            var updateDto = new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Uom = "Box", // Changed stock UOM
                MaintainStock = item.MaintainStock,
                IsActive = true
            };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                itemAppService.UpdateAsync(item.Id, updateDto));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotChangeStockUom);
        });
    }

    [Fact]
    public async Task UpdateAsync_WhenChangingStockUomWithActiveWorkOrder_ThrowsCannotChangeStockUom()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var itemAppService = GetRequiredService<IItemAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "UOM Test Co 3"), autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-UOM-003", "Item UOM 003", ItemType.Goods)
                {
                    Uom = "Unit",
                    MaintainStock = true
                }, autoSave: true);

            var wo = new WorkOrder(
                Guid.NewGuid(),
                company.Id,
                "WO-UOM-001",
                item.Id,
                Guid.NewGuid(),
                10);
            wo.Submit();
            await woRepo.InsertAsync(wo, autoSave: true);

            var updateDto = new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Uom = "Meter", // Changed stock UOM
                MaintainStock = item.MaintainStock,
                IsActive = true
            };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                itemAppService.UpdateAsync(item.Id, updateDto));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotChangeStockUom);
        });
    }

    [Fact]
    public async Task UpdateAsync_WhenChangingMaintainStockWithStockLedgerEntry_ThrowsCannotChangeMaintainStock()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemAppService = GetRequiredService<IItemAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "UOM Test Co 4"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WH-UOM-4"), autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-UOM-004", "Item UOM 004", ItemType.Goods)
                {
                    Uom = "Unit",
                    MaintainStock = true
                }, autoSave: true);

            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(),
                company.Id,
                item.Id,
                warehouse.Id,
                DateTime.UtcNow,
                10,
                100,
                10,
                1000), autoSave: true);

            var updateDto = new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Uom = item.Uom,
                MaintainStock = false, // Changed from true to false
                IsActive = true
            };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                itemAppService.UpdateAsync(item.Id, updateDto));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CannotChangeMaintainStock);
        });
    }

    [Fact]
    public async Task UpdateAsync_WhenNoTransactionsOrOrders_CanChangeStockUomAndMaintainStock_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var itemAppService = GetRequiredService<IItemAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "UOM Test Co 5"), autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-UOM-005", "Item UOM 005", ItemType.Goods)
                {
                    Uom = "Unit",
                    MaintainStock = true
                }, autoSave: true);

            var updateDto = new CreateUpdateItemDto
            {
                CompanyId = company.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemType = item.ItemType,
                Uom = "Box", // New stock UOM
                MaintainStock = false, // New MaintainStock
                IsActive = true
            };

            var result = await itemAppService.UpdateAsync(item.Id, updateDto);

            result.Uom.ShouldBe("Box");
            result.MaintainStock.ShouldBeFalse();
        });
    }
}
