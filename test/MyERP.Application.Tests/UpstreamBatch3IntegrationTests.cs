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
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core integration tests for Batch 3 upstream ERPNext parity:
/// 1) Non-stock service PurchaseReceipt Submit + Cancel skips SLEs and cancels even with an InProgress repost record (80bedf1791).
/// 2) Repeated CreateDisassemblyStockEntryAsync across 2 RM &amp; FG batches allocates Batch 1 first and Batch 2 second without reusing exhausted batches (b9121c2188 &amp; 54fcf4abd5).
/// 3) Cross-currency Dunning (USD SalesInvoice on MYR Receivable account) GetUnpaidOverduePaymentsAsync(inAccountCurrency: true), GetPaymentEntryTemplateAsync("Dunning", id), PaymentEntry Post auto-resolving Dunning, and Cancel reopening Dunning (7ca8bc3290 &amp; 8a09633536).
/// </summary>
public abstract class UpstreamBatch3IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task PurchaseReceipt_NonStockServiceItem_SubmitAndCancel_CreatesZeroSles_And_CancelsEvenWithRepost_80bedf1791()
    {
        Guid prId = Guid.Empty;
        Guid repostId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var repostRepo = GetRequiredService<IRepository<RepostItemValuation, Guid>>();
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "PR NonStock Cancel Co"), autoSave: true);
            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);

            var stockAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1401", "Inventory", AccountType.Asset), autoSave: true);
            var srbnbAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2150", "SRBNB", AccountType.Liability), autoSave: true);
            var expenseAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5100", "Service Expense", AccountType.Expense), autoSave: true);

            company.DefaultInventoryAccountId = stockAcc.Id;
            company.StockReceivedButNotBilledAccountId = srbnbAcc.Id;
            company.DefaultExpenseAccountId = expenseAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR DR Stock", "PurchaseReceipt", true, AccountSource.WarehouseStock, AmountSource.NetTotal)
                { SortOrder = 1, FixedAccountId = stockAcc.Id }, autoSave: true);
            await ruleRepo.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "PR CR SRBNB", "PurchaseReceipt", false, AccountSource.FixedAccount, AmountSource.NetTotal)
                { SortOrder = 2, FixedAccountId = srbnbAcc.Id }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PR Series", "PurchaseReceipt", "PR-NS-"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Service Vendor"), autoSave: true);
            var warehouse = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main WH"), autoSave: true);
            var serviceItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SRV-001", "Consulting Service", ItemType.Service)
                {
                    MaintainStock = false
                }, autoSave: true);

            var created = await prAppService.CreateAsync(new CreatePurchaseReceiptDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                WarehouseId = warehouse.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseReceiptItemDto>
                {
                    new() { ItemId = serviceItem.Id, Description = "Consulting Service", Quantity = 2m, UnitPrice = 250m }
                }
            });
            prId = created.Id;

            await prAppService.SubmitAsync(prId);

            // Seed a Queued repost pointing at this non-SLE PurchaseReceipt to verify 80bedf1791 skips it on Cancel
            var repost = new RepostItemValuation(Guid.NewGuid(), company.Id, RepostMethod.ItemAndWarehouse, DateTime.UtcNow.Date)
            {
                VoucherType = "PurchaseReceipt",
                VoucherId = prId
            };
            await repostRepo.InsertAsync(repost, autoSave: true);
            repostId = repost.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var prAppService = GetRequiredService<IPurchaseReceiptAppService>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var repostRepo = GetRequiredService<IRepository<RepostItemValuation, Guid>>();
            var prRepo = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();

            await prAppService.CancelAsync(prId);

            var pr = await prRepo.GetAsync(prId);
            pr.Status.ShouldBe(DocumentStatus.Cancelled);

            var sleQuery = await sleRepo.GetQueryableAsync();
            sleQuery.Any(s => s.VoucherType == "PurchaseReceipt" && s.VoucherId == prId).ShouldBeFalse();

            var repost = await repostRepo.GetAsync(repostId);
            repost.Status.ShouldBe(RepostStatus.Skipped);
        });
    }

    [Fact]
    public async Task Disassembly_RepeatedAcrossMultipleBatches_AllocatesSecondBatchAfterFirstExhausted_b9121c2188()
    {
        Guid woId = Guid.Empty;
        Guid rmItemId = Guid.Empty;
        Guid fgItemId = Guid.Empty;
        Guid rmBatch1Id = Guid.Empty;
        Guid rmBatch2Id = Guid.Empty;
        Guid fgBatch1Id = Guid.Empty;
        Guid fgBatch2Id = Guid.Empty;
        Guid firstDisassembleId = Guid.Empty;
        Guid secondDisassembleId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var batchRepo = GetRequiredService<IRepository<Batch, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var stockPostingService = GetRequiredService<StockPostingService>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Disassembly Batch Pool Co"), autoSave: true);
            var stockAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1411", "Stock Asset", AccountType.Asset), autoSave: true);
            var adjAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5411", "Stock Adjustment", AccountType.Equity), autoSave: true);
            company.DefaultInventoryAccountId = stockAcc.Id;
            company.DefaultStockAdjustmentAccountId = adjAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series", "SE", "SE-B3-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-BATCH", "Batched FG", ItemType.Goods) { HasBatchNo = true }, autoSave: true);
            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-BATCH", "Batched RM", ItemType.Goods) { HasBatchNo = true }, autoSave: true);
            fgItemId = fgItem.Id;
            rmItemId = rmItem.Id;

            var rmBatch1 = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), rmItem.Id, "RM-B1"), autoSave: true);
            var rmBatch2 = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), rmItem.Id, "RM-B2"), autoSave: true);
            var fgBatch1 = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), fgItem.Id, "FG-B1"), autoSave: true);
            var fgBatch2 = await batchRepo.InsertAsync(new Batch(Guid.NewGuid(), fgItem.Id, "FG-B2"), autoSave: true);
            rmBatch1Id = rmBatch1.Id;
            rmBatch2Id = rmBatch2.Id;
            fgBatch1Id = fgBatch1.Id;
            fgBatch2Id = fgBatch2.Id;

            var fgWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG WH"), autoSave: true);
            var rmWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM WH"), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-BATCH-1", fgItem.Id, Guid.NewGuid(), quantity: 10m)
            {
                FgWarehouseId = fgWh.Id,
                SourceWarehouseId = rmWh.Id
            };
            wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItem.Id, "Batched RM", requiredQuantity: 10m)
            {
                SourceWarehouseId = rmWh.Id
            });
            wo.Submit();
            wo.Start();
            wo.RecordProduction(10m);
            await woRepo.InsertAsync(wo, autoSave: true);
            woId = wo.Id;

            // Seed opening RM batch stock so Manufacture entry can consume 5 of RM-B1 + 5 of RM-B2
            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, rmItem.Id, rmWh.Id,
                DateTime.UtcNow.Date.AddDays(-2), quantityChange: 5m, valuationRate: 4m,
                balanceQuantity: 5m, balanceValue: 20m)
            {
                BatchId = rmBatch1.Id,
                StockQueue = "[[5,4.00]]"
            }, autoSave: true);
            await sleRepo.InsertAsync(new StockLedgerEntry(
                Guid.NewGuid(), company.Id, rmItem.Id, rmWh.Id,
                DateTime.UtcNow.Date.AddDays(-1), quantityChange: 5m, valuationRate: 4m,
                balanceQuantity: 10m, balanceValue: 40m)
            {
                BatchId = rmBatch2.Id,
                StockQueue = "[[5,4.00],[5,4.00]]"
            }, autoSave: true);

            var mfgEntry = new StockEntry(Guid.NewGuid(), company.Id, StockEntryType.Manufacture, DateTime.UtcNow.Date, company.TenantId)
            {
                WorkOrderId = wo.Id,
                EntryNumber = "SE-MFG-B3-1",
                FgCompletedQty = 10m
            };
            mfgEntry.AddItem(rmItem.Id, 5m, sourceWarehouseId: rmWh.Id, targetWarehouseId: null, valuationRate: 4m, batchId: rmBatch1.Id);
            mfgEntry.AddItem(rmItem.Id, 5m, sourceWarehouseId: rmWh.Id, targetWarehouseId: null, valuationRate: 4m, batchId: rmBatch2.Id);
            mfgEntry.AddItem(fgItem.Id, 5m, sourceWarehouseId: null, targetWarehouseId: fgWh.Id, valuationRate: 4m, isFinishedItem: true, batchId: fgBatch1.Id);
            mfgEntry.AddItem(fgItem.Id, 5m, sourceWarehouseId: null, targetWarehouseId: fgWh.Id, valuationRate: 4m, isFinishedItem: true, batchId: fgBatch2.Id);
            mfgEntry.Submit();
            mfgEntry.Post();
            await stockPostingService.PostStockEntryAsync(mfgEntry);
            await seRepo.InsertAsync(mfgEntry, autoSave: true);

            // Disassembly #1 of 5 units -> consumes fgBatch1 & returns rmBatch1
            var res1 = await mfgAppService.CreateDisassemblyStockEntryAsync(new CreateDisassemblyDto
            {
                WorkOrderId = woId,
                Quantity = 5m
            });
            firstDisassembleId = res1.StockEntryId;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var mfgAppService = GetRequiredService<IManufacturingAppService>();
            var seRepo = GetRequiredService<IRepository<StockEntry, Guid>>();

            // Disassembly #2 of 5 units in a fresh UoW -> must load Items via WithDetailsAsync and allocate fgBatch2 & rmBatch2!
            var res2 = await mfgAppService.CreateDisassemblyStockEntryAsync(new CreateDisassemblyDto
            {
                WorkOrderId = woId,
                Quantity = 5m
            });
            secondDisassembleId = res2.StockEntryId;

            var seWithDetails = await seRepo.WithDetailsAsync(s => s.Items);
            var d1 = seWithDetails.Single(s => s.Id == firstDisassembleId);
            var d2 = seWithDetails.Single(s => s.Id == secondDisassembleId);

            d1.Items.Single(i => i.ItemId == fgItemId && i.IsFinishedItem).BatchId.ShouldBe(fgBatch1Id);
            d1.Items.Single(i => i.ItemId == rmItemId && !i.IsFinishedItem).BatchId.ShouldBe(rmBatch1Id);

            d2.Items.Single(i => i.ItemId == fgItemId && i.IsFinishedItem).BatchId.ShouldBe(fgBatch2Id);
            d2.Items.Single(i => i.ItemId == rmItemId && !i.IsFinishedItem).BatchId.ShouldBe(rmBatch2Id);
        });
    }

    [Fact]
    public async Task Dunning_ForeignCurrencyInvoiceOnBaseReceivableAccount_TemplateAndPostAndCancelLifecycle_7ca8bc3290()
    {
        Guid dunningId = Guid.Empty;
        Guid invoiceId = Guid.Empty;
        Guid peId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var dunningAppService = GetRequiredService<IDunningAppService>();
            var peAppService = GetRequiredService<IPaymentEntryAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Dunning CrossCurr Co")
            {
                CurrencyCode = "MYR"
            }, autoSave: true);

            await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31)),
                autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PE Series", "PaymentEntry", "PE-DUN-"), autoSave: true);

            var receivableAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130", "MYR Receivable", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.AccountsReceivable,
                    Currency = "MYR"
                }, autoSave: true);
            var bankAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1120", "MYR Cash", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.CashAccount,
                    Currency = "MYR"
                }, autoSave: true);
            var incomeAcc = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4200", "Dunning Fee Income", AccountType.Revenue)
                {
                    Currency = "MYR"
                }, autoSave: true);

            company.DefaultReceivableAccountId = receivableAcc.Id;
            company.DefaultBankAccountId = bankAcc.Id;
            company.DefaultIncomeAccountId = incomeAcc.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "USD Billed Customer")
            {
                DefaultReceivableAccountId = receivableAcc.Id
            }, autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SRV-USD", "Export Consulting", ItemType.Service) { MaintainStock = false },
                autoSave: true);

            // USD invoice (100 USD @ 4.5 = 450 MYR) posted to MYR Receivable account
            var si = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "SI-USD-DUN-1", DateTime.UtcNow.Date.AddDays(-30))
            {
                CurrencyCode = "USD",
                ExchangeRate = 4.5m,
                DebitToAccountId = receivableAcc.Id,
                DueDate = DateTime.UtcNow.Date.AddDays(-15)
            };
            si.AddItem(item.Id, "Export Consulting", quantity: 1m, unitPrice: 100m, taxAmount: 0m);
            si.Submit();
            await siRepo.InsertAsync(si, autoSave: true);
            invoiceId = si.Id;

            var createdDunning = await dunningAppService.CreateAsync(new CreateDunningDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                PostingDate = DateTime.UtcNow.Date,
                Currency = "USD",
                ConversionRate = 4.5m,
                DunningFee = 10m, // 10 USD = 45 MYR
                IncomeAccountId = incomeAcc.Id,
                OverduePayments =
                [
                    new CreateDunningOverdueDto
                    {
                        SalesInvoiceId = si.Id,
                        OutstandingAmount = 100m,
                        DueDate = DateTime.UtcNow.Date.AddDays(-15),
                        OverdueDays = 15
                    }
                ]
            });
            dunningId = createdDunning.Id;
            await dunningAppService.SubmitAsync(dunningId);

            // 1) GetUnpaidOverduePaymentsAsync: 100 USD in transaction currency vs 450 MYR in receivable account currency
            var unpaidTxn = await dunningAppService.GetUnpaidOverduePaymentsAsync(dunningId, inAccountCurrency: false);
            unpaidTxn.Single().OutstandingAmount.ShouldBe(100m);

            var unpaidAcc = await dunningAppService.GetUnpaidOverduePaymentsAsync(dunningId, inAccountCurrency: true);
            unpaidAcc.Single().OutstandingAmount.ShouldBe(450m);

            // 2) GetPaymentEntryTemplateAsync("Dunning", dunningId): MYR payment of 450 + 45 = 495 MYR!
            var tpl = await peAppService.GetPaymentEntryTemplateAsync("Dunning", dunningId);
            tpl.PaymentCurrency.ShouldBe("MYR");
            tpl.PaidAmount.ShouldBe(495m);
            tpl.References.ShouldNotBeNull();
            tpl.References!.Single().AllocatedAmount.ShouldBe(450m);
            tpl.Taxes.ShouldNotBeNull();
            tpl.Taxes!.Single().TaxAmount.ShouldBe(45m);
            tpl.Taxes!.Single().DunningId.ShouldBe(dunningId);

            // 3) Create, Submit, and Post PaymentEntry from template -> invoice settled & Dunning auto-resolved!
            var createdPe = await peAppService.CreateAsync(tpl);
            peId = createdPe.Id;
            await peAppService.SubmitAsync(peId);
            await peAppService.PostAsync(peId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var dunningRepo = GetRequiredService<IRepository<Dunning, Guid>>();
            var reconAppService = GetRequiredService<IPaymentReconciliationAppService>();
            var peAppService = GetRequiredService<IPaymentEntryAppService>();

            var siAfterPost = await siRepo.GetAsync(invoiceId);
            siAfterPost.OutstandingAmount.ShouldBe(0m);

            var dAfterPost = await dunningRepo.GetAsync(dunningId);
            dAfterPost.PaidDunningAmount.ShouldBe(10m);
            dAfterPost.Status.ShouldBe(DocumentStatus.Posted); // Auto-resolved!

            // 4) Unreconcile invoice allocation then Cancel PaymentEntry -> invoice reopens to 100 USD and Dunning reopens to Submitted!
            await reconAppService.UnreconcileAsync(new UnreconcileDto
            {
                PaymentVoucherType = "PaymentEntry",
                PaymentVoucherId = peId,
                InvoiceVoucherType = "SalesInvoice",
                InvoiceVoucherId = invoiceId
            });
            await peAppService.CancelAsync(peId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var dunningRepo = GetRequiredService<IRepository<Dunning, Guid>>();

            var siAfterCancel = await siRepo.GetAsync(invoiceId);
            siAfterCancel.OutstandingAmount.ShouldBe(100m);

            var dAfterCancel = await dunningRepo.GetAsync(dunningId);
            dAfterCancel.PaidDunningAmount.ShouldBe(0m);
            dAfterCancel.Status.ShouldBe(DocumentStatus.Submitted);
        });
    }
}
