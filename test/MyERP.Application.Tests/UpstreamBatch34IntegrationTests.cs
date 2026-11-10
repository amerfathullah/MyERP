using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch34IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // === 1) PR #64951: Subcontracting Receipt Return Raw Material Valuation ===

    [Fact]
    public async Task SubcontractingReceipt_Return_ReturnsRawMaterialsAtConsumedRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var scoRepo = GetRequiredService<IRepository<SubcontractingOrder, Guid>>();
            var scrRepo = GetRequiredService<IRepository<SubcontractingReceipt, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();
            var stockValuation = GetRequiredService<StockValuationService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Subcontract RM Val Co B34") { CurrencyCode = "MYR" },
                autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SCR Series", "SubcontractingReceipt", "SCR-"),
                autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET Series", "SCR-RET", "SCR-RET-"),
                autoSave: true);

            var stockAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1400", "Stock In Hand", AccountType.Asset),
                autoSave: true);
            var srbnbAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2110", "SRBNB", AccountType.Liability),
                autoSave: true);

            company.DefaultInventoryAccountId = stockAcc.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

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

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Supplier RM Val B34"),
                autoSave: true);

            var fgWarehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "FG Warehouse B34"),
                autoSave: true);

            var supplierWarehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Supplier Warehouse B34"),
                autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-ITEM-B34", "Finished Good B34", ItemType.Goods)
                {
                    MaintainStock = true,
                    DefaultWarehouseId = fgWarehouse.Id
                },
                autoSave: true);

            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-ITEM-B34", "Raw Material B34", ItemType.Goods)
                {
                    MaintainStock = true,
                    DefaultWarehouseId = supplierWarehouse.Id
                },
                autoSave: true);

            // Seed raw material in supplier warehouse at rate of 75 MYR
            await stockValuation.CreateLedgerEntryAsync(
                company.Id, rmItem.Id, supplierWarehouse.Id, DateTime.UtcNow,
                quantityChange: 100m, incomingRate: 75m,
                voucherType: "StockEntry", voucherId: Guid.NewGuid());

            // Create Subcontracting Order: 10 FG requiring 10 RM
            var sco = new SubcontractingOrder(Guid.NewGuid(), company.Id, "SCO-RM-B34", DateTime.UtcNow, supplier.Id)
            {
                SupplierWarehouseId = supplierWarehouse.Id
            };
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, fgItem.Id, "Finished Good B34", 10m, 120m));
            sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, rmItem.Id, "Raw Material B34", 10m)
            {
                ReserveWarehouseId = supplierWarehouse.Id
            });
            sco.Submit();
            await scoRepo.InsertAsync(sco, autoSave: true);

            // Create and submit original Subcontracting Receipt for 10 FG
            var originalReceipt = new SubcontractingReceipt(
                Guid.NewGuid(), company.Id, "SCR-ORIG-VAL-B34", DateTime.UtcNow, supplier.Id, sco.Id)
            {
                WarehouseId = fgWarehouse.Id,
                SupplierWarehouseId = supplierWarehouse.Id
            };
            originalReceipt.AddItem(new SubcontractingReceiptItem(
                Guid.NewGuid(), originalReceipt.Id, fgItem.Id, "Finished Good B34", 10m, 120m)
            {
                WarehouseId = fgWarehouse.Id
            });
            await scrRepo.InsertAsync(originalReceipt, autoSave: true);

            await subcontractingAppService.SubmitReceiptAsync(originalReceipt.Id);

            // Verify original RM consumption SLE was created at 75 MYR outward rate
            var origRmSle = (await sleRepo.GetListAsync(s =>
                s.VoucherType == "SubcontractingReceipt" &&
                s.VoucherId == originalReceipt.Id &&
                s.ItemId == rmItem.Id)).FirstOrDefault();

            origRmSle.ShouldNotBeNull();
            origRmSle.QuantityChange.ShouldBe(-10m);
            origRmSle.OutgoingRate.ShouldBe(75m);

            // Act: Create Return for 4 FG
            var returnReceiptDto = await subcontractingAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
            {
                ReturnAgainstReceiptId = originalReceipt.Id,
                PostingDate = DateTime.UtcNow,
                WarehouseId = fgWarehouse.Id,
                SupplierWarehouseId = supplierWarehouse.Id,
                Items = new List<CreateScrReturnItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = "Finished Good B34",
                        Qty = 4m,
                        Rate = 120m
                    }
                }
            });

            // Submit Return
            await subcontractingAppService.SubmitReceiptAsync(returnReceiptDto.Id);

            // Assert: Return RM SLE has QuantityChange = +4 and IncomingRate = 75 MYR (per PR #64951 / commit 5d5edb8ebc)
            var returnRmSle = (await sleRepo.GetListAsync(s =>
                s.VoucherType == "SubcontractingReceipt" &&
                s.VoucherId == returnReceiptDto.Id &&
                s.ItemId == rmItem.Id)).FirstOrDefault();

            returnRmSle.ShouldNotBeNull();
            returnRmSle.QuantityChange.ShouldBe(4m); // 4 units returned to supplier warehouse
            returnRmSle.IncomingRate.ShouldBe(75m);  // Valued at consumed rate, not 0!
        });
    }

    // === 2) PR #65124: Production Plan Download Raw Materials & Alignment ===

    [Fact]
    public async Task ProductionPlan_DownloadRawMaterials_IncludesPlannedQtyAndOtherWarehouseRows()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var binRepo = GetRequiredService<IRepository<Bin, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var planAppService = GetRequiredService<IProductionPlanAppService>();
            var planRepo = GetRequiredService<IRepository<ProductionPlan, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "PP Download Co B34") { CurrencyCode = "MYR" },
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PP Series", "PP", "PP-"),
                autoSave: true);

            var storesWh = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Stores - TC"),
                autoSave: true);

            var fgWh = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Finished Goods - TC"),
                autoSave: true);

            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-PP-B34", "RM Item B34", ItemType.Goods)
                {
                    MaintainStock = true,
                    Uom = "Nos"
                },
                autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-PP-B34", "FG Item B34", ItemType.Goods)
                {
                    MaintainStock = true,
                    Uom = "Nos"
                },
                autoSave: true);

            // BOM: 1 FG requires 1 RM
            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-PP-B34", fgItem.Id)
            {
                Quantity = 1m,
                Uom = "Nos",
                IsActive = true,
                IsDefault = true,
                SourceWarehouseId = storesWh.Id
            };
            bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "RM Item B34", 1m, 10m, "Nos")
            {
                SourceWarehouseId = storesWh.Id
            });
            await bomRepo.InsertAsync(bom, autoSave: true);

            // Seed Bins:
            // 1. Stores (main warehouse): Actual = 0, Planned = 4
            var binStores = new Bin(Guid.NewGuid(), rmItem.Id, storesWh.Id);
            binStores.PlannedQty = 4m;
            await binRepo.InsertAsync(binStores, autoSave: true);

            // 2. Finished Goods (other warehouse): Actual = 7, Planned = 4
            var binFg = new Bin(Guid.NewGuid(), rmItem.Id, fgWh.Id);
            binFg.ActualQty = 7m;
            binFg.PlannedQty = 4m;
            await binRepo.InsertAsync(binFg, autoSave: true);

            // Create Production Plan for 50 FG
            var planDto = await planAppService.CreateAsync(new CreateProductionPlanDto
            {
                CompanyId = company.Id,
                PostingDate = DateTime.UtcNow,
                Items = new List<CreateProductionPlanItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = "FG Item B34",
                        BomId = bom.Id,
                        PlannedQty = 50m
                    }
                }
            });

            await planAppService.CalculateMaterialRequirementsAsync(planDto.Id);

            // Act: Download raw materials
            var downloadRows = await planAppService.DownloadRawMaterialsAsync(planDto.Id);

            // Assert: per ERPNext PR #65124 (test_download_raw_materials_puts_other_warehouse_qty_under_its_columns)
            downloadRows.Count.ShouldBeGreaterThanOrEqualTo(2);

            var itemRow = downloadRows.FirstOrDefault(r => r.Warehouse == "Stores - TC");
            itemRow.ShouldNotBeNull();
            itemRow.PlannedQty.ShouldBe(4m);

            var otherWhRow = downloadRows.FirstOrDefault(r => r.Warehouse == "Finished Goods - TC");
            otherWhRow.ShouldNotBeNull();
            otherWhRow.ActualQty.ShouldBe(7m);
            otherWhRow.PlannedQty.ShouldBe(4m);
        });
    }

    // === 3) PR #65125: Purchase Receipt Rejected Qty for Non-Stock / Fixed Asset Items ===

    [Fact]
    public async Task PurchaseReceipt_FixedAssetOrNonStockItem_DoesNotValueRejectedMaterials()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settingManager = GetRequiredService<ISettingManager>();
            await settingManager.SetForCurrentTenantAsync(
                Settings.MyERPSettings.Buying.SetValuationRateForRejectedMaterials, "true");

            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "PR Asset Rejected Co B34") { CurrencyCode = "MYR" },
                autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series", "PurchaseReceipt", "PR-"),
                autoSave: true);

            var stockAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1401", "Inventory Asset", AccountType.Asset),
                autoSave: true);
            var srbnbAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2150", "SRBNB Asset", AccountType.Liability),
                autoSave: true);

            company.DefaultInventoryAccountId = stockAcc.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt",
                true, AccountSource.WarehouseStock, AmountSource.NetTotal)
            {
                SortOrder = 1,
                FixedAccountId = stockAcc.Id
            }, autoSave: true);

            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt",
                false, AccountSource.FixedAccount, AmountSource.NetTotal)
            {
                SortOrder = 2,
                FixedAccountId = srbnbAcc.Id
            }, autoSave: true);

            var supplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), company.Id, "Supplier PR Asset B34"),
                autoSave: true);

            var warehouse = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Warehouse PR Asset B34"),
                autoSave: true);

            var rejectedWh = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Rejected Warehouse PR Asset B34"),
                autoSave: true);

            // Fixed Asset item (non-stock item)
            var assetItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ASSET-ITEM-B34", "Server Asset B34", ItemType.FixedAsset)
                {
                    MaintainStock = false,
                    DefaultWarehouseId = warehouse.Id
                },
                autoSave: true);

            var prDto = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                PostingDate = DateTime.UtcNow,
                WarehouseId = warehouse.Id,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = assetItem.Id,
                        Description = "Server Asset B34",
                        Quantity = 2m,
                        RejectedQty = 1m,
                        UnitPrice = 100m,
                        WarehouseId = warehouse.Id,
                        RejectedWarehouseId = rejectedWh.Id
                    }
                }
            });

            await prAppService.SubmitAsync(prDto.Id);

            var receipt = await prRepo.GetAsync(prDto.Id, includeDetails: true);

            // Non-stock fixed asset puts NO rejected qty in stock:
            // PurchaseExpenseTotal values only accepted qty (2 * 100 = 200), NOT 300!
            receipt.PurchaseExpenseTotal.ShouldBe(200m);

            // No SLEs exist for rejected warehouse
            var rejectedSles = await sleRepo.GetListAsync(s =>
                s.VoucherType == "PurchaseReceipt" &&
                s.VoucherId == receipt.Id &&
                s.WarehouseId == rejectedWh.Id);

            rejectedSles.ShouldBeEmpty();
        });
    }
}
