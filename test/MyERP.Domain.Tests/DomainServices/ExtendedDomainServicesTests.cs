using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.DomainServices;

// ═══════════════════════════════════════════════════════════════════
// InvoiceDiscountingService — Additional Tests
// ═══════════════════════════════════════════════════════════════════

public class InvoiceDiscountingExtendedTests
{
    private readonly InvoiceDiscountingService _service = new(null!);

    [Fact]
    public void CalculateDiscountCharge_365Days_EqualsAnnualRate()
    {
        // Full year = total outstanding × rate%
        var charge = _service.CalculateDiscountCharge(100_000m, 8m, 365);
        charge.ShouldBe(8_000m); // 100K × 8% × 365/365
    }

    [Fact]
    public void CalculateDiscountCharge_30Days()
    {
        var charge = _service.CalculateDiscountCharge(500_000m, 12m, 30);
        var expected = Math.Round(500_000m * 12m / 100m * 30m / 365m, 2);
        charge.ShouldBe(expected);
    }

    [Fact]
    public void CalculateDiscountCharge_NegativeRate_ReturnsZero()
    {
        _service.CalculateDiscountCharge(100_000m, -5m, 90).ShouldBe(0);
    }

    [Fact]
    public void ValidateInvoices_ValidList_DoesNotThrow()
    {
        var invoices = new List<InvoiceForDiscounting>
        {
            new() { InvoiceId = Guid.NewGuid(), InvoiceNumber = "SI-001",
                OutstandingAmount = 5000m, IsAlreadyDiscounted = false },
            new() { InvoiceId = Guid.NewGuid(), InvoiceNumber = "SI-002",
                OutstandingAmount = 3000m, IsAlreadyDiscounted = false },
        };

        Should.NotThrow(() => InvoiceDiscountingService.ValidateInvoicesForDiscounting(invoices));
    }

    [Fact]
    public void BuildDisbursementGlEntries_BalancesDebitAndCredit()
    {
        var bank = Guid.NewGuid();
        var expense = Guid.NewGuid();
        var loan = Guid.NewGuid();
        var entries = InvoiceDiscountingService.BuildDisbursementGlEntries(
            bank, expense, loan, 50_000m, 1_200m, 48_800m);

        entries.Sum(e => e.Debit).ShouldBe(entries.Sum(e => e.Credit));
    }
}

// ═══════════════════════════════════════════════════════════════════
// StockClosingService — Additional Tests
// ═══════════════════════════════════════════════════════════════════

public class StockClosingExtendedTests
{
    [Fact]
    public void StockClosingEntry_Cancel_FromSubmitted_Succeeds()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();
        entry.Cancel();
        entry.Status.ShouldBe(StockClosingStatus.Cancelled);
    }

    [Fact]
    public void StockClosingEntry_Cancel_FromDraft_Throws()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 6, 30));
        Should.Throw<BusinessException>(() => entry.Cancel());
    }

    [Fact]
    public void StockClosingEntry_DoubleSubmit_Throws()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();
        Should.Throw<BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void StockClosingEntry_MultiItemWarehouse_CalculatesTotals()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 200m, 8000m, 40m);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 50m, 2500m, 50m);

        entry.Submit();
        entry.TotalEntries.ShouldBe(3);
        entry.TotalStockValue.ShouldBe(15500m);
    }

    [Fact]
    public void StockClosingBalance_PreservesFields()
    {
        var entry = new StockClosingEntry(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 6, 30));
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var fifo = "[{\"qty\":50,\"rate\":100}]";

        entry.AddBalance(itemId, whId, 50m, 5000m, 100m, fifo);

        var balance = entry.Balances.First();
        balance.ItemId.ShouldBe(itemId);
        balance.WarehouseId.ShouldBe(whId);
        balance.Qty.ShouldBe(50m);
        balance.StockValue.ShouldBe(5000m);
        balance.ValuationRate.ShouldBe(100m);
        balance.FifoQueue.ShouldBe(fifo);
    }
}

// ═══════════════════════════════════════════════════════════════════
// SubscriptionBillingEngine — Additional Tests
// ═══════════════════════════════════════════════════════════════════

public class SubscriptionBillingExtendedTests
{
    private readonly SubscriptionBillingEngine _engine = new(null!);

    [Fact]
    public void DetermineStatus_Unpaid_WhenOutstandingAndPastDue()
    {
        var sub = CreateSub(new DateTime(2026, 1, 1), dueAfter: 7);
        sub.AdvancePeriod(); // period Jan 1 - Jan 31
        // Simulate: as of Feb 15, outstanding, within grace but past due
        var status = _engine.DetermineStatus(sub, new DateTime(2026, 2, 15), true, false);
        status.ShouldBe(SubscriptionStatus.Unpaid);
    }

