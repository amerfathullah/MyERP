using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class ProductionPlanCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateProductionPlanAsync_BomFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var productionPlanAppService = GetRequiredService<IProductionPlanAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PP Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PP Guard Other Co"), autoSave: true);

            var fgOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "PP-GUARD-FG-OWNER", "PP Guard FG Owner", ItemType.Goods), autoSave: true);

            var fgOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "PP-GUARD-FG-OTHER", "PP Guard FG Other", ItemType.Goods), autoSave: true);
            var rmOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "PP-GUARD-RM-OTHER", "PP Guard RM Other", ItemType.Goods), autoSave: true);

            var otherBom = new BillOfMaterials(Guid.NewGuid(), otherCompany.Id, "BOM-PP-OTHER", fgOther.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            otherBom.Items.Add(new BomItem(Guid.NewGuid(), otherBom.Id, rmOther.Id, "RM Other", 1, 10));
            await bomRepository.InsertAsync(otherBom, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                productionPlanAppService.CreateAsync(new CreateProductionPlanDto
                {
                    CompanyId = ownerCompany.Id,
                    PostingDate = DateTime.UtcNow,
                    Items = new List<CreateProductionPlanItemDto>
                    {
                        new()
                        {
                            ItemId = fgOwner.Id,
                            ItemName = fgOwner.ItemName,
                            BomId = otherBom.Id,
                            PlannedQty = 5
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateProductionPlanAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var productionPlanAppService = GetRequiredService<IProductionPlanAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PP SO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PP SO Other Co"), autoSave: true);

            var fgOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "PP-GUARD-FG-SO", "PP Guard FG SO", ItemType.Goods), autoSave: true);
            var rmOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "PP-GUARD-RM-SO", "PP Guard RM SO", ItemType.Goods), autoSave: true);

            var bomOwner = new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-PP-SO", fgOwner.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            bomOwner.Items.Add(new BomItem(Guid.NewGuid(), bomOwner.Id, rmOwner.Id, "RM SO", 1, 10));
            await bomRepository.InsertAsync(bomOwner, autoSave: true);

            var fgOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "PP-GUARD-FG-OTHER-SO", "PP Guard FG Other SO", ItemType.Goods), autoSave: true);
            var customerOther = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "Customer Other"), autoSave: true);

            var crossCoSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, customerOther.Id, "SO-PP-CROSS", DateTime.UtcNow);
            crossCoSo.AddItem(fgOther.Id, "FG Other", 2m, 100m, 0m);
            await soRepository.InsertAsync(crossCoSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                productionPlanAppService.CreateAsync(new CreateProductionPlanDto
                {
                    CompanyId = ownerCompany.Id,
                    PostingDate = DateTime.UtcNow,
                    Items = new List<CreateProductionPlanItemDto>
                    {
                        new()
                        {
                            ItemId = fgOwner.Id,
                            ItemName = fgOwner.ItemName,
                            BomId = bomOwner.Id,
                            PlannedQty = 2,
                            SalesOrderId = crossCoSo.Id
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateProductionPlanAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var productionPlanAppService = GetRequiredService<IProductionPlanAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PP Wh Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PP Wh Other Co"), autoSave: true);

            var fgOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "PP-GUARD-FG-WH", "PP Guard FG WH", ItemType.Goods), autoSave: true);
            var rmOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "PP-GUARD-RM-WH", "PP Guard RM WH", ItemType.Goods), autoSave: true);

            var bomOwner = new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-PP-WH", fgOwner.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            bomOwner.Items.Add(new BomItem(Guid.NewGuid(), bomOwner.Id, rmOwner.Id, "RM WH", 1, 10));
            await bomRepository.InsertAsync(bomOwner, autoSave: true);

            var otherWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Warehouse PP"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                productionPlanAppService.CreateAsync(new CreateProductionPlanDto
                {
                    CompanyId = ownerCompany.Id,
                    PostingDate = DateTime.UtcNow,
                    ForWarehouseId = otherWarehouse.Id,
                    Items = new List<CreateProductionPlanItemDto>
                    {
                        new()
                        {
                            ItemId = fgOwner.Id,
                            ItemName = fgOwner.ItemName,
                            BomId = bomOwner.Id,
                            PlannedQty = 1
                        }
                    }
                }));
        });
    }
}
