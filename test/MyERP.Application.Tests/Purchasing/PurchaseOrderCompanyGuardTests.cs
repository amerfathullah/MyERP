using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class PurchaseOrderCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CostCenterFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 1"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Guard Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-1", "PO Item 1", ItemType.Goods), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.CreateAsync(new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    CostCenterId = crossCc.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 1", Quantity = 1, UnitPrice = 50 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ProjectFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var projRepo = GetRequiredService<IRepository<Project, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 2"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Guard Supp 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-2", "PO Item 2", ItemType.Goods), autoSave: true);
            var crossProj = await projRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-PO-2", "Cross Project 2"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.CreateAsync(new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    ProjectId = crossProj.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 2", Quantity = 1, UnitPrice = 50 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 3"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Guard Supp 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-3", "PO Item 3", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 3"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.CreateAsync(new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 3", Quantity = 1, UnitPrice = 50, WarehouseId = crossWh.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ExpenseAccountFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var acctRepo = GetRequiredService<IRepository<Account, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 4"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Guard Supp 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-4", "PO Item 4", ItemType.Goods), autoSave: true);
            var crossAcct = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), otherCompany.Id, "5888", "Cross Expense", AccountType.Expense), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.CreateAsync(new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 4", Quantity = 1, UnitPrice = 50, ExpenseAccountId = crossAcct.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_BlanketOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var boRepo = GetRequiredService<IRepository<BlanketOrder, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 5"), autoSave: true);

            var ownerSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Supp Owner 5"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PO Supp Other 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-5", "PO Item 5", ItemType.Goods), autoSave: true);

            var crossBo = new BlanketOrder(Guid.NewGuid(), otherCompany.Id, "BO-PO-5", "Purchasing", otherSupplier.Id, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddMonths(1));
            await boRepo.InsertAsync(crossBo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.CreateAsync(new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = ownerSupplier.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 5", Quantity = 1, UnitPrice = 50, BlanketOrderId = crossBo.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_CostCenterFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 6"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Supp Owner 6"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-6", "PO Item 6", ItemType.Goods), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC 6"), autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), ownerCompany.Id, supplier.Id, "PO-UPD-6", DateTime.UtcNow.Date);
            po.AddItem(item.Id, "Item 6", 1, 50, 0);
            await poRepo.InsertAsync(po, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.UpdateAsync(po.Id, new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    CostCenterId = crossCc.Id,
                    OrderDate = po.OrderDate,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 6", Quantity = 1, UnitPrice = 50 }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Owner Co 7"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Guard Other Co 7"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PO Supp Owner 7"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PO-ITEM-7", "PO Item 7", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 7"), autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), ownerCompany.Id, supplier.Id, "PO-UPD-7", DateTime.UtcNow.Date);
            po.AddItem(item.Id, "Item 7", 1, 50, 0);
            await poRepo.InsertAsync(po, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                poAppService.UpdateAsync(po.Id, new CreatePurchaseOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = po.OrderDate,
                    Items = new List<CreatePurchaseOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 7", Quantity = 1, UnitPrice = 50, WarehouseId = crossWh.Id }
                    }
                }));
        });
    }
}
