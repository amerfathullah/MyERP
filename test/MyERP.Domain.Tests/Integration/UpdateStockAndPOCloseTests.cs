using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class UpdateStockAndPOCloseTests
{
    [Fact]
    public void SalesInvoice_UpdateStock_DefaultFalse()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.UpdateStock.ShouldBeFalse();
        si.WarehouseId.ShouldBeNull();
    }

    [Fact]
    public void SalesInvoice_CanSetUpdateStock()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.UpdateStock = true;
        si.WarehouseId = Guid.NewGuid();
        si.UpdateStock.ShouldBeTrue();
        si.WarehouseId.ShouldNotBeNull();
    }

    [Fact]
    public void PurchaseOrder_Close_ReleasesOrderedQty_Concept()
    {
        // PO with 100 ordered, 60 received → 40 pending
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 100m, 5m, 0m);
        po.Items[0].ReceivedQty = 60m;

        // Pending receipt qty = what would be released on close
        po.Items[0].PendingReceiptQty.ShouldBe(40m);
    }

    [Fact]
    public void PurchaseOrder_FullyReceived_NoPendingToRelease()
    {
        var po = new PurchaseOrder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 50m, 10m, 0m);
        po.Items[0].ReceivedQty = 50m;
        po.Items[0].PendingReceiptQty.ShouldBe(0m);
    }

    [Fact]
    public void UomConversion_Convert_SameUom_ReturnsOne()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Unit", "Unit", 1m);
        // Same UOM: factor is always 1
        conv.ConversionFactor.ShouldBe(1m);
    }

    [Fact]
    public void UomConversion_Convert_BoxToUnit()
    {
        // 1 Box = 12 Units
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m);
        conv.Convert(5m).ShouldBe(60m); // 5 boxes = 60 units
    }

    [Fact]
    public void UomConversion_ReverseConvert()
    {
        // 1 Box = 12 Units → 1 Unit = 1/12 Box
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m);
        conv.ReverseConvert(24m).ShouldBe(2m); // 24 units = 2 boxes
    }

    [Fact]
    public void UomConversion_ItemSpecific()
    {
        // Item-specific: 1 Drum of Item X = 208.198 Litres
        var itemId = Guid.NewGuid();
        var conv = new UomConversion(Guid.NewGuid(), "Drum", "Litre", 208.198m, itemId);
        conv.ItemId.ShouldBe(itemId);
        conv.Convert(2m).ShouldBe(416.396m);
    }
}