    [Fact]
    public void BuildInvoiceItems_EmptyPlans_ReturnsEmpty()
    {
        var sub = CreateSub(new DateTime(2026, 1, 1));
        var items = _engine.BuildInvoiceItems(sub, new DateTime(2026, 2, 1));
        items.ShouldBeEmpty();
    }

    [Fact]
    public void BuildInvoiceItems_PostTrialPeriod_UsesFullRates()
    {
        var sub = CreateSub(new DateTime(2026, 1, 1), trialDays: 7);
        sub.AddPlan(Guid.NewGuid(), 1, 200m, "Premium");

        // After trial (day 15, trial ended day 8)
        var items = _engine.BuildInvoiceItems(sub, new DateTime(2026, 1, 15));
        items[0].Rate.ShouldBe(200m);
    }

    [Fact]
    public void IsInvoiceDue_WithPeriod_BeforeStart_ReturnsFalse()
    {
        var sub = CreateSub(new DateTime(2026, 3, 1));
        sub.AdvancePeriod(); // March period
        _engine.IsInvoiceDue(sub, new DateTime(2026, 2, 15)).ShouldBeFalse();
    }

    [Fact]
    public void ProrationFactor_CancelledOnDayOne_ReturnsFraction()
    {
        var factor = _engine.CalculateProrationFactor(
            CreateSub(new DateTime(2026, 1, 1)),
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            cancellationDate: new DateTime(2026, 1, 1)); // Cancelled first day
        // 1 day out of 31
        factor.ShouldBeInRange(0.03m, 0.04m);
    }

    [Fact]
    public void IsWithinLateFireCap_ExactlyOneCycle_ReturnsTrue()
    {
        var sub = CreateSub(new DateTime(2026, 1, 1));
        sub.AdvancePeriod(); // end = Jan 31
        // Exactly 1 month after period end
        _engine.IsWithinLateFireCap(sub, new DateTime(2026, 2, 28)).ShouldBeTrue();
    }

    private static Subscription CreateSub(DateTime start, int trialDays = 0, int dueAfter = 0)
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", start, "Monthly");
        sub.TrialPeriodDays = trialDays;
        sub.DaysUntilDue = dueAfter;
        if (trialDays > 0)
            sub.TrialEndDate = start.AddDays(trialDays);
        return sub;
    }
}

// ═══════════════════════════════════════════════════════════════════
// PaymentReconciliationEngine — Additional Tests
// ═══════════════════════════════════════════════════════════════════

