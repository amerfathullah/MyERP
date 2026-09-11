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

public abstract class WorkOrderCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateWorkOrderAsync_BomFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Guard Other Co"), autoSave: true);

            var fgOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-GUARD-FG-OWNER", "WO Guard FG Owner", ItemType.Goods), autoSave: true);

            var fgOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "WO-GUARD-FG-OTHER", "WO Guard FG Other", ItemType.Goods), autoSave: true);
            var rmOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "WO-GUARD-RM-OTHER", "WO Guard RM Other", ItemType.Goods), autoSave: true);

            var otherBom = new BillOfMaterials(Guid.NewGuid(), otherCompany.Id, "BOM-OTHER-CO", fgOther.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            otherBom.Items.Add(new BomItem(Guid.NewGuid(), otherBom.Id, rmOther.Id, "RM Other", 1, 10));
            await bomRepository.InsertAsync(otherBom, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = fgOwner.Id,
                    BomId = otherBom.Id,
                    Quantity = 1,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task CreateWorkOrderAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO SO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO SO Other Co"), autoSave: true);

            var fgOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-GUARD-FG-SO-OWNER", "WO Guard FG SO Owner", ItemType.Goods), autoSave: true);
            var rmOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-GUARD-RM-SO-OWNER", "WO Guard RM SO Owner", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-SO-OWNER", fgOwner.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmOwner.Id, "RM Owner", 1, 10));
            await bomRepository.InsertAsync(bom, autoSave: true);

            var fgOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "WO-GUARD-FG-SO-OTHER", "WO Guard FG SO Other", ItemType.Goods), autoSave: true);

            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "Other Customer"), autoSave: true);

            var crossCoSo = new SalesOrder(
                Guid.NewGuid(), otherCompany.Id, customer.Id, "SO-CROSS-CO", DateTime.UtcNow);
            crossCoSo.AddItem(fgOther.Id, "FG Other", 1m, 100m, 0m);
            await soRepository.InsertAsync(crossCoSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = fgOwner.Id,
                    BomId = bom.Id,
                    Quantity = 1,
                    SalesOrderId = crossCoSo.Id,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task CreateWorkOrderAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Wh Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Wh Other Co"), autoSave: true);

            var fgOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-GUARD-FG-WH", "WO Guard FG WH", ItemType.Goods), autoSave: true);
            var rmOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-GUARD-RM-WH", "WO Guard RM WH", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-WH-OWNER", fgOwner.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmOwner.Id, "RM WH", 1, 10));
            await bomRepository.InsertAsync(bom, autoSave: true);

            var otherWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Warehouse"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = fgOwner.Id,
                    BomId = bom.Id,
                    Quantity = 1,
                    SourceWarehouseId = otherWarehouse.Id,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }
}
