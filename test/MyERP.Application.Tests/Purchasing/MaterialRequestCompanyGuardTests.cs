using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class MaterialRequestCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ProjectFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 1"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-1", "MR Item 1", ItemType.Goods), autoSave: true);
            var crossProj = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PRJ-CROSS-1", "Cross Project 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.Purchase,
                    RequestDate = DateTime.UtcNow.Date,
                    ProjectId = crossProj.Id,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 1", Quantity = 5, Uom = "Unit" }
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
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 2"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-2", "MR Item 2", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "MR-ITEM-2-OTHER", "MR Item 2 Other", ItemType.Goods), autoSave: true);
            var crossWo = new WorkOrder(Guid.NewGuid(), otherCompany.Id, "WO-CROSS-2", otherItem.Id, Guid.NewGuid(), 10);
            await woRepo.InsertAsync(crossWo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.Manufacture,
                    RequestDate = DateTime.UtcNow.Date,
                    WorkOrderId = crossWo.Id,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 2", Quantity = 5, Uom = "Unit" }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SourceWarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 3"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-3", "MR Item 3", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 3"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.MaterialTransfer,
                    RequestDate = DateTime.UtcNow.Date,
                    SourceWarehouseId = crossWh.Id,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 3", Quantity = 5, Uom = "Unit" }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_TargetWarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 4"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-4", "MR Item 4", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 4"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.MaterialTransfer,
                    RequestDate = DateTime.UtcNow.Date,
                    TargetWarehouseId = crossWh.Id,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 4", Quantity = 5, Uom = "Unit" }
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
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 5"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-5", "MR Item 5", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 5"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.Purchase,
                    RequestDate = DateTime.UtcNow.Date,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 5", Quantity = 5, Uom = "Unit", WarehouseId = crossWh.Id }
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
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 6"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 6"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "Other Cust 6"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-6", "MR Item 6", ItemType.Goods), autoSave: true);
            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, customer.Id, "SO-CROSS-6", DateTime.UtcNow.Date);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.Purchase,
                    RequestDate = DateTime.UtcNow.Date,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 6", Quantity = 5, Uom = "Unit", SalesOrderId = crossSo.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_ProjectFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var projectRepo = GetRequiredService<IRepository<Project, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Owner Co 7"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MR Guard Other Co 7"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "MR-ITEM-7", "MR Item 7", ItemType.Goods), autoSave: true);
            var crossProj = await projectRepo.InsertAsync(new Project(Guid.NewGuid(), otherCompany.Id, "PRJ-CROSS-7", "Cross Project 7"), autoSave: true);
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();

            var existingMr = new MaterialRequest(Guid.NewGuid(), ownerCompany.Id, "MR-OWNER-7", MaterialRequestType.Purchase, DateTime.UtcNow.Date);
            existingMr.AddItem(item.Id, "MR Item 7", 5, "Unit");
            await mrRepo.InsertAsync(existingMr, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                mrAppService.UpdateAsync(existingMr.Id, new CreateMaterialRequestDto
                {
                    CompanyId = ownerCompany.Id,
                    RequestType = MaterialRequestType.Purchase,
                    RequestDate = DateTime.UtcNow.Date,
                    ProjectId = crossProj.Id,
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Item 7", Quantity = 5, Uom = "Unit" }
                    }
                }));
        });
    }
}
