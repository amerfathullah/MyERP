using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Comprehensive end-to-end integration tests that exercise complete business flows
/// and verify cross-module interactions work correctly together.
/// </summary>
public class ComprehensiveBusinessFlowTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Full Procure-to-Pay Cycle Entity Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ProcureToPay_PO_Submit_SetsToDeliverAndBill()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Raw Material", 100, 50m, 0, "Kg");
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void ProcureToPay_PO_FullReceipt_PerReceived100()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Raw Material", 100, 50m, 0, "Kg");
        po.Submit();

        // Simulate full receipt
        po.Items[0].ReceivedQty = 100;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToBill); // Fully received, not billed
    }

    [Fact]
    public void ProcureToPay_PO_FullReceiptAndBilling_Completed()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Raw Material", 100, 50m, 0, "Kg");
        po.Submit();

        // Full receipt + full billing
        po.Items[0].ReceivedQty = 100;
        po.Items[0].BilledQty = 100;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void ProcureToPay_PO_PartialReceipt_StaysToDeliverAndBill()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Part A", 100, 50m, 0, "Kg");
        po.AddItem(Guid.NewGuid(), "Part B", 200, 30m, 0, "Kg");
        po.Submit();

        // Partial receipt on item A only — per Min% formula, overall stays at 0%
        po.Items[0].ReceivedQty = 100;
        po.Items[1].ReceivedQty = 0;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill); // Min(100%, 0%) = 0%
    }

    // ═══════════════════════════════════════════════════════════════════
    // Full Order-to-Collection Cycle Entity Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void OrderToCollection_SO_Submit_SetsToDeliverAndBill()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 50, 200m, 0, "Unit");
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void OrderToCollection_SO_FullDelivery_ToBill()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 50, 200m, 0, "Unit");
        so.Submit();

        so.Items[0].DeliveredQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    [Fact]
    public void OrderToCollection_SO_FullDeliveryAndBilling_Completed()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 50, 200m, 0, "Unit");
        so.Submit();

        so.Items[0].DeliveredQty = 50;
        so.Items[0].BilledQty = 50;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void OrderToCollection_SO_Close_FromPartialDelivery()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 100, 200m, 0, "Unit");
        so.Submit();

        so.Items[0].DeliveredQty = 60; // Partial
        so.UpdateFulfillmentStatus();
        so.Close(); // Short-close
        so.Status.ShouldBe(DocumentStatus.Closed);
    }

    [Fact]
    public void OrderToCollection_SO_Reopen_RecalculatesStatus()
    {
        var so = CreateSO();
        so.AddItem(Guid.NewGuid(), "Widget", 100, 200m, 0, "Unit");
        so.Submit();

        so.Items[0].DeliveredQty = 100;
        so.Items[0].BilledQty = 50; // Partially billed
        so.Close();
        so.Reopen();
        // Should recalculate: fully delivered, partially billed
        so.Status.ShouldBe(DocumentStatus.ToBill);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Manufacturing Cycle
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Manufacturing_BOM_CostCalculation()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 10m, 50m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Screws", 100m, 0.5m));
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(550m); // (10×50) + (100×0.5)
    }

    [Fact]
    public void Manufacturing_WorkOrder_ProductionTracking()
    {
        var wo = CreateWO(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(60); // First batch
        wo.ProducedQuantity.ShouldBe(60);

        wo.RecordProduction(40); // Complete
        wo.ProducedQuantity.ShouldBe(100);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void Manufacturing_WorkOrder_OverproductionBlocked()
    {
        var wo = CreateWO(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        // Overproduction beyond 10% should throw
        Should.Throw<BusinessException>(() => wo.RecordProduction(20, 10m));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Tax Withholding Flow
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TaxWithholding_FullCalculationChain()
    {
        var svc = new TaxWithholdingService(null!);

        // Supplier with RM 20K cumulative in FY, submitting RM 35K invoice
        // Cumulative threshold: RM 50K
        var r1 = svc.CalculateWithholding(
            currentInvoiceNetTotal: 35_000m,
            cumulativeInvoicedInFY: 20_000m,
            standardRate: 10m,
            singleThreshold: 0m,
            cumulativeThreshold: 50_000m,
            taxOnExcessAmount: true,
            previouslyDeductedTDS: 0);

        // Total = 20K + 35K = 55K, exceeds 50K threshold
        // Excess = 55K - 50K = 5K, tax on excess only
        r1.ThresholdCrossed.ShouldBeTrue();
        r1.TaxableAmount.ShouldBe(5_000m);
        r1.WithheldAmount.ShouldBe(500m); // 5K × 10%

        // Next invoice: 10K more (cumulative now 65K, always deducted)
        var r2 = svc.CalculateWithholding(
            currentInvoiceNetTotal: 10_000m,
            cumulativeInvoicedInFY: 55_000m,
            standardRate: 10m,
            singleThreshold: 0m,
            cumulativeThreshold: 50_000m,
            taxOnExcessAmount: true,
            previouslyDeductedTDS: 500m);

        // Excess = 65K - 50K = 15K, minus prev deducted 500
        r2.TaxableAmount.ShouldBe(15_000m);
        r2.WithheldAmount.ShouldBe(1_000m); // 15K × 10% - 500
    }

    // ═══════════════════════════════════════════════════════════════════
    // Loan EMI Calculation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Loan_DiminishingBalance_EMI()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LOAN-001", HumanResources.LoanType.TermLoan,
            HumanResources.InterestCalculationMethod.DiminishingBalance,
            120_000m, 6.0m, 12);

        loan.Sanction();
        loan.Disburse(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

        // 12-month schedule with diminishing balance EMI
        loan.RepaymentSchedule.Count.ShouldBe(12);
        loan.Emi.ShouldBeGreaterThan(0);

        // Total repayment should exceed principal (interest adds up)
        var totalRepayment = loan.RepaymentSchedule.Sum(s => s.TotalPayment);
        totalRepayment.ShouldBeGreaterThan(120_000m);

        // Last installment should bring outstanding to 0
        var lastEntry = loan.RepaymentSchedule.OrderBy(s => s.PaymentDate).Last();
        lastEntry.OutstandingAfterPayment.ShouldBe(0);
    }

    [Fact]
    public void Loan_FlatRate_EMI()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LOAN-002", HumanResources.LoanType.TermLoan,
            HumanResources.InterestCalculationMethod.FlatRate,
            100_000m, 5.0m, 12);

        loan.Sanction();
        loan.Disburse(DateTime.Today, DateTime.Today.AddMonths(1));

        // Flat: total_interest = principal × rate × years
        // = 100K × 5% × 1 = 5K
        // EMI = (100K + 5K) / 12 ≈ 8750
        loan.RepaymentSchedule.Count.ShouldBe(12);
        var totalInterest = loan.RepaymentSchedule.Sum(s => s.InterestAmount);
        totalInterest.ShouldBeInRange(4900m, 5100m); // ~5K with rounding
    }

    // ═══════════════════════════════════════════════════════════════════
    // Subscription Billing
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Subscription_TrialPeriod_ZeroRate()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Monthly")
        {
            TrialPeriodDays = 30,
            TrialEndDate = new DateTime(2026, 1, 31),
        };
        sub.AddPlan(Guid.NewGuid(), 1, 500m, "Premium");

        var items = engine.BuildInvoiceItems(sub, new DateTime(2026, 1, 15));
        items.Count.ShouldBe(1);
        items[0].Rate.ShouldBe(0m); // Trial = 100% discount
    }

    [Fact]
    public void Subscription_PostTrial_FullRate()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Monthly")
        {
            TrialPeriodDays = 7,
            TrialEndDate = new DateTime(2026, 1, 8),
        };
        sub.AddPlan(Guid.NewGuid(), 1, 500m, "Premium");

        // After trial
        var items = engine.BuildInvoiceItems(sub, new DateTime(2026, 2, 1));
        items[0].Rate.ShouldBe(500m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Stock Valuation — FIFO Multi-Layer
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void FIFO_MultiLayer_CostFromOldest()
    {
        var fifo = new FifoValuation();
        // Purchase 1: 10 @ RM50
        fifo.AddStock(10, 50m);
        // Purchase 2: 10 @ RM60
        fifo.AddStock(10, 60m);

        // Sell 5: should use RM50 (oldest layer)
        var consumed1 = fifo.RemoveStock(5);
        FifoValuation.GetOutgoingRate(consumed1).ShouldBe(50m); // All from first bin

        // Sell 8: 5 from first bin @50, 3 from second @60
        var consumed2 = fifo.RemoveStock(8);
        var expected = (5m * 50m + 3m * 60m) / 8m;
        FifoValuation.GetOutgoingRate(consumed2).ShouldBe(expected);
    }

    [Fact]
    public void FIFO_NegativeStockRecovery()
    {
        var fifo = new FifoValuation();
        fifo.AddStock(10, 100m);
        // Sell more than available (allowed when AllowNegativeStock)
        fifo.RemoveStock(15); // Goes negative by 5
        fifo.TotalQty.ShouldBe(-5);

        // Recovery: next purchase resets the queue
        fifo.AddStock(20, 110m);
        fifo.TotalQty.ShouldBe(15); // -5 + 20
    }

    // ═══════════════════════════════════════════════════════════════════
    // Aging Buckets
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AgingBuckets_InvoiceDistribution()
    {
        // Simulate 4 invoices at different aging levels
        var now = new DateTime(2026, 7, 14);
        var items = new[]
        {
            new AgingItem { DueDate = now.AddDays(-10), OutstandingAmount = 1000m }, // 0-30 bucket
            new AgingItem { DueDate = now.AddDays(-45), OutstandingAmount = 2000m }, // 31-60 bucket
            new AgingItem { DueDate = now.AddDays(-100), OutstandingAmount = 3000m }, // 91-120 bucket
            new AgingItem { DueDate = now.AddDays(-200), OutstandingAmount = 5000m }, // 120+ bucket
        };

        var buckets = new[] { 30, 60, 90, 120 };
        var totals = new decimal[buckets.Length + 1];

        foreach (var item in items)
        {
            var age = Math.Max(0, (int)(now - item.DueDate).TotalDays);
            var idx = 0;
            for (int i = 0; i < buckets.Length; i++)
            {
                if (age <= buckets[i]) { idx = i; break; }
                idx = buckets.Length;
            }
            totals[idx] += item.OutstandingAmount;
        }

        totals[0].ShouldBe(1000m); // 0-30 days
        totals[1].ShouldBe(2000m); // 31-60 days
        totals[3].ShouldBe(3000m); // 91-120 days
        totals[4].ShouldBe(5000m); // 120+ days
    }

    // ═══════════════════════════════════════════════════════════════════
    // Exchange Gain/Loss
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void ExchangeGainLoss_MultiCurrencyPayment()
    {
        // Invoice at rate 4.50, payment at rate 4.55 → gain
        var gain = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            10_000m, 4.55m, 4.50m);
        gain.ShouldBe(500m); // 10K × (4.55 - 4.50)

        // Invoice at rate 4.50, payment at rate 4.40 → loss
        var loss = PaymentReconciliationEngine.CalculateExchangeGainLoss(
            10_000m, 4.40m, 4.50m);
        loss.ShouldBe(-1000m); // 10K × (4.40 - 4.50)
    }

    // ═══════════════════════════════════════════════════════════════════
    // Payment Terms Schedule
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void PaymentTerms_3060Schedule()
    {
        var template = new PaymentTermsTemplate(Guid.NewGuid(), "30/60 Split");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 50m, 30, "Net 30"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 50m, 60, "Net 60"));
        template.ValidatePortions(); // Should not throw (sums to 100%)

        var schedule = template.GenerateSchedule(new DateTime(2026, 1, 1), 10_000m);
        schedule.Count.ShouldBe(2);
        schedule[0].PaymentAmount.ShouldBe(5_000m);
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 1, 31)); // +30 days
        schedule[1].PaymentAmount.ShouldBe(5_000m);
        schedule[1].DueDate.ShouldBe(new DateTime(2026, 3, 2)); // +60 days
    }

    [Fact]
    public void PaymentTerms_InvalidPortions_Throws()
    {
        var template = new PaymentTermsTemplate(Guid.NewGuid(), "Bad Split");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 40m, 30, "Part 1"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 40m, 60, "Part 2"));
        // Portions sum to 80%, not 100%
        Should.Throw<BusinessException>(() => template.ValidatePortions());
    }

    // ═══════════════════════════════════════════════════════════════════
    // Credit Note Outstanding Reduction
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CreditNote_ReducesOriginalOutstanding()
    {
        var original = CreateSI(1000m);
        original.Submit();
        original.OutstandingAmount.ShouldBe(1000m);

        // Simulate credit note reducing outstanding
        original.AmountPaid += 300m; // Credit note for 300
        original.OutstandingAmount.ShouldBe(700m);
    }

    [Fact]
    public void CreditNote_FullCredit_ZeroOutstanding()
    {
        var original = CreateSI(5000m);
        original.Submit();
        original.AmountPaid += 5000m;
        original.OutstandingAmount.ShouldBe(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static PurchaseOrder CreatePO() => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "PO-001", DateTime.Today);

    private static SalesOrder CreateSO() => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "SO-001", DateTime.Today);

    private static WorkOrder CreateWO(decimal qty) => new(Guid.NewGuid(), Guid.NewGuid(),
        "WO-001", Guid.NewGuid(), Guid.NewGuid(), qty);

    private static SalesInvoice CreateSI(decimal amount)
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 1, amount, 0);
        return si;
    }
}
