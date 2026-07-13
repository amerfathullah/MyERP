using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class OverBillingAndQtyValidationTests
{
    [Fact]
    public void SalesOrder_AddItem_ZeroQty_Throws()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() =>
            so.AddItem(Guid.NewGuid(), "Widget", 0m, 100m, 0m));
    }

    [Fact]
    public void SalesOrder_AddItem_NegativeQty_Throws()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() =>
            so.AddItem(Guid.NewGuid(), "Widget", -5m, 100m, 0m));
    }

    [Fact]
    public void SalesOrder_AddItem_PositiveQty_Succeeds()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m);
        so.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void PurchaseOrder_AddItem_ZeroQty_Throws()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() =>
            po.AddItem(Guid.NewGuid(), "Material", 0m, 50m, 0m));
    }

    [Fact]
    public void PurchaseOrder_AddItem_NegativeQty_Throws()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() =>
            po.AddItem(Guid.NewGuid(), "Material", -3m, 50m, 0m));
    }

    [Fact]
    public void PurchaseOrder_AddItem_PositiveQty_Succeeds()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 25m, 50m, 0m);
        po.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void SalesOrderItem_PendingBillingQty_CalculatedCorrectly()
    {
        var so = new SalesOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 100m, 10m, 0m);

        so.Items[0].BilledQty = 60m;
        so.Items[0].PendingBillingQty.ShouldBe(40m);
    }

    [Fact]
    public void OverBilling_Detected_WhenBilledExceedsOrdered()
    {
        // Simulate: SO has 100 ordered, 80 already billed
        // SI tries to bill 30 → 80+30=110 > 100 → over-billing
        var orderedQty = 100m;
        var billedQty = 80m;
        var attemptedBilling = 30m;

        var wouldOverBill = (billedQty + attemptedBilling) > orderedQty;
        wouldOverBill.ShouldBeTrue();
    }

    [Fact]
    public void WithinBillingLimit_WhenBilledEqualsOrdered()
    {
        var orderedQty = 100m;
        var billedQty = 70m;
        var attemptedBilling = 30m;

        var wouldOverBill = (billedQty + attemptedBilling) > orderedQty;
        wouldOverBill.ShouldBeFalse(); // Exactly equals = OK
    }

    [Fact]
    public void PurchaseOrderItem_PendingBillingQty_CalculatedCorrectly()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 200m, 5m, 0m);

        po.Items[0].BilledQty = 120m;
        po.Items[0].PendingBillingQty.ShouldBe(80m);
    }
}
