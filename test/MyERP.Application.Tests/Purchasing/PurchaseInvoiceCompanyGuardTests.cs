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
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class PurchaseInvoiceCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
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
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Other Co 1"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PI Guard Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PI-ITEM-1", "PI Item 1", ItemType.Goods), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    CostCenterId = crossCc.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
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
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Other Co 2"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PI Guard Supp 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PI-ITEM-2", "PI Item 2", ItemType.Goods), autoSave: true);
            var crossProj = await projRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-PI-2", "Cross Project 2"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    ProjectId = crossProj.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
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
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Other Co 3"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PI Guard Supp 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PI-ITEM-3", "PI Item 3", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 3"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = crossWh.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 3", Quantity = 1, UnitPrice = 50 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_DeferredExpenseAccountFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var acctRepo = GetRequiredService<IRepository<Account, Guid>>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Other Co 4"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PI Guard Supp 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PI-ITEM-4", "PI Item 4", ItemType.Goods), autoSave: true);
            var crossAcct = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), otherCompany.Id, "5999", "Cross Deferred Expense", AccountType.Expense), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = item.Id,
                            Description = "Item 4",
                            Quantity = 1,
                            UnitPrice = 50,
                            EnableDeferredExpense = true,
                            DeferredExpenseAccountId = crossAcct.Id
                        }
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
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Other Co 5"), autoSave: true);

            var ownerSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PI Supp Owner 5"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PI Supp Other 5"), autoSave: true);
            var ownerItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PI-ITEM-OWN-5", "PI Item Own 5", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "PI-ITEM-OTH-5", "PI Item Oth 5", ItemType.Goods), autoSave: true);

            var crossPo = new PurchaseOrder(Guid.NewGuid(), otherCompany.Id, otherSupplier.Id, "PO-PI-5", DateTime.UtcNow.Date);
            crossPo.AddItem(otherItem.Id, "Other PO Item", 1, 50, 0);
            await poRepo.InsertAsync(crossPo, autoSave: true);

            var poItem = crossPo.Items[0];

            await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = ownerSupplier.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = ownerItem.Id,
                            Description = "Item 5",
                            Quantity = 1,
                            UnitPrice = 50,
                            PurchaseOrderItemId = poItem.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_PurchaseReceiptFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PI Guard Other Co 6"), autoSave: true);

            var ownerSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "PI Supp Owner 6"), autoSave: true);
            var otherSupplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "PI Supp Other 6"), autoSave: true);
            var ownerItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "PI-ITEM-OWN-6", "PI Item Own 6", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "PI-ITEM-OTH-6", "PI Item Oth 6", ItemType.Goods), autoSave: true);
            var otherWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Wh 6"), autoSave: true);

            var crossPr = new PurchaseReceipt(Guid.NewGuid(), otherCompany.Id, otherSupplier.Id, otherWh.Id, "PR-PI-6", DateTime.UtcNow.Date);
            crossPr.AddItem(otherItem.Id, "Other PR Item", 1, 50, 0);
            await prRepo.InsertAsync(crossPr, autoSave: true);

            var prItem = crossPr.Items[0];

            await Should.ThrowAsync<BusinessException>(() =>
                piAppService.CreateAsync(new CreatePurchaseInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = ownerSupplier.Id,
                    Items = new List<CreatePurchaseInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = ownerItem.Id,
                            Description = "Item 6",
                            Quantity = 1,
                            UnitPrice = 50,
                            PurchaseReceiptItemId = prItem.Id
                        }
                    }
                }));
        });
    }
}
