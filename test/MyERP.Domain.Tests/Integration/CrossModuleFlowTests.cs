using System;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using MyERP.Tax.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Cross-module integration tests exercising complete business workflows.
/// </summary>
public class CrossModuleFlowTests
{
    [Fact]
    public void PayrollFlow_SalarySlip_EarningsDeductions()
    {
        // Employee → Salary Slip with statutory components
        var slip = new SalarySlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), new DateTime(2026, 7, 31));

        // Earnings
        slip.AddEarning(Guid.NewGuid(), "Basic", 5000m);
        slip.AddEarning(Guid.NewGuid(), "Housing Allowance", 1200m);
        slip.AddEarning(Guid.NewGuid(), "Transport Allowance", 500m);

        // Statutory deductions (MY rates: EPF 11%, SOCSO 0.5%, EIS 0.2%)
        slip.AddDeduction(Guid.NewGuid(), "EPF (Employee)", 550m, isStatutory: true);
        slip.AddDeduction(Guid.NewGuid(), "SOCSO", 25m, isStatutory: true);
        slip.AddDeduction(Guid.NewGuid(), "EIS", 10m, isStatutory: true);
        slip.AddDeduction(Guid.NewGuid(), "PCB/MTD", 200m, isStatutory: true);

        slip.GrossAmount.ShouldBe(6700m);
        slip.TotalDeductions.ShouldBe(785m);
        slip.NetAmount.ShouldBe(5915m);
        slip.Earnings.Count.ShouldBe(3);
        slip.Deductions.Count.ShouldBe(4);
        slip.Deductions.All(d => d.IsStatutory).ShouldBeTrue();
    }

    [Fact]
    public void ExpenseClaim_FullLifecycle()
    {
        var claim = new ExpenseClaim(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        claim.AddExpense(new DateTime(2026, 7, 1), "Flight KL-SG", 800m);
        claim.AddExpense(new DateTime(2026, 7, 1), "Hotel 2 nights", 600m);
        claim.AddExpense(new DateTime(2026, 7, 2), "Taxi", 50m);

        claim.TotalClaimedAmount.ShouldBe(1450m);
        claim.Approve();
        claim.TotalSanctionedAmount.ShouldBe(1450m);
        claim.Submit();
        claim.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void ItemTaxTemplate_OverridesDocumentRate()
    {
        // Template: SST at 6% for this item (document might have 10% default)
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "SST 6% - Services");
        var sstAccountId = Guid.NewGuid();
        template.AddDetail(sstAccountId, 6m);

        // Item with this template assigned → rate lookup
        var rate = template.GetRateForAccount(sstAccountId);
        rate.ShouldBe(6m);

        // Different tax account → returns null (use document default)
        template.GetRateForAccount(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public void ItemTaxTemplate_ExemptItem()
    {
        // Exempt item: N/A for SST (e.g., basic groceries)
        var template = new ItemTaxTemplate(Guid.NewGuid(), Guid.NewGuid(), "Exempt - Basic Needs");
        var sstAccountId = Guid.NewGuid();
        template.AddDetail(sstAccountId, 0m, notApplicable: true);

        // Returns null = exclude this tax entirely for this item
        template.GetRateForAccount(sstAccountId).ShouldBeNull();
        template.Details[0].NotApplicable.ShouldBeTrue();
    }

    [Fact]
    public void Manufacturing_WorkstationCost_FlowsToRouting()
    {
        // Workstation with cost components
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "CNC Machine A");
        ws.AddCost("Labor", 80m);
        ws.AddCost("Electricity", 15m);
        ws.AddCost("Maintenance", 5m);
        ws.HourRate.ShouldBe(100m); // 80+15+5

        // Routing uses this rate
        var routing = new Routing(Guid.NewGuid(), "CNC Routing");
        routing.AddOperation(Guid.NewGuid(), 10, 120m, ws.Id); // 120 mins = 2 hours
        routing.Operations[0].CalculateCost(ws.HourRate);
        routing.Operations[0].OperatingCost.ShouldBe(200m); // 100/hr × 2hrs
    }

    [Fact]
    public void ProductBundle_ValuationForGrossProfit()
    {
        var bundleItemId = Guid.NewGuid();
        var compA = Guid.NewGuid();
        var compB = Guid.NewGuid();
        var compC = Guid.NewGuid();

        var bundle = new ProductBundle(Guid.NewGuid(), bundleItemId);
        bundle.AddItem(compA, 2m, "Widget A");   // 2 units
        bundle.AddItem(compB, 1m, "Widget B");   // 1 unit
        bundle.AddItem(compC, 5m, "Screw Pack"); // 5 units

        // Component valuation rates (from stock)
        var valuation = bundle.CalculateValuation(id =>
            id == compA ? 50m : id == compB ? 120m : id == compC ? 2m : 0m);

        // Bundle valuation = 2×50 + 1×120 + 5×2 = 100 + 120 + 10 = 230
        valuation.ShouldBe(230m);
    }

    [Fact]
    public void Dunning_LevelProgression()
    {
        var customerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // Level 1 dunning
        var d1 = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 1);
        d1.AddOverduePayment(Guid.NewGuid(), 5000m, DateTime.UtcNow.AddDays(-35), 35);
        d1.DunningFee = 50m;
        d1.Submit();
        d1.GrandTotal.ShouldBe(5050m);

        // Level 2 dunning (same customer, escalated)
        var d2 = new Dunning(Guid.NewGuid(), companyId, customerId, DateTime.UtcNow, 2);
        d2.AddOverduePayment(Guid.NewGuid(), 5000m, DateTime.UtcNow.AddDays(-65), 65);
        d2.DunningFee = 100m;
        d2.InterestAmount = 75m;
        d2.Submit();
        d2.GrandTotal.ShouldBe(5175m); // 5000 + 100 + 75
        d2.DunningLevel.ShouldBe(2);
    }

    [Fact]
    public void PaymentRequest_FullLifecycle()
    {
        var pr = new PaymentRequest(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice",
            Guid.NewGuid(), Guid.NewGuid(), "Customer", 10000m);
        pr.Status.ShouldBe(PaymentRequestStatus.Draft);
        pr.OutstandingAmount.ShouldBe(10000m);

        pr.Submit();
        pr.Status.ShouldBe(PaymentRequestStatus.Initiated);

        var peId = Guid.NewGuid();
        pr.MarkPaid(peId);
        pr.Status.ShouldBe(PaymentRequestStatus.Paid);
        pr.PaymentEntryId.ShouldBe(peId);
    }
}