public class PaymentReconciliationExtendedTests
{
    [Fact]
    public void ExchangeGainLoss_LargeAmounts_PreciseCalculation()
    {
        // Test with realistic multi-currency amounts
        var gl = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            50_000m, 4.5123m, 4.4987m);
        gl.ShouldBe(Math.Round(50_000m * (4.5123m - 4.4987m), 2));
    }

    [Fact]
    public void ExchangeGainLoss_ZeroAmount_ReturnsZero()
    {
        var gl = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            0m, 4.5m, 4.0m);
        gl.ShouldBe(0);
    }

    [Fact]
    public void ReconciliationResult_MultipleErrors_TracksAll()
    {
        var result = new ReconciliationResult();
        result.Errors.Add(new ReconciliationError { InvoiceVoucherId = Guid.NewGuid(), Message = "Error 1" });
        result.Errors.Add(new ReconciliationError { InvoiceVoucherId = Guid.NewGuid(), Message = "Error 2" });
        result.ReconciledCount = 3;
        result.TotalAllocated = 15000m;

        result.HasErrors.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.ReconciledCount.ShouldBe(3);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TaxWithholdingService — Extended Calculation Tests
// ═══════════════════════════════════════════════════════════════════

public class TaxWithholdingExtendedTests
{
    private readonly TaxWithholdingService _svc = new(null!);

    [Fact]
    public void CalculateWithholding_BothThresholdsExceeded_UsesInvoiceAmount()
    {
        var result = _svc.CalculateWithholding(
            currentInvoiceNetTotal: 20000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 15000m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0);

        result.ThresholdCrossed.ShouldBeTrue();
        result.WithheldAmount.ShouldBe(2000m); // 20000 × 10%
    }

    [Fact]
    public void CalculateWithholding_ExcessOnly_CumulativeThreshold()
    {
        var result = _svc.CalculateWithholding(
            currentInvoiceNetTotal: 10000m,
            cumulativeInvoicedInFY: 45000m,
            standardRate: 15m,
            singleThreshold: 0m,
            cumulativeThreshold: 50000m,
            taxOnExcessAmount: true,
            previouslyDeductedTDS: 0);

        // Excess = (45000+10000) - 50000 = 5000
        result.TaxableAmount.ShouldBe(5000m);
        result.WithheldAmount.ShouldBe(750m); // 5000 × 15%
    }

    [Fact]
    public void CalculateWithholding_PreviousDeduction_ExceedsCalculated_ReturnsZero()
    {
        var result = _svc.CalculateWithholding(
            currentInvoiceNetTotal: 15000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 10000m,
            cumulativeThreshold: 0m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 2000m); // Already deducted more than current TDS

        result.ThresholdCrossed.ShouldBeTrue();
        result.WithheldAmount.ShouldBe(0); // Max(0, 1500 - 2000) = 0
    }

    [Fact]
    public void CalculateWithholding_LDCCappedByUtilization()
    {
        var ldc = new LdcDetails { CertificateNumber = "LDC-002", LdcRate = 2m, UnutilizedAmount = 5000m };

        var result = _svc.CalculateWithholding(
            currentInvoiceNetTotal: 20000m,
            cumulativeInvoicedInFY: 0m,
            standardRate: 10m,
            singleThreshold: 1m,
            cumulativeThreshold: 0m,
            taxOnExcessAmount: false,
            previouslyDeductedTDS: 0,
            ldc: ldc);

        // Taxable amount capped by LDC unutilized: min(20000, 5000) = 5000
        result.TaxableAmount.ShouldBe(5000m);
        result.WithheldAmount.ShouldBe(100m); // 5000 × 2%
        result.HasLDC.ShouldBeTrue();
    }

    [Fact]
    public void DistributeTds_ZeroNetTotal_ReturnsEmpty()
    {
        var items = new List<(Guid, decimal)>
        {
            (Guid.NewGuid(), 0m),
            (Guid.NewGuid(), 0m),
        };

        var dist = TaxWithholdingService.DistributeTdsAcrossItems(100m, items);
        dist.ShouldBeEmpty();
    }

    [Fact]
    public void DistributeTds_LastItemAbsorbsRounding()
    {
        // 3 items: 333.33, 333.33, 333.34 distribution
        var items = new List<(Guid, decimal)>
        {
            (Guid.NewGuid(), 1000m),
            (Guid.NewGuid(), 1000m),
            (Guid.NewGuid(), 1000m),
        };

        var dist = TaxWithholdingService.DistributeTdsAcrossItems(100m, items);
        // First two get 33.33 each, last gets remainder
        dist.Values.Sum().ShouldBe(100m);
        var lastItem = items.Last().Item1;
        dist[lastItem].ShouldBe(100m - dist[items[0].Item1] - dist[items[1].Item1]);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TaxWithholdingEntry — Entity Tests
// ═══════════════════════════════════════════════════════════════════

public class TaxWithholdingEntryTests
{
    [Fact]
    public void Create_CalculatesWithheldAmount()
    {
        var entry = new Tax.Entities.TaxWithholdingEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseInvoice", Guid.NewGuid(), Guid.NewGuid(),
            10m, 50000m, DateTime.Today);

        entry.WithheldAmount.ShouldBe(5000m); // 50000 × 10%
    }

    [Fact]
    public void Create_DefaultStatus_IsDraft()
    {
        var entry = CreateEntry();
        entry.Status.ShouldBe(Core.DocumentStatus.Draft);
    }

    [Fact]
    public void ApplyLDC_ReducesWithheldAmount()
    {
        var entry = CreateEntry(rate: 10, taxable: 100000);
        entry.WithheldAmount.ShouldBe(10000m); // 100K × 10%

        entry.ApplyLDC(3m, "LDC-001");
        entry.HasLDC.ShouldBeTrue();
        entry.LdcRate.ShouldBe(3m);
        entry.CertificateNumber.ShouldBe("LDC-001");
        entry.WithheldAmount.ShouldBe(3000m); // 100K × 3%
    }

    [Fact]
    public void Submit_FromDraft_Succeeds()
    {
        var entry = CreateEntry();
        entry.Submit();
        entry.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_FromSubmitted_Throws()
    {
        var entry = CreateEntry();
        entry.Submit();
        Should.Throw<BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void Cancel_FromSubmitted_Succeeds()
    {
        var entry = CreateEntry();
        entry.Submit();
        entry.Cancel();
        entry.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_Throws()
    {
        var entry = CreateEntry();
        Should.Throw<BusinessException>(() => entry.Cancel());
    }

    private static Tax.Entities.TaxWithholdingEntry CreateEntry(
        decimal rate = 10m, decimal taxable = 50000m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PurchaseInvoice", Guid.NewGuid(), Guid.NewGuid(),
            rate, taxable, DateTime.Today);
}
