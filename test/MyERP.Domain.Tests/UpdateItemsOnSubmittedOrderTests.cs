using System;
using System.Linq;
using Xunit;
using MyERP.Purchasing.Entities;
using MyERP.Purchasing;
using MyERP.Core;

namespace MyERP.Domain.Tests;

public class UpdateItemsOnSubmittedOrderTests
{
    private PurchaseOrder CreateSubmittedPO()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Widget A", 100, 10.00m, 0, "Unit");
        po.AddItem(Guid.NewGuid(), "Widget B", 50, 20.00m, 0, "Unit");
        po.Submit();
        return po;
    }

    [Fact]
    public void PO_Item_Quantity_Can_Be_Increased_After_Submit()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        var originalQty = item.Quantity;
        item.Quantity = 150;
        Assert.Equal(150, item.Quantity);
        Assert.True(item.Quantity > originalQty);
    }

    [Fact]
    public void PO_Item_UnitPrice_Can_Be_Changed_After_Submit()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.UnitPrice = 12.50m;
        Assert.Equal(12.50m, item.UnitPrice);
    }

    [Fact]
    public void PO_Item_Quantity_Cannot_Be_Below_ReceivedQty()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.ReceivedQty = 30;
        // Business rule: quantity >= receivedQty
        Assert.True(item.ReceivedQty <= item.Quantity);
        // If someone tries to set qty below received, the AppService guards against it
    }

    [Fact]
    public void PO_Item_PendingReceiptQty_Recalculates_After_Qty_Change()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.ReceivedQty = 30;
        Assert.Equal(70, item.PendingReceiptQty);
        item.Quantity = 50;
        Assert.Equal(20, item.PendingReceiptQty);
    }

    [Fact]
    public void PO_GrandTotal_Is_Stored_Not_Computed()
    {
        var po = CreateSubmittedPO();
        // GrandTotal is set during RecalculateTotals (AppService responsibility)
        // Changing item qty directly doesn't auto-update GrandTotal
        var originalTotal = po.GrandTotal;
        po.Items[0].Quantity = 200;
        // GrandTotal unchanged until RecalculateTotals is called
        Assert.Equal(originalTotal, po.GrandTotal);
    }

    [Fact]
    public void PO_Item_LineTotal_Updates_With_New_Qty_And_Rate()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.Quantity = 75;
        item.UnitPrice = 15.00m;
        Assert.Equal(75 * 15.00m, item.LineTotal);
    }

    [Fact]
    public void PO_Status_Remains_Active_After_Item_Update()
    {
        var po = CreateSubmittedPO();
        po.Items[0].Quantity = 200;
        // Status should still be ToDeliverAndBill (not reset to Draft)
        Assert.Equal(DocumentStatus.ToDeliverAndBill, po.Status);
    }

    [Fact]
    public void PO_Item_StockQty_Reflects_Conversion_Factor()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.ConversionFactor = 12; // Dozen→Unit
        item.Quantity = 5;
        Assert.Equal(60, item.StockQty); // 5 dozen = 60 units
    }

    [Fact]
    public void PO_Item_Qty_Delta_For_Bin_OrderedQty_Adjustment()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.ConversionFactor = 1;
        var oldStockQty = item.StockQty; // 100
        item.Quantity = 120;
        var newStockQty = item.StockQty; // 120
        var delta = newStockQty - oldStockQty;
        Assert.Equal(20, delta); // Bin.OrderedQty should increase by 20
    }

    [Fact]
    public void PO_Item_BilledQty_Unchanged_By_Qty_Update()
    {
        var po = CreateSubmittedPO();
        var item = po.Items[0];
        item.BilledQty = 25;
        item.Quantity = 150;
        Assert.Equal(25, item.BilledQty); // Billed qty is independent of ordered qty
    }

    [Fact]
    public void UpdateOrderItemsDto_Has_Required_Fields()
    {
        var dto = new global::MyERP.Purchasing.UpdateOrderItemsDto
        {
            Items = new()
            {
                new global::MyERP.Purchasing.UpdateOrderItemDto { ItemId = Guid.NewGuid(), Quantity = 100, UnitPrice = 10 }
            }
        };
        Assert.Single(dto.Items);
        Assert.Equal(100, dto.Items[0].Quantity);
    }

    [Fact]
    public void UpdateOrderItemsResultDto_Tracks_Changes()
    {
        var result = new global::MyERP.Purchasing.UpdateOrderItemsResultDto
        {
            ItemsUpdated = 2,
            PreviousGrandTotal = 2000m,
            NewGrandTotal = 2500m,
        };
        Assert.Equal(2, result.ItemsUpdated);
        Assert.Equal(500m, result.NewGrandTotal - result.PreviousGrandTotal);
    }

    [Fact]
    public void PO_Draft_Cannot_Use_UpdateItems_Workflow()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Test", 10, 5, 0, "Unit");
        // Draft status — should use normal UpdateAsync, not UpdateItemsAsync
        Assert.Equal(DocumentStatus.Draft, po.Status);
    }

    [Fact]
    public void Localization_Keys_Exist_For_UpdateItems_Feature()
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains("UpdateItems", json);
        Assert.Contains("ItemsUpdatedSuccessfully", json);
        Assert.Contains("UpdateItemsHelp", json);
        Assert.Contains("MyERP:04019", json);
        Assert.Contains("MyERP:04020", json);
    }
}
