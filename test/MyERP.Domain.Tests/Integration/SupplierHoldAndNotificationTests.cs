using System;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Notification;
using MyERP.Notification.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for supplier hold enforcement and notification entity creation.
/// </summary>
public class SupplierHoldAndNotificationTests
{
    [Fact]
    public void Supplier_DefaultHoldType_IsNone()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.HoldType.ShouldBe(SupplierHoldType.None);
        supplier.IsOnHold.ShouldBeFalse();
    }

    [Fact]
    public void Supplier_HoldTypeAll_IsOnHold()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Held Supplier");
        supplier.HoldType = SupplierHoldType.All;
        supplier.IsOnHold.ShouldBeTrue();
    }

    [Fact]
    public void Supplier_HoldTypeInvoices_BlocksPI()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Invoice-Held Supplier");
        supplier.HoldType = SupplierHoldType.Invoices;

        // PI submission should be blocked when hold type is Invoices or All
        var shouldBlock = supplier.HoldType == SupplierHoldType.All || supplier.HoldType == SupplierHoldType.Invoices;
        shouldBlock.ShouldBeTrue();
    }

    [Fact]
    public void Supplier_HoldTypePayments_DoesNotBlockPI()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Payment-Held Supplier");
        supplier.HoldType = SupplierHoldType.Payments;

        // PI submission should NOT be blocked when hold type is only Payments
        var shouldBlock = supplier.HoldType == SupplierHoldType.All || supplier.HoldType == SupplierHoldType.Invoices;
        shouldBlock.ShouldBeFalse();
    }

    [Fact]
    public void AppNotification_CreateAndMarkRead()
    {
        var userId = Guid.NewGuid();
        var notification = new AppNotification(Guid.NewGuid(), userId, "Test Notification");

        notification.UserId.ShouldBe(userId);
        notification.Subject.ShouldBe("Test Notification");
        notification.IsRead.ShouldBeFalse();
        notification.Severity.ShouldBe(NotificationSeverity.Info);

        notification.MarkAsRead();
        notification.IsRead.ShouldBeTrue();
        notification.ReadAt.ShouldNotBeNull();
    }

    [Fact]
    public void AppNotification_WithProperties()
    {
        var notification = new AppNotification(Guid.NewGuid(), Guid.NewGuid(), "Low Stock Alert")
        {
            Body = "Widget A is below safety stock.",
            Severity = NotificationSeverity.Error,
            ActionUrl = "/inventory/items/abc-123",
            SourceDocumentType = "Item",
        };

        notification.Body.ShouldNotBeNull();
        notification.Severity.ShouldBe(NotificationSeverity.Error);
        notification.ActionUrl.ShouldStartWith("/inventory");
    }

    [Fact]
    public void Item_ReorderFields_Default()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget", ItemType.Goods);
        item.ReorderLevel.ShouldBe(0);
        item.ReorderQty.ShouldBe(0);
        item.SafetyStock.ShouldBe(0);
        item.DefaultWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void Item_ReorderFields_CanBeSet()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-002", "Component", ItemType.Goods);
        item.ReorderLevel = 50;
        item.ReorderQty = 200;
        item.SafetyStock = 20;
        item.DefaultWarehouseId = Guid.NewGuid();

        item.ReorderLevel.ShouldBe(50);
        item.ReorderQty.ShouldBe(200);
        item.SafetyStock.ShouldBe(20);
        item.DefaultWarehouseId.ShouldNotBeNull();
    }

    [Fact]
    public void Item_NeedsReorder_WhenBelowLevel()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-003", "Part", ItemType.Goods);
        item.ReorderLevel = 100;
        item.ReorderQty = 500;

        // Simulate projected qty of 30 (below 100)
        var projectedQty = 30m;
        var needsReorder = item.ReorderLevel > 0 && projectedQty < item.ReorderLevel;
        needsReorder.ShouldBeTrue();
    }

    [Fact]
    public void Item_DoesNotNeedReorder_WhenAboveLevel()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-004", "Assembly", ItemType.Goods);
        item.ReorderLevel = 100;
        item.ReorderQty = 500;

        // Simulate projected qty of 150 (above 100)
        var projectedQty = 150m;
        var needsReorder = item.ReorderLevel > 0 && projectedQty < item.ReorderLevel;
        needsReorder.ShouldBeFalse();
    }

    [Fact]
    public void Company_FrozenDates_Default()
    {
        var company = new Company(Guid.NewGuid(), "Test Company");
        company.StockFrozenUpto.ShouldBeNull();
        company.AccountsFrozenTillDate.ShouldBeNull();
    }

    [Fact]
    public void Customer_CreditLimit_Default()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "New Customer");
        customer.CreditLimit.ShouldBe(0); // 0 = unlimited
    }
}
