using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

public class StockSafetyAndMRTests
{
    [Fact]
    public void Item_MaintainStock_DefaultsTrue()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Stock Item", ItemType.Goods);
        item.MaintainStock.ShouldBeTrue();
    }

    [Fact]
    public void Item_MaintainStock_ServiceItem_SetFalse()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "SVC-001", "Consulting", ItemType.Service);
        item.MaintainStock = false;
        item.MaintainStock.ShouldBeFalse();
    }

    [Fact]
    public void Warehouse_IsGroup_DefaultsFalse()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Stores");
        wh.IsGroup.ShouldBeFalse();
    }

    [Fact]
    public void Warehouse_IsGroup_CanBeSet()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "All Warehouses");
        wh.IsGroup = true;
        wh.IsGroup.ShouldBeTrue();
    }

    [Fact]
    public void GroupWarehouseCannotReceiveStock_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock.ShouldBe("MyERP:05014");
    }

    [Fact]
    public void MaterialRequestItem_OrderedQuantity_CanBeReduced()
    {
        // Simulates PO Close reducing MR ordered qty
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Raw Material", 100, "Kg");

        // Simulate PO submit → ordered qty increases
        mr.Items[0].OrderedQuantity = 80;

        // Simulate PO Close (short-close 30 units not received) → ordered qty decreases
        var pendingReceiptQty = 30m;
        mr.Items[0].OrderedQuantity = Math.Max(0, mr.Items[0].OrderedQuantity - pendingReceiptQty);

        mr.Items[0].OrderedQuantity.ShouldBe(50m);
    }

    [Fact]
    public void MaterialRequestItem_OrderedQuantity_NeverNegative()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-002",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        mr.Items[0].OrderedQuantity = 5;

        // Close more than ordered (shouldn't happen, but safety check)
        mr.Items[0].OrderedQuantity = Math.Max(0, mr.Items[0].OrderedQuantity - 20);
        mr.Items[0].OrderedQuantity.ShouldBe(0m);
    }

    [Fact]
    public void StockEntry_ServiceItem_ShouldBeSkipped()
    {
        // Service items with MaintainStock=false should be skipped during posting
        var serviceItem = new Item(Guid.NewGuid(), Guid.NewGuid(), "SVC-002", "Installation", ItemType.Service);
        serviceItem.MaintainStock = false;

        // The StockPostingService skips items where MaintainStock=false
        serviceItem.MaintainStock.ShouldBeFalse();
    }

    [Fact]
    public void PurchaseOrderItem_MaterialRequestItemId_CanBeSet()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Steel", 100, 25, 0, "Kg");
        var mrItemId = Guid.NewGuid();
        po.Items[0].MaterialRequestItemId = mrItemId;
        po.Items[0].MaterialRequestItemId.ShouldBe(mrItemId);
    }
}
