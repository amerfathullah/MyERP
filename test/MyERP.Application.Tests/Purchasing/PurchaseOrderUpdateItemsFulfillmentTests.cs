using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression tests for PurchaseOrderAppService.UpdateItemsAsync downstream
/// recalculations: totals (NetTotal/GrandTotal), SalesOrderItem.OrderedQty (cf91622391),
/// MaterialRequestItem.OrderedQuantity + status, and SupplierQuotationItem.OrderedQty.
/// </summary>
public abstract class PurchaseOrderUpdateItemsFulfillmentTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task UpdateItemsAsync_RecalculatesTotals_And_Syncs_SO_MR_SQ_OrderedQty()
    {
        Guid companyId = default, supplierId = default, item1Id = default, item2Id = default;
        Guid soId = default, soItem1Id = default, soItem2Id = default;
        Guid mrId = default, mrItem1Id = default, mrItem2Id = default;
        Guid sqId = default, sqItem1Id = default, sqItem2Id = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var sqRepo = GetRequiredService<IRepository<SupplierQuotation, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Upd Sync Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PO Series", "PurchaseOrder", "POUPD-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "PO Upd Supplier"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "PO Upd Customer"), autoSave: true);

            var item1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "POUPD-1", "Item 1", ItemType.Goods) { MaintainStock = false }, autoSave: true);
            var item2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "POUPD-2", "Item 2", ItemType.Goods) { MaintainStock = false }, autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-UPD-001", DateTime.UtcNow.Date, company.TenantId);
            so.AddItem(item1.Id, "Item 1", 10m, 100m, 0m, "Unit");
            so.AddItem(item2.Id, "Item 2", 6m, 50m, 0m, "Unit");
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-UPD-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item1.Id, "Item 1", 10m, "Unit", conversionFactor: 1m);
            mr.AddItem(item2.Id, "Item 2", 6m, "Unit", conversionFactor: 1m);
            mr.Submit();
            await mrRepo.InsertAsync(mr, autoSave: true);

            var sq = new SupplierQuotation(Guid.NewGuid(), company.Id, supplier.Id, DateTime.UtcNow.Date, company.TenantId);
            sq.AddItem(item1.Id, 10m, 80m, "Item 1", "Unit");
            sq.AddItem(item2.Id, 6m, 40m, "Item 2", "Unit");
            sq.Submit();
            await sqRepo.InsertAsync(sq, autoSave: true);

            companyId = company.Id;
            supplierId = supplier.Id;
            item1Id = item1.Id;
            item2Id = item2.Id;
            soId = so.Id;
            soItem1Id = so.Items.ElementAt(0).Id;
            soItem2Id = so.Items.ElementAt(1).Id;
            mrId = mr.Id;
            mrItem1Id = mr.Items.ElementAt(0).Id;
            mrItem2Id = mr.Items.ElementAt(1).Id;
            sqId = sq.Id;
            sqItem1Id = sq.Items.ElementAt(0).Id;
            sqItem2Id = sq.Items.ElementAt(1).Id;
        });

        var poAppService = GetRequiredService<IPurchaseOrderAppService>();
        var poDto = await poAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            OrderDate = DateTime.UtcNow.Date,
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new()
                {
                    ItemId = item1Id, Description = "Item 1", Quantity = 10m, UnitPrice = 80m, Uom = "Unit",
                    SalesOrderItemId = soItem1Id, MaterialRequestItemId = mrItem1Id, SupplierQuotationItemId = sqItem1Id
                },
                new()
                {
                    ItemId = item2Id, Description = "Item 2", Quantity = 6m, UnitPrice = 40m, Uom = "Unit",
                    SalesOrderItemId = soItem2Id, MaterialRequestItemId = mrItem2Id, SupplierQuotationItemId = sqItem2Id
                }
            }
        });
        await poAppService.SubmitAsync(poDto.Id);

        // Verify initial full ordering on SO, MR, and SQ after Submit
        await WithUnitOfWorkAsync(async () =>
        {
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var sqRepo = GetRequiredService<IRepository<SupplierQuotation, Guid>>();

            var so = (await soRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == soId);
            so.Items.First(i => i.Id == soItem1Id).OrderedQty.ShouldBe(10m);
            so.Items.First(i => i.Id == soItem2Id).OrderedQty.ShouldBe(6m);

            var mr = (await mrRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == mrId);
            mr.Items.First(i => i.Id == mrItem1Id).OrderedQuantity.ShouldBe(10m);
            mr.Items.First(i => i.Id == mrItem2Id).OrderedQuantity.ShouldBe(6m);

            var sq = (await sqRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == sqId);
            sq.Items.First(i => i.Id == sqItem1Id).OrderedQty.ShouldBe(10m);
            sq.Items.First(i => i.Id == sqItem2Id).OrderedQty.ShouldBe(6m);
        });

        var poLine1Id = poDto.Items.First(i => i.ItemId == item1Id).Id;
        var poLine2Id = poDto.Items.First(i => i.ItemId == item2Id).Id;

        // Shrink item 1 from 10 -> 4 @ 75, remove item 2 completely
        var result = await poAppService.UpdateItemsAsync(poDto.Id, new UpdateOrderItemsDto
        {
            RemovedItemIds = new List<Guid> { poLine2Id },
            Items = new List<UpdateOrderItemDto>
            {
                new() { ItemId = poLine1Id, Quantity = 4m, UnitPrice = 75m, Uom = "Unit", ConversionFactor = 1m }
            }
        });

        // Initial total was 10*80 + 6*40 = 1040; new total should be 4*75 = 300
        result.PreviousGrandTotal.ShouldBe(1040m);
        result.NewGrandTotal.ShouldBe(300m);

        var updatedPo = await poAppService.GetAsync(poDto.Id);
        updatedPo.GrandTotal.ShouldBe(300m);
        updatedPo.NetTotal.ShouldBe(300m);

        await WithUnitOfWorkAsync(async () =>
        {
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var sqRepo = GetRequiredService<IRepository<SupplierQuotation, Guid>>();

            var so = (await soRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == soId);
            so.Items.First(i => i.Id == soItem1Id).OrderedQty.ShouldBe(4m);
            so.Items.First(i => i.Id == soItem2Id).OrderedQty.ShouldBe(0m);

            var mr = (await mrRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == mrId);
            mr.Items.First(i => i.Id == mrItem1Id).OrderedQuantity.ShouldBe(4m);
            mr.Items.First(i => i.Id == mrItem2Id).OrderedQuantity.ShouldBe(0m);

            var sq = (await sqRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == sqId);
            sq.Items.First(i => i.Id == sqItem1Id).OrderedQty.ShouldBe(4m);
            sq.Items.First(i => i.Id == sqItem2Id).OrderedQty.ShouldBe(0m);
        });
    }

    [Fact]
    public async Task UpdateItemsAsync_HandlesNullRemovedItemIds_And_CrossUomMrAdjustment()
    {
        Guid companyId = default, supplierId = default, itemId = default;
        Guid mrId = default, mrItemId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PO Upd CrossUom Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PO Series", "PurchaseOrder", "POUOM-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "PO Upd CrossUom Supplier"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "POUOM-1", "Boxed Item", ItemType.Goods) { MaintainStock = false }, autoSave: true);

            // MR in Unit (factor = 1), 20 units requested
            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-UOM-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item.Id, "Boxed Item", 20m, "Unit", conversionFactor: 1m);
            mr.Submit();
            await mrRepo.InsertAsync(mr, autoSave: true);

            companyId = company.Id;
            supplierId = supplier.Id;
            itemId = item.Id;
            mrId = mr.Id;
            mrItemId = mr.Items.Single().Id;
        });

        var poAppService = GetRequiredService<IPurchaseOrderAppService>();
        // PO in Box: 2 Boxes -> set ConversionFactor = 5 before Submit => 10 Units ordered on MR
        var poDto = await poAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            OrderDate = DateTime.UtcNow.Date,
            Items = new List<CreatePurchaseOrderItemDto>
            {
                new()
                {
                    ItemId = itemId, Description = "Boxed Item", Quantity = 2m, UnitPrice = 100m,
                    Uom = "Box", MaterialRequestItemId = mrItemId
                }
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var po = (await poRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == poDto.Id);
            po.Items.Single().ConversionFactor = 5m;
            await poRepo.UpdateAsync(po, autoSave: true);
        });

        await poAppService.SubmitAsync(poDto.Id);

        var poLineId = poDto.Items.Single().Id;
        // Change PO qty from 2 -> 4 Boxes (cf=5 => 20 Units on MR), RemovedItemIds omitted
        await poAppService.UpdateItemsAsync(poDto.Id, new UpdateOrderItemsDto
        {
            RemovedItemIds = null!,
            Items = new List<UpdateOrderItemDto>
            {
                new() { ItemId = poLineId, Quantity = 4m, UnitPrice = 100m, Uom = "Box", ConversionFactor = 5m }
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var mr = (await mrRepo.WithDetailsAsync(x => x.Items)).First(x => x.Id == mrId);
            mr.Items.Single().OrderedQuantity.ShouldBe(20m);
        });
    }
}
