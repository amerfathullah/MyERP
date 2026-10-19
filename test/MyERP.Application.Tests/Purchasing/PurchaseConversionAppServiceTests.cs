using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Xunit;

namespace MyERP.Purchasing;

public abstract class PurchaseConversionAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ConvertMaterialRequestToPurchaseOrder_Copies_ConversionFactor_And_Deducts_Draft_POs()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Purchase Conversion Test Co"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier ABC"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-CONV-1", "Conv Item", ItemType.Goods)
            {
                StandardBuyingPrice = 45m
            }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "POS", "PurchaseOrder", "PO-"), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-CONV-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item.Id, "Conv Item", quantity: 10m, uom: "Box", conversionFactor: 5m);
            mr.Submit();
            await mrRepo.InsertAsync(mr, autoSave: true);

            // Act 1: Convert MR to PO
            var poDto = await conversionService.ConvertMaterialRequestToPurchaseOrderAsync(mr.Id, supplier.Id);

            poDto.ShouldNotBeNull();
            poDto.Items.Count.ShouldBe(1);
            poDto.Items[0].ItemId.ShouldBe(item.Id);
            poDto.Items[0].Quantity.ShouldBe(10m);
            poDto.Items[0].ConversionFactor.ShouldBe(5m);
            poDto.Items[0].MaterialRequestItemId.ShouldBe(mr.Items.Single().Id);
            poDto.Status.ShouldBe("Draft");

            // Act 2: Second conversion attempt should fail because the first draft PO already covers 10m
            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
                await conversionService.ConvertMaterialRequestToPurchaseOrderAsync(mr.Id, supplier.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentAlreadyConverted);
        });
    }

    [Fact]
    public async Task ConvertMaterialRequestToRfq_Filters_Fully_Ordered_Items_And_Maps_Pending_Qty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "RFQ Conversion Test Co"), autoSave: true);
            var item1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-RFQ-1", "Fully Ordered Item", ItemType.Goods), autoSave: true);
            var item2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-RFQ-2", "Pending Item", ItemType.Goods), autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "RFQ", "RFQ", "RFQ-"), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-RFQ-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item1.Id, "Fully Ordered Item", quantity: 10m, uom: "Unit");
            mr.AddItem(item2.Id, "Pending Item", quantity: 10m, uom: "Unit");
            mr.Submit();

            // Simulate item 1 fully ordered, item 2 partially ordered (ERPNext PR #58534 / commit c93815b4ae)
            mr.Items[0].OrderedQuantity = 10m;
            mr.Items[1].OrderedQuantity = 4m;

            await mrRepo.InsertAsync(mr, autoSave: true);

            // Act 1: Convert MR to RFQ
            var rfqDto = await conversionService.ConvertMaterialRequestToRfqAsync(mr.Id);

            rfqDto.ShouldNotBeNull();
            // Fully ordered item 1 must be excluded
            rfqDto.Items.Count.ShouldBe(1);
            rfqDto.Items[0].ItemId.ShouldBe(item2.Id);
            rfqDto.Items[0].Qty.ShouldBe(6m); // 10 - 4 = 6
            rfqDto.Items[0].MaterialRequestItemId.ShouldBe(mr.Items[1].Id);

            // Act 2: Second conversion attempt must fail because the draft RFQ covers the remaining 6 units
            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
                await conversionService.ConvertMaterialRequestToRfqAsync(mr.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentAlreadyConverted);
        });
    }

    [Fact]
    public async Task ConvertPurchaseOrderToReceipt_WithUseTransactionDateExchangeRate_UsesTransactionDateRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var ceRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var settingManager = GetRequiredService<ISettingManager>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            await settingManager.SetGlobalAsync(MyERPSettings.Buying.UseTransactionDateExchangeRate, "true");

            try
            {
                var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Exchange Test Co") { CurrencyCode = "MYR" }, autoSave: true);
                var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier FX"), autoSave: true);
                var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FX Main WH", company.TenantId), autoSave: true);
                var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-FX-1", "FX Item", ItemType.Goods), autoSave: true);

                await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PRS", "PurchaseReceipt", "PR-"), autoSave: true);
                await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PIS", "PurchaseInvoice", "PI-"), autoSave: true);

                await ceRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.85m, DateTime.UtcNow.Date), autoSave: true);

                var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-FX-001", DateTime.UtcNow.Date.AddDays(-5), company.TenantId)
                {
                    CurrencyCode = "USD",
                    ExchangeRate = 4.20m
                };
                po.AddItem(item.Id, "FX Item", 10m, 100m, 0m, "Unit", warehouse.Id);
                po.Submit();
                await poRepo.InsertAsync(po, autoSave: true);

                var prDto = await conversionService.ConvertPurchaseOrderToReceiptAsync(po.Id);
                prDto.ShouldNotBeNull();
                prDto.CurrencyCode.ShouldBe("USD");
                prDto.ExchangeRate.ShouldBe(4.85m);
                prDto.UseTransactionDateExchangeRate.ShouldBeTrue();

                var piDto = await conversionService.ConvertPurchaseOrderToInvoiceAsync(po.Id);
                piDto.ShouldNotBeNull();
                piDto.CurrencyCode.ShouldBe("USD");
                piDto.ExchangeRate.ShouldBe(4.85m);
                piDto.UseTransactionDateExchangeRate.ShouldBeTrue();
            }
            finally
            {
                await settingManager.SetGlobalAsync(MyERPSettings.Buying.UseTransactionDateExchangeRate, "false");
            }
        });
    }

    [Fact]
    public async Task ConvertPurchaseOrderToReceipt_WithoutUseTransactionDateExchangeRate_PreservesOrderRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var ceRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var settingManager = GetRequiredService<ISettingManager>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            await settingManager.SetGlobalAsync(MyERPSettings.Buying.UseTransactionDateExchangeRate, "false");

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR NoFx Test Co") { CurrencyCode = "MYR" }, autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier NoFx"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "NoFx Main WH", company.TenantId), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-NOFX-1", "NoFx Item", ItemType.Goods), autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PRS", "PurchaseReceipt", "PR-"), autoSave: true);
            await ceRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.85m, DateTime.UtcNow.Date), autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-NOFX-001", DateTime.UtcNow.Date.AddDays(-5), company.TenantId)
            {
                CurrencyCode = "USD",
                ExchangeRate = 4.20m
            };
            po.AddItem(item.Id, "NoFx Item", 10m, 100m, 0m, "Unit", warehouse.Id);
            po.Submit();
            await poRepo.InsertAsync(po, autoSave: true);

            var prDto = await conversionService.ConvertPurchaseOrderToReceiptAsync(po.Id);

            prDto.ShouldNotBeNull();
            prDto.CurrencyCode.ShouldBe("USD");
            prDto.ExchangeRate.ShouldBe(4.20m);
            prDto.UseTransactionDateExchangeRate.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task ConvertPurchaseReceiptToInvoice_And_ConvertPurchaseInvoiceToReceipt_PreserveSourceRates()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var ceRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var settingManager = GetRequiredService<ISettingManager>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            await settingManager.SetGlobalAsync(MyERPSettings.Buying.UseTransactionDateExchangeRate, "true");

            try
            {
                var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR to PI Preserved Co") { CurrencyCode = "MYR" }, autoSave: true);
                var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier Preserved"), autoSave: true);
                var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Preserved WH", company.TenantId), autoSave: true);
                var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-PRES-1", "Preserved Item", ItemType.Goods), autoSave: true);

                await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PIS", "PurchaseInvoice", "PI-"), autoSave: true);
                await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PRS", "PurchaseReceipt", "PR-"), autoSave: true);

                await ceRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 4.90m, DateTime.UtcNow.Date), autoSave: true);

                // Case 1: PR -> PI keeps PR rate
                var pr = new PurchaseReceipt(Guid.NewGuid(), company.Id, supplier.Id, warehouse.Id, "PR-PRES-001", DateTime.UtcNow.Date, company.TenantId)
                {
                    CurrencyCode = "USD",
                    ExchangeRate = 4.30m
                };
                pr.AddItem(item.Id, "Preserved Item", 10m, 50m, 0m, "Unit");
                pr.Submit();
                await prRepo.InsertAsync(pr, autoSave: true);

                var piDto = await conversionService.ConvertPurchaseReceiptToInvoiceAsync(pr.Id);
                piDto.ShouldNotBeNull();
                piDto.CurrencyCode.ShouldBe("USD");
                piDto.ExchangeRate.ShouldBe(4.30m);
                piDto.UseTransactionDateExchangeRate.ShouldBeFalse();

                // Case 2: PI -> PR keeps PI rate
                var pi = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-PRES-001", DateTime.UtcNow.Date, company.TenantId)
                {
                    CurrencyCode = "USD",
                    ExchangeRate = 4.40m,
                    WarehouseId = warehouse.Id
                };
                pi.AddItem(item.Id, "Preserved Item", 5m, 50m, 0m, "Unit", warehouse.Id);
                pi.Submit();
                await piRepo.InsertAsync(pi, autoSave: true);

                var prFromPiDto = await conversionService.ConvertPurchaseInvoiceToReceiptAsync(pi.Id);
                prFromPiDto.ShouldNotBeNull();
                prFromPiDto.CurrencyCode.ShouldBe("USD");
                prFromPiDto.ExchangeRate.ShouldBe(4.40m);
                prFromPiDto.UseTransactionDateExchangeRate.ShouldBeFalse();
            }
            finally
            {
                await settingManager.SetGlobalAsync(MyERPSettings.Buying.UseTransactionDateExchangeRate, "false");
            }
        });
    }

    [Fact]
    public async Task ConvertPurchaseOrderToInvoice_Leaves_Draft_Returns_Out_Of_Draft_Billed_Qty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Order Neutral Corp") { CurrencyCode = "MYR" }, autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Neutral Supplier", company.TenantId), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-NEUTRAL", "Neutral Item", ItemType.Goods)
            {
                MaintainStock = true,
                Uom = "Unit"
            }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PIS", "PurchaseInvoice", "PI-"), autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-NEUT-001", DateTime.UtcNow.Date, company.TenantId);
            po.AddItem(item.Id, "Neutral Item", 10m, 100m, 0m, "Unit");
            po.Submit();
            await poRepo.InsertAsync(po, autoSave: true);

            // Draft debit note against the PO (is_return = 1)
            var draftDebitNote = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-RET-001", DateTime.UtcNow.Date, company.TenantId)
            {
                IsReturn = true
            };
            draftDebitNote.AddItem(item.Id, "Neutral Item", -4m, 100m, 0m, "Unit");
            draftDebitNote.Items.First().PurchaseOrderItemId = po.Items.First().Id;
            await piRepo.InsertAsync(draftDebitNote, autoSave: true);

            // Convert PO to PI: should bill full 10 units (draft debit note must not deduct from pending qty)
            var piDto = await conversionService.ConvertPurchaseOrderToInvoiceAsync(po.Id);
            piDto.ShouldNotBeNull();
            piDto.Items.Count.ShouldBe(1);
            piDto.Items[0].Quantity.ShouldBe(10m);
        });
    }

    [Fact]
    public async Task ConvertPurchaseOrderToInvoice_OrderNeutralDebitNote_DoesNotReopenBilledQty()
    {
        // Per ERPNext PR #61336 (commits ca38172e49, 4192f7a567):
        // Order-neutral debit note is left out of billed qty calculation, so it is not offered again when making PI
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Order Neutral Corp 2") { CurrencyCode = "MYR" }, autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Neutral Supplier 2", company.TenantId), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-NEUTRAL-2", "Neutral Item 2", ItemType.Goods)
            {
                MaintainStock = true,
                Uom = "Unit"
            }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PIS2", "PurchaseInvoice", "PI-"), autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-NEUT-002", DateTime.UtcNow.Date, company.TenantId);
            po.AddItem(item.Id, "Neutral Item 2", 10m, 100m, 0m, "Unit");
            po.Submit();
            // PO item billed 6m originally.
            // When an order-neutral debit note (-2m) was posted without touching the order,
            // poItem.BilledQty remains 6m, and PendingBillingQty is 4m.
            po.Items.First().BilledQty = 6m;
            await poRepo.InsertAsync(po, autoSave: true);

            // Converting PO to PI offers exactly 4m (10 - 6), NOT 6m
            var piDto = await conversionService.ConvertPurchaseOrderToInvoiceAsync(po.Id);
            piDto.ShouldNotBeNull();
            piDto.Items.Count.ShouldBe(1);
            piDto.Items[0].Quantity.ShouldBe(4m);
        });
    }
}
