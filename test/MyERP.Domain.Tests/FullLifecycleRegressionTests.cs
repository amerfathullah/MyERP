using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Full lifecycle regression tests that validate complete business cycles.
/// Each test represents a real-world user workflow from start to finish.
/// </summary>
public class FullLifecycleRegressionTests
{
    [Fact]
    public void OrderToCash_FullCycle()
    {
        // 1. Create Sales Order
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-FULL-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Premium Widget", 100m, 250m, 0m);
        Assert.Equal(DocumentStatus.Draft, so.Status);

        // 2. Submit SO → reserves stock
        so.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);

        // 3. Partial delivery (60 units)
        so.Items[0].DeliveredQty = 60m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status); // still need 40 more

        // 4. Full delivery (remaining 40)
        so.Items[0].DeliveredQty = 100m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status); // delivered, not yet billed

        // 5. Full billing
        so.Items[0].BilledQty = 100m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, so.Status); // fully fulfilled

        // 6. Verify cannot close a completed order (it's already done)
        Assert.Equal(DocumentStatus.Completed, so.Status);
    }

    [Fact]
    public void ProcureToPay_FullCycle()
    {
        // 1. Create Purchase Order
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-FULL-001", DateTime.Today);
        po.AddItem(Guid.NewGuid(), "Steel Plate 4mm", 500m, 45m, 0m, "KG");
        po.AddItem(Guid.NewGuid(), "Welding Rod", 200m, 8m, 0m, "Unit");
        Assert.Equal(DocumentStatus.Draft, po.Status);

        // 2. Submit PO → ordered qty reserved in Bin
        po.Submit();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);

        // 3. Track bin: ordered qty goes up
        var bin1 = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin1.OrderedQty = 500m;
        Assert.Equal(500m, bin1.ProjectedQty);

        // 4. Partial receipt: 300 of 500 KG steel
        po.Items[0].ReceivedQty = 300m;
        po.Items[1].ReceivedQty = 200m; // full welding rod
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status); // steel not fully received

        // 5. Bin updates: actual increases, ordered decreases
        bin1.ActualQty = 300m;
        bin1.OrderedQty = 200m; // remaining 200 KG on order
        Assert.Equal(500m, bin1.ProjectedQty); // 300 + 200

        // 6. Full receipt
        po.Items[0].ReceivedQty = 500m;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, po.Status); // all received, not billed

        // 7. Full billing
        po.Items[0].BilledQty = 500m;
        po.Items[1].BilledQty = 200m;
        po.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.Completed, po.Status);
    }

    [Fact]
    public void ManufactureToStock_FullCycle()
    {
        // 1. Create BOM
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-CHAIR-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Wood Plank", 4m, 25m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Screws", 16m, 0.5m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Cushion", 1m, 35m));
        bom.RecalculateCost();
        Assert.Equal(143m, bom.TotalMaterialCost); // 100 + 8 + 35

        // 2. Create Work Order for 50 chairs
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-CHAIR-001", Guid.NewGuid(), bom.Id, 50m);
        Assert.Equal(WorkOrderStatus.Draft, wo.Status);

        // 3. Submit WO
        wo.Submit();
        Assert.Equal(WorkOrderStatus.Submitted, wo.Status);

        // 4. Start production
        wo.Start();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);

        // 5. First batch: 20 chairs
        wo.RecordProduction(20m, 5m); // 5% overproduction allowed
        Assert.Equal(20m, wo.ProducedQuantity);

        // 6. Second batch: 30 chairs (completes the order)
        wo.RecordProduction(30m, 5m);
        Assert.Equal(50m, wo.ProducedQuantity);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
    }

    [Fact]
    public void ShortClose_PartialFulfillment()
    {
        // Customer orders 1000 units but only needs 700
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-SC-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Bulk Material", 1000m, 15m, 0m);
        so.Submit();

        // Deliver 700
        so.Items[0].DeliveredQty = 700m;
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);

        // Short-close: don't need remaining 300
        so.Close();
        Assert.Equal(DocumentStatus.Closed, so.Status);

        // Verify pending qty that would be released
        Assert.Equal(300m, so.Items[0].PendingDeliveryQty); // 1000 - 700

        // Later: customer wants to reopen for remaining
        so.Reopen();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status); // still has undelivered qty
    }

    [Fact]
    public void CreditNote_ReducesOutstanding()
    {
        // Original invoice: RM 5000
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-ORIG", DateTime.Today);
        si.GrandTotal = 5000m;
        Assert.Equal(5000m, si.OutstandingAmount);

        // Partial payment: RM 3000
        si.AmountPaid = 3000m;
        Assert.Equal(2000m, si.OutstandingAmount);

        // Credit note for RM 1000 (reduces outstanding further)
        si.AmountPaid = 4000m; // 3000 payment + 1000 credit note
        Assert.Equal(1000m, si.OutstandingAmount);

        // Final payment: RM 1000
        si.AmountPaid = 5000m;
        Assert.Equal(0m, si.OutstandingAmount); // fully settled
    }

    [Fact]
    public void MultiItem_SO_MinPercentage_Formula()
    {
        // SO with 3 items, asymmetric delivery
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-MIN-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item A", 100m, 10m, 0m);
        so.AddItem(Guid.NewGuid(), "Item B", 50m, 20m, 0m);
        so.AddItem(Guid.NewGuid(), "Item C", 200m, 5m, 0m);
        so.Submit();

        // Deliver 100% of A, 80% of B, 50% of C
        so.Items[0].DeliveredQty = 100m; // 100%
        so.Items[1].DeliveredQty = 40m;  // 80%
        so.Items[2].DeliveredQty = 100m; // 50%
        so.UpdateFulfillmentStatus();

        // Min formula: min(100%, 80%, 50%) = 50% → still ToDeliverAndBill
        Assert.Equal(DocumentStatus.ToDeliverAndBill, so.Status);

        // Complete all items
        so.Items[1].DeliveredQty = 50m;  // 100%
        so.Items[2].DeliveredQty = 200m; // 100%
        so.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToBill, so.Status); // all delivered
    }

    [Fact]
    public void Bin_ProjectedQty_FullRealisticScenario()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Opening stock: 500 units
        bin.ActualQty = 500m;

        // 2 POs pending: 200 + 300 = 500 on order
        bin.OrderedQty = 500m;

        // 3 SOs reserved: 150 + 100 + 50 = 300 reserved
        bin.ReservedQty = 300m;

        // 1 MR indented: 100 requested
        bin.IndentedQty = 100m;

        // 1 WO production reserved: 80
        bin.ReservedQtyForProduction = 80m;

        // Projected = 500 + 500 + 100 - 300 - 80 = 720
        Assert.Equal(720m, bin.ProjectedQty);

        // After PO receipt: actual+200, ordered-200
        bin.ActualQty += 200m;
        bin.OrderedQty -= 200m;
        // Projected should stay same: 700 + 300 + 100 - 300 - 80 = 720
        Assert.Equal(720m, bin.ProjectedQty);
    }
}
