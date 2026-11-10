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
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch33IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // === 1) PR #65127: Sales Invoice Batch Check Only When UpdateStock ===

    [Fact]
    public async Task SalesInvoice_Submit_SkipsBatchValidationWhenUpdateStockFalse()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var invoiceAppService = GetRequiredService<ISalesInvoiceAppService>();
            var invoiceRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "SI Batch Co B33") { CurrencyCode = "MYR" },
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Cust B33"),
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-BATCH-B33", "Batched Item B33", ItemType.Goods)
                {
                    MaintainStock = true,
                    HasBatchNo = true
                },
                autoSave: true);

            // Expired batch (expired 30 days ago)
            var expiredBatch = await batchRepo.InsertAsync(
                new Batch(Guid.NewGuid(), item.Id, "BATCH-EXP-B33")
                {
                    ExpiryDate = DateTime.UtcNow.AddDays(-30)
                },
                autoSave: true);

            // Case A: Invoice with UpdateStock = false (e.g. billed after Delivery Note where stock already moved)
            var invoiceDto = await invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.UtcNow,
                UpdateStock = false,
                CurrencyCode = "MYR",
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Batched Item",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        TaxAmount = 0m,
                        Uom = "Unit"
                    }
                }
            });

            // Assign expired batch to item
            var invEntity = await invoiceRepo.GetAsync(invoiceDto.Id, includeDetails: true);
            invEntity.Items[0].BatchId = expiredBatch.Id;
            await invoiceRepo.UpdateAsync(invEntity, autoSave: true);

            // Submit should SUCCEED because UpdateStock = false
            await Should.NotThrowAsync(async () =>
            {
                await invoiceAppService.SubmitAsync(invEntity.Id);
            });
        });
    }

    [Fact]
    public async Task SalesInvoice_Submit_BlocksExpiredBatchWhenUpdateStockTrue()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var invoiceAppService = GetRequiredService<ISalesInvoiceAppService>();
            var invoiceRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "SI Expired Batch Co B33") { CurrencyCode = "MYR" },
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-"),
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Warehouse B33"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Cust B33-2"),
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-BATCH-B33-2", "Batched Item B33-2", ItemType.Goods)
                {
                    MaintainStock = true,
                    HasBatchNo = true,
                    DefaultWarehouseId = warehouse.Id
                },
                autoSave: true);

            // Expired batch (expired 30 days ago)
            var expiredBatch = await batchRepo.InsertAsync(
                new Batch(Guid.NewGuid(), item.Id, "BATCH-EXP-B33-2")
                {
                    ExpiryDate = DateTime.UtcNow.AddDays(-30)
                },
                autoSave: true);

            // Case B: Invoice with UpdateStock = true
            var invoiceDto = await invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.UtcNow,
                WarehouseId = warehouse.Id,
                UpdateStock = true,
                CurrencyCode = "MYR",
                Items = new List<CreateSalesInvoiceItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = "Batched Item",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        TaxAmount = 0m,
                        Uom = "Unit"
                    }
                }
            });

            // Assign expired batch to item
            var invEntity = await invoiceRepo.GetAsync(invoiceDto.Id, includeDetails: true);
            invEntity.Items[0].BatchId = expiredBatch.Id;
            await invoiceRepo.UpdateAsync(invEntity, autoSave: true);

            // Submit should FAIL because UpdateStock = true and batch has expired
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await invoiceAppService.SubmitAsync(invEntity.Id);
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (ex.Data["batchNo"]?.ToString() ?? string.Empty).ShouldBe("BATCH-EXP-B33-2");
        });
    }

    // === 2) PR #65109: Subcontracting Receipt Return Supplier Warehouse ===

    [Fact]
    public async Task SubcontractingReceipt_Return_EnforcesSameSupplierWarehouse()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Subcontracting Co B33") { CurrencyCode = "MYR" },
                autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Supplier B33"),
                autoSave: true);

            var supplierWh1 = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Supplier Warehouse 1"),
                autoSave: true);

            var supplierWh2 = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Supplier Warehouse 2"),
                autoSave: true);

            var sco = await scoRepo.InsertAsync(
                new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-B33", DateTime.UtcNow, supplier.Id),
                autoSave: true);

            var originalReceipt = new SubcontractingReceipt(
                Guid.NewGuid(), company.Id, "SCR-ORIG-B33", DateTime.UtcNow, supplier.Id, sco.Id)
            {
                SupplierWarehouseId = supplierWh1.Id
            };
            originalReceipt.AddItem(new SubcontractingReceiptItem(
                Guid.NewGuid(), originalReceipt.Id, Guid.NewGuid(), "Subcontracted FG", 10m, 50m));
            originalReceipt.Submit();
            await scrRepo.InsertAsync(originalReceipt, autoSave: true);

            // Case A: Create return with different supplier warehouse -> throws ValidationFailed
            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await subcontractingAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
                {
                    ReturnAgainstReceiptId = originalReceipt.Id,
                    PostingDate = DateTime.UtcNow,
                    SupplierWarehouseId = supplierWh2.Id, // Mismatched!
                    Items = new List<CreateScrReturnItemDto>
                    {
                        new()
                        {
                            ItemId = originalReceipt.Items[0].ItemId,
                            ItemName = "Subcontracted FG",
                            Qty = 2m,
                            Rate = 50m
                        }
                    }
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Supplier Warehouse must be");

            // Case B: Create return with matching supplier warehouse -> succeeds
            var returnReceipt = await subcontractingAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = originalReceipt.Id,
                PostingDate = DateTime.UtcNow,
                SupplierWarehouseId = supplierWh1.Id, // Matching
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = originalReceipt.Items[0].ItemId,
                        ItemName = "Subcontracted FG",
                        Qty = 2m,
                        Rate = 50m
                    }
                }
            });

            returnReceipt.ShouldNotBeNull();
            returnReceipt.IsReturn.ShouldBeTrue();
            returnReceipt.SupplierWarehouseId.ShouldBe(supplierWh1.Id);
        });
    }

    // === 3) PR #65115 & PR #65116: Pick List for Bundle with Repeated Component ===

    [Fact]
    public async Task SalesOrder_ConvertToPickList_ProductBundle_RepeatedComponentsAndPerComponentQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var plRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var conversionSvc = GetRequiredService<IDocumentConversionAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Bundle Pick Co B33") { CurrencyCode = "MYR" },
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Cust Bundle B33"),
                autoSave: true);

            var warehouse = await warehouseRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Main Warehouse B33"),
                autoSave: true);

            // Bundle parent item (non-stock)
            var parentItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-PC-B33", "PC Kit B33", ItemType.Goods)
                {
                    MaintainStock = false
                },
                autoSave: true);

            // Repeated component item (stock item)
            var compItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "CABLE-B33", "USB Cable B33", ItemType.Goods)
                {
                    MaintainStock = true,
                    DefaultWarehouseId = warehouse.Id
                },
                autoSave: true);

            // Bundle with repeated component: 2 rows of the same component
            var bundle = new ProductBundle(Guid.NewGuid(), parentItem.Id);
            bundle.Description = "PC Kit Bundle";
            bundle.AddItem(compItem.Id, 2m, "Front Cable", "Unit");
            bundle.AddItem(compItem.Id, 3m, "Rear Cable", "Unit");
            await bundleRepo.InsertAsync(bundle, autoSave: true);
            var bundleItem1 = bundle.Items[0];
            var bundleItem2 = bundle.Items[1];

            // Sales Order for 2 bundles:
            // Row 1 expects 2 * 2 = 4 cables
            // Row 2 expects 2 * 3 = 6 cables
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-BUNDLE-B33", DateTime.UtcNow);
            so.AddItem(parentItem.Id, "PC Kit B33", 2m, 500m, 0m, "Unit");
            so.Items[0].WarehouseId = warehouse.Id;
            so.Items[0].ProductBundleId = bundle.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Act: Convert SO to Pick List
            var plId = await conversionSvc.ConvertSalesOrderToPickListAsync(so.Id);
            var pickList = await plRepo.GetAsync(plId, includeDetails: true);

            // Assert:
            // Per PR #65116 (commit 3dedfaee04): repeated bundle component has a pick list row per packed row
            pickList.Items.Count.ShouldBe(2);

            var row1 = pickList.Items.FirstOrDefault(i => i.ProductBundleItemId == bundleItem1.Id);
            row1.ShouldNotBeNull();
            row1.ItemId.ShouldBe(compItem.Id);
            row1.Qty.ShouldBe(4m); // 2 * 2 = 4

            var row2 = pickList.Items.FirstOrDefault(i => i.ProductBundleItemId == bundleItem2.Id);
            row2.ShouldNotBeNull();
            row2.ItemId.ShouldBe(compItem.Id);
            row2.Qty.ShouldBe(6m); // 2 * 3 = 6
        });
    }
}
