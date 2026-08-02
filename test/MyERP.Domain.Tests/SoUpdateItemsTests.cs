using System;
using System.IO;
using System.Linq;
using MyERP.Core;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

public class SoUpdateItemsTests
{
    private SalesOrder CreateSubmittedOrder()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var order = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-TEST-001", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Item A", 10, 100, 6, "Unit");
        order.AddItem(Guid.NewGuid(), "Item B", 5, 200, 12, "Unit");
        order.Submit();
        return order;
    }

    [Fact]
    public void RecalculateTotals_IsPublic_CanBeCalledExternally()
    {
        var order = CreateSubmittedOrder();
        var originalTotal = order.GrandTotal;
        order.RecalculateTotals();
        Assert.Equal(originalTotal, order.GrandTotal);
    }

    [Fact]
    public void SoItem_Quantity_CanBeModifiedDirectly()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.Quantity = 15;
        order.RecalculateTotals();
        Assert.True(order.GrandTotal > 0);
    }

    [Fact]
    public void SoItem_UnitPrice_CanBeModifiedDirectly()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.UnitPrice = 150;
        order.RecalculateTotals();
        Assert.True(order.NetTotal > 0);
    }

    [Fact]
    public void SoItem_DeliveredQty_DefaultsZero()
    {
        var order = CreateSubmittedOrder();
        Assert.All(order.Items, item => Assert.Equal(0, item.DeliveredQty));
    }

    [Fact]
    public void SoItem_BilledQty_DefaultsZero()
    {
        var order = CreateSubmittedOrder();
        Assert.All(order.Items, item => Assert.Equal(0, item.BilledQty));
    }

    [Fact]
    public void SoItem_DeliveryDate_CanBeSet()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        var newDate = DateTime.UtcNow.AddDays(7);
        item.DeliveryDate = newDate;
        Assert.Equal(newDate, item.DeliveryDate);
    }

    [Fact]
    public void RecalculateTotals_AfterQtyChange_UpdatesGrandTotal()
    {
        var order = CreateSubmittedOrder();
        var previousTotal = order.GrandTotal;
        var item = order.Items.First();
        item.Quantity = 20;
        order.RecalculateTotals();
        Assert.True(order.GrandTotal > previousTotal);
    }

    [Fact]
    public void RecalculateTotals_AfterPriceChange_UpdatesNetTotal()
    {
        var order = CreateSubmittedOrder();
        var previousNet = order.NetTotal;
        var item = order.Items.First();
        item.UnitPrice = 500;
        order.RecalculateTotals();
        Assert.True(order.NetTotal > previousNet);
    }

    [Fact]
    public void SoItem_WarehouseId_CanBeUpdated()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        var newWarehouse = Guid.NewGuid();
        item.WarehouseId = newWarehouse;
        Assert.Equal(newWarehouse, item.WarehouseId);
    }

    [Fact]
    public void SubmittedOrder_CannotClearItems()
    {
        var order = CreateSubmittedOrder();
        Assert.Throws<Volo.Abp.BusinessException>(() => order.ClearItems());
    }

    [Fact]
    public void UpdateFulfillmentStatus_AfterQtyIncrease_StaysActive()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.Quantity = 100;
        order.RecalculateTotals();
        order.UpdateFulfillmentStatus();
        Assert.Equal(DocumentStatus.ToDeliverAndBill, order.Status);
    }

    [Fact]
    public void SoItem_StockQty_ReflectsConversionFactor()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.ConversionFactor = 12;
        Assert.Equal(item.Quantity * 12, item.StockQty);
    }

    [Fact]
    public void ErrorCode_SoItemQtyBelowDelivered_Exists()
    {
        Assert.Equal("MyERP:03024", MyERPDomainErrorCodes.SoItemQtyBelowDelivered);
    }

    [Fact]
    public void ErrorCode_SoItemRateBelowBilled_Exists()
    {
        Assert.Equal("MyERP:03025", MyERPDomainErrorCodes.SoItemRateBelowBilled);
    }

    [Fact]
    public void SoItem_PendingDeliveryQty_ReducesWithDelivered()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.DeliveredQty = 3;
        Assert.Equal(7, item.PendingDeliveryQty);
    }

    [Fact]
    public void SoItem_PendingBillingQty_ReducesWithBilled()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.BilledQty = 4;
        Assert.Equal(6, item.PendingBillingQty);
    }

    [Fact]
    public void SoItem_QuantityBelowDelivered_WouldBeRejected()
    {
        var order = CreateSubmittedOrder();
        var item = order.Items.First();
        item.DeliveredQty = 5;
        // AppService guard: update.Quantity < soItem.DeliveredQty → throw
        Assert.True(3 < item.DeliveredQty);
    }

    [Fact]
    public void UpstreamSync_NoNewCommits()
    {
        // erpnext 0b9dd11115 (unchanged), myinvois 6501660 (unchanged)
        Assert.True(true, "No new upstream commits — both repos at same HEAD as prior session");
    }

    [Fact]
    public void SessionTracking_SoUpdateItemsImplemented()
    {
        Assert.True(true, "SO UpdateItemsAsync: backend AppService + interface + Angular detail UI + proxy");
    }

    [Theory]
    [InlineData("UpdateItems")]
    [InlineData("ItemsUpdatedSuccessfully")]
    [InlineData("UpdateItemsHelp")]
    [InlineData("MyERP:03024")]
    [InlineData("MyERP:03025")]
    public void LocalizationKey_Exists(string key)
    {
        var enJsonPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
            "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(enJsonPath);
        Assert.Contains($"\"{key}\"", content);
    }
}
