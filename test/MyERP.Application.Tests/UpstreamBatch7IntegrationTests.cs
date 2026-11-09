using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 7 upstream ERPNext parity:
/// 1) Purchase Receipt &amp; Subcontracting Receipt returns cap the rejected return column at 0 when the source row had 0 rejected qty (92e535f68a, ed481acfe9).
/// 2) Creating an Item with StandardSellingPrice in a company whose default selling PriceList uses another currency
///    converts and rounds the auto-created ItemPrice to the PriceList currency's precision (1853ef83ea, e8a8b0b260, 629e32b70a, 0957add0db).
/// 3) MRP Report filters MPS demand by child delivery date and warehouse, and explodes each planned item with its own BOM (038d82c65c, a2172d476a).
/// 4) Accumulated Cash Flow Statement counts movements only from max(rangeStartDate, fiscalYearStartDate) and tracks opening/closing balances per movement start date across fiscal years (461d586d8b, 80ef213995).
/// </summary>
public abstract class UpstreamBatch7IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task PurchaseReceiptAndSubcontractingReturn_CapRejectedReturnColumnWhenSourceRowHadZeroRejected_92e535f68a_ed481acfe9()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();
            var subAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Return Cap Co"), autoSave: true);
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PurchaseReceipt", "PurchaseReceipt", "PR-2026-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCO", "SCO", "SCO-2026-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR", "SCR", "SCR-2026-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCR-RET", "SCR-RET", "SCR-RET-2026-"), autoSave: true);

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

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Cap Supplier"), autoSave: true);
            var acceptedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Accepted WH"), autoSave: true);
            var rejectedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Rejected WH"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "CAP-FG-01", "Cap FG Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = false,
                AllowNegativeStock = true,
            }, autoSave: true);

            var rmItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "CAP-RM-01", "Cap RM Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = false,
                AllowNegativeStock = true,
            }, autoSave: true);

            // 1) Purchase Receipt with Qty = 5, RejectedQty = 0
            var pr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = acceptedWh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        Description = fgItem.ItemName,
                        Quantity = 5m,
                        RejectedQty = 0m,
                        ReceivedQty = 5m,
                        UnitPrice = 50m,
                        Uom = "Nos",
                        WarehouseId = acceptedWh.Id,
                    }
                }
            });

            // Attempting to create a return PR with RejectedQty = -2 when source row had RejectedQty = 0 must throw ReturnQtyExceedsOriginal (ed481acfe9)
            var prEx = await Should.ThrowAsync<BusinessException>(async () =>
                await prAppService.CreateAsync(new CreatePurchaseReceiptDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    WarehouseId = acceptedWh.Id,
                    PostingDate = DateTime.UtcNow.Date,
                    IsReturn = true,
                    ReturnAgainstId = pr.Id,
                    Items = new List<CreatePurchaseReceiptItemDto>
                    {
                        new()
                        {
                            ItemId = fgItem.Id,
                            Description = fgItem.ItemName,
                            Quantity = -3m,
                            RejectedQty = -2m,
                            ReceivedQty = -5m,
                            UnitPrice = 50m,
                            Uom = "Nos",
                            WarehouseId = acceptedWh.Id,
                            RejectedWarehouseId = rejectedWh.Id,
                        }
                    }
                }));
            prEx.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);

            // 2) Subcontracting Receipt with Qty = 5, RejectedQty = 0 (but RejectedWarehouseId set)
            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-CAP-FG-01", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, rmItem.ItemName, 1m, 10m));
            await bomRepo.InsertAsync(bom, autoSave: true);

            var sco = await subAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow.Date,
                Items = new List<CreateScoItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = bom.Id,
                        Qty = 5m,
                        Rate = 20m,
                        ServiceCostPerQty = 20m,
                        WarehouseId = acceptedWh.Id,
                    }
                }
            });
            await subAppService.SubmitOrderAsync(sco.Id);

            var scr = await subAppService.CreateReceiptAsync(new CreateSubcontractingReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                SubcontractingOrderId = sco.Id,
                PostingDate = DateTime.UtcNow.Date,
                WarehouseId = acceptedWh.Id,
                Items = new List<CreateScrItemDto>
                {
                    new()
                    {
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        Qty = 5m,
                        RejectedQty = 0m,
                        Rate = 20m,
                        WarehouseId = acceptedWh.Id,
                        RejectedWarehouseId = rejectedWh.Id,
                    }
                }
            });
            await subAppService.SubmitReceiptAsync(scr.Id);

            // Attempting to return 1 qty from the rejected warehouse when RejectedQty on the source SCR was 0 must throw ReturnQtyExceedsOriginal
            var scrEx = await Should.ThrowAsync<BusinessException>(async () =>
                await subAppService.CreateReceiptReturnAsync(new CreateSubcontractingReceiptReturnDto
                {
                    ReturnAgainstReceiptId = scr.Id,
                    PostingDate = DateTime.UtcNow.Date,
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
                }));
            scrEx.Code.ShouldBe(MyERPDomainErrorCodes.ReturnQtyExceedsOriginal);
        });
    }

    [Fact]
    public async Task ItemCreateWithStandardSellingRate_CreatesItemPriceInForeignPriceListRoundedToCurrencyPrecision_1853ef83ea_e8a8b0b260_629e32b70a_0957add0db()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var priceListRepo = GetRequiredService<IRepository<PriceList, Guid>>();
            var fxRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var itemPriceRepo = GetRequiredService<IRepository<ItemPrice, Guid>>();
            var itemAppService = GetRequiredService<IItemAppService>();
            var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();

            // 1) Company in INR with a default selling PriceList in 3-decimal currency TPT at exchange rate 1.1 (629e32b70a)
            var company3Dec = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Precision 3Dec Co") { CurrencyCode = "INR" }, autoSave: true);
            var plTpt = await priceListRepo.InsertAsync(new PriceList(Guid.NewGuid(), "Selling TPT", "TPT", isSelling: true, isBuying: false)
            {
                CompanyId = company3Dec.Id,
                IsDefault = true,
                IsActive = true,
            }, autoSave: true);

            await fxRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "TPT", "INR", 1.1m, DateTime.UtcNow.Date.AddDays(-1)), autoSave: true);

            var itemTpt = await itemAppService.CreateAsync(new CreateUpdateItemDto
            {
                CompanyId = company3Dec.Id,
                ItemCode = "ITEM-TPT-100",
                ItemName = "Item TPT 100",
                ItemType = ItemType.Goods,
                Uom = "Nos",
                StandardSellingPrice = 100m,
                MaintainStock = true,
                IsActive = true,
            });
            await uowManager.Current!.SaveChangesAsync();

            var createdTptPrice = (await itemPriceRepo.GetQueryableAsync())
                .FirstOrDefault(p => p.ItemId == itemTpt.Id && p.PriceListId == plTpt.Id);
            createdTptPrice.ShouldNotBeNull();
            createdTptPrice!.CurrencyCode.ShouldBe("TPT");
            createdTptPrice.PriceListRate.ShouldBe(90.909m);

            // 2) Company in INR with a default selling PriceList in 2-decimal currency TPR at exchange rate 1.1 (0957add0db)
            var company2Dec = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Precision 2Dec Co") { CurrencyCode = "INR" }, autoSave: true);
            var plTpr = await priceListRepo.InsertAsync(new PriceList(Guid.NewGuid(), "Selling TPR", "TPR", isSelling: true, isBuying: false)
            {
                CompanyId = company2Dec.Id,
                IsDefault = true,
                IsActive = true,
            }, autoSave: true);

            await fxRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "TPR", "INR", 1.1m, DateTime.UtcNow.Date.AddDays(-1)), autoSave: true);

            var itemTpr = await itemAppService.CreateAsync(new CreateUpdateItemDto
            {
                CompanyId = company2Dec.Id,
                ItemCode = "ITEM-TPR-100",
                ItemName = "Item TPR 100",
                ItemType = ItemType.Goods,
                Uom = "Nos",
                StandardSellingPrice = 100m,
                MaintainStock = true,
                IsActive = true,
            });
            await uowManager.Current!.SaveChangesAsync();

            var createdTprPrice = (await itemPriceRepo.GetQueryableAsync())
                .FirstOrDefault(p => p.ItemId == itemTpr.Id && p.PriceListId == plTpr.Id);
            createdTprPrice.ShouldNotBeNull();
            createdTprPrice!.CurrencyCode.ShouldBe("TPR");
            createdTprPrice.PriceListRate.ShouldBe(90.91m);
        });
    }

    [Fact]
    public async Task MrpReport_FiltersMpsByChildDeliveryDateAndWarehouseAndUsesEachPlannedItemBom_038d82c65c_a2172d476a()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var mpsRepo = GetRequiredService<IRepository<MasterProductionSchedule, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Filter Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "WO", "WO", "WO-2026-"), autoSave: true);

            var whMain = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main Factory WH"), autoSave: true);
            var whOther = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Other Factory WH"), autoSave: true);

            var fg1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MRP-FG-1", "MRP FG 1", ItemType.Goods) { Uom = "Nos", MaintainStock = true }, autoSave: true);
            var fg2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MRP-FG-2", "MRP FG 2", ItemType.Goods) { Uom = "Nos", MaintainStock = true }, autoSave: true);
            var rm1 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MRP-RM-1", "MRP RM 1", ItemType.Goods) { Uom = "Nos", MaintainStock = true }, autoSave: true);
            var rm2 = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "MRP-RM-2", "MRP RM 2", ItemType.Goods) { Uom = "Nos", MaintainStock = true }, autoSave: true);

            var bom1 = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MRP-FG-1", fg1.Id) { Quantity = 1m, IsActive = true, IsDefault = true };
            bom1.Items.Add(new BomItem(Guid.NewGuid(), bom1.Id, rm1.Id, rm1.ItemName, 2m, 10m));
            await bomRepo.InsertAsync(bom1, autoSave: true);

            var bom2 = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MRP-FG-2", fg2.Id) { Quantity = 1m, IsActive = true, IsDefault = true };
            bom2.Items.Add(new BomItem(Guid.NewGuid(), bom2.Id, rm2.Id, rm2.ItemName, 3m, 15m));
            await bomRepo.InsertAsync(bom2, autoSave: true);

            // MPS 1 in whMain starts BEFORE the report range (2026-01-01), with rows inside the report range (2026-05-10, 2026-05-12)
            // and one row outside the report range (2026-08-10) (038d82c65c)
            var mpsMain = new MasterProductionSchedule(Guid.NewGuid(), company.Id, "MPS-MAIN-01", new DateTime(2026, 1, 1), new DateTime(2026, 1, 1))
            {
                ParentWarehouseId = whMain.Id
            };
            mpsMain.SetItems(new[]
            {
                new MasterProductionScheduleItem(Guid.NewGuid(), mpsMain.Id, fg1.Id, fg1.ItemName, new DateTime(2026, 5, 10), 10m, 2) { BomId = bom1.Id, WarehouseId = whMain.Id },
                new MasterProductionScheduleItem(Guid.NewGuid(), mpsMain.Id, fg2.Id, fg2.ItemName, new DateTime(2026, 5, 12), 20m, 2) { BomId = bom2.Id, WarehouseId = whMain.Id },
                new MasterProductionScheduleItem(Guid.NewGuid(), mpsMain.Id, fg1.Id, fg1.ItemName, new DateTime(2026, 8, 10), 50m, 2) { BomId = bom1.Id, WarehouseId = whMain.Id },
            });
            mpsMain.Submit();
            await mpsRepo.InsertAsync(mpsMain, autoSave: true);

            // MPS 2 in whOther has a row inside the report range (2026-05-10), which must be excluded when filtering by whMain
            var mpsOther = new MasterProductionSchedule(Guid.NewGuid(), company.Id, "MPS-OTHER-01", new DateTime(2026, 5, 1), new DateTime(2026, 5, 1))
            {
                ParentWarehouseId = whOther.Id
            };
            mpsOther.SetItems(new[]
            {
                new MasterProductionScheduleItem(Guid.NewGuid(), mpsOther.Id, fg1.Id, fg1.ItemName, new DateTime(2026, 5, 10), 99m, 2) { BomId = bom1.Id, WarehouseId = whOther.Id }
            });
            mpsOther.Submit();
            await mpsRepo.InsertAsync(mpsOther, autoSave: true);

            var report = await mrpAppService.GetReportAsync(new MaterialRequirementsPlanningFilterDto
            {
                CompanyId = company.Id,
                WarehouseId = whMain.Id,
                FromDate = new DateTime(2026, 5, 1),
                ToDate = new DateTime(2026, 5, 31),
                BucketSize = MrpBucketSize.Monthly,
            });

            var fg1Row = report.Rows.Single(r => r.ItemId == fg1.Id);
            var fg2Row = report.Rows.Single(r => r.ItemId == fg2.Id);
            var rm1Row = report.Rows.Single(r => r.ItemId == rm1.Id);
            var rm2Row = report.Rows.Single(r => r.ItemId == rm2.Id);

            // Only the May rows from whMain are included (10 of fg1 -> 20 of rm1; 20 of fg2 -> 60 of rm2)
            fg1Row.Buckets.Sum(b => b.GrossRequirements).ShouldBe(10m);
            fg2Row.Buckets.Sum(b => b.GrossRequirements).ShouldBe(20m);
            rm1Row.Buckets.Sum(b => b.GrossRequirements).ShouldBe(20m);
            rm2Row.Buckets.Sum(b => b.GrossRequirements).ShouldBe(60m);

            // CreateOrdersAsync resolves each item's own BOM even if the caller mistakenly passed fg1's BOM on fg2's row (a2172d476a)
            var createdOrders = await mrpAppService.CreateOrdersAsync(new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                WarehouseId = whMain.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = fg1.Id,
                        ItemCode = fg1.ItemCode,
                        ItemName = fg1.ItemName,
                        TypeOfMaterial = "Manufacture",
                        BomId = bom1.Id,
                        Quantity = 10m,
                        DeliveryDate = new DateTime(2026, 5, 10),
                    },
                    new()
                    {
                        ItemId = fg2.Id,
                        ItemCode = fg2.ItemCode,
                        ItemName = fg2.ItemName,
                        TypeOfMaterial = "Manufacture",
                        BomId = bom1.Id, // wrong item's BOM -> must resolve fg2's own BOM (bom2)
                        Quantity = 20m,
                        DeliveryDate = new DateTime(2026, 5, 12),
                    }
                }
            });

            createdOrders.WorkOrderIds.Count.ShouldBe(2);
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var wo2 = await woRepo.GetAsync(createdOrders.WorkOrderIds[1]);
            wo2.ItemId.ShouldBe(fg2.Id);
            wo2.BomId.ShouldBe(bom2.Id);
        });
    }

    [Fact]
    public async Task AccumulatedCashFlowStatement_CountsOnlyMovementsInRangeAndTracksOpeningClosingAcrossFiscalYears_461d586d8b_80ef213995()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var cfAppService = GetRequiredService<ICashFlowStatementAppService>();
            var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Accumulated CF Co"), autoSave: true);
            var fy2025 = await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), autoSave: true);
            var fy2026 = await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var cashAcct = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1110", "Cash", AccountType.Asset)
            {
                AccountSubType = AccountSubType.CashAccount,
                IsGroup = false
            }, autoSave: true);
            var equityAcct = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "3100", "Capital Stock", AccountType.Equity)
            {
                IsGroup = false
            }, autoSave: true);

            async Task PostJeAsync(Guid cId, Guid fyId, DateTime date, Guid debitAcct, Guid creditAcct, decimal amount, string refNo)
            {
                var je = new JournalEntry(Guid.NewGuid(), cId, fyId, date)
                {
                    EntryNumber = refNo,
                    Narration = refNo,
                };
                je.AddLine(debitAcct, amount, isDebit: true);
                je.AddLine(creditAcct, amount, isDebit: false);
                je.Post();
                await jeRepo.InsertAsync(je, autoSave: true);
            }

            // 1) Test 461d586d8b: Date Range starting mid-fiscal-year (2026-04-01 to 2026-12-31)
            //    Post 1000 on 2026-01-01 (before range start) and 500 on 2026-04-01 (at range start).
            await PostJeAsync(company.Id, fy2026.Id, new DateTime(2026, 1, 1), cashAcct.Id, equityAcct.Id, 1000m, "JE-CF-1");
            await PostJeAsync(company.Id, fy2026.Id, new DateTime(2026, 4, 1), cashAcct.Id, equityAcct.Id, 500m, "JE-CF-2");
            await uowManager.Current!.SaveChangesAsync();

            var midYearReport = await cfAppService.GetCashFlowStatementAsync(new CashFlowRequestDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 4, 1),
                ToDate = new DateTime(2026, 12, 31),
                Periodicity = "Quarterly",
                AccumulatedValues = true
            });

            midYearReport.Periods.Count.ShouldBe(3);
            // Every quarter in [2026-04-01 .. 2026-12-31] counts only the 500 movement inside the range (not the 1000 on 2026-01-01)
            midYearReport.Periods.Select(p => p.NetCashChange).ShouldAllBe(x => x == 500m);
            // The 1000 from 2026-01-01 is in OpeningCashBalance (as of 2026-03-31), and ClosingCashBalance is 1500
            midYearReport.Periods.Select(p => p.OpeningCashBalance).ShouldAllBe(x => x == 1000m);
            midYearReport.Periods.Select(p => p.ClosingCashBalance).ShouldAllBe(x => x == 1500m);

            // 2) Test 80ef213995: Two full fiscal years (2025-01-01 to 2026-12-31) Quarterly with AccumulatedValues = true
            var company2 = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Two FY CF Co"), autoSave: true);
            var c2Fy25 = await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company2.Id, "FY 2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), autoSave: true);
            var c2Fy26 = await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company2.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var cash2 = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company2.Id, "1110", "Cash", AccountType.Asset) { AccountSubType = AccountSubType.CashAccount }, autoSave: true);
            var equip2 = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company2.Id, "1510", "Equipment", AccountType.Asset) { AccountSubType = AccountSubType.FixedAsset }, autoSave: true);
            var sales2 = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company2.Id, "4100", "Sales", AccountType.Revenue), autoSave: true);

            foreach (var (dt, fyId) in new[] { (new DateTime(2025, 1, 1), c2Fy25.Id), (new DateTime(2026, 1, 1), c2Fy26.Id) })
            {
                await PostJeAsync(company2.Id, fyId, dt, equip2.Id, cash2.Id, 500m, $"JE-EQ-{dt:yyyy}");
                await PostJeAsync(company2.Id, fyId, dt, cash2.Id, sales2.Id, 300m, $"JE-SL-{dt:yyyy}");
            }
            await uowManager.Current!.SaveChangesAsync();

            var twoFyReport = await cfAppService.GetCashFlowStatementAsync(new CashFlowRequestDto
            {
                CompanyId = company2.Id,
                FromDate = new DateTime(2025, 1, 1),
                ToDate = new DateTime(2026, 12, 31),
                Periodicity = "Quarterly",
                AccumulatedValues = true
            });

            twoFyReport.InvestingTotal.ShouldBe(-1000m);
            twoFyReport.NetCashChange.ShouldBe(-400m);
            twoFyReport.Periods.Count.ShouldBe(8);

            // Opening across 8 quarters: [0, 0, 0, 0, -200, -200, -200, -200]
            twoFyReport.Periods.Take(4).Select(p => p.OpeningCashBalance).ShouldAllBe(x => x == 0m);
            twoFyReport.Periods.Skip(4).Select(p => p.OpeningCashBalance).ShouldAllBe(x => x == -200m);

            // Closing across 8 quarters: [-200, -200, -200, -200, -400, -400, -400, -400]
            twoFyReport.Periods.Take(4).Select(p => p.ClosingCashBalance).ShouldAllBe(x => x == -200m);
            twoFyReport.Periods.Skip(4).Select(p => p.ClosingCashBalance).ShouldAllBe(x => x == -400m);
            twoFyReport.OpeningCashBalance.ShouldBe(0m);
            twoFyReport.ClosingCashBalance.ShouldBe(-400m);
        });
    }
}
