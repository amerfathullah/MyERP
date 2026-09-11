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

    [Fact]
    public async Task CreateWorkOrderAsync_SalesOrderItemMismatch_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO SO Mismatch Co"), autoSave: true);
            var itemA = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-ITEM-A", "Item A", ItemType.Goods), autoSave: true);
            var itemB = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-ITEM-B", "Item B", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-ITEM-RM", "Item RM", ItemType.Goods), autoSave: true);

            var bomB = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-ITEM-B", itemB.Id) { Quantity = 1, IsActive = true };
            bomB.Items.Add(new BomItem(Guid.NewGuid(), bomB.Id, rmItem.Id, "Item RM", 1, 10));
            await bomRepository.InsertAsync(bomB, autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Customer Mismatch"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-MISMATCH-001", DateTime.UtcNow);
            so.AddItem(itemA.Id, "Item A", 5m, 100m, 0m);
            await soRepository.InsertAsync(so, autoSave: true);

            // WO produces itemB, but referenced SO only has itemA -> must throw
            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
                {
                    CompanyId = company.Id,
                    ItemId = itemB.Id,
                    BomId = bomB.Id,
                    Quantity = 1,
                    SalesOrderId = so.Id,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task CreateWorkOrderAsync_WipSameAsFgWarehouse_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Wh Clash Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-FG-CLASH", "FG Clash", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-RM-CLASH", "RM Clash", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-CLASH", fgItem.Id) { Quantity = 1, IsActive = true };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "RM Clash", 1, 10));
            await bomRepository.InsertAsync(bom, autoSave: true);

            var sharedWh = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Shared WIP and FG WH"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
                {
                    CompanyId = company.Id,
                    ItemId = fgItem.Id,
                    BomId = bom.Id,
                    Quantity = 1,
                    WipWarehouseId = sharedWh.Id,
                    FgWarehouseId = sharedWh.Id,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task UpdateWorkOrderAsync_NonDraft_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Upd NonDraft Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-FG-ND", "FG NonDraft", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-RM-ND", "RM NonDraft", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-ND", fgItem.Id) { Quantity = 1, IsActive = true };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "RM NonDraft", 1, 10));
            await bomRepository.InsertAsync(bom, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-SUBMITTED", fgItem.Id, bom.Id, 10);
            wo.Submit();
            await woRepository.InsertAsync(wo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.UpdateWorkOrderAsync(wo.Id, new CreateWorkOrderDto
                {
                    CompanyId = company.Id,
                    ItemId = fgItem.Id,
                    BomId = bom.Id,
                    Quantity = 20,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task UpdateWorkOrderAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Upd Wh Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Upd Wh Other Co"), autoSave: true);

            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-FG-UPD-WH", "FG Upd WH", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "WO-RM-UPD-WH", "RM Upd WH", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-UPD-WH", fgItem.Id) { Quantity = 1, IsActive = true };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "RM Upd WH", 1, 10));
            await bomRepository.InsertAsync(bom, autoSave: true);

            var otherWh = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross-Co Warehouse"), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), ownerCompany.Id, "WO-DRAFT-UPD", fgItem.Id, bom.Id, 5);
            await woRepository.InsertAsync(wo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                manufacturingAppService.UpdateWorkOrderAsync(wo.Id, new CreateWorkOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = fgItem.Id,
                    BomId = bom.Id,
                    Quantity = 10,
                    FgWarehouseId = otherWh.Id,
                    PlannedStartDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task UpdateWorkOrderAsync_Success()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Upd Success Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-FG-SUCCESS", "FG Success", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "WO-RM-SUCCESS", "RM Success", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SUCCESS", fgItem.Id) { Quantity = 1, IsActive = true };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "RM Success", 2, 10));
            await bomRepository.InsertAsync(bom, autoSave: true);

            var wipWh = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WIP WH"), autoSave: true);
            var fgWh = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "FG WH"), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-SUCCESS-001", fgItem.Id, bom.Id, 5);
            await woRepository.InsertAsync(wo, autoSave: true);

            var updated = await manufacturingAppService.UpdateWorkOrderAsync(wo.Id, new CreateWorkOrderDto
            {
                CompanyId = company.Id,
                ItemId = fgItem.Id,
                BomId = bom.Id,
                Quantity = 10,
                WipWarehouseId = wipWh.Id,
                FgWarehouseId = fgWh.Id,
                PlannedStartDate = DateTime.UtcNow,
                Notes = "Updated notes"
            });

            updated.ShouldNotBeNull();
            updated.Quantity.ShouldBe(10);
            updated.Notes.ShouldBe("Updated notes");
            updated.WipWarehouseId.ShouldBe(wipWh.Id);
            updated.FgWarehouseId.ShouldBe(fgWh.Id);
        });
    }
}
