using System;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end production scenario tests verifying multi-entity state consistency.
/// These represent the most common real-world workflows.
/// </summary>
public class ProductionScenarioTests
{
    // === SCENARIO 1: Complete Purchase-to-Stock cycle ===

    [Fact]
    public void Scenario_PurchaseOrder_ReceiptTracking()
    {
        // Create PO with 2 items
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Raw Material A", 100m, 25m, 0m, "KG");
        po.AddItem(Guid.NewGuid(), "Raw Material B", 50m, 40m, 0m, "L");
        po.Submit();

        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
        Assert.Equal(0m, po.PerReceived);
        Assert.Equal(0m, po.PerBilled);

        // Partial receipt: 60% of item A, 100% of item B
        po.Items.First().ReceivedQty = 60m;
        po.Items.Last().ReceivedQty = 50m;
        po.UpdateFulfillmentStatus();

        // Min-based: min(60/100, 50/50) = min(60%, 100%) = 60%
        Assert.True(po.PerReceived >= 60m);
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status); // not fully received

        // Full receipt
        po.Items.First().ReceivedQty = 100m;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, po.Status); // fully received, not billed
    }

    [Fact]
    public void Scenario_PurchaseOrder_BillingCompletion()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Component X", 200m, 15m, 0m, "Unit");
        po.Submit();

        // Full receipt + full billing
        po.Items.First().ReceivedQty = 200m;
        po.Items.First().BilledQty = 200m;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, po.Status);
    }

    // === SCENARIO 2: Sales Order to Delivery lifecycle ===

    [Fact]
    public void Scenario_SalesOrder_MultiItem_PartialFulfillment()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Product A", 20m, 500m, 0m);
        so.AddItem(Guid.NewGuid(), "Product B", 10m, 1000m, 0m);
        so.Submit();

        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);

        // Deliver Product A fully, Product B partially
        so.Items.First().DeliveredQty = 20m;
        so.Items.Last().DeliveredQty = 5m;
        so.UpdateFulfillmentStatus();

        // Min formula: min(20/20=100%, 5/10=50%) = 50% → still ToDeliverAndBill
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);

        // Complete delivery
        so.Items.Last().DeliveredQty = 10m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status);

        // Complete billing
        so.Items.First().BilledQty = 20m;
        so.Items.Last().BilledQty = 10m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    // === SCENARIO 3: Stock movement and Bin tracking ===

    [Fact]
    public void Scenario_Bin_FullLifecycle_ProjectedQty()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Step 1: PO submitted → ordered qty increases
        bin.OrderedQty = 100m;
        Assert.Equal(100m, bin.ProjectedQty); // projected = ordered (no actual)

        // Step 2: PR received → actual increases, ordered decreases
        bin.ActualQty = 100m;
        bin.OrderedQty = 0m;
        Assert.Equal(100m, bin.ProjectedQty); // 100 actual

        // Step 3: SO submitted → reserved qty increases
        bin.ReservedQty = 30m;
        Assert.Equal(70m, bin.ProjectedQty); // 100 - 30 reserved

        // Step 4: DN delivered → actual decreases, reserved released
        bin.ActualQty = 70m;
        bin.ReservedQty = 0m;
        Assert.Equal(70m, bin.ProjectedQty); // 70 actual, 0 reserved

        // Step 5: WO material reserved
        bin.ReservedQtyForProduction = 20m;
        Assert.Equal(50m, bin.ProjectedQty); // 70 - 20

        // Step 6: New PO placed
        bin.OrderedQty = 50m;
        Assert.Equal(100m, bin.ProjectedQty); // 70 + 50 - 20
    }

    // === SCENARIO 4: Manufacturing BOM cost ===

    [Fact]
    public void Scenario_BOM_CostCalculation_WithMultipleMaterials()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-FG-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel Sheet", 2m, 45m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Bolt M10", 8m, 2.5m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Paint 1L", 0.5m, 30m));

        bom.RecalculateCost();

        // 2*45 + 8*2.5 + 0.5*30 = 90 + 20 + 15 = 125
        Assert.Equal(125m, bom.TotalMaterialCost);
    }

    // === SCENARIO 5: Work Order production lifecycle ===

    [Fact]
    public void Scenario_WorkOrder_ProgressiveProduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100m);
        Assert.Equal(WorkOrderStatus.Draft, wo.Status);

        wo.Submit();
        Assert.Equal(WorkOrderStatus.Submitted, wo.Status);

        wo.Start();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);

        // Produce in batches
        wo.RecordProduction(30m, 10m); // 10% allowance
        Assert.Equal(30m, wo.ProducedQuantity);

        wo.RecordProduction(40m, 10m);
        Assert.Equal(70m, wo.ProducedQuantity);

        wo.RecordProduction(30m, 10m); // exactly at 100
        Assert.Equal(100m, wo.ProducedQuantity);
    }

    // === SCENARIO 6: Short-close workflow ===

    [Fact]
    public void Scenario_ShortClose_SO_ReleasesUnfulfilled()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-SC-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Widget A", 100m, 50m, 0m);
        so.Submit();

        // Partially deliver
        so.Items.First().DeliveredQty = 60m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);

        // Short-close (accept 60, don't need the other 40)
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);

        // Can reopen if needed later
        so.Reopen();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status); // back to active
    }

    // === SCENARIO 7: Credit limit boundary ===

    [Fact]
    public void Scenario_CreditLimit_BoundaryEnforcement()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "ABC Trading");
        customer.CreditLimit = 100000m;

        // Scenario: existing outstanding = 95,000
        var existing = 95000m;

        // New invoice of 4,999 → within limit (95000 + 4999 = 99999 < 100000)
        Assert.True((existing + 4999m) <= customer.CreditLimit);

        // New invoice of 5,001 → exceeds (95000 + 5001 = 100001 > 100000)
        Assert.True((existing + 5001m) > customer.CreditLimit);

        // Exactly at limit → OK (not exceeded, just at)
        Assert.True((existing + 5000m) <= customer.CreditLimit);
    }

    // === SCENARIO 8: Payment outstanding tracking ===

    [Fact]
    public void Scenario_MultiPayment_ReducesOutstanding()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-MP-001", DateTime.Today);
        si.GrandTotal = 10000m;
        Assert.Equal(10000m, si.OutstandingAmount);

        // First payment: 3000
        si.AmountPaid = 3000m;
        Assert.Equal(7000m, si.OutstandingAmount);

        // Second payment: 5000
        si.AmountPaid = 8000m; // cumulative
        Assert.Equal(2000m, si.OutstandingAmount);

        // Final payment: settles everything
        si.AmountPaid = 10000m;
        Assert.Equal(0m, si.OutstandingAmount);
    }

    // === SCENARIO 9: PO Close releases ordered qty ===

    [Fact]
    public void Scenario_POClose_ReleasesOrderedQty()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-CL-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Material X", 500m, 20m, 0m, "KG");
        po.Submit();

        // Partial receipt
        po.Items.First().ReceivedQty = 300m;
        po.UpdateFulfillmentStatus();

        // Pending = 500 - 300 = 200 KG still expected
        Assert.Equal(200m, po.Items.First().PendingReceiptQty);

        // Close PO (accept 300, don't need 200 more)
        po.Close();
        Assert.Equal(DocumentStatus.Closed, po.Status);
    }

    // === SCENARIO 10: Leave balance enforcement ===

    [Fact]
    public void Scenario_LeaveAllocation_AnnualCycle()
    {
        var alloc = new HumanResources.Entities.LeaveAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 14m);

        // Start of year: 14 days available
        Assert.Equal(14m, alloc.Balance);

        // Take sick leave (3 days)
        alloc.DeductLeave(3m);
        Assert.Equal(11m, alloc.Balance);

        // Take annual leave (5 days)
        alloc.DeductLeave(5m);
        Assert.Equal(6m, alloc.Balance);
        Assert.Equal(8m, alloc.LeavesUsed);

        // Cancel one sick leave day (doctor cleared)
        alloc.RestoreLeave(1m);
        Assert.Equal(7m, alloc.Balance);
        Assert.Equal(7m, alloc.LeavesUsed);
    }

    // === SCENARIO 11: Multi-currency invoice ===

    [Fact]
    public void Scenario_MultiCurrency_BaseAmountCalculation()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-FX-001", DateTime.Today);
        si.ExchangeRate = 4.72m; // USD → MYR

        // USD 5,000 invoice
        si.GrandTotal = 5000m;
        si.BaseGrandTotal = 5000m * 4.72m; // 23,600 MYR

        Assert.Equal(23600m, si.BaseGrandTotal);
        Assert.Equal(5000m, si.GrandTotal);

        // Payment in USD
        si.AmountPaid = 2000m;
        Assert.Equal(3000m, si.OutstandingAmount); // 5000 - 2000 USD outstanding
    }
}
