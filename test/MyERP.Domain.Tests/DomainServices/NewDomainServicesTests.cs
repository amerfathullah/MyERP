using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.DomainServices;

// ═══════════════════════════════════════════════════════════════════
// SubscriptionBillingEngine Tests
// ═══════════════════════════════════════════════════════════════════

public class SubscriptionBillingEngineTests
{
    private static Subscription CreateSub(DateTime start, string interval = "Monthly",
        int trialDays = 0, DateTime? endDate = null, int graceDays = 0, int dueAfter = 0)
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", start, interval);
        sub.TrialPeriodDays = trialDays;
        sub.CancelAfterGraceDays = graceDays;
        sub.DaysUntilDue = dueAfter;
        if (trialDays > 0)
            sub.TrialEndDate = start.AddDays(trialDays);
        if (endDate.HasValue)
            sub.EndDate = endDate;
        return sub;
    }

    // --- Status Determination ---

    [Fact]
    public void DetermineStatus_ActiveWithNoOutstanding_ReturnsActive()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        var status = engine.DetermineStatus(sub, new DateTime(2026, 6, 15), false, false);
        status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void DetermineStatus_InTrialPeriod_ReturnsActive()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1), trialDays: 30);
        var status = engine.DetermineStatus(sub, new DateTime(2026, 1, 15), false, false);
        status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void DetermineStatus_PastEndDateNoOutstanding_ReturnsCompleted()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1), endDate: new DateTime(2026, 6, 30));
        var status = engine.DetermineStatus(sub, new DateTime(2026, 7, 15), false, false);
        status.ShouldBe(SubscriptionStatus.Completed);
    }

    [Fact]
    public void DetermineStatus_PastGracePeriodWithOutstanding_ReturnsCancelled()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1), graceDays: 7);
        sub.AdvancePeriod(); // sets current period
        sub.AdvancePeriod(); // move to next
        var status = engine.DetermineStatus(sub, new DateTime(2026, 6, 15), true, false);
        status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void DetermineStatus_FullyRefunded_ReturnsCancelled()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        var status = engine.DetermineStatus(sub, new DateTime(2026, 6, 15), true, true);
        status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    // --- Invoice Due Check ---

    [Fact]
    public void IsInvoiceDue_ActiveWithNoPeriod_ReturnsTrue()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        engine.IsInvoiceDue(sub, new DateTime(2026, 1, 1)).ShouldBeTrue();
    }

    [Fact]
    public void IsInvoiceDue_CancelledSubscription_ReturnsFalse()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.Cancel();
        engine.IsInvoiceDue(sub, new DateTime(2026, 2, 1)).ShouldBeFalse();
    }

    // --- Late Fire Cap ---

    [Fact]
    public void IsWithinLateFireCap_WithinOneMonth_ReturnsTrue()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.AdvancePeriod(); // end = 2026-01-31
        engine.IsWithinLateFireCap(sub, new DateTime(2026, 2, 15)).ShouldBeTrue();
    }

    [Fact]
    public void IsWithinLateFireCap_PastOneMonth_ReturnsFalse()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.AdvancePeriod(); // end = 2026-01-31
        engine.IsWithinLateFireCap(sub, new DateTime(2026, 3, 15)).ShouldBeFalse();
    }

    // --- Proration ---

    [Fact]
    public void ProrationFactor_FullPeriod_Returns1()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var factor = engine.CalculateProrationFactor(
            CreateSub(new DateTime(2026, 1, 1)),
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
        factor.ShouldBe(1m);
    }

    [Fact]
    public void ProrationFactor_HalfPeriod_ReturnsHalf()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 31);
        var cancel = new DateTime(2026, 1, 16); // 16 days out of 31
        var factor = engine.CalculateProrationFactor(CreateSub(start), start, end, cancel);
        factor.ShouldBeInRange(0.51m, 0.52m);
    }

    // --- Invoice Items ---

    [Fact]
    public void BuildInvoiceItems_NormalPeriod_UsesFullRates()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Plan A");
        sub.AddPlan(Guid.NewGuid(), 2, 50m, "Plan B");

        var items = engine.BuildInvoiceItems(sub, new DateTime(2026, 2, 1));
        items.Count.ShouldBe(2);
        items[0].Rate.ShouldBe(100m);
        items[1].Rate.ShouldBe(50m);
    }

    [Fact]
    public void BuildInvoiceItems_TrialPeriod_AllRatesZero()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1), trialDays: 30);
        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Plan A");

        var items = engine.BuildInvoiceItems(sub, new DateTime(2026, 1, 15)); // within trial
        items[0].Rate.ShouldBe(0m);
    }

    [Fact]
    public void BuildInvoiceItems_WithProration_ReducesRates()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Plan A");

        var items = engine.BuildInvoiceItems(sub, new DateTime(2026, 2, 1), 0.5m);
        items[0].Rate.ShouldBe(50m);
    }

    // --- Period Advancement ---

    [Fact]
    public void AdvancePeriodAndCheckCompletion_NotPastEndDate_ReturnsFalse()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31));
        sub.AdvancePeriod(); // set initial period
        var completed = engine.AdvancePeriodAndCheckCompletion(sub);
        completed.ShouldBeFalse();
    }

    [Fact]
    public void AdvancePeriodAndCheckCompletion_PastEndDate_AutoCancels()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1), endDate: new DateTime(2026, 2, 28));
        // Advance past end date
        for (int i = 0; i < 3; i++) sub.AdvancePeriod();
        var completed = engine.AdvancePeriodAndCheckCompletion(sub);
        completed.ShouldBeTrue();
        sub.Status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void GenerateInvoiceReference_FormatsCorrectly()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.SubscriptionNumber = "SUB-001";
        sub.AdvancePeriod();
        var ref_ = engine.GenerateInvoiceReference(sub);
        ref_.ShouldBe("SUB-SUB-001-20260101");
    }
}

