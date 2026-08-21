using System;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Purchasing;

/// <summary>
/// Unit tests for Purchase Order PerReceived calculation (Gotcha #370):
/// - Formula: SUM(MIN(item.received_qty, item.qty)) / SUM(item.qty) * 100
/// - Over-receipt on one item does not inflate overall percentage beyond 100% of that item
/// - Partial receipt calculates weighted average completion across items
/// </summary>
public class PurchaseOrderPerReceivedTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    [Fact]
    public void PurchaseOrder_PerReceived_OverReceiptOnOneItem_CappedAtOrderedQty()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-2026-0001", DateTime.UtcNow);
        po.AddItem(_itemId1, "Item A", 10m, 100m, 0m);
        po.AddItem(_itemId2, "Item B", 10m, 50m, 0m);

        // Item 1 received 15 (capped at 100%), Item 2 received 5 (50%)
        po.Items[0].ReceivedQty = 15m;
        po.Items[1].ReceivedQty = 5m;

        // Min(100%, 50%) = 50%
        Assert.Equal(50m, po.PerReceived);
    }

    [Fact]
    public void PurchaseOrder_PerReceived_FullyReceived_Returns100Percent()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-2026-0002", DateTime.UtcNow);
        po.AddItem(_itemId1, "Item A", 10m, 100m, 0m);
        po.AddItem(_itemId2, "Item B", 20m, 50m, 0m);

        po.Items[0].ReceivedQty = 10m;
        po.Items[1].ReceivedQty = 20m;

        Assert.Equal(100m, po.PerReceived);
    }

    [Fact]
    public void PurchaseOrder_PerReceived_NoReceipt_ReturnsZero()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-2026-0003", DateTime.UtcNow);
        po.AddItem(_itemId1, "Item A", 10m, 100m, 0m);

        Assert.Equal(0m, po.PerReceived);
    }
}
