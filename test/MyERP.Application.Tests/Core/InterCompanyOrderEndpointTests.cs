using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Core;

/// <summary>
/// Integration tests for inter-company Sales Order ↔ Purchase Order creation,
/// partial pending-qty visibility, and counterpart duplicate suppression (ERPNext PR #66964).
/// </summary>
public abstract class InterCompanyOrderEndpointTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SalesOrder_InternalCustomer_PendingQtyAndCounterpartGuard_Work()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var seller = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC Order Seller"), autoSave: true);
            var buyer = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC Order Buyer"), autoSave: true);

            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), seller.Id, "SO", "SalesOrder", "SO-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), buyer.Id, "PO", "PurchaseOrder", "PO-"), autoSave: true);

            var internalCustomer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), seller.Id, "Buyer (Internal Customer)") { RepresentsCompanyId = buyer.Id },
                autoSave: true);
            var internalSupplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), buyer.Id, "Seller (Internal Supplier)") { RepresentsCompanyId = seller.Id },
                autoSave: true);
            var sellerItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), seller.Id, "IC-ORD-01", "Inter-Company Service", ItemType.Service),
                autoSave: true);
            var buyerItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), buyer.Id, "IC-ORD-01", "Inter-Company Service", ItemType.Service),
                autoSave: true);

            // Create and submit SO: auto-creates inter-company PO for full qty (5)
            var soDto = await soAppService.CreateAsync(new CreateSalesOrderDto
            {
                CompanyId = seller.Id,
                CustomerId = internalCustomer.Id,
                OrderDate = DateTime.UtcNow.Date,
                DeliveryDate = DateTime.UtcNow.Date.AddDays(5),
                Items = new List<CreateSalesOrderItemDto>
                {
                    new() { ItemId = sellerItem.Id, Description = "Inter-Company Service", Quantity = 5m, UnitPrice = 20m, TaxAmount = 0m, Uom = "Unit" }
                }
            });

            await soAppService.SubmitAsync(soDto.Id);

            var afterAuto = await soAppService.GetAsync(soDto.Id);
            afterAuto.IsInternalCustomer.ShouldBeTrue();
            afterAuto.InterCompanyPurchaseOrderId.ShouldNotBeNull();
            // Full qty already ordered -> button hidden per PR #66964
            afterAuto.CanCreateInterCompanyPurchaseOrder.ShouldBeFalse();

            // Counterpart PO (created from SO) must not re-offer creating another inter-company SO
            var autoPoDto = await poAppService.GetAsync(afterAuto.InterCompanyPurchaseOrderId!.Value);
            autoPoDto.IsInternalSupplier.ShouldBeTrue();
            autoPoDto.InterCompanySalesOrderId.ShouldBe(soDto.Id);
            autoPoDto.CanCreateInterCompanySalesOrder.ShouldBeFalse();

            // Replace full auto-PO with a partial PO for 2 units linked to the SO item row
            var autoPo = await poRepo.GetAsync(autoPoDto.Id);
            autoPo.Cancel();
            await poRepo.UpdateAsync(autoPo, autoSave: true);

            var soEntity = await soRepo.GetAsync(soDto.Id);
            var soLine = soEntity.Items.First();

            var partialPo = new PurchaseOrder(Guid.NewGuid(), buyer.Id, internalSupplier.Id, "PO-IC-PART-1", DateTime.UtcNow.Date);
            var partialPoItem = partialPo.AddItem(buyerItem.Id, "Inter-Company Service", 2m, 20m, 0m);
            partialPoItem.SalesOrderId = soEntity.Id;
            partialPoItem.SalesOrderItemId = soLine.Id;
            partialPo.InterCompanySalesOrderId = soEntity.Id;
            await poRepo.InsertAsync(partialPo, autoSave: true);

            // PR #66964: pending qty = 5 - 2 = 3 -> button visible again
            var whilePending = await soAppService.GetAsync(soDto.Id);
            whilePending.CanCreateInterCompanyPurchaseOrder.ShouldBeTrue();

            var secondPo = await soAppService.CreateInterCompanyPurchaseOrderAsync(soDto.Id);
            secondPo.Items.Single().Quantity.ShouldBe(3m);
            secondPo.CompanyId.ShouldBe(buyer.Id);

            var afterSecond = await soAppService.GetAsync(soDto.Id);
            afterSecond.CanCreateInterCompanyPurchaseOrder.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task PurchaseOrder_InternalSupplier_SubmitAutoCreatesSalesOrderAndHidesCounterpartButton()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();

            var buyer = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC PO Buyer Co"), autoSave: true);
            var seller = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC PO Seller Co"), autoSave: true);

            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), buyer.Id, "PO", "PurchaseOrder", "PO-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), seller.Id, "SO", "SalesOrder", "SO-"), autoSave: true);

            var internalSupplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), buyer.Id, "Seller (Internal Supplier)") { RepresentsCompanyId = seller.Id },
                autoSave: true);
            await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), seller.Id, "Buyer (Internal Customer)") { RepresentsCompanyId = buyer.Id },
                autoSave: true);
            var buyerItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), buyer.Id, "IC-ORD-02", "Inter-Company Consulting", ItemType.Service),
                autoSave: true);
            var sellerItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), seller.Id, "IC-ORD-02", "Inter-Company Consulting", ItemType.Service),
                autoSave: true);

            var draftPo = await poAppService.CreateAsync(new CreatePurchaseOrderDto
            {
                CompanyId = buyer.Id,
                SupplierId = internalSupplier.Id,
                OrderDate = DateTime.UtcNow.Date,
                ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(3),
                Items = new List<CreatePurchaseOrderItemDto>
                {
                    new() { ItemId = buyerItem.Id, Description = "Inter-Company Consulting", Quantity = 4m, UnitPrice = 25m, TaxAmount = 0m, Uom = "Unit" }
                }
            });

            await poAppService.SubmitAsync(draftPo.Id);

            var afterSubmit = await poAppService.GetAsync(draftPo.Id);
            afterSubmit.IsInternalSupplier.ShouldBeTrue();
            afterSubmit.InterCompanySalesOrderId.ShouldNotBeNull();
            afterSubmit.CanCreateInterCompanySalesOrder.ShouldBeFalse();

            var createdSo = await soRepo.GetAsync(afterSubmit.InterCompanySalesOrderId!.Value);
            createdSo.CompanyId.ShouldBe(seller.Id);
            createdSo.Items.Single().ItemId.ShouldBe(sellerItem.Id);
            createdSo.GrandTotal.ShouldBe(100m);

            // Counterpart SO must suppress creating another PO back (PR #66964)
            var counterpartSoDto = await soAppService.GetAsync(createdSo.Id);
            counterpartSoDto.IsInternalCustomer.ShouldBeTrue();
            counterpartSoDto.CanCreateInterCompanyPurchaseOrder.ShouldBeFalse();

            // Deleting a PO row linked by an active inter-company Sales Order must throw (PR #66992)
            var poRowId = afterSubmit.Items[0].Id;
            var delEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                poAppService.UpdateItemsAsync(draftPo.Id, new UpdateOrderItemsDto
                {
                    RemovedItemIds = new List<Guid> { poRowId },
                }));
            delEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (delEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain(createdSo.OrderNumber);
        });
    }

    [Fact]
    public async Task SalesOrder_BundleRowOrderedOnPurchaseOrder_And_ReturnedOrderRows_CannotBeDeleted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var soAppService = GetRequiredService<ISalesOrderAppService>();
            var poAppService = GetRequiredService<IPurchaseOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Bundle Order Guard Co"), autoSave: true);
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SO", "SalesOrder", "SO-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PO", "PurchaseOrder", "PO-"), autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Bundle Customer"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Bundle Supplier"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main WH"), autoSave: true);

            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BND-01", "Starter Kit", ItemType.Service),
                autoSave: true);
            var packedComponent = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "CMP-01", "Packed Component", ItemType.Goods),
                autoSave: true);

            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(packedComponent.Id, 2m, "Packed Component", "Unit");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var soDto = await soAppService.CreateAsync(new CreateSalesOrderDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                OrderDate = DateTime.UtcNow.Date,
                DeliveryDate = DateTime.UtcNow.Date.AddDays(5),
                Items = new List<CreateSalesOrderItemDto>
                {
                    new() { ItemId = bundleItem.Id, Description = "Starter Kit", Quantity = 1m, UnitPrice = 100m, TaxAmount = 0m, Uom = "Unit", WarehouseId = warehouse.Id }
                }
            });
            await soAppService.SubmitAsync(soDto.Id);

            // Create a draft Purchase Order for the packed component linked to the Sales Order (commit 14a3847119)
            var poForPacked = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-PACKED-001", DateTime.UtcNow.Date);
            var poPackedItem = poForPacked.AddItem(packedComponent.Id, "Packed Component", 2m, 25m, 0m, "Unit", warehouse.Id);
            poPackedItem.SalesOrderId = soDto.Id;
            await poRepo.InsertAsync(poForPacked, autoSave: true);

            var soRowId = soDto.Items[0].Id;
            var bundleDelEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                soAppService.UpdateItemsAsync(soDto.Id, new UpdateOrderItemsDto
                {
                    RemovedItemIds = new List<Guid> { soRowId }
                }));
            bundleDelEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (bundleDelEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("PO-PACKED-001");

            // Create a submitted Purchase Order whose row was received and fully returned (ReceivedQty == 0, non-cancelled PR exists — commit a4819db4f9)
            var poDto = await poAppService.CreateAsync(new CreatePurchaseOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow.Date,
                ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(3),
                Items = new List<CreatePurchaseOrderItemDto>
                {
                    new() { ItemId = packedComponent.Id, Description = "Packed Component", Quantity = 5m, UnitPrice = 20m, TaxAmount = 0m, Uom = "Unit", WarehouseId = warehouse.Id }
                }
            });
            await poAppService.SubmitAsync(poDto.Id);

            var poRowForReturn = poDto.Items[0].Id;
            var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouse.Id, "PR-RET-001", DateTime.UtcNow.Date);
            pr.AddItem(packedComponent.Id, "Packed Component", 5m, 20m, 0m, "Unit", poRowForReturn, warehouse.Id);
            pr.Submit();
            await prRepo.InsertAsync(pr, autoSave: true);

            var retDelEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                poAppService.UpdateItemsAsync(poDto.Id, new UpdateOrderItemsDto
                {
                    RemovedItemIds = new List<Guid> { poRowForReturn }
                }));
            retDelEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (retDelEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("already been received");
        });
    }
}
