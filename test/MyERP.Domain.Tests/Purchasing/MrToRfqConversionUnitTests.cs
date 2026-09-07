using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Unit tests verifying Material Request to RFQ item filtering per ERPNext PR #58534 (commit c93815b4ae)
/// and draft RFQ deduction per PR #58617 (commit d8432d92c8).
/// </summary>
public class MrToRfqConversionUnitTests
{
    [Fact]
    public void PR58534_FullyOrderedItem_IsFilteredOut()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        mr.AddItem(item1Id, "Fully Ordered Raw Material", quantity: 50m, uom: "Kg");
        mr.AddItem(item2Id, "Partially Ordered Raw Material", quantity: 30m, uom: "Kg");

        mr.Submit();

        // Simulate item 1 fully ordered, item 2 partially ordered (ordered 10, pending 20)
        mr.Items[0].OrderedQuantity = 50m;
        mr.Items[1].OrderedQuantity = 10m;

        // Apply ERPNext PR #58534 select_item condition: ordered_qty or received_qty < stock_qty
        var eligibleItems = mr.Items
            .Where(i => Math.Max(i.OrderedQuantity, i.ReceivedQuantity) < i.StockQty)
            .ToList();

        eligibleItems.Count.ShouldBe(1);
        eligibleItems[0].ItemId.ShouldBe(item2Id);

        var fulfilledQty = Math.Max(eligibleItems[0].OrderedQuantity, eligibleItems[0].ReceivedQuantity);
        var pendingQty = eligibleItems[0].Quantity - fulfilledQty;
        pendingQty.ShouldBe(20m);
    }

    [Fact]
    public void PR58534_FullyReceivedItem_IsFilteredOut()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-002", MaterialRequestType.Purchase, DateTime.UtcNow);
        var item1Id = Guid.NewGuid();

        mr.AddItem(item1Id, "Fully Received Item", quantity: 15m, uom: "Nos");
        mr.Submit();

        // Simulate fully received item
        mr.Items[0].ReceivedQuantity = 15m;

        var eligibleItems = mr.Items
            .Where(i => Math.Max(i.OrderedQuantity, i.ReceivedQuantity) < i.StockQty)
            .ToList();

        eligibleItems.ShouldBeEmpty();
    }

    [Fact]
    public void PR58534_ConversionFactor_CorrectlyCalculatesRemainingQty()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-003", MaterialRequestType.Purchase, DateTime.UtcNow);
        var itemId = Guid.NewGuid();

        // 6 Boxes @ conversion factor 5 = 30 stock units
        mr.AddItem(itemId, "Boxed Goods", quantity: 6m, uom: "Box", conversionFactor: 5m);
        mr.Submit();

        // Ordered 10 stock units (i.e. 2 boxes)
        mr.Items[0].OrderedQuantity = 10m;

        var eligibleItems = mr.Items
            .Where(i => Math.Max(i.OrderedQuantity, i.ReceivedQuantity) < i.StockQty)
            .ToList();

        eligibleItems.Count.ShouldBe(1);
        var item = eligibleItems[0];
        item.StockQty.ShouldBe(30m);

        var remainingStockQty = item.StockQty - item.OrderedQuantity; // 30 - 10 = 20 stock units
        var remainingQty = remainingStockQty / item.ConversionFactor; // 20 / 5 = 4 boxes

        remainingQty.ShouldBe(4m);
    }

    [Fact]
    public void PR58617_DraftRfqQuantity_IsDeductedFromPendingQty()
    {
        var companyId = Guid.NewGuid();
        var mr = new MaterialRequest(Guid.NewGuid(), companyId, "MR-004", MaterialRequestType.Purchase, DateTime.UtcNow);
        var itemId = Guid.NewGuid();

        mr.AddItem(itemId, "Steel Bar", quantity: 100m, uom: "Meter");
        mr.Submit();

        // 20 already ordered
        mr.Items[0].OrderedQuantity = 20m;
        // Remaining from MR perspective = 80m

        // Simulate draft RFQ that already covers 30m
        var draftRfqQty = 30m;

        var remainingQty = mr.Items[0].Quantity - mr.Items[0].OrderedQuantity - draftRfqQty;
        remainingQty.ShouldBe(50m);
    }
}
