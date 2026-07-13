using System;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Integration tests exercising multi-entity workflows added in recent migration sessions.
/// </summary>
public class AdvancedFlowTests
{
    [Fact]
    public void ManufacturingFlow_BOM_Routing_WorkOrder_JobCard()
    {
        // Setup: Operation → Routing → BOM → Work Order → Job Card
        var opId = Guid.NewGuid();
        var operation = new Operation(opId, "Cutting");

        var routing = new Routing(Guid.NewGuid(), "Standard CNC Routing");
        routing.AddOperation(opId, 10, 30m); // 30 mins
        routing.AddOperation(Guid.NewGuid(), 20, 45m); // 45 mins
        routing.Operations.Count.ShouldBe(2);
        routing.Operations[0].SequenceId.ShouldBe(10);
        routing.Operations[1].SequenceId.ShouldBe(20);

        // Work Order
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        // Job Card for first operation
        var jc = new JobCard(Guid.NewGuid(), wo.CompanyId, wo.Id, opId, 50m, 10);
        jc.PlannedTimeInMins = 30m;
        jc.Start();
        jc.AddTimeLog(new DateTime(2026, 7, 12, 8, 0, 0), new DateTime(2026, 7, 12, 8, 30, 0), 25m);
        jc.AddTimeLog(new DateTime(2026, 7, 12, 9, 0, 0), new DateTime(2026, 7, 12, 9, 30, 0), 25m);
        jc.CompletedQty.ShouldBe(50m);
        jc.TotalTimeInMins.ShouldBe(60m);
        jc.Complete();
        jc.Status.ShouldBe(JobCardStatus.Completed);
    }

    [Fact]
    public void PickList_PartialTransfer_MultiSE()
    {
        // Create pick list with 100 units
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m, itemName: "Widget A");
        pl.Submit();

        // First transfer: 40 units
        pl.Items[0].RecordTransfer(40m);
        pl.IsPartiallyTransferred.ShouldBeTrue();
        pl.IsFullyTransferred.ShouldBeFalse();
        pl.Items[0].PendingQty.ShouldBe(60m);

        // Second transfer: 60 units (completes)
        pl.Items[0].RecordTransfer(60m);
        pl.IsFullyTransferred.ShouldBeTrue();
    }

    [Fact]
    public void LandedCost_DistributionByAmount_ProportionalAllocation()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        lcv.DistributionMethod = LandedCostDistributionMethod.BasedOnAmount;

        // Item A: RM2000 (66.67%), Item B: RM1000 (33.33%)
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 10, 2000m);
        lcv.AddItem(Guid.NewGuid(), "PurchaseReceipt", Guid.NewGuid(), 5, 1000m);
        lcv.AddCharge("Freight", Guid.NewGuid(), 300m);

        lcv.Submit();

        // 300 distributed proportionally: 200 + 100
        lcv.Items[0].ApplicableCharges.ShouldBe(200m);
        lcv.Items[1].ApplicableCharges.ShouldBe(100m);
        lcv.TotalDistributedAmount.ShouldBe(300m);
    }

    [Fact]
    public void Subscription_BillingPeriodAdvancement()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1, 99.90m, "Basic Plan");
        sub.TotalPerInterval.ShouldBe(99.90m);

        // First period
        sub.AdvancePeriod();
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 1, 1));
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 1, 31));

        // Second period
        sub.AdvancePeriod();
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 2, 1));
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void BlanketOrder_QuantityTracking_WithAllowance()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Guid.NewGuid(), "BO-001", "Selling",
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        bo.AddItem(Guid.NewGuid(), 1000, 5.00m, "Steel Rod");
        bo.Submit();

        // Order 400 units (40%)
        bo.Items[0].RecordOrder(400m, 10); // 10% allowance → max 1100
        bo.Items[0].RemainingQty.ShouldBe(600m);

        // Order another 600 (100% utilized)
        bo.Items[0].RecordOrder(600m, 10);
        bo.Items[0].RemainingQty.ShouldBe(0m);

        // Order 100 more (within 10% allowance)
        bo.Items[0].RecordOrder(100m, 10);
        bo.Items[0].OrderedQty.ShouldBe(1100m);

        // One more unit would exceed allowance
        Should.Throw<BusinessException>(() => bo.Items[0].RecordOrder(1m, 10));
    }

    [Fact]
    public void PeriodClosing_MultiAccountEntries()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 12, 31), new DateTime(2026, 12, 31), Guid.NewGuid());

        // Multiple P&L accounts being closed
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 50000m, false); // Revenue (CR)
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 30000m, true);  // COGS (DR)
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 10000m, true);  // OpEx (DR)

        pcv.TotalClosingAmount.ShouldBe(90000m); // sum of absolute amounts
        pcv.Submit();
        pcv.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void StockReservation_DeliveryTracking()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 200m);
        sre.Submit();

        // Partial delivery
        sre.RecordDelivery(80m);
        sre.AvailableQty.ShouldBe(120m);

        // Another delivery
        sre.RecordDelivery(120m);
        sre.AvailableQty.ShouldBe(0m);
        sre.DeliveredQty.ShouldBe(200m);

        // Cannot deliver more
        Should.Throw<BusinessException>(() => sre.RecordDelivery(1m));
    }
}
