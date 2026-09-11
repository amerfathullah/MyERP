using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class DeliveryNoteCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 1"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-1", "DN Item 1", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = crossWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 1", Quantity = 1, UnitPrice = 100 }
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
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 2"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 2"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-2", "DN Item 2", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 2"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-2", DateTime.UtcNow.Date);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    SalesOrderId = crossSo.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 2", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 3"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 3"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 3"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-3", "DN Item 3", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "DN-ITEM-3-OTHER", "DN Item 3 Other", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 3"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-3", DateTime.UtcNow.Date);
            crossSo.AddItem(otherItem.Id, "Other Item", 1, 100, 0);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            var crossSoItemId = crossSo.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 3", Quantity = 1, UnitPrice = 100, SalesOrderItemId = crossSoItemId }
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
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 4"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 4"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 4"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 4"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-4", "DN Item 4", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 4"), autoSave: true);
            var otherWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Warehouse 4"), autoSave: true);

            var crossDn = new DeliveryNote(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, otherWh.Id, "DN-OTHER-4", DateTime.UtcNow.Date);
            await dnRepo.InsertAsync(crossDn, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.CreateAsync(new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    IsReturn = true,
                    ReturnAgainstId = crossDn.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "DN Item 4", Quantity = 1, UnitPrice = 100 }
                    }
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_SalesOrderItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Owner Co 5"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "DN Guard Other Co 5"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), ownerCompany.Id, "DN Guard Cust 5"), autoSave: true);
            var otherCustomer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "DN Guard Other Cust 5"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "DN-ITEM-5", "DN Item 5", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "DN-ITEM-5-OTHER", "DN Item 5 Other", ItemType.Goods), autoSave: true);
            var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), ownerCompany.Id, "Owner Warehouse 5"), autoSave: true);

            var crossSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, otherCustomer.Id, "SO-OTHER-5", DateTime.UtcNow.Date);
            crossSo.AddItem(otherItem.Id, "Other Item", 1, 100, 0);
            await soRepo.InsertAsync(crossSo, autoSave: true);

            var existingDn = new DeliveryNote(Guid.NewGuid(), ownerCompany.Id, customer.Id, ownerWh.Id, "DN-OWNER-5", DateTime.UtcNow.Date);
            existingDn.AddItem(item.Id, "Item 5", 1, 100, 0);
            await dnRepo.InsertAsync(existingDn, autoSave: true);

            var crossSoItemId = crossSo.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                dnAppService.UpdateAsync(existingDn.Id, new CreateDeliveryNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    WarehouseId = ownerWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<CreateDeliveryNoteItemDto>
                    {
                        new() { ItemId = item.Id, Description = "Updated Item 5", Quantity = 1, UnitPrice = 100, SalesOrderItemId = crossSoItemId }
                    }
                }));
        });
    }
}
