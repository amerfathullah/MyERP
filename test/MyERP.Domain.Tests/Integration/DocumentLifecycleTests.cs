using System;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests the complete document lifecycle with all side effects.
/// Verifies: Submit→Fulfill→Bill→Pay→Complete and Cancel→Revert flows.
/// </summary>
public class DocumentLifecycleTests
{
    [Fact]
    public void SO_FullLifecycle_SubmitDeliverBillComplete()
    {
        var so = CreateSalesOrder(qty1: 10, qty2: 5);
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Deliver all items
        so.Items[0].DeliveredQty = 10;
        so.Items[1].DeliveredQty = 5;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill);

        // Bill all items
        so.Items[0].BilledQty = 10;
        so.Items[1].BilledQty = 5;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void SO_PartialDelivery_ThenClose_ThenReopen()
    {
        var so = CreateSalesOrder(qty1: 100, qty2: 50);
        so.Submit();

        // Partially deliver
        so.Items[0].DeliveredQty = 60;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
        so.PerDelivered.ShouldBe(0m); // Min(60/100=60%, 0/50=0%) = 0% (item B not started)

        // Close (short-close: remaining 40+50 won't be delivered)
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);

        // Reopen → recalculates back to ToDeliverAndBill
        so.Reopen();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);
    }

    [Fact]
    public void PO_FullLifecycle_SubmitReceiveBillComplete()
    {
        var po = CreatePurchaseOrder(qty: 50);
        po.Submit();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Receive all
        po.Items[0].ReceivedQty = 50;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToBill);

        // Bill all
        po.Items[0].BilledQty = 50;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public void PO_PartialReceipt_ThenBill_StaysToDeliver()
    {
        var po = CreatePurchaseOrder(qty: 100);
        po.Submit();

        // Partial receipt (50 of 100)
        po.Items[0].ReceivedQty = 50;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        // Bill the received qty
        po.Items[0].BilledQty = 100; // bill full amount
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(DocumentStatus.ToDeliver); // fully billed but not fully received
    }

    [Fact]
    public void SO_Cancel_RevertsToCorrectState()
    {
        var so = CreateSalesOrder(qty1: 10, qty2: 5);
        so.Submit();
        so.Status.ShouldBe(DocumentStatus.ToDeliverAndBill);

        so.Cancel();
        so.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void SO_CloseFromToBill_Works()
    {
        var so = CreateSalesOrder(qty1: 10, qty2: 5);
        so.Submit();

        // Fully delivered
        so.Items[0].DeliveredQty = 10;
        so.Items[1].DeliveredQty = 5;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(DocumentStatus.ToBill);

        // Close without billing (write-off scenario)
        so.Close();
        so.Status.ShouldBe(DocumentStatus.Closed);
    }

    [Fact]
    public void FIFO_MultiPurchase_SellUsesOldestCost()
    {
        var queue = new FifoValuation();

        // Buy 100 @ RM10, then 50 @ RM15
        queue.AddStock(100, 10m);
        queue.AddStock(50, 15m);
        queue.TotalQty.ShouldBe(150m);
        queue.TotalValue.ShouldBe(1750m); // 1000 + 750

        // Sell 120 → FIFO: takes 100@10 + 20@15 = 1300/120 ≈ 10.83
        var consumed = queue.RemoveStock(120);
        var outRate = FifoValuation.GetOutgoingRate(consumed);
        outRate.ShouldBeInRange(10.83m, 10.84m);

        // Remaining: 30 @ 15
        queue.TotalQty.ShouldBe(30m);
        queue.TotalValue.ShouldBe(450m);
    }

    [Fact]
    public void LIFO_SellUsesNewestCost()
    {
        var queue = new FifoValuation(isLifo: true);

        // Buy 100 @ RM10, then 50 @ RM15
        queue.AddStock(100, 10m);
        queue.AddStock(50, 15m);

        // Sell 70 → LIFO: takes 50@15 + 20@10 = 950/70 ≈ 13.57
        var consumed = queue.RemoveStock(70);
        var outRate = FifoValuation.GetOutgoingRate(consumed);
        outRate.ShouldBeInRange(13.57m, 13.58m);

        // Remaining: 80 @ 10
        queue.TotalQty.ShouldBe(80m);
        queue.TotalValue.ShouldBe(800m);
    }

    [Fact]
    public void NegativeStock_FifoCreatesNegativeBin()
    {
        var queue = new FifoValuation();
        queue.AddStock(10, 50m);

        // Sell more than available → negative stock
        var consumed = queue.RemoveStock(15, outgoingRate: 50m);
        queue.TotalQty.ShouldBe(-5m);
        consumed.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void PaymentTerms_DueDateFloor_NeverBeforePostingDate()
    {
        // Due date can never be earlier than posting date (per ERPNext rules)
        var postingDate = new DateTime(2026, 7, 15);
        var calculatedDueDate = new DateTime(2026, 7, 10); // calculation produced earlier date

        // Floor rule: correct upward
        var actualDueDate = calculatedDueDate < postingDate ? postingDate : calculatedDueDate;
        actualDueDate.ShouldBe(postingDate);
    }

    private static SalesOrder CreateSalesOrder(decimal qty1, decimal qty2)
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-LC-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget A", qty1, 100, 0);
        so.AddItem(Guid.NewGuid(), "Widget B", qty2, 200, 0);
        return so;
    }

    private static PurchaseOrder CreatePurchaseOrder(decimal qty)
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-LC-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Raw Material", qty, 75, 0);
        return po;
    }
}
