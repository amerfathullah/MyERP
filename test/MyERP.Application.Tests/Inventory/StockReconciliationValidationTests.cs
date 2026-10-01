using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public async Task CreateAsync_NonStockItem_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "SR Guard Non Stock Co"), autoSave: true);
            var warehouse = await GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>().InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Guard WH NS"), autoSave: true);
            var service = await GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>().InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "SR-SVC-1", "SR Guard Service", MyERP.Inventory.ItemType.Service), autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                GetRequiredService<IStockReconciliationAppService>().CreateAsync(new CreateStockReconciliationDto
                {
                    CompanyId = company.Id,
                    PostingDate = DateTime.Today,
                    Purpose = "Stock Reconciliation",
                    Items = new List<CreateStockReconciliationItemDto>
                    {
                        new() { ItemId = service.Id, WarehouseId = warehouse.Id, NewQuantity = 5m, NewValuationRate = 10m }
                    }.ToArray()
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task GetItemsForReconciliationAsync_BatchedItem_FetchesBatchWiseValuationRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var batchRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Batch, Guid>>();
            var sleRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SR Preview Co"), autoSave: true);
            var wh = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Preview WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "BATCH-ITEM-1", "Batch Item 1", MyERP.Inventory.ItemType.Goods)
            {
                HasBatchNo = true,
                StandardBuyingPrice = 10m
            }, autoSave: true);

            var batch1 = await batchRepo.InsertAsync(new MyERP.Inventory.Entities.Batch(Guid.NewGuid(), item.Id, "BATCH-001"), autoSave: true);
            var batch2 = await batchRepo.InsertAsync(new MyERP.Inventory.Entities.Batch(Guid.NewGuid(), item.Id, "BATCH-002"), autoSave: true);

            var today = DateTime.Today;
            // Batch 1 has 10 units with valuation rate 15 (StockValueDifference = 150)
            await sleRepo.InsertAsync(new MyERP.Inventory.Entities.StockLedgerEntry(
                Guid.NewGuid(), company.Id, item.Id, wh.Id, today.AddDays(-2),
                quantityChange: 10m, valuationRate: 15m, balanceQuantity: 10m, balanceValue: 150m)
            {
                BatchId = batch1.Id,
                StockValueDifference = 150m
            }, autoSave: true);

            // Batch 2 has 20 units with valuation rate 25 (StockValueDifference = 500)
            await sleRepo.InsertAsync(new MyERP.Inventory.Entities.StockLedgerEntry(
                Guid.NewGuid(), company.Id, item.Id, wh.Id, today.AddDays(-1),
                quantityChange: 20m, valuationRate: 25m, balanceQuantity: 30m, balanceValue: 650m)
            {
                BatchId = batch2.Id,
                StockValueDifference = 500m
            }, autoSave: true);

            var preview = await srAppService.GetItemsForReconciliationAsync(new GetStockReconciliationItemsInputDto
            {
                CompanyId = company.Id,
                WarehouseId = wh.Id,
                PostingDate = today,
                ItemId = item.Id
            });

            // Per ERPNext commit b132e3f22a: each batch returns its individual batch valuation rate
            preview.Count.ShouldBe(2);

            var b1Row = preview.First(r => r.BatchId == batch1.Id);
            b1Row.BatchNo.ShouldBe("BATCH-001");
            b1Row.CurrentQuantity.ShouldBe(10m);
            b1Row.CurrentValuationRate.ShouldBe(15m);

            var b2Row = preview.First(r => r.BatchId == batch2.Id);
            b2Row.BatchNo.ShouldBe("BATCH-002");
            b2Row.CurrentQuantity.ShouldBe(20m);
            b2Row.CurrentValuationRate.ShouldBe(25m);
        });
    }

    [Fact]
    public async Task GetItemsForReconciliationAsync_NonBatchedItem_FetchesPreviousSleBalanceAndRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var sleRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SR Preview Co 2"), autoSave: true);
            var wh = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Preview WH 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "REG-ITEM-1", "Regular Item 1", MyERP.Inventory.ItemType.Goods)
            {
                HasBatchNo = false,
                StandardBuyingPrice = 12m
            }, autoSave: true);

            var today = DateTime.Today;
            await sleRepo.InsertAsync(new MyERP.Inventory.Entities.StockLedgerEntry(
                Guid.NewGuid(), company.Id, item.Id, wh.Id, today.AddDays(-1),
                quantityChange: 7m, valuationRate: 35m, balanceQuantity: 7m, balanceValue: 245m), autoSave: true);

            var preview = await srAppService.GetItemsForReconciliationAsync(new GetStockReconciliationItemsInputDto
            {
                CompanyId = company.Id,
                WarehouseId = wh.Id,
                PostingDate = today,
                ItemId = item.Id
            });

            preview.Count.ShouldBe(1);
            preview[0].ItemId.ShouldBe(item.Id);
            preview[0].CurrentQuantity.ShouldBe(7m);
            preview[0].CurrentValuationRate.ShouldBe(35m);
            preview[0].HasBatchNo.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task GetItemsForReconciliationAsync_IgnoreEmptyStock_FiltersOutZeroBalance()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SR Preview Co 3"), autoSave: true);
            var wh = await whRepo.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "SR Preview WH 3"), autoSave: true);
            var item = await itemRepo.InsertAsync(new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "EMPTY-ITEM-1", "Empty Item 1", MyERP.Inventory.ItemType.Goods)
            {
                HasBatchNo = false,
                StandardBuyingPrice = 10m
            }, autoSave: true);

            var today = DateTime.Today;

            // With IgnoreEmptyStock = true: should be empty
            var previewEmpty = await srAppService.GetItemsForReconciliationAsync(new GetStockReconciliationItemsInputDto
            {
                CompanyId = company.Id,
                WarehouseId = wh.Id,
                PostingDate = today,
                ItemId = item.Id,
                IgnoreEmptyStock = true
            });
            previewEmpty.ShouldBeEmpty();

            // With IgnoreEmptyStock = false: should return 1 row with 0 qty
            var previewAll = await srAppService.GetItemsForReconciliationAsync(new GetStockReconciliationItemsInputDto
            {
                CompanyId = company.Id,
                WarehouseId = wh.Id,
                PostingDate = today,
                ItemId = item.Id,
                IgnoreEmptyStock = false
            });
            previewAll.Count.ShouldBe(1);
            previewAll[0].CurrentQuantity.ShouldBe(0m);
            previewAll[0].CurrentValuationRate.ShouldBe(10m);
        });
    }
}
