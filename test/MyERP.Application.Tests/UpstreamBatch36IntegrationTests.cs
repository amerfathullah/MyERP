using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
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

public abstract class UpstreamBatch36IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // === 1) PR #65115 / commit 51954d883a: Next Pick List picks only what is left on each bundle component ===

    [Fact]
    public async Task ConvertSalesOrderToPickList_NextPickListPicksWhatIsLeftOnEachBundleComponent()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var plRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var docConversionService = GetRequiredService<IDocumentConversionAppService>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Pick Uneven Co B36") { CurrencyCode = "MYR" },
                autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SO Series", "SalesOrder", "SO-B36-"),
                autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PL Series", "PickList", "PL-B36-"),
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Customer B36"),
                autoSave: true);

            var warehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores B36"),
                autoSave: true);

            var compA = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-A-B36", "Component A", ItemType.Goods)
                {
                    MaintainStock = true,
                    StandardBuyingPrice = 10m
                },
                autoSave: true);

            var compB = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "COMP-B-B36", "Component B", ItemType.Goods)
                {
                    MaintainStock = true,
                    StandardBuyingPrice = 20m
                },
                autoSave: true);

            var bundleItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "BUNDLE-AB-B36", "Bundle AB", ItemType.Goods)
                {
                    MaintainStock = false
                },
                autoSave: true);

            // 1 Bundle = 1 of A and 2 of B
            var bundle = new ProductBundle(Guid.NewGuid(), bundleItem.Id);
            bundle.AddItem(compA.Id, 1m, "Component A", "Nos");
            bundle.AddItem(compB.Id, 2m, "Component B", "Nos");
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Sales Order for 3 bundles (needs 3 A and 6 B)
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-B36-001", DateTime.UtcNow.Date);
            so.AddItem(bundleItem.Id, "Bundle AB", 3m, 100m, 0m, "Nos");
            so.Items[0].WarehouseId = warehouse.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Create First Pick List
            var pl1Id = await docConversionService.ConvertSalesOrderToPickListAsync(so.Id);
            var pl1 = await plRepo.GetAsync(pl1Id);
            pl1.Items.Count.ShouldBe(2);

            // Suppose component B had stock shortage: user adjusts picked qty of B to 3 (instead of 6)
            var bRow = pl1.Items.First(i => i.ItemId == compB.Id);
            bRow.Qty = 3m;
            bRow.StockQty = 3m;
            await plRepo.UpdateAsync(pl1, autoSave: true);

            // Submit Pick List 1 (3 A and 3 B picked)
            await pickListAppService.SubmitAsync(pl1Id);

            // Now create Second Pick List for the same Sales Order
            var pl2Id = await docConversionService.ConvertSalesOrderToPickListAsync(so.Id);
            var pl2 = await plRepo.GetAsync(pl2Id);

            // Component A is fully picked (3 out of 3) -> skipped!
            // Component B had 3 remaining (6 needed - 3 picked) -> included with Qty = 3!
            pl2.Items.Count.ShouldBe(1);
            pl2.Items[0].ItemId.ShouldBe(compB.Id);
            pl2.Items[0].Qty.ShouldBe(3m);
        });
    }

    // === 2) PR #65109 / commit d7feab165b: Return Raw Materials to Receipt's Supplier Warehouse ===

    [Fact]
    public async Task SubcontractingReceipt_Return_ValidatesSupplierWarehouseMatch()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var subAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Subcontract WH Guard Co B36") { CurrencyCode = "MYR" },
                autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SCR Series", "SubcontractingReceipt", "SCR-B36-"),
                autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET Series", "SCR-RET", "SCR-RET-B36-"),
                autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Supplier B36"),
                autoSave: true);

            var plantWarehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Plant WH B36"),
                autoSave: true);

            var supplierWhOriginal = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Job Worker WH 1"),
                autoSave: true);

            var supplierWhDifferent = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Job Worker WH 2"),
                autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-B36", "Finished Good B36", ItemType.Goods) { MaintainStock = true },
                autoSave: true);

            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-B36", "Raw Material B36", ItemType.Goods) { MaintainStock = true },
                autoSave: true);

            var sco = new SubcontractingOrder(
                Guid.NewGuid(), company.Id, "SCO-B36-001", DateTime.UtcNow.Date, supplier.Id)
            {
                SupplierWarehouseId = supplierWhOriginal.Id
            };
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, "Finished Good B36", 10m, 100m));
            sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItem.Id, "Raw Material B36", 10m)
            {
                ReserveWarehouseId = supplierWhOriginal.Id
            });
            sco.Submit();
            await scoRepo.InsertAsync(sco, autoSave: true);

            // Create & Submit original Subcontracting Receipt
            var receipt = new SubcontractingReceipt(
                Guid.NewGuid(), company.Id, "SCR-B36-001", DateTime.UtcNow.Date, supplier.Id, sco.Id)
            {
                WarehouseId = plantWarehouse.Id,
                SupplierWarehouseId = supplierWhOriginal.Id
            };
            receipt.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), receipt.Id, fgItem.Id, "FG B36", 10m, 100m));
            receipt.Submit();
            await scrRepo.InsertAsync(receipt, autoSave: true);

            // Attempting to create a return receipt with a DIFFERENT supplier warehouse must fail
            var invalidReturnDto = new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = receipt.Id,
                PostingDate = DateTime.UtcNow.Date,
                SupplierWarehouseId = supplierWhDifferent.Id,
                Items = new List<CreateScrReturnItemDto>
                {
                    new() { ItemId = fgItem.Id, ItemName = "FG B36", Qty = 2m, Rate = 100m }
                }
            };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                subAppService.CreateReceiptReturnAsync(invalidReturnDto));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Creating return receipt with the original supplier warehouse succeeds
            var validReturnDto = new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = receipt.Id,
                PostingDate = DateTime.UtcNow.Date,
                SupplierWarehouseId = supplierWhOriginal.Id,
                Items = new List<CreateScrReturnItemDto>
                {
                    new() { ItemId = fgItem.Id, ItemName = "FG B36", Qty = 2m, Rate = 100m }
                }
            };

            var returnResult = await subAppService.CreateReceiptReturnAsync(validReturnDto);
            returnResult.ShouldNotBeNull();
            returnResult.IsReturn.ShouldBeTrue();
            returnResult.SupplierWarehouseId.ShouldBe(supplierWhOriginal.Id);
        });
    }
}
