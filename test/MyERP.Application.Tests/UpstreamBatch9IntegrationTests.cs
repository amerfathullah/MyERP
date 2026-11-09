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
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 9 upstream ERPNext parity:
/// 1) Batch ManufacturingDate vs ExpiryDate validation &amp; POS batch reservation filtering by warehouse and batch (de7f4dfd77, 39e0571409).
/// 2) Variant / template BOM row UOM conversion to StockQty &amp; rate per stock UOM on Work Order, plus MRP whole-unit Work Order quantity rounding before ceiling (e20967e15e, 8ae5e72ea6, 3c4b17dbc9, 10303f585c).
/// 3) Invoice Discounting company validation rejecting cross-company invoices (5da098a712).
/// 4) Pull unconsumed raw materials into Manufacture Stock Entry after a partial Material Consumption for Manufacture entry (57fb18aa0a).
/// </summary>
public abstract class UpstreamBatch9IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task BatchValidationAndPosReservation_RejectsInvalidDates_AndFiltersPosBatchReservationsByWarehouseAndBatch()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var invoiceRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var batchAppService = GetRequiredService<IBatchAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "POS Batch 9 Co"), autoSave: true);
            var whMain = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main Store"), autoSave: true);
            var whBranch = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Branch Store"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "B9-BATCH-ITEM", "Batch Tracked Item", ItemType.Goods)
            {
                MaintainStock = true,
                HasBatchNo = true,
                Uom = "Nos"
            }, autoSave: true);

            // 1) BatchAppService.CreateAsync rejects ManufacturingDate > ExpiryDate (de7f4dfd77)
            var dateEx = await Should.ThrowAsync<BusinessException>(() =>
                batchAppService.CreateAsync(new CreateBatchDto
                {
                    ItemId = item.Id,
                    BatchNo = "B9-INVALID-DATE",
                    ManufacturingDate = new DateTime(2026, 7, 10),
                    ExpiryDate = new DateTime(2026, 7, 1)
                }));
            dateEx.Code.ShouldBe(MyERPDomainErrorCodes.InvalidDateRange);

            // 2) Create valid batch with 10 Nos in Main Store and 10 Nos in Branch Store
            var batchDto = await batchAppService.CreateAsync(new CreateBatchDto
            {
                ItemId = item.Id,
                BatchNo = "B9-LOT-01",
                ManufacturingDate = new DateTime(2026, 1, 1),
                ExpiryDate = new DateTime(2027, 1, 1)
            });

            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, item.Id, whMain.Id,
                new DateTime(2026, 5, 1), 10m, 10m, 10m, 100m)
            {
                BatchId = batchDto.Id,
                VoucherType = "StockEntry",
                VoucherId = Guid.NewGuid()
            }, autoSave: true);

            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, item.Id, whBranch.Id,
                new DateTime(2026, 5, 1), 10m, 10m, 10m, 100m)
            {
                BatchId = batchDto.Id,
                VoucherType = "StockEntry",
                VoucherId = Guid.NewGuid()
            }, autoSave: true);

            // 3) Unconsolidated POS Invoice reserves 4 Nos in Main Store via SerialAndBatchBundle (39e0571409)
            var receivableAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1190-B9", "POS Receivable", AccountType.Asset)
            {
                AccountSubType = AccountSubType.AccountsReceivable
            }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "POS Buyer"), autoSave: true);

            var posInvoice = new SalesInvoice(
                Guid.NewGuid(), company.Id, customer.Id, "POS-B9-0001",
                new DateTime(2026, 5, 2))
            {
                DebitToAccountId = receivableAcc.Id,
                IsPos = true,
                UpdateStock = false
            };
            posInvoice.AddItem(item.Id, "Batch Tracked Item", 4m, 25m, 0m, "Nos");
            posInvoice.Submit();
            posInvoice.Post();
            await invoiceRepo.InsertAsync(posInvoice, autoSave: true);

            var posBundle = new SerialAndBatchBundle(
                Guid.NewGuid(), company.Id, item.Id, whMain.Id,
                BundleTransactionType.Outward,
                "PosInvoice",
                posInvoice.Id,
                new DateTime(2026, 5, 2));
            posBundle.AddEntry(new SerialAndBatchEntry(
                Guid.NewGuid(), posBundle.Id, qty: -4m, incomingRate: 10m, batchId: batchDto.Id)
            {
                WarehouseId = whMain.Id
            });
            await bundleRepo.InsertAsync(posBundle, autoSave: true);

            // Query available batches for Main Store -> 10 - 4 = 6 available
            var mainAvailable = await batchAppService.GetAvailableBatchesAsync(new GetAvailableBatchesDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = whMain.Id
            });
            mainAvailable.Count.ShouldBe(1);
            mainAvailable[0].AvailableQuantity.ShouldBe(6m);

            // Query available batches for Branch Store -> reservation in Main Store does NOT bleed into Branch Store (10 available)
            var branchAvailable = await batchAppService.GetAvailableBatchesAsync(new GetAvailableBatchesDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = whBranch.Id
            });
            branchAvailable.Count.ShouldBe(1);
            branchAvailable[0].AvailableQuantity.ShouldBe(10m);
        });
    }

    [Fact]
    public async Task VariantBomWorkOrderAndMrp_ConvertBomRowUomToStockQtyAndStockRate_AndRoundWorkOrderWholeUnits()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var uomRepo = GetRequiredService<IRepository<Uom, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();
            var mrpAppService = GetRequiredService<IMaterialRequirementsPlanningAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Variant BOM B9 Co"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "WO", "WO", "WO-B9-"), autoSave: true);

            var whSource = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Store B9"), autoSave: true);
            var whWip = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP Store B9"), autoSave: true);
            var whFg = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Store B9"), autoSave: true);

            var existingNos = (await uomRepo.GetQueryableAsync()).FirstOrDefault(u => u.Name == "Nos");
            if (existingNos == null)
            {
                await uomRepo.InsertAsync(new Uom(Guid.NewGuid(), "Nos") { MustBeWholeNumber = true }, autoSave: true);
            }
            else
            {
                existingNos.MustBeWholeNumber = true;
                await uomRepo.UpdateAsync(existingNos, autoSave: true);
            }

            // Template FG item + Variant FG item + Raw Material item with Box UOM (1 Box = 10 Nos)
            var templateFg = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "B9-TPL-FG", "Template Shirt", ItemType.Goods)
            {
                HasVariants = true,
                Uom = "Nos"
            }, autoSave: true);

            var variantFg = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "B9-VAR-FG-M", "Variant Shirt Medium", ItemType.Goods)
            {
                VariantOfId = templateFg.Id,
                Uom = "Nos"
            }, autoSave: true);

            var rmButton = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "B9-RM-BTN", "Shirt Buttons", ItemType.Goods)
            {
                Uom = "Nos"
            }, autoSave: true);

            // Template BOM has 2 Boxes of Buttons (ConversionFactor = 10 -> StockQty = 20 Nos) at Rate = 50 per Box (5 per Nos)
            var templateBom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-B9-TPL-001", templateFg.Id)
            {
                Quantity = 1m,
                IsActive = true,
                IsDefault = true,
                SourceWarehouseId = whSource.Id,
                TargetWarehouseId = whFg.Id
            };
            templateBom.AddItem(new BomItem(
                Guid.NewGuid(), templateBom.Id, rmButton.Id, rmButton.ItemName,
                quantity: 2m, rate: 50m, uom: "Box", conversionFactor: 10m, stockUom: "Nos")
            {
                SourceWarehouseId = whSource.Id
            });
            templateBom.RecalculateCost();
            await bomRepo.InsertAsync(templateBom, autoSave: true);

            // 1) Create Work Order for Variant using Template BOM (e20967e15e, 8ae5e72ea6)
            var woDto = await mfgAppService.CreateWorkOrderAsync(new CreateWorkOrderDto
            {
                CompanyId = company.Id,
                ItemId = variantFg.Id,
                BomId = templateBom.Id,
                Quantity = 3m,
                SourceWarehouseId = whSource.Id,
                WipWarehouseId = whWip.Id,
                FgWarehouseId = whFg.Id
            });

            var createdWo = await woRepo.GetAsync(woDto.Id, includeDetails: true);
            createdWo.RequiredItems.Count.ShouldBe(1);
            // 2 Boxes * 10 Nos/Box * 3 FG = 60 Nos required in stock UOM
            createdWo.RequiredItems[0].RequiredQuantity.ShouldBe(60m);

            // 2) MRP CreateOrdersAsync rounds 5.00001 to 5.0000 before Math.Ceiling for whole-number UOM (3c4b17dbc9)
            var mrpOrders = await mrpAppService.CreateOrdersAsync(new CreateOrdersFromMrpInput
            {
                CompanyId = company.Id,
                WarehouseId = whFg.Id,
                SelectedRows = new List<MrpOrderRowInputDto>
                {
                    new()
                    {
                        ItemId = variantFg.Id,
                        ItemCode = variantFg.ItemCode,
                        ItemName = variantFg.ItemName,
                        Uom = "Nos",
                        TypeOfMaterial = "Manufacture",
                        BomId = templateBom.Id,
                        Quantity = 5.00001m,
                        DeliveryDate = new DateTime(2026, 6, 15),
                        ReleaseDate = new DateTime(2026, 6, 10)
                    }
                }
            });

            mrpOrders.WorkOrderIds.Count.ShouldBe(1);
            var mrpWo = await woRepo.GetAsync(mrpOrders.WorkOrderIds[0], includeDetails: true);
            mrpWo.Quantity.ShouldBe(5m);
            mrpWo.RequiredItems[0].RequiredQuantity.ShouldBe(100m); // 20 Nos per FG * 5 FG = 100 Nos
        });
    }

    [Fact]
    public async Task InvoiceDiscounting_CreateAsync_RejectsInvoiceFromDifferentCompany_5da098a712()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var invoiceRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var discountingService = GetRequiredService<IInvoiceDiscountingAppService>();

            var companyA = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Discounting Co A"), autoSave: true);
            var companyB = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Discounting Co B"), autoSave: true);

            var loanAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyA.Id, "2150-A", "Short Term Loan", AccountType.Liability), autoSave: true);
            var bankAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyA.Id, "1120-A", "Bank Account", AccountType.Asset), autoSave: true);
            var chargesAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyA.Id, "5150-A", "Bank Charges", AccountType.Expense), autoSave: true);
            var arCreditAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyA.Id, "1131-A", "AR Credit", AccountType.Asset), autoSave: true);
            var arDiscAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyA.Id, "1132-A", "AR Discounted", AccountType.Asset), autoSave: true);
            var arUnpaidAcc = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyA.Id, "1133-A", "AR Unpaid", AccountType.Asset), autoSave: true);

            var arCompanyB = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), companyB.Id, "1130-B", "AR Co B", AccountType.Asset)
            {
                AccountSubType = AccountSubType.AccountsReceivable
            }, autoSave: true);
            var customerB = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), companyB.Id, "Customer B"), autoSave: true);

            var invoiceB = new SalesInvoice(
                Guid.NewGuid(), companyB.Id, customerB.Id, "SINV-B-001",
                new DateTime(2026, 5, 1))
            {
                DebitToAccountId = arCompanyB.Id
            };
            invoiceB.AddItem(Guid.NewGuid(), "Consulting", 1m, 1000m, 0m);
            invoiceB.Submit();
            invoiceB.Post();
            await invoiceRepo.InsertAsync(invoiceB, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                discountingService.CreateAsync(new CreateInvoiceDiscountingDto
                {
                    CompanyId = companyA.Id,
                    PostingDate = new DateTime(2026, 5, 5),
                    ShortTermLoanAccountId = loanAcc.Id,
                    BankAccountId = bankAcc.Id,
                    BankChargesAccountId = chargesAcc.Id,
                    AccountsReceivableCreditAccountId = arCreditAcc.Id,
                    AccountsReceivableDiscountedAccountId = arDiscAcc.Id,
                    AccountsReceivableUnpaidAccountId = arUnpaidAcc.Id,
                    Invoices = new List<CreateInvoiceDiscountingInvoiceDto>
                    {
                        new()
                        {
                            SalesInvoiceId = invoiceB.Id,
                            OutstandingAmount = 1000m
                        }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyMismatch);
        });
    }
}
