using System;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests exercising entity relationships and workflow completion.
/// </summary>
public class EntityRelationshipTests
{
    [Fact]
    public void PickList_to_DeliveryFlow()
    {
        // Pick list for delivery
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        var soId = Guid.NewGuid();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m, itemName: "Laptop");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m, itemName: "Mouse");
        pl.Submit();

        // Partial transfer (first batch)
        pl.Items[0].RecordTransfer(30m);
        pl.Items[1].RecordTransfer(100m); // fully picked
        pl.IsPartiallyTransferred.ShouldBeTrue();

        // Second transfer completes first item
        pl.Items[0].RecordTransfer(20m);
        pl.IsFullyTransferred.ShouldBeTrue();
    }

    [Fact]
    public void JobCard_to_WorkOrder_Completion()
    {
        // Work order for 100 units
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-100",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();

        // Two job cards for same operation (batch split: 60 + 40)
        var jc1 = new JobCard(Guid.NewGuid(), wo.CompanyId, wo.Id, Guid.NewGuid(), 60m, 10);
        var jc2 = new JobCard(Guid.NewGuid(), wo.CompanyId, wo.Id, Guid.NewGuid(), 40m, 10);

        jc1.Start();
        jc1.AddTimeLog(new DateTime(2026, 7, 12, 8, 0, 0), new DateTime(2026, 7, 12, 10, 0, 0), 60m);
        jc1.Complete();

        jc2.Start();
        jc2.AddTimeLog(new DateTime(2026, 7, 12, 10, 30, 0), new DateTime(2026, 7, 12, 12, 0, 0), 40m);
        jc2.Complete();

        // Both complete
        jc1.CompletedQty.ShouldBe(60m);
        jc2.CompletedQty.ShouldBe(40m);
        (jc1.CompletedQty + jc2.CompletedQty).ShouldBe(wo.Quantity);
    }

    [Fact]
    public void Subscription_MultiPeriodAdvancement()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Quarterly")
        { BillingIntervalCount = 1 };
        sub.AddPlan(Guid.NewGuid(), 1, 300m, "Premium Quarterly");

        // Advance through a full year
        sub.AdvancePeriod(); // Q1: Jan 1 - Mar 31
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 3, 31));
        sub.AdvancePeriod(); // Q2: Apr 1 - Jun 30
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 4, 1));
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 6, 30));
        sub.AdvancePeriod(); // Q3: Jul 1 - Sep 30
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 7, 1));
        sub.AdvancePeriod(); // Q4
        sub.CurrentInvoiceStart.ShouldNotBeNull();
    }

    [Fact]
    public void BlanketOrder_MultipleOrders_ExhaustAllocation()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Guid.NewGuid(), "BO-2026-001",
            "Selling", Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        bo.AddItem(Guid.NewGuid(), 500, 10m, "Raw Steel");
        bo.Submit();

        // Multiple orders drawn from blanket
        bo.Items[0].RecordOrder(100m, 0); // PO #1
        bo.Items[0].RecordOrder(150m, 0); // PO #2
        bo.Items[0].RecordOrder(200m, 0); // PO #3
        bo.Items[0].OrderedQty.ShouldBe(450m);
        bo.Items[0].RemainingQty.ShouldBe(50m);

        // Final order uses up remaining
        bo.Items[0].RecordOrder(50m, 0);
        bo.Items[0].RemainingQty.ShouldBe(0m);
    }

    [Fact]
    public void QualityInspection_MixedReadings()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Inventory.InspectionType.Incoming, DateTime.UtcNow);

        // Numeric reading (passes)
        qi.AddReading("Tensile Strength", null, 200m, 500m, "350", isNumeric: true);
        // Value-based reading (passes)
        qi.AddReading("Surface Finish", "Smooth", null, null, "Smooth");
        // Numeric reading (fails - below min)
        qi.AddReading("Hardness", null, 60m, 80m, "45", isNumeric: true);

        qi.Evaluate();
        qi.Status.ShouldBe(Inventory.InspectionStatus.Rejected); // one reading failed
        qi.Readings[0].Status.ShouldBe(Inventory.InspectionStatus.Accepted);
        qi.Readings[1].Status.ShouldBe(Inventory.InspectionStatus.Accepted);
        qi.Readings[2].Status.ShouldBe(Inventory.InspectionStatus.Rejected);
    }

    [Fact]
    public void PeriodClosing_NetProfitCalculation()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 12, 31), new DateTime(2026, 12, 31), Guid.NewGuid());

        // Revenue accounts (credit balances → debit to close)
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 100000m, true); // Sales DR
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 5000m, true);   // Other Income DR

        // Expense accounts (debit balances → credit to close)
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 60000m, false); // COGS CR
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 20000m, false); // OpEx CR
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 5000m, false);  // Depreciation CR

        pcv.TotalClosingAmount.ShouldBe(190000m); // sum of absolutes
        pcv.Entries.Count.ShouldBe(5);
        pcv.Submit();
    }
}
