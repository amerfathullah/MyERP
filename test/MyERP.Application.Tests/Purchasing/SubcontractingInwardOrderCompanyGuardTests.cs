using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: SubcontractingInwardOrderAppService.CreateAsync only ran items through
/// ItemTransactionValidationService.ValidateItemAsync (IsActive check only, not company ownership)
/// and never wired CompanyRestrictionValidationService, mirroring the same gap fixed in the
/// sibling SubcontractingAppService (Order/Receipt) this session.
/// </summary>
public abstract class SubcontractingInwardOrderCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Guard Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCIO Guard Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCIO-GUARD-1", "SCIO Guard Item", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScioItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1m, Rate = 10m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var soRepository = GetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO SO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO SO Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCIO SO Supplier"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "SCIO Customer Other"), autoSave: true);
            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCIO-SO-ITEM-OWNER", "SCIO SO Item Owner", ItemType.Goods), autoSave: true);
            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCIO-SO-ITEM-OTHER", "SCIO SO Item Other", ItemType.Goods), autoSave: true);

            var crossCoSo = new Sales.Entities.SalesOrder(Guid.NewGuid(), otherCompany.Id, customer.Id, "SO-SCIO-CROSS", DateTime.UtcNow);
            crossCoSo.AddItem(itemOther.Id, "Widget Other", 1, 50, 0);
            await soRepository.InsertAsync(crossCoSo, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    SalesOrderId = crossCoSo.Id,
                    Items = new List<CreateScioItemDto>
                    {
                        new() { ItemId = itemOwner.Id, Quantity = 1m, Rate = 10m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SubcontractingOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var scoRepository = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO SCO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO SCO Other Co"), autoSave: true);

            var supplierOwner = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCIO SCO Supplier Owner"), autoSave: true);
            var supplierOther = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "SCIO SCO Supplier Other"), autoSave: true);

            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCIO-SCO-ITEM-OWNER", "SCIO SCO Item Owner", ItemType.Goods), autoSave: true);
            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCIO-SCO-ITEM-OTHER", "SCIO SCO Item Other", ItemType.Goods), autoSave: true);

            var otherSco = new SubcontractingOrder(Guid.NewGuid(), otherCompany.Id, "SCO-SCIO-CROSS", DateTime.UtcNow, supplierOther.Id);
            otherSco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), otherSco.Id, itemOther.Id, "Item Other", 1, 50));
            await scoRepository.InsertAsync(otherSco, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplierOwner.Id,
                    OrderDate = DateTime.UtcNow,
                    SubcontractingOrderId = otherSco.Id,
                    Items = new List<CreateScioItemDto>
                    {
                        new() { ItemId = itemOwner.Id, Quantity = 1m, Rate = 10m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_BomFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Bom Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Bom Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCIO Bom Supplier"), autoSave: true);
            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCIO-BOM-ITEM-OWNER", "SCIO Bom Item Owner", ItemType.Goods), autoSave: true);

            var fgOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCIO-BOM-FG-OTHER", "SCIO Bom FG Other", ItemType.Goods), autoSave: true);
            var rmOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCIO-BOM-RM-OTHER", "SCIO Bom RM Other", ItemType.Goods), autoSave: true);

            var otherBom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), otherCompany.Id, "BOM-SCIO-OTHER", fgOther.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            otherBom.Items.Add(new Manufacturing.Entities.BomItem(Guid.NewGuid(), otherBom.Id, rmOther.Id, "RM Other", 1, 10));
            await bomRepository.InsertAsync(otherBom, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScioItemDto>
                    {
                        new() { ItemId = itemOwner.Id, Quantity = 1m, Rate = 10m, BomId = otherBom.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Wh Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Wh Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCIO Wh Supplier"), autoSave: true);
            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCIO-WH-ITEM-OWNER", "SCIO Wh Item Owner", ItemType.Goods), autoSave: true);

            var otherWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other SCIO Warehouse"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScioItemDto>
                    {
                        new() { ItemId = itemOwner.Id, Quantity = 1m, Rate = 10m, WarehouseId = otherWarehouse.Id }
                    }
                }));
        });
    }

    // ERPNext commit 7e93294514: allow_delivery_of_overproduced_qty setting on SCIO.
    [Fact]
    public async Task ReceiveItemsAsync_RespectsAllowDeliveryOfOverproducedQtySetting()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();
            var settingsService = GetRequiredService<MyERP.Settings.IErpSettingsAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Overprod Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCIO Overprod Sup"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCIO-OVP-1", "SCIO OVP Item", ItemType.Goods), autoSave: true);
            var wh = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "SCIO OVP WH"), autoSave: true);

            var created = await scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow.Date,
                Items = new List<CreateScioItemDto>
                {
                    new() { ItemId = item.Id, Quantity = 10m, Rate = 20m, WarehouseId = wh.Id, ServiceCostPerQty = 5m }
                }
            });
            await scioAppService.SubmitAsync(created.Id);

            // Default false: receiving 12 > 10 fails
            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.ReceiveItemsAsync(created.Id, new ScioReceiveItemsDto
                {
                    PostingDate = DateTime.UtcNow.Date,
                    Items = new List<ScioReceiveItemDto>
                    {
                        new() { SubcontractingInwardOrderItemId = created.Items[0].Id, ItemId = item.Id, Qty = 12m }
                    }
                }));

            // Enable AllowDeliveryOfOverproducedQty: action summary exposes it and receipt > ordered succeeds
            await settingsService.SetAsync(
                MyERP.Settings.MyERPSettings.Selling.AllowDeliveryOfOverproducedQty,
                "true");

            var summary = await scioAppService.GetActionSummaryAsync(created.Id);
            summary.AllowDeliveryOfOverproducedQty.ShouldBeTrue();

            var updated = await scioAppService.ReceiveItemsAsync(created.Id, new ScioReceiveItemsDto
            {
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<ScioReceiveItemDto>
                {
                    new() { SubcontractingInwardOrderItemId = created.Items[0].Id, ItemId = item.Id, Qty = 12m }
                }
            });
            updated.Items[0].ReceivedQty.ShouldBe(12m);
        });
    }
}
