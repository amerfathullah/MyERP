using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 3:
/// - 0ca4c81ea3: Actual tax distribution over applicable items only, zero-net fallback, remainder absorption, and N/A cascade reset.
/// - 80bedf1791: StockRepostGuardService skips cancellation block when voucher has zero SLEs.
/// - 0953a16dba, 4bd25cf1df, 240a3a97a3: PaymentScheduleEntry zero-portion BaseOutstanding, keepSettled PaidAmount, and Amount discount scaling.
/// - 7623dae273: PaymentReconciliationEngine &amp; PaymentLedgerService Receivable vs Payable direction from Account instead of party_type + negative Payable AutoAllocate.
/// - b9121c2188 &amp; 54fcf4abd5: StockEntryManager disassembly batch pool subtraction &amp; rounding + batch-split scale validation.
/// - 7ca8bc3290 &amp; 8a09633536: Dunning UnpaidBaseDunningAmount, oldest-due schedule deduction, and receivable-account currency outstanding.
/// </summary>
public class UpstreamBatch3ParityTests
{
    [Fact]
    public void TaxesAndTotals_ActualTax_ExcludesNotApplicableItems_HandlesZeroNet_And_AbsorbsRemainder_0ca4c81ea3()
    {
        var svc = new TaxesAndTotalsService();
        var parentId = Guid.NewGuid();
        var actualTaxAccountId = Guid.NewGuid();

        // 1) Item 2 (200) is marked N/A (decimal.MinValue) for actualTaxAccountId; Actual 100 splits 25 / 0 / 75 over Items 1 (100) & 3 (300)
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 100m, NetAmount = 100m },
            new()
            {
                ItemId = Guid.NewGuid(),
                Qty = 1m,
                Rate = 200m,
                NetAmount = 200m,
                ItemTaxRateOverrides = new Dictionary<Guid, decimal> { [actualTaxAccountId] = decimal.MinValue }
            },
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 300m, NetAmount = 300m }
        };
        var taxRows = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", parentId, 1, "Freight", "Actual", 100m)
            {
                AccountId = actualTaxAccountId
            }
        };

        var totals = svc.Calculate(items, taxRows, roundRowWiseTax: true);
        totals.NetTotal.ShouldBe(600m);
        totals.TotalTax.ShouldBe(100m);
        totals.GrandTotal.ShouldBe(700m);
        taxRows[0].TaxAmount.ShouldBe(100m);

        // 2) 3 equal items (100 each) with Actual = 100 and roundRowWiseTax = true -> 33.33 + 33.33 + 33.34 = 100.00 (no 0.01 drift)
        var threeItems = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 100m, NetAmount = 100m },
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 100m, NetAmount = 100m },
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 100m, NetAmount = 100m }
        };
        var threeTaxRows = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", parentId, 1, "Actual Fee", "Actual", 100m)
            {
                AccountId = actualTaxAccountId
            }
        };

        var threeTotals = svc.Calculate(threeItems, threeTaxRows, roundRowWiseTax: true);
        threeTotals.TotalTax.ShouldBe(100m);
        threeTaxRows[0].TaxAmount.ShouldBe(100m);

        // 3) Free/zero-rate items (NetTotal == 0): Actual tax 50m still applies to last applicable item instead of dropping to 0
        var zeroItems = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 0m },
            new() { ItemId = Guid.NewGuid(), Qty = 2m, Rate = 0m }
        };
        var zeroTaxRows = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", parentId, 1, "Handling", "Actual", 50m)
            {
                AccountId = actualTaxAccountId
            }
        };

        var zeroTotals = svc.Calculate(zeroItems, zeroTaxRows, roundRowWiseTax: true);
        zeroTotals.NetTotal.ShouldBe(0m);
        zeroTotals.TotalTax.ShouldBe(50m);
        zeroTotals.GrandTotal.ShouldBe(50m);
    }

    [Fact]
    public void TaxesAndTotals_NotApplicableRow_ResetsPerItemCascadeForPreviousRowDependentTax_0ca4c81ea3()
    {
        var svc = new TaxesAndTotalsService();
        var parentId = Guid.NewGuid();
        var row1AccountId = Guid.NewGuid();
        var row2AccountId = Guid.NewGuid();

        // Item 1: 100m, Row 1 (10%) = 10m, Row 2 (50% On Previous Row Amount) = 5m
        // Item 2: 100m, Row 1 = N/A (0m), Row 2 (50% On Previous Row Amount) MUST be 0m (not stale 5m from Item 1!)
        var items = new List<TransactionItem>
        {
            new() { ItemId = Guid.NewGuid(), Qty = 1m, Rate = 100m, NetAmount = 100m },
            new()
            {
                ItemId = Guid.NewGuid(),
                Qty = 1m,
                Rate = 100m,
                NetAmount = 100m,
                ItemTaxRateOverrides = new Dictionary<Guid, decimal> { [row1AccountId] = decimal.MinValue }
            }
        };

        var taxRows = new List<TransactionTaxRow>
        {
            new(Guid.NewGuid(), "SalesInvoice", parentId, 1, "Base Duty", "On Net Total", 10m)
            {
                AccountId = row1AccountId
            },
            new(Guid.NewGuid(), "SalesInvoice", parentId, 2, "Surcharge on Duty", "On Previous Row Amount", 50m)
            {
                AccountId = row2AccountId,
                ReferenceRowIndex = 1
            }
        };

        var totals = svc.Calculate(items, taxRows);
        taxRows[0].TaxAmount.ShouldBe(10m);
        taxRows[1].TaxAmount.ShouldBe(5m);
        totals.TotalTax.ShouldBe(15m);
        totals.GrandTotal.ShouldBe(215m);
    }

    [Fact]
    public async Task StockRepostGuard_ZeroSleVoucher_AllowsCancelAndSkipsQueued_WhileSleVoucherBlocks_80bedf1791()
    {
        var repostRepo = Substitute.For<IRepository<RepostItemValuation, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();

        var companyId = Guid.NewGuid();
        var nonStockVoucherId = Guid.NewGuid();
        var stockVoucherId = Guid.NewGuid();

        var inProgressNoSle = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse, DateTime.Today)
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = nonStockVoucherId
        };
        inProgressNoSle.StartProcessing();

        var queuedNoSle = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse, DateTime.Today)
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = nonStockVoucherId
        };

        var inProgressWithSle = new RepostItemValuation(Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse, DateTime.Today)
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = stockVoucherId
        };
        inProgressWithSle.StartProcessing();

        var repostList = new List<RepostItemValuation> { inProgressNoSle, queuedNoSle, inProgressWithSle };
        repostRepo.GetQueryableAsync().Returns(Task.FromResult(repostList.AsQueryable()));

        var sleEntry = new StockLedgerEntry(
            Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today, quantityChange: 5m, valuationRate: 10m, balanceQuantity: 5m, balanceValue: 50m)
        {
            VoucherType = "PurchaseReceipt",
            VoucherId = stockVoucherId
        };
        var sleList = new List<StockLedgerEntry> { sleEntry };
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(sleList.AsQueryable()));

        var guard = new StockRepostGuardService(repostRepo, sleRepo);

        // Voucher with 0 SLEs does NOT throw even when InProgress repost exists, and marks Queued repost Skipped
        await guard.ValidateCanCancelVoucherAsync("PurchaseReceipt", nonStockVoucherId);
        queuedNoSle.Status.ShouldBe(RepostStatus.Skipped);

        // Voucher with SLEs DOES throw RepostAlreadyInProgress
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            guard.ValidateCanCancelVoucherAsync("PurchaseReceipt", stockVoucherId));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.RepostAlreadyInProgress);
    }

    [Fact]
    public void PaymentSchedule_ZeroPortionBaseOutstanding_KeepSettled_And_AmountDiscountScaling_0953a16dba_4bd25cf1df_240a3a97a3()
    {
        var invoiceId = Guid.NewGuid();

        // 1) Zero-portion fixed-amount row computes BasePaymentAmount & BaseOutstanding from exchangeRate (0953a16dba)
        var zeroPortion = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", invoiceId, DateTime.Today.AddDays(15), invoicePortion: 0m, paymentAmount: 250m);
        zeroPortion.SyncAmounts(grandTotal: 1000m, exchangeRate: 4.4m, keepSettled: false);
        zeroPortion.PaymentAmount.ShouldBe(250m);
        zeroPortion.BasePaymentAmount.ShouldBe(1100m);
        zeroPortion.BaseOutstanding.ShouldBe(1100m);

        // 2) keepSettled: true preserves PaidAmount instead of wiping settled installments (4bd25cf1df)
        var settledRow = new PaymentScheduleEntry(
            Guid.NewGuid(), "SalesInvoice", invoiceId, DateTime.Today.AddDays(30), invoicePortion: 50m, paymentAmount: 500m)
        {
            PaidAmount = 500m
        };
        settledRow.SyncAmounts(grandTotal: 1000m, exchangeRate: 2m, keepSettled: true);
        settledRow.PaidAmount.ShouldBe(500m);
        settledRow.Outstanding.ShouldBe(0m);
        settledRow.BaseOutstanding.ShouldBe(0m);

        // 3) MapScheduleEntries scales fixed "Amount" discount proportionally while preserving "Percentage" discount (240a3a97a3)
        var svc = new PaymentScheduleValidationService();
        var sourceRows = new List<PaymentScheduleEntry>
        {
            new(Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), DateTime.Today.AddDays(10), 60m, 600m)
            {
                DiscountType = "Amount",
                DiscountPercentage = 60m,
                DiscountValidTill = DateTime.Today.AddDays(5)
            },
            new(Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), DateTime.Today.AddDays(30), 40m, 400m)
            {
                DiscountType = "Percentage",
                DiscountPercentage = 5m,
                DiscountValidTill = DateTime.Today.AddDays(15)
            }
        };

        var mapped = svc.MapScheduleEntries(sourceRows, "SalesInvoice", invoiceId, targetGrandTotal: 500m, exchangeRate: 4m);
        mapped.Count.ShouldBe(2);

        // 600 -> 300 (half), so fixed Amount discount 60 -> 30, DiscountedAmount = 270
        mapped[0].PaymentAmount.ShouldBe(300m);
        mapped[0].BasePaymentAmount.ShouldBe(1200m);
        mapped[0].DiscountPercentage.ShouldBe(30m);
        mapped[0].DiscountedAmount.ShouldBe(270m);

        // 400 -> 200, Percentage 5% stays 5%, DiscountedAmount = 190
        mapped[1].PaymentAmount.ShouldBe(200m);
        mapped[1].DiscountPercentage.ShouldBe(5m);
        mapped[1].DiscountedAmount.ShouldBe(190m);
    }

    [Fact]
    public async Task PaymentReconciliation_UsesAccountSubTypeInsteadOfPartyType_ForEmployeePayableAndReceivable_7623dae273()
    {
        var pleList = new List<PaymentLedgerEntry>();
        var jeList = new List<JournalEntry>();

        var pleRepo = Substitute.For<IRepository<PaymentLedgerEntry, Guid>>();
        pleRepo.GetQueryableAsync().Returns(_ => Task.FromResult(pleList.AsQueryable()));
        pleRepo.InsertAsync(Arg.Any<PaymentLedgerEntry>(), Arg.Any<bool>())
            .Returns(ci =>
            {
                var p = ci.Arg<PaymentLedgerEntry>();
                pleList.Add(p);
                return Task.FromResult(p);
            });

        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var fxGainAccId = Guid.NewGuid();
        var fyId = Guid.NewGuid();

        var payableEmpAcc = new Account(Guid.NewGuid(), companyId, "2150", "Employee Payable", AccountType.Liability)
        {
            AccountSubType = AccountSubType.AccountsPayable,
            Currency = "USD"
        };
        var receivableEmpAcc = new Account(Guid.NewGuid(), companyId, "1150", "Employee Advance Receivable", AccountType.Asset)
        {
            AccountSubType = AccountSubType.AccountsReceivable,
            Currency = "USD"
        };

        var accounts = new List<Account> { payableEmpAcc, receivableEmpAcc };
        var accountRepo = Substitute.For<IRepository<Account, Guid>>();
        accountRepo.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(ci => Task.FromResult(accounts.FirstOrDefault(a => a.Id == ci.ArgAt<Guid>(0))));

        var company = new Company(companyId, "FX Recon Co")
        {
            ExchangeGainLossAccountId = fxGainAccId
        };
        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.GetAsync(companyId, Arg.Any<bool>()).Returns(Task.FromResult(company));

        var fy = new FiscalYear(fyId, companyId, "2020-2030", new DateTime(2020, 1, 1), new DateTime(2030, 12, 31));
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        fyRepo.GetQueryableAsync().Returns(Task.FromResult(new List<FiscalYear> { fy }.AsQueryable()));

        var siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();

        var payableJeVoucher = new JournalEntry(Guid.NewGuid(), companyId, fyId, DateTime.UtcNow.Date);
        payableJeVoucher.AddLine(payableEmpAcc.Id, amount: 450m, isDebit: false);
        payableJeVoucher.Lines.First().ExchangeRate = 4.5m;

        var receivableJeVoucher = new JournalEntry(Guid.NewGuid(), companyId, fyId, DateTime.UtcNow.Date);
        receivableJeVoucher.AddLine(receivableEmpAcc.Id, amount: 450m, isDebit: true);
        receivableJeVoucher.Lines.First().ExchangeRate = 4.5m;

        var sourceJes = new List<JournalEntry> { payableJeVoucher, receivableJeVoucher };
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        jeRepo.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(ci => Task.FromResult(sourceJes.FirstOrDefault(j => j.Id == ci.ArgAt<Guid>(0))));
        jeRepo.InsertAsync(Arg.Any<JournalEntry>(), Arg.Any<bool>())
            .Returns(ci =>
            {
                var created = ci.Arg<JournalEntry>();
                jeList.Add(created);
                return Task.FromResult(created);
            });

        var payPe = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Pay, DateTime.UtcNow.Date, 100m, Guid.NewGuid(), payableEmpAcc.Id)
        {
            ExchangeRate = 4.4m
        };
        var recPe = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Receive, DateTime.UtcNow.Date, 100m, receivableEmpAcc.Id, Guid.NewGuid())
        {
            ExchangeRate = 4.4m
        };
        var pes = new List<PaymentEntry> { payPe, recPe };
        peRepo.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(ci => Task.FromResult(pes.Single(p => p.Id == ci.ArgAt<Guid>(0))));
        peRepo.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(ci => Task.FromResult(pes.FirstOrDefault(p => p.Id == ci.ArgAt<Guid>(0))));

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetService<Volo.Abp.Guids.IGuidGenerator>(Arg.Any<Func<IServiceProvider, object>>())
            .Returns(Volo.Abp.Guids.SimpleGuidGenerator.Instance);
        lazySp.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>()
            .Returns(Volo.Abp.Guids.SimpleGuidGenerator.Instance);

        var pleService = new PaymentLedgerService(pleRepo, accountRepo)
        {
            LazyServiceProvider = lazySp
        };
        var engine = new PaymentReconciliationEngine(
            pleService, pleRepo, siRepo, piRepo, peRepo, jeRepo, fyRepo, companyRepo, accountRepo)
        {
            LazyServiceProvider = lazySp
        };

        // Verify AutoAllocate handles negative Payable Outstanding (-100m)
        var autoAlloc = PaymentReconciliationEngine.AutoAllocate(
            new List<UnreconciledPayment>
            {
                new() { VoucherType = "PaymentEntry", VoucherId = payPe.Id, TotalAmount = 100m, UnallocatedAmount = 100m }
            },
            new List<OutstandingVoucher>
            {
                new() { AccountId = payableEmpAcc.Id, VoucherType = "JournalEntry", VoucherId = payableJeVoucher.Id, Outstanding = -100m }
            });
        autoAlloc.Count.ShouldBe(1);
        autoAlloc[0].AllocatedAmount.ShouldBe(100m);

        // Seed PLEs: Employee Payable (-100 USD) & Employee Receivable (+100 USD)
        pleList.Add(new PaymentLedgerEntry(
            Guid.NewGuid(), companyId, DateTime.UtcNow.Date,
            payableEmpAcc.Id, "Employee", employeeId,
            "JournalEntry", payableJeVoucher.Id, "JournalEntry", payableJeVoucher.Id,
            amount: -450m, amountInAccountCurrency: -100m, accountCurrency: "USD"));

        pleList.Add(new PaymentLedgerEntry(
            Guid.NewGuid(), companyId, DateTime.UtcNow.Date,
            receivableEmpAcc.Id, "Employee", employeeId,
            "JournalEntry", receivableJeVoucher.Id, "JournalEntry", receivableJeVoucher.Id,
            amount: 450m, amountInAccountCurrency: 100m, accountCurrency: "USD"));

        // 1) Reconcile Employee Payable @ payment rate 4.4 vs voucher rate 4.5 -> Payable gain (+10 MYR: DR Payable 10, CR FX Gain 10)
        var payRes = await engine.ReconcileAndApplyAsync(
            companyId, "Employee", employeeId, payableEmpAcc.Id, "USD",
            new List<ReconciliationAllocation>
            {
                new()
                {
                    PaymentVoucherType = "PaymentEntry",
                    PaymentVoucherId = payPe.Id,
                    InvoiceVoucherType = "JournalEntry",
                    InvoiceVoucherId = payableJeVoucher.Id,
                    AllocatedAmount = 100m
                }
            });
        payRes.ReconciledCount.ShouldBe(1);
        (await pleService.GetOutstandingAsync("JournalEntry", payableJeVoucher.Id)).ShouldBe(0m);
        jeList.Count.ShouldBe(1);
        var payPartyLine = jeList[0].Lines.Single(l => l.AccountId == payableEmpAcc.Id);
        payPartyLine.IsDebit.ShouldBeTrue();
        payPartyLine.Amount.ShouldBe(10m);
        var payFxLine = jeList[0].Lines.Single(l => l.AccountId == fxGainAccId);
        payFxLine.IsDebit.ShouldBeFalse();
        payFxLine.Amount.ShouldBe(10m);

        // 2) Reconcile Employee Receivable @ payment rate 4.4 vs voucher rate 4.5 -> Receivable loss (-10 MYR: DR FX Gain/Loss 10, CR Receivable 10)
        var recRes = await engine.ReconcileAndApplyAsync(
            companyId, "Employee", employeeId, receivableEmpAcc.Id, "USD",
            new List<ReconciliationAllocation>
            {
                new()
                {
                    PaymentVoucherType = "PaymentEntry",
                    PaymentVoucherId = recPe.Id,
                    InvoiceVoucherType = "JournalEntry",
                    InvoiceVoucherId = receivableJeVoucher.Id,
                    AllocatedAmount = 100m
                }
            });
        recRes.ReconciledCount.ShouldBe(1);
        (await pleService.GetOutstandingAsync("JournalEntry", receivableJeVoucher.Id)).ShouldBe(0m);
        jeList.Count.ShouldBe(2);
        var recPartyLine = jeList[1].Lines.Single(l => l.AccountId == receivableEmpAcc.Id);
        recPartyLine.IsDebit.ShouldBeFalse();
        recPartyLine.Amount.ShouldBe(10m);
        var recFxLine = jeList[1].Lines.Single(l => l.AccountId == fxGainAccId);
        recFxLine.IsDebit.ShouldBeTrue();
        recFxLine.Amount.ShouldBe(10m);
    }

    [Fact]
    public void StockEntryManager_DisassemblyBatchPool_SubtractsPriorDisassemblies_And_ValidatesSplitScale_b9121c2188_54fcf4abd5()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var fgWhId = Guid.NewGuid();
        var rmWhId = Guid.NewGuid();
        var rmBatch1 = Guid.NewGuid();
        var rmBatch2 = Guid.NewGuid();
        var fgBatch1 = Guid.NewGuid();
        var fgBatch2 = Guid.NewGuid();

        var mfgEntry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.Today)
        {
            FgCompletedQty = 10m
        };
        mfgEntry.AddItem(rmItemId, 5m, sourceWarehouseId: rmWhId, targetWarehouseId: null, valuationRate: 2m, batchId: rmBatch1);
        mfgEntry.AddItem(rmItemId, 5m, sourceWarehouseId: rmWhId, targetWarehouseId: null, valuationRate: 2m, batchId: rmBatch2);
        mfgEntry.AddItem(fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: fgWhId, valuationRate: 10m, isFinishedItem: true, batchId: fgBatch1);
        mfgEntry.AddItem(fgItemId, 5m, sourceWarehouseId: null, targetWarehouseId: fgWhId, valuationRate: 10m, isFinishedItem: true, batchId: fgBatch2);
        mfgEntry.Submit();
        mfgEntry.Post();

        // Prior submitted Disassemble already reversed 5 units (consuming fgBatch1 and returning rmBatch1)
        var priorDisassemble = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Disassemble, DateTime.Today)
        {
            FgCompletedQty = 5m
        };
        priorDisassemble.AddItem(fgItemId, 5m, sourceWarehouseId: fgWhId, targetWarehouseId: null, isFinishedItem: true, batchId: fgBatch1);
        priorDisassemble.AddItem(rmItemId, 5m, sourceWarehouseId: null, targetWarehouseId: rmWhId, valuationRate: 2m, batchId: rmBatch1);
        priorDisassemble.Submit();
        priorDisassemble.Post();

        // Cancelled Disassemble must be ignored
        var cancelledDisassemble = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Disassemble, DateTime.Today)
        {
            FgCompletedQty = 5m
        };
        cancelledDisassemble.AddItem(fgItemId, 5m, sourceWarehouseId: fgWhId, targetWarehouseId: null, isFinishedItem: true, batchId: fgBatch2);
        cancelledDisassemble.AddItem(rmItemId, 5m, sourceWarehouseId: null, targetWarehouseId: rmWhId, valuationRate: 2m, batchId: rmBatch2);
        cancelledDisassemble.Submit();
        cancelledDisassemble.Post();
        cancelledDisassemble.Cancel();

        var pool = StockEntryManager.BuildAvailableDisassemblyBatchPool(
            new[] { mfgEntry },
            new[] { priorDisassemble, cancelledDisassemble });

        var nextRmBatches = StockEntryManager.CollectDisassemblyBatches(pool, rmItemId, rmWhId, isFinishedItem: false, requiredQty: 5m);
        nextRmBatches.Count.ShouldBe(1);
        nextRmBatches[0].BatchId.ShouldBe(rmBatch2);
        nextRmBatches[0].Qty.ShouldBe(5m);

        var nextFgBatches = StockEntryManager.CollectDisassemblyBatches(pool, fgItemId, fgWhId, isFinishedItem: true, requiredQty: 5m);
        nextFgBatches.Count.ShouldBe(1);
        nextFgBatches[0].BatchId.ShouldBe(fgBatch2);
        nextFgBatches[0].Qty.ShouldBe(5m);

        // ValidateDisassembleScaleFactor supports multi-batch source & multi-batch disassembly rows
        var fullDisassemble = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Disassemble, DateTime.Today)
        {
            FgCompletedQty = 8m
        };
        fullDisassemble.AddItem(fgItemId, 8m, sourceWarehouseId: fgWhId, targetWarehouseId: null, isFinishedItem: true);
        fullDisassemble.AddItem(rmItemId, 5m, sourceWarehouseId: null, targetWarehouseId: rmWhId, batchId: rmBatch1);
        fullDisassemble.AddItem(rmItemId, 3m, sourceWarehouseId: null, targetWarehouseId: rmWhId, batchId: rmBatch2);

        var manager = new StockEntryManager(
            Substitute.For<IRepository<Warehouse, Guid>>(),
            Substitute.For<IRepository<Item, Guid>>(),
            null!);
        Should.NotThrow(() =>
            manager.ValidateDisassembleScaleFactor(fullDisassemble.Items, mfgEntry.Items, disassembleQty: 8m, sourceFgQty: 10m));
    }

    [Fact]
    public void Dunning_UnpaidBaseAmount_ScheduleDeduction_And_AccountCurrencyOutstanding_7ca8bc3290_8a09633536()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-USD-001", DateTime.Today.AddDays(-40))
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m
        };
        invoice.AddItem(itemId, "Consulting", quantity: 2m, unitPrice: 100m, taxAmount: 0m);
        invoice.Submit();
        // Simulate unallocated 60 USD payment recorded at invoice header level without schedule term allocation
        invoice.AmountPaid = 60m;

        var sched1 = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", invoice.Id, DateTime.Today.AddDays(-30), 50m, 100m);
        var sched2 = new PaymentScheduleEntry(Guid.NewGuid(), "SalesInvoice", invoice.Id, DateTime.Today.AddDays(-10), 50m, 100m);
        var schedules = new List<PaymentScheduleEntry> { sched2, sched1 };

        // In transaction currency (USD): 60 USD deducted from oldest schedule (sched1) -> sched1 = 40 USD, sched2 = 100 USD
        var mapTxn = DunningManager.GetPaymentScheduleOutstanding(invoice, schedules, "MYR", inAccountCurrency: false);
        mapTxn[sched1.Id].ShouldBe(40m);
        mapTxn[sched2.Id].ShouldBe(100m);

        // In receivable account currency (MYR @ 4.5): sched1 = 180 MYR, sched2 = 450 MYR
        var mapAcc = DunningManager.GetPaymentScheduleOutstanding(invoice, schedules, "MYR", inAccountCurrency: true);
        mapAcc[sched1.Id].ShouldBe(180m);
        mapAcc[sched2.Id].ShouldBe(450m);

        var dunning = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.Today, dunningLevel: 1)
        {
            Currency = "USD",
            ConversionRate = 4.5m,
            DunningFee = 20m,
            IncomeAccountId = Guid.NewGuid()
        };
        dunning.AddOverduePayment(invoice.Id, 100m, sched1.DueDate, overdueDays: 30, paymentScheduleId: sched1.Id);
        dunning.AddOverduePayment(invoice.Id, 100m, sched2.DueDate, overdueDays: 10, paymentScheduleId: sched2.Id);
        dunning.RecordDunningPayment(5m);

        dunning.UnpaidDunningAmount.ShouldBe(15m);
        dunning.UnpaidBaseDunningAmount.ShouldBe(67.50m);

        var row1LiveMyr = DunningManager.GetCurrentOverdueOutstanding(
            dunning.OverduePayments[0], invoice, schedules, "MYR", inAccountCurrency: true);
        var row2LiveMyr = DunningManager.GetCurrentOverdueOutstanding(
            dunning.OverduePayments[1], invoice, schedules, "MYR", inAccountCurrency: true);

        row1LiveMyr.ShouldBe(180m);
        row2LiveMyr.ShouldBe(450m);
    }
}
