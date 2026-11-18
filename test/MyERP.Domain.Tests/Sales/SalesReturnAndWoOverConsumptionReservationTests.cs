using System;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class SalesReturnAndWoOverConsumptionReservationTests
{
    [Fact]
    public void SalesOrderItem_PendingReservedDeliveryQty_ClampsReturnedDeliveriesToZero()
    {
        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, itemId, "Widget", 10m, 25m, 0m, "Nos");

        soItem.PendingReservedDeliveryQty.ShouldBe(10m);

        // Delivered 10, then returned 10 -> DeliveredQty drops to 0 and ReturnedQty = 10
        soItem.ReturnedQty = 10m;
        soItem.DeliveredQty = 0m;

        // Per ERPNext commit 01d061402d: net_reserved = max(0, qty - delivered - returned) = 0
        soItem.PendingReservedDeliveryQty.ShouldBe(0m);
        soItem.CalculateReservationReleaseForDelivery(10m).ShouldBe(0m);
        soItem.CalculateReservationRestoreForDeliveryCancel(10m).ShouldBe(0m);
    }

    [Fact]
    public void SalesOrderItem_PartialReturnAndReplacement_ReleasesOnlyRemainingReservation()
    {
        var soId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, itemId, "Packaged Widget", 10m, 25m, 0m, "Nos")
        {
            ConversionFactor = 2m
        };

        // Initial delivery of 6 out of 10 -> releases 12 stock units (remaining = 4 order / 8 stock)
        soItem.CalculateReservationReleaseForDelivery(6m).ShouldBe(12m);
        soItem.DeliveredQty = 6m;

        // Customer returns 4 of those 6 -> DeliveredQty = 2, ReturnedQty = 4
        // Net still reserved in bin = max(0, 10 - 2 - 4) = 4 order units (8 stock units)
        soItem.DeliveredQty = 2m;
        soItem.ReturnedQty = 4m;
        soItem.PendingReservedDeliveryQty.ShouldBe(4m);

        // Replacement delivery of 6: first 4 release remaining 8 stock units; extra 2 release 0
        soItem.CalculateReservationReleaseForDelivery(6m).ShouldBe(8m);
    }

    [Fact]
    public void WorkOrderItem_GetPendingProductionReservationQty_ZeroWhenConsumedPastRequiredQty()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var woItem = new WorkOrderItem(Guid.NewGuid(), woId, itemId, "Steel Sheet", 10m)
        {
            TransferredQuantity = 6m,
            ConsumedQuantity = 12m
        };

        // Per ERPNext commit bb29dd2027: consumed > required reserves 0 even with skipTransfer false
        woItem.GetPendingProductionReservationQty(skipTransfer: false).ShouldBe(0m);
        woItem.GetPendingProductionReservationQty(skipTransfer: true).ShouldBe(0m);
    }

    [Fact]
    public void WorkOrderItem_CalculateProductionReservationReleaseForConsumption_CapsAtRequiredQty()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var woItem = new WorkOrderItem(Guid.NewGuid(), woId, itemId, "Steel Sheet", 10m)
        {
            ConsumedQuantity = 8m
        };

        // Consuming 5 units when only 2 required remain must release only 2 (never 5)
        woItem.CalculateProductionReservationReleaseForConsumption(5m).ShouldBe(2m);

        // Once consumed past required, further consumption releases 0
        woItem.ConsumedQuantity = 13m;
        woItem.CalculateProductionReservationReleaseForConsumption(3m).ShouldBe(0m);
    }
}
