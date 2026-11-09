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
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 8 upstream ERPNext parity:
/// 1) MPS UOM conversion validation, ToDate extension, single-counting MPS item Sales Orders in MRP,
///    net undelivered Product Bundle decomposition, and whole-pack Purchase Order creation (38b9280852, 26bf8efb9e, 7754616e80, b314476739, 11ea4f022a, d172ca0198, 2e6a5cd8e6, 78e4242252, 007b7f3162, b7fa9539a2).
/// 2) Sales Person disable check on fully-returned Delivery Notes and self-parent / default-root guards across tree masters (9fa7b05e44, 92f0443e61, 0e78d62389, fc907018ef).
/// 3) POS Closing Entry cancellation cancels consolidated Sales Invoice, reverses SLE/Bin, restores Serial &amp; Batch Bundle to source POS Invoice, and clears ConsolidatedSalesInvoiceId (e553c17711).
/// 4) Payment Entry cancellation reopens settled Payment Request and updates referenced Purchase Order AdvancePaymentStatus back to Initiated (97de257a13).
/// 5) Purchase Receipt and return Purchase Receipt with rejected-only quantities submit cleanly without empty GL journal errors (846728fb12, b5f1d700b2).
/// </summary>
public abstract class UpstreamBatch8IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task MasterProductionScheduleAndMrp_ConvertRowUom_ExtendToDate_SingleCountMpsSalesOrders_DecomposeNetDeliveredProductBundles_AndCreateWholePackPurchaseOrders()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var uomRepo = GetRequiredService<IRepository<Uom, Guid>>();
            var uomConversionRepo = GetRequiredService<IRepository<UomConversion, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<ProductBundle, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var mpsRepo = GetRequiredService<IRepository<MasterProductionSchedule, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mpsAppService = GetRequiredService<IMasterProductionScheduleAppService>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MRP Batch 8 Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "MPS", "MPS", "MPS-2026-"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PurchaseOrder", "PurchaseOrder", "PO-2026-"), autoSave: true);

            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "MRP WH"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "MRP Customer"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "MRP Supplier"), autoSave: true);

            // Whole-number Purchase UOM "Box"
            var existingBoxUom = (await uomRepo.GetQueryableAsync()).FirstOrDefault(u => u.Name == "Box");
            if (existingBoxUom == null)
            {
                await uomRepo.InsertAsync(new Uom(Guid.NewGuid(), "Box") { MustBeWholeNumber = true }, autoSave: true);
            }
            else
            {
                existingBoxUom.MustBeWholeNumber = true;
                await uomRepo.UpdateAsync(existingBoxUom, autoSave: true);
            }

            // Raw Material with Stock UOM = "Nos", Purchase UOM = "Box" (1 Box = 10 Nos)
            var rmItem = new Item(Guid.NewGuid(), company.Id, "B8-RM-01", "Batch 8 Raw Material", ItemType.Goods)
            {
                Uom = "Nos",
                PurchaseUom = "Box",
                MaintainStock = true,
            };
            await itemRepo.InsertAsync(rmItem, autoSave: true);
            await uomConversionRepo.InsertAsync(new UomConversion(Guid.NewGuid(), "Box", "Nos", 10m, rmItem.Id), autoSave: true);

            // Finished Good with Stock UOM = "Nos", plus "Case" = 5 Nos
            var fgItem = new Item(Guid.NewGuid(), company.Id, "B8-FG-01", "Batch 8 Finished Good", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = true,
            };
            await itemRepo.InsertAsync(fgItem, autoSave: true);
            await uomConversionRepo.InsertAsync(new UomConversion(Guid.NewGuid(), "Case", "Nos", 5m, fgItem.Id), autoSave: true);

            // BOM for FG: 1 FG requires 1 RM
            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-B8-FG-01", fgItem.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, rmItem.ItemName, 1m, 10m));
            await bomRepo.InsertAsync(bom, autoSave: true);

            // Non-stock parent item for Product Bundle (1 Bundle = 2 RM)
            var bundleParentItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "B8-BUNDLE-01", "Batch 8 Kit", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = false,
            }, autoSave: true);

            var productBundle = new ProductBundle(Guid.NewGuid(), bundleParentItem.Id)
            {
                ItemName = bundleParentItem.ItemName,
                Description = "Kit of 2 RMs",
                IsActive = true,
            };
            productBundle.AddItem(rmItem.Id, qty: 2m, itemName: rmItem.ItemName, uom: "Nos");
            await bundleRepo.InsertAsync(productBundle, autoSave: true);

            // 1) Create MPS from 2026-04-01 with an initial ToDate of 2026-04-15 and one row
            var mps = await mpsAppService.CreateAsync(new CreateMasterProductionScheduleDto
            {
                CompanyId = company.Id,
                PostingDate = new DateTime(2026, 4, 1),
                FromDate = new DateTime(2026, 4, 1),
                ParentWarehouseId = wh.Id,
            });

            var mpsEntity = await mpsRepo.GetAsync(mps.Id, includeDetails: true);
            mpsEntity.ExtendToDate(new DateTime(2026, 4, 15));
            var mpsRowId = Guid.NewGuid();
            mpsEntity.SetItems(new[]
            {
                new MasterProductionScheduleItem(mpsRowId, mpsEntity.Id, fgItem.Id, fgItem.ItemName, new DateTime(2026, 4, 10), 1m, 0)
                {
                    BomId = bom.Id,
                    Uom = "Nos",
                    WarehouseId = wh.Id,
                }
            });
            await mpsRepo.UpdateAsync(mpsEntity, autoSave: true);

            // Updating MPS with an undefined UOM ("Pallet") must throw UomNotConfiguredForItem (7754616e80 / b314476739)
            var invalidUomEx = await Should.ThrowAsync<BusinessException>(async () =>
                await mpsAppService.UpdateAsync(mps.Id, new UpdateMasterProductionScheduleDto
                {
                    PostingDate = new DateTime(2026, 4, 1),
                    FromDate = new DateTime(2026, 4, 1),
                    ParentWarehouseId = wh.Id,
                    Items = new List<MasterProductionScheduleItemDto>
                    {
                        new()
                        {
                            Id = mpsRowId,
                            ItemId = fgItem.Id,
                            ItemName = fgItem.ItemName,
                            BomId = bom.Id,
                            Uom = "Pallet",
                            PlannedQty = 1m,
                            DeliveryDate = new DateTime(2026, 4, 10),
                            WarehouseId = wh.Id,
                        }
                    }
                }));
            invalidUomEx.Code.ShouldBe(MyERPDomainErrorCodes.UomNotConfiguredForItem);

            // Updating MPS with valid UOM ("Case" = 5 Nos, PlannedQty = 1 Case -> 5 Nos) and DeliveryDate = 2026-04-25
            // extends ToDate from 2026-04-15 to 2026-04-25 (38b9280852 / 26bf8efb9e)
            mps = await mpsAppService.UpdateAsync(mps.Id, new UpdateMasterProductionScheduleDto
            {
                PostingDate = new DateTime(2026, 4, 1),
                FromDate = new DateTime(2026, 4, 1),
                ParentWarehouseId = wh.Id,
                Items = new List<MasterProductionScheduleItemDto>
                {
                    new()
                    {
                        Id = mpsRowId,
                        ItemId = fgItem.Id,
                        ItemName = fgItem.ItemName,
                        BomId = bom.Id,
                        Uom = "Case",
                        PlannedQty = 1m,
                        DeliveryDate = new DateTime(2026, 4, 25),
                        WarehouseId = wh.Id,
                    }
                }
            });
            mps.ToDate.ShouldBe(new DateTime(2026, 4, 25));
            mps = await mpsAppService.SubmitAsync(mps.Id);

            // 2) Create an open Sales Order that includes:
            //    - fgItem (qty 8): already planned in MPS -> must NOT be counted again in MRP (2e6a5cd8e6 / 78e4242252)
            //    - bundleParentItem (ordered qty 5, already delivered 2 -> net remaining 3 kits * 2 RM/kit = 6 RM) (007b7f3162 / b7fa9539a2)
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-B8-0001", new DateTime(2026, 4, 2))
            {
                DeliveryDate = new DateTime(2026, 4, 25),
            };
            so.AddItem(fgItem.Id, fgItem.ItemName, quantity: 8m, unitPrice: 100m, taxAmount: 0m, uom: "Nos");
            so.AddItem(bundleParentItem.Id, bundleParentItem.ItemName, quantity: 5m, unitPrice: 50m, taxAmount: 0m, uom: "Nos");
            var bundleSoItem = so.Items.First(i => i.ItemId == bundleParentItem.Id);
            bundleSoItem.DeliveredQty = 2m; // 2 delivered, 3 remaining
            so.Submit();
            await soRepo.InsertAsync(so, autoSave: true);

            // 3) Run MRP Report for 2026-04-01 .. 2026-04-30
            var report = await mrpAppService.GetReportAsync(new MaterialRequirementsPlanningFilterDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 4, 1),
                ToDate = new DateTime(2026, 4, 30),
                BucketSize = MrpBucketSize.Monthly,
                WarehouseId = wh.Id,
            });

            // fgItem demand should come ONLY from MPS (1 Case * 5 Nos/Case = 5 Nos), NOT +8 from the Sales Order
            var fgReq = report.Requirements.FirstOrDefault(r => r.ItemId == fgItem.Id);
            fgReq.ShouldNotBeNull();
            fgReq!.RequiredQty.ShouldBe(5m);

            // rmItem demand should be 5 Nos (exploded from 5 fgItem) + 6 Nos (exploded from 3 undelivered bundle kits * 2 RM) = 11 Nos
            var rmReq = report.Requirements.FirstOrDefault(r => r.ItemId == rmItem.Id);
            rmReq.ShouldNotBeNull();
            rmReq!.RequiredQty.ShouldBe(11m);

            // 4) Create Purchase Order from MRP for rmItem (11 Nos required, PurchaseUom = "Box" of 10 Nos, MustBeWholeNumber = true):
            //    Must round up to 2 Boxes (StockQty = 20 Nos, ConversionFactor = 10) (11ea4f022a / d172ca0198)
            var createdOrders = await mrpAppService.CreateOrdersAsync(new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                WarehouseId = wh.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = rmItem.Id,
                        ItemCode = rmItem.ItemCode,
                        ItemName = rmItem.ItemName,
                        TypeOfMaterial = "Purchase",
                        Quantity = rmReq.PlannedQty,
                        Uom = "Nos",
                        DeliveryDate = new DateTime(2026, 4, 25),
                        DefaultSupplierId = supplier.Id,
                        WarehouseId = wh.Id,
                    }
                }
            });

            createdOrders.PurchaseOrdersCount.ShouldBe(1);
            var createdPo = await poRepo.GetAsync(createdOrders.PurchaseOrderIds[0]);
            createdPo.Items.Count.ShouldBe(1);
            var poLine = createdPo.Items.First();
            poLine.Uom.ShouldBe("Box");
            poLine.StockUom.ShouldBe("Nos");
            poLine.ConversionFactor.ShouldBe(10m);
            poLine.Quantity.ShouldBe(2m);
            poLine.StockQty.ShouldBe(20m);
        });
    }

    [Fact]
    public async Task SalesPersonAndTreeMasters_AllowDisablingSalesPersonWhenDeliveryNoteFullyReturned_RejectSelfParent_AndDefaultRootParent()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var salesTeamRepo = GetRequiredService<IRepository<SalesTeamEntry, Guid>>();
            var salesPersonAppService = GetRequiredService<ISalesPersonAppService>();
            var customerGroupAppService = GetRequiredService<ICustomerGroupAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Sales Tree Co"), autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "DN Customer"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "DN WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "DN-ITEM-01", "DN Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = false,
            }, autoSave: true);

            // 1) Create root Sales Person and child Sales Person (with empty ParentSalesPersonId -> auto-resolves to root per 0e78d62389 / fc907018ef)
            var rootSp = await salesPersonAppService.CreateAsync(new CreateSalesPersonDto
            {
                Name = "Sales Team",
                IsGroup = true,
                CommissionRate = 0m,
            });
            var childSp = await salesPersonAppService.CreateAsync(new CreateSalesPersonDto
            {
                Name = "Rep Alice",
                IsGroup = false,
                CommissionRate = 5m,
                ParentSalesPersonId = null,
            });
            childSp.ParentSalesPersonId.ShouldBe(rootSp.Id);

            // Self-parent on SalesPerson or CustomerGroup must be rejected (0e78d62389 / fc907018ef)
            var selfParentSpEx = await Should.ThrowAsync<BusinessException>(async () =>
                await salesPersonAppService.UpdateAsync(childSp.Id, new UpdateSalesPersonDto
                {
                    CommissionRate = 5m,
                    ParentSalesPersonId = childSp.Id,
                }));
            selfParentSpEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var childCg = await customerGroupAppService.CreateAsync(new CreateUpdateCustomerGroupDto
            {
                Name = "Retail Customers",
                IsGroup = false,
                ParentId = null,
            });
            childCg.ParentId.ShouldNotBeNull();
            var resolvedParentCg = await customerGroupAppService.GetAsync(childCg.ParentId!.Value);
            resolvedParentCg.IsGroup.ShouldBeTrue();
            resolvedParentCg.ParentId.ShouldBeNull();

            var selfParentCgEx = await Should.ThrowAsync<BusinessException>(async () =>
                await customerGroupAppService.UpdateAsync(childCg.Id, new CreateUpdateCustomerGroupDto
                {
                    Name = childCg.Name,
                    ParentId = childCg.Id,
                }));
            selfParentCgEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // 2) Create a submitted DeliveryNote with SalesTeamEntry linked to childSp (9fa7b05e44 / 92f0443e61)
            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, wh.Id, "DN-B8-0001", DateTime.UtcNow.Date);
            dn.AddItem(item.Id, item.ItemName, quantity: 5m, unitPrice: 100m, taxAmount: 0m);
            dn.Submit();
            await dnRepo.InsertAsync(dn, autoSave: true);

            await salesTeamRepo.InsertAsync(
                new SalesTeamEntry(Guid.NewGuid(), childSp.Id, "DeliveryNote", dn.Id, 100m, 500m, 5m),
                autoSave: true);

            // While DN is unbilled and not returned (BillingStatus == "To Bill"), disabling childSp is blocked
            var disableBlockedEx = await Should.ThrowAsync<BusinessException>(async () =>
                await salesPersonAppService.DisableAsync(childSp.Id));
            disableBlockedEx.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            // Once DN is 100% returned (PerReturned == 100 -> BillingStatus == "Not Applicable"), disabling childSp succeeds!
            var dnItem = dn.Items.First();
            dnItem.ReturnedQty = 5m;
            await dnRepo.UpdateAsync(dn, autoSave: true);

            await salesPersonAppService.DisableAsync(childSp.Id);
            var disabledSp = await salesPersonAppService.GetAsync(childSp.Id);
            disabledSp.IsEnabled.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task PosClosingCancel_CancelsConsolidatedInvoice_ReversesSleAndBin_RestoresBundleToSourcePosInvoice_AndClearsConsolidatedId()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var costCenterRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mopRepo = GetRequiredService<IRepository<ModeOfPayment, Guid>>();
            var posProfileRepo = GetRequiredService<IRepository<PosProfile, Guid>>();
            var posOpeningRepo = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var posClosingAppService = GetRequiredService<IPosClosingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "POS Cancel Co"), autoSave: true);
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SalesInvoice", "SalesInvoice", "SINV-POS-"), autoSave: true);

            var arAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1100", "Debtors", AccountType.Asset)
            {
                AccountSubType = AccountSubType.AccountsReceivable
            }, autoSave: true);
            var cashAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1010", "Cash", AccountType.Asset)
            {
                AccountSubType = AccountSubType.CashAccount
            }, autoSave: true);
            var revAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "4000", "Sales", AccountType.Revenue), autoSave: true);

            company.DefaultReceivableAccountId = arAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "SI Receivable Dr", "SalesInvoice",
                isDebit: true, AccountSource.FixedAccount, AmountSource.GrandTotal)
            {
                FixedAccountId = arAcc.Id,
                SortOrder = 1
            }, autoSave: true);
            await ruleRepo.InsertAsync(new AccountingRule(
                Guid.NewGuid(), company.Id, "SI Revenue Cr", "SalesInvoice",
                isDebit: false, AccountSource.FixedAccount, AmountSource.NetTotal)
            {
                FixedAccountId = revAcc.Id,
                SortOrder = 2
            }, autoSave: true);

            var mop = await mopRepo.InsertAsync(new ModeOfPayment(Guid.NewGuid(), "Cash", "Cash")
            {
                CompanyId = company.Id,
                DefaultAccountId = cashAcc.Id,
            }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Walk-In Customer"), autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "POS Store WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "POS-ITEM-01", "POS Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = true,
                HasBatchNo = true,
            }, autoSave: true);

            var costCenter = await costCenterRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "POS Cost Center", isGroup: false), autoSave: true);
            var profile = await posProfileRepo.InsertAsync(new PosProfile(Guid.NewGuid(), company.Id, "Main Counter", wh.Id), autoSave: true);
            var opening = new PosOpeningEntry(Guid.NewGuid(), company.Id, profile.Id, Guid.NewGuid());
            opening.AddOpeningBalance(mop.Id, mop.Name, 100m);
            await posOpeningRepo.InsertAsync(opening, autoSave: true);

            // Source POS Invoice
            var posInvoice = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "POS-INV-0001", new DateTime(2026, 5, 10))
            {
                IsPos = true,
                PosProfileId = profile.Id,
                DebitToAccountId = arAcc.Id,
                CostCenterId = costCenter.Id,
            };
            posInvoice.AddItem(item.Id, item.ItemName, quantity: 2m, unitPrice: 50m, taxAmount: 0m);
            posInvoice.Submit();
            posInvoice.Post();
            await siRepo.InsertAsync(posInvoice, autoSave: true);

            // Create and submit POS Closing Entry
            var closingDto = await posClosingAppService.CreateAsync(new CreatePosClosingDto
            {
                CompanyId = company.Id,
                PosProfileId = profile.Id,
                PosOpeningEntryId = opening.Id,
                UserId = opening.UserId,
                Invoices = new List<CreatePosClosingInvoiceDto>
                {
                    new()
                    {
                        PosInvoiceId = posInvoice.Id,
                        InvoiceNumber = posInvoice.InvoiceNumber,
                        GrandTotal = 100m,
                    }
                },
                Payments = new List<CreatePosClosingPaymentDto>
                {
                    new()
                    {
                        ModeOfPaymentId = mop.Id,
                        ModeName = mop.Name,
                        ExpectedAmount = 100m,
                        ClosingAmount = 100m,
                    }
                }
            });

            // Close the opening entry so closing can be cancelled later without active-session block
            opening.Close(closingDto.Id);
            await posOpeningRepo.UpdateAsync(opening, autoSave: true);

            closingDto = await posClosingAppService.SubmitAsync(closingDto.Id);
            closingDto.ConsolidatedSalesInvoiceId.ShouldNotBeNull();
            var consolidatedSiId = closingDto.ConsolidatedSalesInvoiceId!.Value;

            // Simulate SLE and SerialAndBatchBundle attached to the consolidated Sales Invoice (e553c17711)
            var sle = await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, item.Id, wh.Id, new DateTime(2026, 5, 10), -2m, 25m, 8m, 200m)
            {
                VoucherType = "SalesInvoice",
                VoucherId = consolidatedSiId,
            }, autoSave: true);

            var bundle = new SerialAndBatchBundle(
                Guid.NewGuid(), company.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "SalesInvoice", consolidatedSiId, new DateTime(2026, 5, 10));
            bundle.Submit();
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            // Cancel the POS Closing Entry
            var cancelledClosing = await posClosingAppService.CancelAsync(closingDto.Id);
            cancelledClosing.ConsolidatedSalesInvoiceId.ShouldBeNull();

            // 1) Consolidated SalesInvoice is Cancelled
            var consolidatedSi = await siRepo.GetAsync(consolidatedSiId);
            consolidatedSi.Status.ShouldBe(DocumentStatus.Cancelled);

            // 2) Consolidated SLE is marked IsCancelled = true
            var updatedSle = await sleRepo.GetAsync(sle.Id);
            updatedSle.IsCancelled.ShouldBeTrue();

            // 3) SerialAndBatchBundle is restored back to source POS Invoice and not cancelled
            var updatedBundle = await bundleRepo.GetAsync(bundle.Id);
            updatedBundle.VoucherId.ShouldBe(posInvoice.Id);
            updatedBundle.IsCancelled.ShouldBeFalse();

            // 4) Source POS Invoice ConsolidatedSalesInvoiceId is cleared so it can be re-consolidated
            var updatedSourcePos = await siRepo.GetAsync(posInvoice.Id);
            updatedSourcePos.ConsolidatedSalesInvoiceId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task PaymentEntryCancel_ReopensSettledPaymentRequestAndRestoresPurchaseOrderAdvancePaymentStatusToInitiated()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var poRepo = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var paymentRequestAppService = GetRequiredService<IPaymentRequestAppService>();
            var paymentEntryAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR Cancel Co"), autoSave: true);
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PaymentEntry", "PaymentEntry", "PE-2026-"), autoSave: true);

            var bankAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1020", "Bank", AccountType.Asset)
            {
                AccountSubType = AccountSubType.BankAccount
            }, autoSave: true);
            var apAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "2000", "Creditors", AccountType.Liability)
            {
                AccountSubType = AccountSubType.AccountsPayable
            }, autoSave: true);

            company.DefaultBankAccountId = bankAcc.Id;
            company.DefaultPayableAccountId = apAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Advance Supplier"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ADV-ITEM", "Advance Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = false,
            }, autoSave: true);

            var po = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-ADV-0001", DateTime.UtcNow.Date);
            po.AddItem(item.Id, item.ItemName, quantity: 1m, unitPrice: 500m, taxAmount: 0m, uom: "Nos");
            po.Submit();
            await poRepo.InsertAsync(po, autoSave: true);

            var pr = await paymentRequestAppService.CreateAsync(new CreatePaymentRequestDto
            {
                CompanyId = company.Id,
                ReferenceDoctype = "PurchaseOrder",
                ReferenceId = po.Id,
                PartyType = "Supplier",
                PartyId = supplier.Id,
                PartyName = supplier.Name,
                GrandTotal = 500m,
                Currency = "MYR",
            });
            pr = await paymentRequestAppService.SubmitAsync(pr.Id);

            var poAfterSubmit = await poRepo.GetAsync(po.Id);
            poAfterSubmit.AdvancePaymentStatus.ShouldBe("Initiated");

            // Pay the Payment Request -> creates, submits, and posts a PaymentEntry
            pr = await paymentRequestAppService.PayAsync(pr.Id);
            pr.Status.ShouldBe((int)PaymentRequestStatus.Paid);
            pr.PaymentEntryId.ShouldNotBeNull();

            var poAfterPay = await poRepo.GetAsync(po.Id);
            poAfterPay.AdvancePaymentStatus.ShouldBe("Fully Paid");

            // Cancel the PaymentEntry -> reopens PaymentRequest to Initiated AND restores PurchaseOrder.AdvancePaymentStatus to "Initiated" (97de257a13)
            await paymentEntryAppService.CancelAsync(pr.PaymentEntryId!.Value);

            var prAfterCancel = await paymentRequestAppService.GetAsync(pr.Id);
            prAfterCancel.Status.ShouldBe((int)PaymentRequestStatus.Initiated);

            var poAfterCancel = await poRepo.GetAsync(po.Id);
            poAfterCancel.AdvancePaymentStatus.ShouldBe("Initiated");
        });
    }

    [Fact]
    public async Task PurchaseReceiptSubmit_AllowsRejectedOnlyReceiptAndReturnWithoutEmptyJournalError()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Rejected Only PR Co"), autoSave: true);
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PurchaseReceipt", "PurchaseReceipt", "PR-REJ-"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Rejected Supplier"), autoSave: true);
            var acceptedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Accepted WH"), autoSave: true);
            var rejectedWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Rejected WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "REJ-ITEM-01", "Rejected Item", ItemType.Goods)
            {
                Uom = "Nos",
                MaintainStock = true,
                AllowNegativeStock = true,
            }, autoSave: true);

            // 1) Create and submit a Purchase Receipt with Quantity = 0, RejectedQty = 5
            var pr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = acceptedWh.Id,
                PostingDate = new DateTime(2026, 5, 12),
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = item.ItemName,
                        Quantity = 0m,
                        RejectedQty = 5m,
                        ReceivedQty = 5m,
                        UnitPrice = 40m,
                        Uom = "Nos",
                        WarehouseId = acceptedWh.Id,
                        RejectedWarehouseId = rejectedWh.Id,
                    }
                }
            });

            pr = await prAppService.SubmitAsync(pr.Id);
            pr.Status.ShouldBe(DocumentStatus.Submitted.ToString());

            // 2) Create and submit a return Purchase Receipt returning -2 rejected qty (Quantity = 0, RejectedQty = -2)
            var returnPr = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = acceptedWh.Id,
                PostingDate = new DateTime(2026, 5, 13),
                IsReturn = true,
                ReturnAgainstId = pr.Id,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new()
                    {
                        ItemId = item.Id,
                        Description = item.ItemName,
                        Quantity = 0m,
                        RejectedQty = -2m,
                        ReceivedQty = -2m,
                        UnitPrice = 40m,
                        Uom = "Nos",
                        WarehouseId = acceptedWh.Id,
                        RejectedWarehouseId = rejectedWh.Id,
                    }
                }
            });

            returnPr = await prAppService.SubmitAsync(returnPr.Id);

            // Verify SLEs in Rejected WH: +5 on original receipt, -2 on return receipt
            var rejectedSles = (await sleRepo.GetQueryableAsync())
                .Where(s => s.ItemId == item.Id && s.WarehouseId == rejectedWh.Id && !s.IsCancelled)
                .OrderBy(s => s.PostingDateTime)
                .ToList();
            rejectedSles.Count.ShouldBe(2);
            rejectedSles[0].QuantityChange.ShouldBe(5m);
            rejectedSles[1].QuantityChange.ShouldBe(-2m);
        });
    }
}