// ═══════════════════════════════════════════════════════════════════
// TaxWithholdingService Tests
// ═══════════════════════════════════════════════════════════════════

public class TaxWithholdingServiceTests
{
    private readonly TaxWithholdingService _service = new(null!);

    [Fact]
    public void CalculateWithholding_BelowThreshold_NoTax()
    {
        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 5000m,
            cumulativeInvoicedInFY: 10000m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 50000m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0);

        result.ThresholdCrossed.ShouldBeFalse();
        result.WithheldAmount.ShouldBe(0);
    }

    [Fact]
    public void CalculateWithholding_SingleThresholdExceeded_DeductsTax()
    {
        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 15000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 0m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0);

        result.ThresholdCrossed.ShouldBeTrue();
        result.WithheldAmount.ShouldBe(1500m); // 15000 × 10%
    }

    [Fact]
    public void CalculateWithholding_CumulativeExceeded_DeductsTax()
    {
        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 5000m,
            cumulativeInvoicedInFY: 48000m,
            standardRate: 10m,
            singleThreshold: 0m,
            cumulativeThreshold: 50000m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0);

        result.ThresholdCrossed.ShouldBeTrue();
        result.WithheldAmount.ShouldBe(500m); // 5000 × 10%
    }

    [Fact]
    public void CalculateWithholding_TaxOnExcess_OnlyTaxesExcess()
    {
        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 5000m,
            cumulativeInvoicedInFY: 48000m,
            standardRate: 10m,
            singleThreshold: 0m,
            cumulativeThreshold: 50000m,
            taxOnExcessAmount: true,
            previouslyDeductedTDS: 0);

        result.ThresholdCrossed.ShouldBeTrue();
        result.TaxableAmount.ShouldBe(3000m); // (48000+5000) - 50000 = 3000
        result.WithheldAmount.ShouldBe(300m); // 3000 × 10%
    }

    [Fact]
    public void CalculateWithholding_WithPreviousDeduction_SubtractsPrior()
    {
        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 15000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 0m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 500m);

        result.WithheldAmount.ShouldBe(1000m); // 1500 - 500
    }

    [Fact]
    public void CalculateWithholding_WithLDC_UsesReducedRate()
    {
        var ldc = new LdcDetails { CertificateNumber = "LDC-001", LdcRate = 5m, UnutilizedAmount = 50000m };

        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 15000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 0m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0,
            ldc: ldc);

        result.HasLDC.ShouldBeTrue();
        result.EffectiveRate.ShouldBe(5m);
        result.WithheldAmount.ShouldBe(750m); // 15000 × 5%
    }

    [Fact]
    public void CalculateWithholding_LDCFullyUtilized_UsesStandardRate()
    {
        var ldc = new LdcDetails { CertificateNumber = "LDC-001", LdcRate = 5m, UnutilizedAmount = 0m };

        var result = _service.CalculateWithholding(
            currentInvoiceNetTotal: 15000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 0m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0,
            ldc: ldc);

        result.HasLDC.ShouldBeFalse();
        result.EffectiveRate.ShouldBe(10m);
    }

    [Fact]
    public void DistributeTdsAcrossItems_ProportionalWithRounding()
    {
        var items = new List<(Guid, decimal)>
        {
            (Guid.NewGuid(), 300m),
            (Guid.NewGuid(), 200m),
            (Guid.NewGuid(), 500m),
        };

        var dist = TaxWithholdingService.DistributeTdsAcrossItems(100m, items);
        dist.Count.ShouldBe(3);
        dist.Values.Sum().ShouldBe(100m); // rounding absorbed by last item
    }

    [Fact]
    public void DistributeTdsAcrossItems_SingleItem_GetsAll()
    {
        var itemId = Guid.NewGuid();
        var dist = TaxWithholdingService.DistributeTdsAcrossItems(
            150m, new List<(Guid, decimal)> { (itemId, 1000m) });
        dist[itemId].ShouldBe(150m);
    }
}

