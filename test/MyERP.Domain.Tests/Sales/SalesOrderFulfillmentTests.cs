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

    private static SalesOrder CreateSalesOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
}
