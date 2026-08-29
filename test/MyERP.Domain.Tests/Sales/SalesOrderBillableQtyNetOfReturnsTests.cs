using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

public class SalesOrderBillableQtyNetOfReturnsTests
{
    [Fact]
    public void BillableQty_AfterDeliveryAndFullReturn_IsZero()
    {
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item A", 10m, 100m, 0m, "Unit")
        {
            DeliveredQty = 0m,
            ReturnedQty = 10m,
            BilledQty = 0m
        };

        soItem.BillableQty.ShouldBe(0m);
        soItem.PendingBillingQty.ShouldBe(0m);
    }

    [Fact]
    public void BillableQty_AfterReturnAndFullRedelivery_IsFullyBillable()
    {
        // Ordered: 10, Returned: 10, Re-delivered: 10
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item A", 10m, 100m, 0m, "Unit")
        {
            DeliveredQty = 10m,
            ReturnedQty = 10m,
            BilledQty = 0m
        };

        soItem.BillableQty.ShouldBe(10m);
        soItem.PendingBillingQty.ShouldBe(10m);
    }

    [Fact]
    public void BillableQty_WithPartialDelivery_BillsOrderedQty()
    {
        // Ordered: 10, Delivered: 4, Returned: 0, Billed: 0
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item A", 10m, 100m, 0m, "Unit")
        {
            DeliveredQty = 4m,
            ReturnedQty = 0m,
            BilledQty = 0m
        };

        soItem.BillableQty.ShouldBe(10m);
        soItem.PendingBillingQty.ShouldBe(10m);
    }

    [Fact]
    public void BillableQty_AfterPartialBilling_Return_And_Redelivery_CalculatesPendingCorrectly()
    {
        // Ordered: 10, Billed: 4, Returned: 10, Re-delivered: 5
        // BillableQty = min(10, max(10 - 10, 5)) = 5
        // PendingBillingQty = 5 - 4 = 1
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Item A", 10m, 100m, 0m, "Unit")
        {
            DeliveredQty = 5m,
            ReturnedQty = 10m,
            BilledQty = 4m
        };

        soItem.BillableQty.ShouldBe(5m);
        soItem.PendingBillingQty.ShouldBe(1m);
    }
}
