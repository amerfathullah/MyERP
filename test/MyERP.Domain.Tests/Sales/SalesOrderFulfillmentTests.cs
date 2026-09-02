using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

public class SalesOrderFulfillmentTests
{
    [Fact]
    public void PerDelivered_NoDeliveries_ReturnsZero()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);

        so.PerDelivered.ShouldBe(0);
    }

    [Fact]
    public void PerDelivered_PartialDelivery_ReturnsCorrectPercentage()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Widget A", 10, 100, 0);

        // Simulate partial delivery
        so.Items[0].DeliveredQty = 3;

        so.PerDelivered.ShouldBe(30m);
    }

    [Fact]
    public void PerDelivered_FullDelivery_Returns100()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Widget", 5, 200, 0);

        so.Items[0].DeliveredQty = 5;

        so.PerDelivered.ShouldBe(100m);
    }

    [Fact]
    public void PerBilled_PartialBilling_ReturnsCorrectPercentage()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Service A", 1, 1000, 0);
        so.AddItem(Guid.NewGuid(), "Service B", 1, 500, 0);

        // Bill only Service A
        so.Items[0].BilledQty = 1;

        // Billed amount = 1000, GrandTotal = 1500
        so.PerBilled.ShouldBe(66.67m);
    }

    [Fact]
    public void PendingDeliveryQty_CalculatesCorrectly()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Item", 10, 50, 0);

        so.Items[0].DeliveredQty = 7;

        so.Items[0].PendingDeliveryQty.ShouldBe(3);
    }

    [Fact]
    public void PendingBillingQty_CalculatesCorrectly()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Item", 10, 50, 0);

        so.Items[0].BilledQty = 4;

        so.Items[0].PendingBillingQty.ShouldBe(6);
    }

    [Fact]
    public void PerDelivered_AllServiceItemsWithSkipDelivery_Returns100()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Consulting Service", 1, 1000, 0);
        so.Items[0].SkipDelivery = true;

        so.PerDelivered.ShouldBe(100m);
        so.Items[0].PendingDeliveryQty.ShouldBe(0m);
    }

    [Fact]
    public void PerDelivered_MixedGoodsAndService_TracksOnlyGoods()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Physical Item", 10, 100, 0);
        so.AddItem(Guid.NewGuid(), "Installation Service", 1, 200, 0);
        so.Items[1].SkipDelivery = true;

        so.PerDelivered.ShouldBe(0m);

        so.Items[0].DeliveredQty = 10m;
        so.PerDelivered.ShouldBe(100m);
    }

    [Fact]
    public void Submit_WhenAllItemsSkipDelivery_StatusIsToBill()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Consulting Service", 1, 1000, 0);
        so.Items[0].SkipDelivery = true;

        so.Submit();

        so.Status.ShouldBe(Core.DocumentStatus.ToBill);
    }

    [Fact]
    public void CloseItem_ExcludesFromPendingAndUpdatesStatus()
    {
        var so = CreateSalesOrder();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        so.AddItem(item1Id, "Widget 1", 10, 100, 0);
        so.AddItem(item2Id, "Widget 2", 5, 200, 0);

        so.Submit();
        so.Status.ShouldBe(Core.DocumentStatus.ToDeliverAndBill);

        // Fulfill item 1 completely
        so.Items[0].DeliveredQty = 10;
        so.Items[0].BilledQty = 10;
        so.UpdateFulfillmentStatus();
        so.Status.ShouldBe(Core.DocumentStatus.ToDeliverAndBill);

        // Close remaining item 2 (ERPNext PR #57596)
        var row2 = so.Items[1];
        so.CloseItem(row2.Id);

        row2.IsClosed.ShouldBeTrue();
        row2.PendingDeliveryQty.ShouldBe(0m);
        row2.PendingBillingQty.ShouldBe(0m);

        // SO should now be completed because all active items are 100% fulfilled
        so.PerDelivered.ShouldBe(100m);
        so.PerBilled.ShouldBe(100m);
        so.Status.ShouldBe(Core.DocumentStatus.Completed);

        // Reopen item 2
        so.ReopenItem(row2.Id);
        row2.IsClosed.ShouldBeFalse();
        row2.PendingDeliveryQty.ShouldBe(5m);
        row2.PendingBillingQty.ShouldBe(5m);
        so.Status.ShouldBe(Core.DocumentStatus.ToDeliverAndBill);
    }

    private static SalesOrder CreateSalesOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
}
