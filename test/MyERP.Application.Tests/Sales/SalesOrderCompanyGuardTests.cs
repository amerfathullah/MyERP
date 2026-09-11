using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
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

public abstract class SalesOrderCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
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
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 1"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-1", "SO Item 1", ItemType.Goods), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross Cost Center 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    CostCenterId = crossCc.Id,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 1", Quantity = 1, UnitPrice = 100 }
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
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 2"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-2", "SO Item 2", ItemType.Goods), autoSave: true);
            var crossProj = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PRJ-CROSS-2", "Cross Project 2"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    ProjectId = crossProj.Id,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 2", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_QuotationFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var quotationRepo = GetRequiredService<IRepository<Quotation, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 3"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 3"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "SO Guard Other Cust 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-3", "SO Item 3", ItemType.Goods), autoSave: true);

            var crossQuotation = new Quotation(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "QTN-OTHER-3", DateTime.UtcNow.Date);
            await quotationRepo.InsertAsync(crossQuotation, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    QuotationId = crossQuotation.Id,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 3", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_LineWarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 4"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-4", "SO Item 4", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 4"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 4", Quantity = 1, UnitPrice = 100, WarehouseId = crossWh.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_QuotationItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var quotationRepo = GetRequiredService<IRepository<Quotation, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 5"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 5"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "SO Guard Other Cust 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-5", "SO Item 5", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "SO-ITEM-5-OTHER", "SO Item 5 Other", ItemType.Goods), autoSave: true);

            var crossQuotation = new Quotation(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "QTN-OTHER-5", DateTime.UtcNow.Date);
            crossQuotation.AddItem(otherItem.Id, "SO Item 5 Other", 1, 100, 0);
            await quotationRepo.InsertAsync(crossQuotation, autoSave: true);

            var crossQuotationItemId = crossQuotation.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 5", Quantity = 1, UnitPrice = 100, QuotationItemId = crossQuotationItemId }
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
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var boRepo = GetRequiredService<IRepository<BlanketOrder, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 6"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 6"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "SO Guard Other Cust 6"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-6", "SO Item 6", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "SO-ITEM-6-OTHER", "SO Item 6 Other", ItemType.Goods), autoSave: true);

            var crossBo = new BlanketOrder(Guid.NewGuid(), otherCompany.Id, "BO-OTHER-6", "Selling", otherCustomer.Id, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
            crossBo.AddItem(otherItem.Id, 10, 100);
            await boRepo.InsertAsync(crossBo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.CreateAsync(new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 6", Quantity = 1, UnitPrice = 100, BlanketOrderId = crossBo.Id }
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
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Owner Co 7"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SO Guard Other Co 7"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "SO Guard Cust 7"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SO-ITEM-7", "SO Item 7", ItemType.Goods), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross Cost Center 7"), autoSave: true);

            var existingSo = new SalesOrder(Guid.NewGuid(), ownerCompany.Id, customer.Id, "SO-OWNER-7", DateTime.UtcNow.Date);
            existingSo.AddItem(item.Id, "SO Item 7", 1, 100, 0);
            await soRepo.InsertAsync(existingSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                soAppService.UpdateAsync(existingSo.Id, new CreateSalesOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    OrderDate = DateTime.UtcNow.Date,
                    CostCenterId = crossCc.Id,
                    Items = new List<CreateSalesOrderItemDto>
                    {
                        new() { ItemId = item.Id, Description = "SO Item 7", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }
}
