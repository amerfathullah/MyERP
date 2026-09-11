using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class PurchaseReceiptCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_HeaderWarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Other Co 1"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PR Guard Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PR-ITEM-1", "PR Item 1", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = crossWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PR Item 1", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ItemWarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Other Co 2"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PR Guard Supp 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PR-ITEM-2", "PR Item 2", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 2"), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 2"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PR Item 2", Quantity = 1, UnitPrice = 100, WarehouseId = crossWh.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_PurchaseOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Other Co 3"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PR Guard Supp 3"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PR Guard Other Supp 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PR-ITEM-3", "PR Item 3", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 3"), autoSave: true);

            var crossPo = new PurchaseOrder(Guid.NewGuid(), otherCompany.Id, otherSupplier.Id, "PO-OTHER-3", DateTime.UtcNow.Date);
            await poRepo.InsertAsync(crossPo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = ownerWh.Id,
                    PurchaseOrderId = crossPo.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PR Item 3", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_PurchaseOrderItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Other Co 4"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PR Guard Supp 4"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PR Guard Other Supp 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PR-ITEM-4", "PR Item 4", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "PR-ITEM-4-OTHER", "PR Item 4 Other", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 4"), autoSave: true);

            var crossPo = new PurchaseOrder(Guid.NewGuid(), otherCompany.Id, otherSupplier.Id, "PO-OTHER-4", DateTime.UtcNow.Date);
            crossPo.AddItem(otherItem.Id, "Other Item", 1, 50, 0);
            await poRepo.InsertAsync(crossPo, autoSave: true);

            var crossPoItemId = crossPo.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PR Item 4", Quantity = 1, UnitPrice = 100, PurchaseOrderItemId = crossPoItemId }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ReturnAgainstFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Other Co 5"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PR Guard Supp 5"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PR Guard Other Supp 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PR-ITEM-5", "PR Item 5", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 5"), autoSave: true);
            var otherWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Warehouse 5"), autoSave: true);

            var crossPr = new PurchaseReceipt(Guid.NewGuid(), otherCompany.Id, otherSupplier.Id, otherWh.Id, "PR-OTHER-5", DateTime.UtcNow.Date);
            await prRepo.InsertAsync(crossPr, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = ownerWh.Id,
                    IsReturn = true,
                    ReturnAgainstId = crossPr.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new() { ItemId = item.Id, Description = "PR Item 5", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_PurchaseOrderItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Guard Other Co 6"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PR Guard Supp 6"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PR Guard Other Supp 6"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PR-ITEM-6", "PR Item 6", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "PR-ITEM-6-OTHER", "PR Item 6 Other", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 6"), autoSave: true);

            var crossPo = new PurchaseOrder(Guid.NewGuid(), otherCompany.Id, otherSupplier.Id, "PO-OTHER-6", DateTime.UtcNow.Date);
            crossPo.AddItem(otherItem.Id, "Other Item", 1, 50, 0);
            await poRepo.InsertAsync(crossPo, autoSave: true);

            var existingPr = new PurchaseReceipt(Guid.NewGuid(), ownerCompany.Id, supplier.Id, ownerWh.Id, "PR-OWNER-6", DateTime.UtcNow.Date);
            existingPr.AddItem(item.Id, "Item 6", 1, 100, 0);
            await prRepo.InsertAsync(existingPr, autoSave: true);

            var crossPoItemId = crossPo.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                prAppService.UpdateAsync(existingPr.Id, new CreatePurchaseReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Updated Item 6", Quantity = 1, UnitPrice = 100, PurchaseOrderItemId = crossPoItemId }
                    }
                }));
        });
    }
}
