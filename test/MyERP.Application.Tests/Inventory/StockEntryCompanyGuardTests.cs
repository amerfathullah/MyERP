using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

public abstract class StockEntryCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CostCenterFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 1"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-1", "SE Item 1", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "SE Wh 1"), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.CreateAsync(new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    CostCenterId = crossCc.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = wh.Id, ValuationRate = 10 }
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
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var projRepo = GetRequiredService<IRepository<Project, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 2"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-2", "SE Item 2", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "SE Wh 2"), autoSave: true);
            var crossProj = await projRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PROJ-SE-2", "Cross Proj 2"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.CreateAsync(new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    ProjectId = crossProj.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = wh.Id, ValuationRate = 10 }
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
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var acctRepo = GetRequiredService<IRepository<Account, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 3"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-3", "SE Item 3", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "SE Wh 3"), autoSave: true);
            var crossAcct = await acctRepo.InsertAsync(new Account(Guid.NewGuid(), otherCompany.Id, "5999", "Cross Expense", AccountType.Expense), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.CreateAsync(new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = wh.Id, ValuationRate = 10, ExpenseAccountId = crossAcct.Id }
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
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 4"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-4", "SE Item 4", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Wh 4"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.CreateAsync(new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = crossWh.Id, ValuationRate = 10 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_WorkOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 5"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-5", "SE Item 5", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "SE-ITEM-5-OTHER", "SE Item 5 Other", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "SE Wh 5"), autoSave: true);

            var crossWo = new WorkOrder(Guid.NewGuid(), otherCompany.Id, "WO-OTHER-5", otherItem.Id, Guid.NewGuid(), 10);
            await woRepo.InsertAsync(crossWo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.CreateAsync(new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    WorkOrderId = crossWo.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = wh.Id, ValuationRate = 10 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderReferenceFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 6"), autoSave: true);

            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "Other Cust 6"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-6", "SE Item 6", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "SE Wh 6"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-6", DateTime.UtcNow.Date);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.CreateAsync(new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    ReferenceType = "SalesOrder",
                    ReferenceId = crossSo.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = wh.Id, ValuationRate = 10 }
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
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seAppService = GetRequiredService<IStockEntryAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Owner Co 7"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SE Guard Other Co 7"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SE-ITEM-7", "SE Item 7", ItemType.Goods), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "SE Wh 7"), autoSave: true);
            var crossCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Cross CC 7"), autoSave: true);

            var entry = new StockEntry(Guid.NewGuid(), ownerCompany.Id, StockEntryType.MaterialReceipt, DateTime.UtcNow.Date);
            entry.AddItem(item.Id, 1, null, wh.Id, 10);
            await seRepo.InsertAsync(entry, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                seAppService.UpdateAsync(entry.Id, new CreateStockEntryDto
                {
                    CompanyId = ownerCompany.Id,
                    EntryType = StockEntryType.MaterialReceipt,
                    CostCenterId = crossCc.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateStockEntryItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1, TargetWarehouseId = wh.Id, ValuationRate = 10 }
                    }
                }));
        });
    }
}