// ═══════════════════════════════════════════════════════════════════
// InvoiceDiscountingService Tests
// ═══════════════════════════════════════════════════════════════════

public class InvoiceDiscountingServiceTests
{
    [Fact]
    public void CalculateDiscountCharge_StandardCalculation()
    {
        var service = new InvoiceDiscountingService(null!);
        // 100,000 × 8% × 90/365
        var charge = service.CalculateDiscountCharge(100_000m, 8m, 90);
        charge.ShouldBeGreaterThan(0);
        charge.ShouldBe(Math.Round(100_000m * 8m / 100m * 90m / 365m, 2));
    }

    [Fact]
    public void CalculateDiscountCharge_ZeroDays_ReturnsZero()
    {
        var service = new InvoiceDiscountingService(null!);
        service.CalculateDiscountCharge(100_000m, 8m, 0).ShouldBe(0);
    }

    [Fact]
    public void CalculateDisbursementAmount_CorrectlyDeducts()
    {
        var service = new InvoiceDiscountingService(null!);
        service.CalculateDisbursementAmount(100_000m, 2_000m).ShouldBe(98_000m);
    }

    [Fact]
    public void DetermineStatus_CreditOnLoan_SanctionedToDisbursed()
    {
        var service = new InvoiceDiscountingService(null!);
        var loanAcct = Guid.NewGuid();
        var lines = new List<JournalEntryLine>
        {
            CreateJeLine(loanAcct, 0, 100_000), // credit on loan
        };

        var newStatus = service.DetermineStatusFromJournalEntry(
            InvoiceDiscountingStatus.Sanctioned, loanAcct, lines, true);
        newStatus.ShouldBe(InvoiceDiscountingStatus.Disbursed);
    }

