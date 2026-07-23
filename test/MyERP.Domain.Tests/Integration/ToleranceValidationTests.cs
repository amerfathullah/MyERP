using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for over-delivery/receipt tolerance validation.
/// Per ERPNext StatusUpdater: max_allowed = ordered_qty × (1 + allowance_pct / 100).
/// The tolerance comes from Company.OverDeliveryReceiptAllowance.
/// </summary>
public class ToleranceValidationTests
{
    private static SalesOrder CreateSO(decimal qty)
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-TEST-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Test Item", qty, 100m, 0m);
        return so;
    }

    private static PurchaseOrder CreatePO(decimal qty)
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-TEST-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test Item", qty, 50m, 0m);
        return po;
    }

    // --- Over-Delivery Tolerance ---

    [Fact]
    public void DeliveryQty_ExactPending_Passes_ZeroTolerance()
    {
        var so = CreateSO(10);
        var soItem = so.Items.First();

        // Should not throw — delivering exact pending qty
        var ex = Record.Exception(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 10m, 0m));
        ex.ShouldBeNull();
    }

    [Fact]
    public void DeliveryQty_Over_Throws_ZeroTolerance()
    {
        var so = CreateSO(10);
        var soItem = so.Items.First();

        // Should throw — delivering more than ordered with 0% tolerance
        Should.Throw<Volo.Abp.BusinessException>(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 10.01m, 0m));
    }

    [Fact]
    public void DeliveryQty_WithinTolerance_5Percent_Passes()
    {
        var so = CreateSO(100);
        var soItem = so.Items.First();

        // 5% tolerance: max allowed = 100 × 1.05 = 105
        var ex = Record.Exception(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 105m, 5m));
        ex.ShouldBeNull();
    }

    [Fact]
    public void DeliveryQty_ExceedsTolerance_5Percent_Throws()
    {
        var so = CreateSO(100);
        var soItem = so.Items.First();

        // 5% tolerance: max allowed = 105, requesting 106
        Should.Throw<Volo.Abp.BusinessException>(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 106m, 5m));
    }

    [Fact]
    public void DeliveryQty_PartialDelivered_ToleranceOnRemaining()
    {
        var so = CreateSO(100);
        var soItem = so.Items.First();
        // Simulate 80 already delivered
        soItem.DeliveredQty = 80;

        // Max total = 105 (5%), remaining = 105 - 80 = 25
        var ex = Record.Exception(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 25m, 5m));
        ex.ShouldBeNull();

        // 26 exceeds remaining allowed
        Should.Throw<Volo.Abp.BusinessException>(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 26m, 5m));
    }

    // --- Over-Receipt Tolerance ---

    [Fact]
    public void ReceiptQty_ExactPending_Passes_ZeroTolerance()
    {
        var po = CreatePO(20);
        var poItem = po.Items.First();

        var ex = Record.Exception(() =>
            new PurchaseOrderManager(null!, null!, null!).ValidateReceiptQty(po, poItem.ItemId, 20m, 0m));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ReceiptQty_Over_Throws_ZeroTolerance()
    {
        var po = CreatePO(20);
        var poItem = po.Items.First();

        Should.Throw<Volo.Abp.BusinessException>(() =>
            new PurchaseOrderManager(null!, null!, null!).ValidateReceiptQty(po, poItem.ItemId, 20.01m, 0m));
    }

    [Fact]
    public void ReceiptQty_WithinTolerance_10Percent_Passes()
    {
        var po = CreatePO(50);
        var poItem = po.Items.First();

        // 10% tolerance: max = 50 × 1.10 = 55
        var ex = Record.Exception(() =>
            new PurchaseOrderManager(null!, null!, null!).ValidateReceiptQty(po, poItem.ItemId, 55m, 10m));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ReceiptQty_ExceedsTolerance_10Percent_Throws()
    {
        var po = CreatePO(50);
        var poItem = po.Items.First();

        // 10% tolerance: max = 55, requesting 56
        Should.Throw<Volo.Abp.BusinessException>(() =>
            new PurchaseOrderManager(null!, null!, null!).ValidateReceiptQty(po, poItem.ItemId, 56m, 10m));
    }

    [Fact]
    public void ReceiptQty_PartialReceived_ToleranceOnRemaining()
    {
        var po = CreatePO(100);
        var poItem = po.Items.First();
        // Simulate 90 already received
        poItem.ReceivedQty = 90;

        // Max total = 110 (10%), remaining = 110 - 90 = 20
        var ex = Record.Exception(() =>
            new PurchaseOrderManager(null!, null!, null!).ValidateReceiptQty(po, poItem.ItemId, 20m, 10m));
        ex.ShouldBeNull();

        // 21 exceeds remaining allowed
        Should.Throw<Volo.Abp.BusinessException>(() =>
            new PurchaseOrderManager(null!, null!, null!).ValidateReceiptQty(po, poItem.ItemId, 21m, 10m));
    }

    // --- Edge Cases ---

    [Fact]
    public void DeliveryQty_UnknownItem_Passes()
    {
        var so = CreateSO(10);
        // Non-existent item ID should just return without throwing
        var ex = Record.Exception(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, Guid.NewGuid(), 999m, 0m));
        ex.ShouldBeNull();
    }

    [Fact]
    public void DeliveryQty_LargeAllowance_50Percent()
    {
        var so = CreateSO(100);
        var soItem = so.Items.First();

        // 50% tolerance: max = 150
        var ex = Record.Exception(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 150m, 50m));
        ex.ShouldBeNull();

        Should.Throw<Volo.Abp.BusinessException>(() =>
            new SalesOrderManager(null!).ValidateDeliveryQty(so, soItem.ItemId, 151m, 50m));
    }
}
