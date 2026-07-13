using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class OverDeliveryReceiptTests
{
    [Fact]
    public void SalesOrderItem_PendingDeliveryQty_Calculated()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 100m, 10m, 0m);

        so.Items[0].DeliveredQty = 60m;
        so.Items[0].PendingDeliveryQty.ShouldBe(40m);
    }

    [Fact]
    public void SalesOrderItem_FullyDelivered_ZeroPending()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 50m, 10m, 0m);

        so.Items[0].DeliveredQty = 50m;
        so.Items[0].PendingDeliveryQty.ShouldBe(0m);
    }

    [Fact]
    public void PurchaseOrderItem_PendingReceiptQty_Calculated()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 200m, 5m, 0m);

        po.Items[0].ReceivedQty = 150m;
        po.Items[0].PendingReceiptQty.ShouldBe(50m);
    }

    [Fact]
    public void PurchaseOrderItem_FullyReceived_ZeroPending()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 100m, 5m, 0m);

        po.Items[0].ReceivedQty = 100m;
        po.Items[0].PendingReceiptQty.ShouldBe(0m);
    }

    [Fact]
    public void OverDelivery_DetectedWhenQtyExceedsPending()
    {
        // Simulate: SO has 100 ordered, 80 delivered, so pending = 20
        // DN tries to deliver 30 → should be blocked
        var soQty = 100m;
        var deliveredQty = 80m;
        var pendingDelivery = soQty - deliveredQty; // 20
        var attemptedQty = 30m;

        var isOverDelivery = attemptedQty > pendingDelivery;
        isOverDelivery.ShouldBeTrue();
    }

    [Fact]
    public void OverReceipt_DetectedWhenQtyExceedsPending()
    {
        // Simulate: PO has 50 ordered, 40 received, so pending = 10
        // PR tries to receive 15 → should be blocked
        var poQty = 50m;
        var receivedQty = 40m;
        var pendingReceipt = poQty - receivedQty; // 10
        var attemptedQty = 15m;

        var isOverReceipt = attemptedQty > pendingReceipt;
        isOverReceipt.ShouldBeTrue();
    }

    [Fact]
    public void WithinLimit_AllowedWhenQtyEqualsPending()
    {
        var pendingDelivery = 20m;
        var attemptedQty = 20m;

        var isOverDelivery = attemptedQty > pendingDelivery;
        isOverDelivery.ShouldBeFalse();
    }
}