    [Fact]
    public void DetermineStatus_DebitOnLoan_DisbursedToSettled()
    {
        var service = new InvoiceDiscountingService(null!);
        var loanAcct = Guid.NewGuid();
        var lines = new List<JournalEntryLine>
        {
            CreateJeLine(loanAcct, 100_000, 0), // debit on loan
        };

        var newStatus = service.DetermineStatusFromJournalEntry(
            InvoiceDiscountingStatus.Disbursed, loanAcct, lines, true);
        newStatus.ShouldBe(InvoiceDiscountingStatus.Settled);
    }

    [Fact]
    public void DetermineStatus_CancelDisbursement_RevertToSanctioned()
    {
        var service = new InvoiceDiscountingService(null!);
        var loanAcct = Guid.NewGuid();
        var lines = new List<JournalEntryLine>
        {
            CreateJeLine(loanAcct, 0, 100_000), // credit on loan
        };

        var newStatus = service.DetermineStatusFromJournalEntry(
            InvoiceDiscountingStatus.Disbursed, loanAcct, lines, false);
        newStatus.ShouldBe(InvoiceDiscountingStatus.Sanctioned);
    }

    [Fact]
    public void DetermineStatus_NoLoanRow_NoChange()
    {
        var service = new InvoiceDiscountingService(null!);
        var loanAcct = Guid.NewGuid();
        var otherAcct = Guid.NewGuid();
        var lines = new List<JournalEntryLine>
        {
            CreateJeLine(otherAcct, 100, 0),
        };

        var newStatus = service.DetermineStatusFromJournalEntry(
            InvoiceDiscountingStatus.Sanctioned, loanAcct, lines, true);
        newStatus.ShouldBe(InvoiceDiscountingStatus.Sanctioned);
    }

    [Fact]
    public void ValidateInvoices_AlreadyDiscounted_Throws()
    {
        var invoices = new List<InvoiceForDiscounting>
        {
            new() { InvoiceId = Guid.NewGuid(), InvoiceNumber = "SI-001",
                OutstandingAmount = 1000m, IsAlreadyDiscounted = true },
        };

        Should.Throw<BusinessException>(() =>
            InvoiceDiscountingService.ValidateInvoicesForDiscounting(invoices));
    }

    [Fact]
    public void ValidateInvoices_ZeroOutstanding_Throws()
    {
        var invoices = new List<InvoiceForDiscounting>
        {
            new() { InvoiceId = Guid.NewGuid(), InvoiceNumber = "SI-001",
                OutstandingAmount = 0, IsAlreadyDiscounted = false },
        };

        Should.Throw<BusinessException>(() =>
            InvoiceDiscountingService.ValidateInvoicesForDiscounting(invoices));
    }

    [Fact]
    public void BuildDisbursementGlEntries_ThreeLines()
    {
        var entries = InvoiceDiscountingService.BuildDisbursementGlEntries(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100_000m, 2_000m, 98_000m);

        entries.Count.ShouldBe(3);
        entries.Sum(e => e.Debit).ShouldBe(100_000m); // 98000 + 2000
        entries.Sum(e => e.Credit).ShouldBe(100_000m);
    }

    [Fact]
    public void BuildSettlementGlEntries_TwoLines()
    {
        var entries = InvoiceDiscountingService.BuildSettlementGlEntries(
            Guid.NewGuid(), Guid.NewGuid(), 100_000m);

        entries.Count.ShouldBe(2);
        entries.Sum(e => e.Debit).ShouldBe(100_000m);
        entries.Sum(e => e.Credit).ShouldBe(100_000m);
    }

    private static JournalEntryLine CreateJeLine(Guid accountId, decimal debit, decimal credit)
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today);
        // JournalEntryLine stores Amount + IsDebit (not separate debit/credit fields)
        if (debit > 0)
            je.AddLine(accountId, debit, true);
        else
            je.AddLine(accountId, credit, false);
        return je.Lines.Last();
    }
}

