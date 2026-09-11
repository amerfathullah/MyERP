using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class SalesInvoiceCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CostCenterFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 1"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-1", "SI Item 1", ItemType.Goods), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "SI Cross CC 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    CostCenterId = crossCc.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 1", Quantity = 1, UnitPrice = 100 }
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
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var projRepo = GetRequiredService<IRepository<Project, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 2"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-2", "SI Item 2", ItemType.Goods), autoSave: true);
            var crossProj = await projRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-SI-2", "Cross Project 2"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    ProjectId = crossProj.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 2", Quantity = 1, UnitPrice = 100 }
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
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 3"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-3", "SI Item 3", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 3"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = crossWh.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Item 3", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_DeferredRevenueAccountFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var acctRepo = GetRequiredService<IRepository<Account, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 4"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-4", "SI Item 4", ItemType.Goods), autoSave: true);
            var crossAcct = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), otherCompany.Id, "4999", "Cross Deferred Acct", AccountType.Revenue), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = item.Id,
                            Description = "Item 4",
                            Quantity = 1,
                            UnitPrice = 100,
                            EnableDeferredRevenue = true,
                            DeferredRevenueAccountId = crossAcct.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_AssetFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var assetRepo = GetRequiredService<IRepository<Asset, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 5"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-5", "SI Item 5", ItemType.Goods), autoSave: true);
            var crossAsset = await assetRepo.InsertAsync(new Asset(Guid.NewGuid(), otherCompany.Id, "AST-SI-5", "Cross Asset 5", DateTime.UtcNow.Date, 500m), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = item.Id,
                            Description = "Item 5",
                            Quantity = 1,
                            UnitPrice = 100,
                            IsFixedAsset = true,
                            AssetId = crossAsset.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 6"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 6"), autoSave: true);
            var otherCust = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "SI Guard Cust Other 6"), autoSave: true);
            var ownerItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-6", "SI Item 6", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "SI-ITEM-OTHER-6", "SI Item Other 6", ItemType.Goods), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCust.Id, "SO-SI-6", DateTime.UtcNow.Date);
            crossSo.AddItem(otherItem.Id, "Item SO", 1, 100, 0);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            var soItem = crossSo.Items[0];

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = ownerItem.Id,
                            Description = "Item 6",
                            Quantity = 1,
                            UnitPrice = 100,
                            SalesOrderItemId = soItem.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_DeliveryNoteFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Owner Co 7"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SI Guard Other Co 7"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SI Guard Cust 7"), autoSave: true);
            var otherCust = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "SI Guard Cust Other 7"), autoSave: true);
            var ownerItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SI-ITEM-7", "SI Item 7", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "SI-ITEM-OTHER-7", "SI Item Other 7", ItemType.Goods), autoSave: true);
            var otherWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Wh 7"), autoSave: true);

            var crossDn = new DeliveryNote(Guid.NewGuid(), otherCompany.Id, otherCust.Id, otherWh.Id, "DN-SI-7", DateTime.UtcNow.Date);
            crossDn.AddItem(otherItem.Id, "Item DN", 1, 100, 0);
            await dnRepo.InsertAsync(crossDn, autoSave: true);

            var dnItem = crossDn.Items[0];

            await Should.ThrowAsync<BusinessException>(() =>
                siAppService.CreateAsync(new CreateSalesInvoiceDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    Items = new List<CreateSalesInvoiceItemDto>
                    {
                        new()
                        {
                            ItemId = ownerItem.Id,
                            Description = "Item 7",
                            Quantity = 1,
                            UnitPrice = 100,
                            DeliveryNoteItemId = dnItem.Id
                        }
                    }
                }));
        });
    }
}
