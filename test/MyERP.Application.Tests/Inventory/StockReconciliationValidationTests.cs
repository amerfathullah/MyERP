using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Dtos;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for real gaps found via ERPNext validate() parity: stock_reconciliation.py
/// validate_data() rejects negative qty/valuation rate and duplicate item+warehouse rows (the
/// second row would silently overwrite the SLE the first row just created for that combination).
/// StockReconciliationAppService.CreateAsync had none of these checks, nor the company-restriction
/// check every other transaction AppService wires in.
/// </summary>
public abstract class StockReconciliationValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_NegativeQuantity_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SR Guard Neg Qty Co"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Guard WH 1"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "SR-ITEM-1", "SR Guard Item 1", MyERP.Inventory.ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                srAppService.CreateAsync(new CreateStockReconciliationDto
                {
                    CompanyId = company.Id,
                    PostingDate = DateTime.Today,
                    Purpose = "Stock Reconciliation",
                    Items = new List<CreateStockReconciliationItemDto>
                    {
                        new() { ItemId = item.Id, WarehouseId = warehouse.Id, NewQuantity = -5m, NewValuationRate = 10m }
                    }.ToArray()
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_DuplicateItemWarehouseRow_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SR Guard Dup Co"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Guard WH 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "SR-ITEM-2", "SR Guard Item 2", MyERP.Inventory.ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                srAppService.CreateAsync(new CreateStockReconciliationDto
                {
                    CompanyId = company.Id,
                    PostingDate = DateTime.Today,
                    Purpose = "Stock Reconciliation",
                    Items = new List<CreateStockReconciliationItemDto>
                    {
                        new() { ItemId = item.Id, WarehouseId = warehouse.Id, NewQuantity = 10m, NewValuationRate = 10m },
                        new() { ItemId = item.Id, WarehouseId = warehouse.Id, NewQuantity = 20m, NewValuationRate = 12m }
                    }.ToArray()
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SR Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SR Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), ownerCompany.Id, "SR-ITEM-3", "SR Guard Item 3", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), otherCompany.Id, "SR Guard Cross-Co WH"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                srAppService.CreateAsync(new CreateStockReconciliationDto
                {
                    CompanyId = ownerCompany.Id,
                    PostingDate = DateTime.Today,
                    Purpose = "Stock Reconciliation",
                    Items = new List<CreateStockReconciliationItemDto>
                    {
                        new() { ItemId = item.Id, WarehouseId = warehouse.Id, NewQuantity = 10m, NewValuationRate = 10m }
                    }.ToArray()
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ValidRows_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SR Guard Happy Co"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Guard Happy WH"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "SR-ITEM-4", "SR Guard Item 4", MyERP.Inventory.ItemType.Goods), autoSave: true);

            var dto = await srAppService.CreateAsync(new CreateStockReconciliationDto
            {
                CompanyId = company.Id,
                PostingDate = DateTime.Today,
                Purpose = "Stock Reconciliation",
                Items = new List<CreateStockReconciliationItemDto>
                {
                    new() { ItemId = item.Id, WarehouseId = warehouse.Id, NewQuantity = 10m, NewValuationRate = 10m }
                }.ToArray()
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