// ═══════════════════════════════════════════════════════════════════
// StockClosingService Tests
// ═══════════════════════════════════════════════════════════════════

public class StockClosingServiceTests
{
    [Fact]
    public void StockClosingEntry_AddBalance_StoresSnapshot()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30));
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        entry.AddBalance(itemId, warehouseId, 100m, 5000m, 50m, "[{\"qty\":100,\"rate\":50}]");

        entry.Balances.Count.ShouldBe(1);
        var bal = entry.Balances.First();
        bal.ItemId.ShouldBe(itemId);
        bal.Qty.ShouldBe(100m);
        bal.StockValue.ShouldBe(5000m);
        bal.ValuationRate.ShouldBe(50m);
        bal.FifoQueue.ShouldNotBeNull();
    }

    [Fact]
    public void StockClosingEntry_Submit_CalculatesTotals()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 200m, 8000m, 40m);

        entry.Submit();

        entry.Status.ShouldBe(StockClosingStatus.Submitted);
        entry.TotalEntries.ShouldBe(2);
        entry.TotalStockValue.ShouldBe(13000m);
    }

    [Fact]
    public void StockClosingEntry_Submit_Empty_Throws()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30));

        Should.Throw<BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void StockClosingEntry_AddBalance_AfterSubmit_Throws()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();

        Should.Throw<BusinessException>(() =>
            entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 50m, 2500m, 50m));
    }

    [Fact]
    public void StockClosingEntry_IncrementalReference()
    {
        var previousId = Guid.NewGuid();
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 6, 30));
        entry.PreviousClosingEntryId = previousId;
        entry.ScannedFromDate = new DateTime(2026, 4, 1);

        entry.PreviousClosingEntryId.ShouldBe(previousId);
        entry.ScannedFromDate.ShouldBe(new DateTime(2026, 4, 1));
    }
}

// ═══════════════════════════════════════════════════════════════════
// PaymentReconciliationEngine Tests
// ═══════════════════════════════════════════════════════════════════

public class PaymentReconciliationEngineTests
{
    [Fact]
    public void CalculateExchangeGainLoss_SameRate_ReturnsZero()
    {
        var gainLoss = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            1000m, 4.5m, 4.5m);
        gainLoss.ShouldBe(0);
    }

    [Fact]
    public void CalculateExchangeGainLoss_HigherPaymentRate_ReturnsGain()
    {
        var gainLoss = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            1000m, 4.6m, 4.5m);
        gainLoss.ShouldBe(100m); // 1000 × (4.6 - 4.5)
    }

    [Fact]
    public void CalculateExchangeGainLoss_LowerPaymentRate_ReturnsLoss()
    {
        var gainLoss = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            1000m, 4.4m, 4.5m);
        gainLoss.ShouldBe(-100m); // 1000 × (4.4 - 4.5)
    }

    [Fact]
    public void ReconciliationResult_NoErrors_HasErrorsFalse()
    {
        var result = new ReconciliationResult { ReconciledCount = 3 };
        result.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void ReconciliationResult_WithErrors_HasErrorsTrue()
    {
        var result = new ReconciliationResult();
        result.Errors.Add(new ReconciliationError
        {
            InvoiceVoucherId = Guid.NewGuid(),
            Message = "Stale outstanding",
        });
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void ReconciliationAllocation_StoresFields()
    {
        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var alloc = new ReconciliationAllocation
        {
            PaymentVoucherType = "PaymentEntry",
            PaymentVoucherId = paymentId,
            InvoiceVoucherType = "SalesInvoice",
            InvoiceVoucherId = invoiceId,
            AllocatedAmount = 5000m,
        };
        alloc.PaymentVoucherId.ShouldBe(paymentId);
        alloc.InvoiceVoucherId.ShouldBe(invoiceId);
        alloc.AllocatedAmount.ShouldBe(5000m);
    }
}
