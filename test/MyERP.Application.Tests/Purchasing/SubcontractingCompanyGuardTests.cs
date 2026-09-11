using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: SubcontractingAppService.CreateOrderAsync/CreateReceiptAsync only validated Items via
/// ItemTransactionValidationService (which just checks IsActive, not company ownership) and never
/// wired CompanyRestrictionValidationService — unlike PurchaseOrder/PurchaseReceipt, which both do.
/// A cross-company Supplier or Item could be used on a Subcontracting Order/Receipt with no guard.
/// </summary>
public abstract class SubcontractingCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateOrderAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Guard Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCO Guard Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCO-GUARD-1", "SCO Guard Item", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScoItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "Cross-co item", Qty = 1m, Rate = 10m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateOrderAsync_PurchaseOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var poRepository = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO PO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO PO Other Co"), autoSave: true);

            var supplierOwner = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCO PO Supplier Owner"), autoSave: true);
            var supplierOther = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "SCO PO Supplier Other"), autoSave: true);

            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCO-PO-ITEM", "SCO PO Item", ItemType.Goods), autoSave: true);
            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCO-PO-ITEM-OTHER", "SCO PO Item Other", ItemType.Goods), autoSave: true);

            var crossCoPo = new PurchaseOrder(Guid.NewGuid(), otherCompany.Id, supplierOther.Id, "PO-SCO-CROSS", DateTime.UtcNow);
            crossCoPo.AddItem(itemOther.Id, "PO Item Other", 1m, 100m, 0m);
            await poRepository.InsertAsync(crossCoPo, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplierOwner.Id,
                    OrderDate = DateTime.UtcNow,
                    PurchaseOrderId = crossCoPo.Id,
                    Items = new List<CreateScoItemDto>
                    {
                        new() { ItemId = itemOwner.Id, ItemName = "Owner item", Qty = 1m, Rate = 10m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateOrderAsync_BomFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Bom Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Bom Other Co"), autoSave: true);

            var supplierOwner = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCO Bom Supplier"), autoSave: true);
            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCO-BOM-FG", "SCO Bom FG", ItemType.Goods), autoSave: true);

            var fgOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCO-BOM-FG-OTHER", "SCO Bom FG Other", ItemType.Goods), autoSave: true);
            var rmOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCO-BOM-RM-OTHER", "SCO Bom RM Other", ItemType.Goods), autoSave: true);

            var otherBom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), otherCompany.Id, "BOM-SCO-OTHER", fgOther.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            otherBom.Items.Add(new Manufacturing.Entities.BomItem(Guid.NewGuid(), otherBom.Id, rmOther.Id, "RM Other", 1, 10));
            await bomRepository.InsertAsync(otherBom, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplierOwner.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScoItemDto>
                    {
                        new() { ItemId = itemOwner.Id, ItemName = "Owner item", Qty = 1m, Rate = 10m, BomId = otherBom.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateOrderAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Wh Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Wh Other Co"), autoSave: true);

            var supplierOwner = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCO Wh Supplier"), autoSave: true);
            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCO-WH-FG", "SCO Wh FG", ItemType.Goods), autoSave: true);
            var otherWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other SCO Warehouse"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplierOwner.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScoItemDto>
                    {
                        new() { ItemId = itemOwner.Id, ItemName = "Owner item", Qty = 1m, Rate = 10m, WarehouseId = otherWarehouse.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateReceiptAsync_SubcontractingOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var scoRepository = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCR SCO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCR SCO Other Co"), autoSave: true);

            var supplierOwner = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCR SCO Supplier Owner"), autoSave: true);
            var supplierOther = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), otherCompany.Id, "SCR SCO Supplier Other"), autoSave: true);

            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SCR-FG-OWNER", "SCR FG Owner", ItemType.Goods), autoSave: true);
            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCR-FG-OTHER", "SCR FG Other", ItemType.Goods), autoSave: true);

            var otherSco = new SubcontractingOrder(Guid.NewGuid(), otherCompany.Id, "SCO-OTHER-001", DateTime.UtcNow, supplierOther.Id);
            otherSco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), otherSco.Id, itemOther.Id, "Item Other", 1, 50));
            await scoRepository.InsertAsync(otherSco, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subcontractingAppService.CreateReceiptAsync(new CreateSubcontractingReceiptDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplierOwner.Id,
                    SubcontractingOrderId = otherSco.Id,
                    PostingDate = DateTime.UtcNow,
                    Items = new List<CreateScrItemDto>
                    {
                        new() { ItemId = itemOwner.Id, ItemName = "Item Owner", Qty = 1, Rate = 50 }
                    }
                }));
        });
    }
}
