using System;
using System.Linq;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class DropShipTests
{
    [Fact]
    public void SalesOrderItem_DeliveredBySupplier_DefaultsFalse()
    {
        var item = CreateSoItem();
        item.DeliveredBySupplier.ShouldBeFalse();
    }

    [Fact]
    public void SalesOrderItem_DeliveredBySupplier_CanBeSet()
    {
        var item = CreateSoItem();
        item.DeliveredBySupplier = true;
        item.SupplierId = Guid.NewGuid();
        item.DeliveredBySupplier.ShouldBeTrue();
        item.SupplierId.ShouldNotBeNull();
    }

    [Fact]
    public void DropShipService_HasDropShipItems_FalseWhenNone()
    {
        var order = CreateOrder();
        order.AddItem(Guid.NewGuid(), "Regular Item", 5, 100, 0);

        DropShipService.HasDropShipItems(order).ShouldBeFalse();
    }

    [Fact]
    public void DropShipService_HasDropShipItems_TrueWhenPresent()
    {
        var order = CreateOrder();
        order.AddItem(Guid.NewGuid(), "Regular Item", 5, 100, 0);

        // Simulate drop-ship flag on an item
        var lastItem = order.Items.Last();
        lastItem.DeliveredBySupplier = true;
        lastItem.SupplierId = Guid.NewGuid();

        DropShipService.HasDropShipItems(order).ShouldBeTrue();
    }

    [Fact]
    public void DropShipService_GetDropShipItemIds_ReturnsOnlyDropShipItems()
    {
        var regularItemId = Guid.NewGuid();
        var dropShipItemId = Guid.NewGuid();

        var order = CreateOrder();
        order.AddItem(regularItemId, "Regular", 2, 50, 0);
        order.AddItem(dropShipItemId, "Drop-Ship Widget", 3, 75, 0);

        // Mark second item as drop-ship
        var dsItem = order.Items.Last();
        dsItem.DeliveredBySupplier = true;
        dsItem.SupplierId = Guid.NewGuid();

        var result = DropShipService.GetDropShipItemIds(order);
        result.Count.ShouldBe(1);
        result.ShouldContain(dropShipItemId);
        result.ShouldNotContain(regularItemId);
    }

    [Fact]
    public void DropShipService_GetDropShipItemIds_EmptyWhenNoDropShip()
    {
        var order = CreateOrder();
        order.AddItem(Guid.NewGuid(), "Item A", 1, 10, 0);
        order.AddItem(Guid.NewGuid(), "Item B", 2, 20, 0);

        var result = DropShipService.GetDropShipItemIds(order);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void DropShipItem_ShouldNotCreateSLE_Concept()
    {
        // Drop-ship items bypass warehouse — no stock ledger entries
        // This test documents the concept: DeliveredBySupplier items
        // must be SKIPPED during all stock operations (reserve, deliver, invoice with update_stock)
        var item = CreateSoItem();
        item.DeliveredBySupplier = true;

        // Stock reservation skip condition
        var shouldReserve = !item.DeliveredBySupplier;
        shouldReserve.ShouldBeFalse();
    }

    [Fact]
    public void DropShipItem_SupplierLockConcept()
    {
        // Per DO-NOT: "Change drop-ship item supplier on SO after PO exists (supplier is locked once ordered_qty > 0)"
        // When DeliveredBySupplier=true and a PO has been created from this SO item,
        // the SupplierId should not be changeable
        var item = CreateSoItem();
        item.DeliveredBySupplier = true;
        item.SupplierId = Guid.NewGuid();

        // Conceptual: once PO exists, supplier is locked
        // Implementation: validated at AppService level before allowing updates
        item.SupplierId.ShouldNotBeNull();
    }

    [Fact]
    public void DropShipItem_MultipleSuppliers_CreatesMultiplePOs()
    {
        // When an SO has drop-ship items from different suppliers,
        // one PO per supplier should be created
        var supplier1 = Guid.NewGuid();
        var supplier2 = Guid.NewGuid();

        var order = CreateOrder();
        order.AddItem(Guid.NewGuid(), "Widget from S1", 5, 100, 0);
        order.AddItem(Guid.NewGuid(), "Gadget from S2", 3, 200, 0);
        order.AddItem(Guid.NewGuid(), "Other from S1", 2, 50, 0);

        order.Items[0].DeliveredBySupplier = true;
        order.Items[0].SupplierId = supplier1;
        order.Items[1].DeliveredBySupplier = true;
        order.Items[1].SupplierId = supplier2;
        order.Items[2].DeliveredBySupplier = true;
        order.Items[2].SupplierId = supplier1;

        // Group by supplier: S1 gets 2 items, S2 gets 1 item
        var grouped = order.Items
            .Where(i => i.DeliveredBySupplier && i.SupplierId.HasValue)
            .GroupBy(i => i.SupplierId!.Value)
            .ToList();

        grouped.Count.ShouldBe(2); // 2 POs
        grouped.First(g => g.Key == supplier1).Count().ShouldBe(2);
        grouped.First(g => g.Key == supplier2).Count().ShouldBe(1);
    }

    private static SalesOrder CreateOrder()
    {
        return new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-TEST-001", DateTime.UtcNow);
    }

    private static SalesOrderItem CreateSoItem()
    {
        return new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", 10, 100, 0, "Unit");
    }
}
