using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 6 upstream ERPNext parity:
/// 1) Standard Cost valuation takes the item's standard rate before serial/batch bundle or SLE valuation,
///    and still returns the current standard rate after full consumption (fe05246e80, bc7701bad1, 838c54142c).
/// 2) Pick List preserves the ordered conversion factor from Sales Order / Material Request rows ONLY when
///    the Pick List row references the ordered item and UOM (d0796ff33b, 4305cb27f5, 0de4680ea9).
/// 3) Production Plan sub-assembly items fall back to their Item Default warehouse when no plan/BOM warehouse is specified (ae0cfde014).
/// 4) Subcontracting Receipt return rows are treated as rejected ONLY while flagged AND in the original rejected warehouse,
///    pricing and SCO received-qty adjustments follow the same rule (aeb5278994, 0146c3b6d3, 7a711015db).
/// </summary>
public abstract class UpstreamBatch6IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task StandardCostValuation_TakesStandardRateBeforeSerialAndBatchAndAfterFullConsumption_fe05246e80_bc7701bad1_838c54142c()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var valuationService = GetRequiredService<StockValuationService>();
            var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "StdCost Co"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Standard Warehouse"), autoSave: true);

            var stdItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "STD-RM-01", "Standard Cost RM", ItemType.Goods)
            {
                Uom = "Nos",
                ValuationMethod = ValuationMethod.StandardCost,
                StandardBuyingPrice = 100m,
                HasBatchNo = true,
                HasSerialNo = true,
            }, autoSave: true);

            var batch = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), stdItem.Id, "BATCH-STD-1")
            {
                UseBatchwiseValuation = true,
            }, autoSave: true);

            var inwardBundle = new SerialAndBatchBundle(
                Guid.NewGuid(), company.Id, stdItem.Id, wh.Id,
                BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), new DateTime(2026, 5, 1));
            inwardBundle.AddEntry(new SerialAndBatchEntry(
                Guid.NewGuid(), inwardBundle.Id, qty: 5m, incomingRate: 180m,
                serialNo: "SN-STD-001", batchId: batch.Id)
            {
                WarehouseId = wh.Id
            });
            inwardBundle.Submit();
            await bundleRepo.InsertAsync(inwardBundle, autoSave: true);

            // 1) Inward SLE at actual incoming rate 180 -> StandardCost item is valued at 100 (StandardBuyingPrice)
            var inSle = await valuationService.CreateLedgerEntryAsync(
                company.Id, stdItem.Id, wh.Id, new DateTime(2026, 5, 1),
                quantityChange: 5m, incomingRate: 180m,
                voucherType: "PurchaseReceipt", voucherId: inwardBundle.VoucherId,
                batchId: batch.Id, serialAndBatchBundleId: inwardBundle.Id);
            await uowManager.Current!.SaveChangesAsync();

            inSle.ValuationRate.ShouldBe(100m);
            inSle.BalanceQuantity.ShouldBe(5m);
            inSle.BalanceValue.ShouldBe(500m);

            // 2) GetValuationRateAsync with batchId and serialAndBatchBundleId returns standard rate (100m),
            //    NOT the bundle's inward rate (180m) (bc7701bad1, 838c54142c)
            var rateForBundle = await valuationService.GetValuationRateAsync(
                stdItem.Id, wh.Id,
                batchId: batch.Id,
                serialAndBatchBundleId: inwardBundle.Id,
                asOfDate: new DateTime(2026, 5, 2));
            rateForBundle.ShouldBe(100m);

            // 3) Outward SLE consuming all 5 units against bundle/batch uses standard rate 100m (not 180m)
            var outSle = await valuationService.CreateLedgerEntryAsync(
                company.Id, stdItem.Id, wh.Id, new DateTime(2026, 5, 2),
                quantityChange: -5m, incomingRate: 0m,
                voucherType: "DeliveryNote", voucherId: Guid.NewGuid(),
                batchId: batch.Id, serialAndBatchBundleId: inwardBundle.Id);
            await uowManager.Current!.SaveChangesAsync();

            outSle.ValuationRate.ShouldBe(100m);
            outSle.OutgoingRate.ShouldBe(100m);
            outSle.BalanceQuantity.ShouldBe(0m);
            outSle.BalanceValue.ShouldBe(0m);

            // 4) Update StandardBuyingPrice to 125m after full consumption -> GetValuationRateAsync returns 125m (fe05246e80)
            stdItem.StandardBuyingPrice = 125m;
            await itemRepo.UpdateAsync(stdItem, autoSave: true);

            var rateAfterConsumption = await valuationService.GetValuationRateAsync(
                stdItem.Id, wh.Id,
                asOfDate: new DateTime(2026, 5, 3));
            rateAfterConsumption.ShouldBe(125m);
        });
    }

    [Fact]
    public async Task PickListAppService_PreservesOrderedConversionFactorOnSalesOrderRows_d0796ff33b_0de4680ea9()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var uomConvRepo = GetRequiredService<IRepository<UomConversion, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PickList UOM Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "PickList Customer"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Pick Warehouse"), autoSave: true);

            var orderedItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PL-ITEM-1", "Ordered Item", ItemType.Goods)
            {
                Uom = "Nos",
            }, autoSave: true);

            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PL-ITEM-2", "Substitute Item", ItemType.Goods)
            {
                Uom = "Nos",
            }, autoSave: true);

            // Initially 1 Box = 10 Nos on orderedItem, and 1 Box = 6 Nos on otherItem
            var conv1 = await uomConvRepo.InsertAsync(new UomConversion(Guid.NewGuid(), "Box", "Nos", 10m, orderedItem.Id), autoSave: true);
            await uomConvRepo.InsertAsync(new UomConversion(Guid.NewGuid(), "Box", "Nos", 6m, otherItem.Id), autoSave: true);

            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-PL-UOM-001", new DateTime(2026, 6, 1));
            so.AddItem(orderedItem.Id, "Ordered Item", quantity: 2m, unitPrice: 100m, taxAmount: 0m, uom: "Box");
            var soItem = so.Items[0];
            soItem.StockUom = "Nos";
            soItem.ConversionFactor = 10m;
            soItem.WarehouseId = wh.Id;
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // Later, item master conversion factor for orderedItem's Box is changed from 10 to 25!
            conv1.ConversionFactor = 25m;
            await uomConvRepo.UpdateAsync(conv1, autoSave: true);

            // Create Pick List against SO with:
            // Row 1: orderedItem in "Box" linked to soItem.Id -> must keep ordered conversion factor 10 (StockQty = 20)
            // Row 2: otherItem in "Box" linked to soItem.Id -> different item, so must use otherItem's own factor 6 (StockQty = 12) (0de4680ea9)
            var plDto = await pickListAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = company.Id,
                Purpose = "Delivery",
                SalesOrderId = so.Id,
                CustomerId = customer.Id,
                PickManually = true,
                Items =
                [
                    new CreatePickListItemDto
                    {
                        ItemId = orderedItem.Id,
                        WarehouseId = wh.Id,
                        Qty = 2m,
                        Uom = "Box",
                        SourceDocumentItemId = soItem.Id,
                    },
                    new CreatePickListItemDto
                    {
                        ItemId = otherItem.Id,
                        WarehouseId = wh.Id,
                        Qty = 2m,
                        Uom = "Box",
                        SourceDocumentItemId = soItem.Id,
                    }
                ]
            });

            var row1 = plDto.Items.First(i => i.ItemId == orderedItem.Id);
            row1.Uom.ShouldBe("Box");
            row1.ConversionFactor.ShouldBe(10m);
            row1.StockQty.ShouldBe(20m);

            var row2 = plDto.Items.First(i => i.ItemId == otherItem.Id);
            row2.Uom.ShouldBe("Box");
            row2.ConversionFactor.ShouldBe(6m);
            row2.StockQty.ShouldBe(12m);
        });
    }

    [Fact]
    public async Task ProductionPlanAppService_SubAssemblyGetsItemDefaultWarehouseFallback_ae0cfde014()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var ppAppService = GetRequiredService<IProductionPlanAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PP SubWh Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PP-Series", "PP", "PP-"), autoSave: true);

            var fgWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Warehouse"), autoSave: true);
            var subDefaultWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Sub-Assembly Default WH"), autoSave: true);
            var rmDefaultWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Default WH"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "FG-SUB-WH", "FG Item", ItemType.Goods), autoSave: true);
            var subItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SUB-SUB-WH", "Sub-Assembly Item", ItemType.Goods)
            {
                DefaultWarehouseId = subDefaultWh.Id
            }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "RM-SUB-WH", "Raw Material Item", ItemType.Goods)
            {
                DefaultWarehouseId = rmDefaultWh.Id
            }, autoSave: true);

            var subBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SUB-WH-1", subItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            subBom.Items.Add(new BomItem(Guid.NewGuid(), subBom.Id, rmItem.Id, "Raw Material Item", 2m, 10m));
            await bomRepo.InsertAsync(subBom, autoSave: true);

            var fgBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-FG-WH-1", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            fgBom.Items.Add(new BomItem(Guid.NewGuid(), fgBom.Id, subItem.Id, "Sub-Assembly Item", 1m, 20m)
            {
                SubBomId = subBom.Id
            });
            await bomRepo.InsertAsync(fgBom, autoSave: true);

            // Create Production Plan with NO ForWarehouseId and NO SubAssemblyWarehouseId
            var planDto = await ppAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 6, 10),
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = fgBom.Id,
                        PlannedQty = 4m,
                        WarehouseId = fgWh.Id,
                    }
                }
            });

            var calcDto = await ppAppService.CalculateMaterialRequirementsAsync(planDto.Id);
            var subRow = calcDto.MaterialRequirements.FirstOrDefault(m => m.ItemId == subItem.Id);
            subRow.ShouldNotBeNull();
            subRow.WarehouseId.ShouldBe(subDefaultWh.Id);
        });
    }

    [Fact]
    public async Task SubcontractingReceiptReturn_TreatsRowAsRejectedOnlyWhileFlaggedAndInRejectedWarehouse_aeb5278994_0146c3b6d3_7a711015db()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Subcontract Return Co"), autoSave: true);
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCO-Series", "SCO", "SCO-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-Series", "SCR", "SCR-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET-Series", "SCR-RET", "SCR-RET-"), autoSave: true);

            var stockAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1400", "Stock In Hand", AccountType.Asset), autoSave: true);
            var srbnbAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2110", "SRBNB", AccountType.Liability), autoSave: true);

            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "SCR Stock Dr", "SubcontractingReceipt",
                isDebit: true, AccountSource.FixedAccount, AmountSource.NetTotal)
            {
                FixedAccountId = stockAcc.Id,
                SortOrder = 1
            }, autoSave: true);
            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "SCR SRBNB Cr", "SubcontractingReceipt",
                isDebit: false, AccountSource.FixedAccount, AmountSource.NetTotal)
            {
                FixedAccountId = srbnbAcc.Id,
                SortOrder = 2
            }, autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Subcontractor Ltd"), autoSave: true);
            var acceptedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Accepted Warehouse"), autoSave: true);
            var rejectedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Rejected Warehouse"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SC-FG-01", "Subcontracted FG", ItemType.Goods)
            {
                AllowNegativeStock = true,
            }, autoSave: true);

            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SC-RM-01", "Subcontracted RM", ItemType.Goods)
            {
                AllowNegativeStock = true,
            }, autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SC-FG-1", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "Subcontracted RM", 1m, 10m));
            await bomRepo.InsertAsync(bom, autoSave: true);

            var scoDto = await scAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = new DateTime(2026, 6, 1),
                Items = new List<CreateScoItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 10m,
                        Rate = 50m,
                        ServiceCostPerQty = 50m,
                        BomId = bom.Id,
                        WarehouseId = acceptedWh.Id,
                    }
                }
            });
            await scAppService.SubmitOrderAsync(scoDto.Id);

            // Receive 8 accepted into acceptedWh and 2 rejected into rejectedWh
            var scrDto = await scAppService.CreateReceiptAsync(new CreateSubcontractingReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                SubcontractingOrderId = scoDto.Id,
                PostingDate = new DateTime(2026, 6, 2),
                WarehouseId = acceptedWh.Id,
                Items = new List<CreateScrItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 8m,
                        RejectedQty = 2m,
                        Rate = 50m,
                        WarehouseId = acceptedWh.Id,
                        RejectedWarehouseId = rejectedWh.Id,
                    }
                }
            });
            await scAppService.SubmitReceiptAsync(scrDto.Id);

            // 1) Return row flagged ReturnQtyFromRejectedWarehouse = true AND in rejectedWh -> treated as rejected return (Rate = 0, does not reduce SCO ReceivedQty)
            var rejReturnDto = await scAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = scrDto.Id,
                PostingDate = new DateTime(2026, 6, 3),
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 1m,
                        WarehouseId = rejectedWh.Id,
                        ReturnQtyFromRejectedWarehouse = true,
                    }
                }
            });
            rejReturnDto.Items[0].Rate.ShouldBe(0m);
            rejReturnDto.Items[0].WarehouseId.ShouldBe(rejectedWh.Id);
            await scAppService.SubmitReceiptAsync(rejReturnDto.Id);

            var scoAfterRejReturn = await scoRepo.GetAsync(scoDto.Id, includeDetails: true);
            scoAfterRejReturn.Items[0].ReceivedQty.ShouldBe(8m);

            // 2) Return row flagged ReturnQtyFromRejectedWarehouse = true BUT warehouse changed to acceptedWh -> treated as ACCEPTED return (Rate = 50, reduces SCO ReceivedQty)
            var accReturnDto1 = await scAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = scrDto.Id,
                PostingDate = new DateTime(2026, 6, 4),
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 2m,
                        WarehouseId = acceptedWh.Id,
                        ReturnQtyFromRejectedWarehouse = true,
                    }
                }
            });
            accReturnDto1.Items[0].Rate.ShouldBe(50m);
            await scAppService.SubmitReceiptAsync(accReturnDto1.Id);

            var scoAfterAccReturn1 = await scoRepo.GetAsync(scoDto.Id, includeDetails: true);
            scoAfterAccReturn1.Items[0].ReceivedQty.ShouldBe(6m);

            // 3) Return row with ReturnQtyFromRejectedWarehouse = false AND warehouse = rejectedWh -> treated as ACCEPTED return (Rate = 50, reduces SCO ReceivedQty)
            var accReturnDto2 = await scAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = scrDto.Id,
                PostingDate = new DateTime(2026, 6, 5),
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 1m,
                        WarehouseId = rejectedWh.Id,
                        ReturnQtyFromRejectedWarehouse = false,
                    }
                }
            });
            accReturnDto2.Items[0].Rate.ShouldBe(50m);
            await scAppService.SubmitReceiptAsync(accReturnDto2.Id);

            var scoAfterAccReturn2 = await scoRepo.GetAsync(scoDto.Id, includeDetails: true);
            scoAfterAccReturn2.Items[0].ReceivedQty.ShouldBe(5m);
        });
    }
}
