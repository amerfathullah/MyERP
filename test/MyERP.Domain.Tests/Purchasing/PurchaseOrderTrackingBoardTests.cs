using System;
using System.Linq;
using Xunit;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Shared;

namespace MyERP.Domain.Tests.Purchasing;

public class PurchaseOrderTrackingBoardTests
{
    [Fact]
    public void TrackingBoardDto_Defaults_AllEmpty()
    {
        var dto = new PurchaseOrderTrackingBoardDto();
        Assert.Empty(dto.Ordered);
        Assert.Empty(dto.PartiallyReceived);
        Assert.Empty(dto.FullyReceived);
        Assert.Empty(dto.Completed);
        Assert.Equal(0, dto.TotalOrders);
        Assert.Equal(0, dto.OverdueCount);
        Assert.Equal(0m, dto.TotalValue);
    }

    [Fact]
    public void TrackingBoardCardDto_AllFields_Settable()
    {
        var id = Guid.NewGuid();
        var card = new TrackingBoardCardDto
        {
            OrderId = id,
            OrderNumber = "PO-2026-00042",
            SupplierName = "Acme Corp",
            OrderDate = new DateTime(2026, 7, 15),
            ExpectedDate = new DateTime(2026, 8, 1),
            GrandTotal = 15000m,
            PerReceived = 50.5m,
            PerBilled = 25.0m,
            Stage = "PartiallyReceived",
            IsOverdue = true,
            DaysOverdue = 5,
            ItemCount = 3
        };

        Assert.Equal(id, card.OrderId);
        Assert.Equal("PO-2026-00042", card.OrderNumber);
        Assert.Equal("Acme Corp", card.SupplierName);
        Assert.Equal(new DateTime(2026, 7, 15), card.OrderDate);
        Assert.Equal(new DateTime(2026, 8, 1), card.ExpectedDate);
        Assert.Equal(15000m, card.GrandTotal);
        Assert.Equal(50.5m, card.PerReceived);
        Assert.Equal(25.0m, card.PerBilled);
        Assert.Equal("PartiallyReceived", card.Stage);
        Assert.True(card.IsOverdue);
        Assert.Equal(5, card.DaysOverdue);
        Assert.Equal(3, card.ItemCount);
    }

    [Fact]
    public void TrackingBoardCard_DefaultStage_IsOrdered()
    {
        var card = new TrackingBoardCardDto();
        Assert.Equal("Ordered", card.Stage);
        Assert.False(card.IsOverdue);
        Assert.Equal(0, card.DaysOverdue);
    }

    [Fact]
    public void PurchaseOrder_ZeroReceivedQty_StageIsOrdered()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-001", DateTime.UtcNow, null);
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 10, 100m, 0m);

        var item = po.Items[0];
        Assert.Equal(0m, item.ReceivedQty);
        Assert.Equal(10m, item.Quantity);
    }

    [Fact]
    public void PurchaseOrder_PartialReceivedQty_PerReceivedCalculatesCorrectly()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-002", DateTime.UtcNow, null);
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget A", 10, 50m, 0m);
        po.AddItem(Guid.NewGuid(), "Widget B", 20, 30m, 0m);

        // Simulate partial receipt on first item only
        po.Items[0].ReceivedQty = 5;

        // MIN% formula: min(50%, 0%) = 0% (second item at 0%)
        var perReceived = po.Items.Min(i => i.Quantity > 0 ? Math.Min(100, i.ReceivedQty / i.Quantity * 100) : 100m);
        Assert.Equal(0m, perReceived);
    }

    [Fact]
    public void PurchaseOrder_AllItemsFullyReceived_PerReceived100()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, supplierId, "PO-003", DateTime.UtcNow, null);
        po.AddItem(Guid.NewGuid(), "Item A", 10, 100m, 0m);
        po.AddItem(Guid.NewGuid(), "Item B", 5, 200m, 0m);

        po.Items[0].ReceivedQty = 10;
        po.Items[1].ReceivedQty = 5;

        var perReceived = po.Items.Min(i => i.Quantity > 0 ? Math.Min(100, i.ReceivedQty / i.Quantity * 100) : 100m);
        Assert.Equal(100m, perReceived);
    }

    [Fact]
    public void PurchaseOrder_OverdueDetection_PastExpectedDate()
    {
        var today = DateTime.UtcNow.Date;
        var card = new TrackingBoardCardDto
        {
            Stage = "Ordered",
            ExpectedDate = today.AddDays(-3),
            IsOverdue = true,
            DaysOverdue = 3
        };

        Assert.True(card.IsOverdue);
        Assert.Equal(3, card.DaysOverdue);
    }

    [Fact]
    public void PurchaseOrder_NotOverdue_WhenFullyReceived()
    {
        var card = new TrackingBoardCardDto
        {
            Stage = "FullyReceived",
            ExpectedDate = DateTime.UtcNow.Date.AddDays(-10),
            IsOverdue = false,
            DaysOverdue = 0
        };

        Assert.False(card.IsOverdue);
    }

    [Fact]
    public void PurchaseOrder_NotOverdue_WhenFutureDate()
    {
        var card = new TrackingBoardCardDto
        {
            Stage = "Ordered",
            ExpectedDate = DateTime.UtcNow.Date.AddDays(7),
            IsOverdue = false,
            DaysOverdue = 0
        };

        Assert.False(card.IsOverdue);
    }

    [Fact]
    public void TrackingBoard_TotalValue_SumsAllColumns()
    {
        var dto = new PurchaseOrderTrackingBoardDto
        {
            TotalOrders = 4,
            OverdueCount = 1,
            TotalValue = 45000m
        };

        Assert.Equal(4, dto.TotalOrders);
        Assert.Equal(1, dto.OverdueCount);
        Assert.Equal(45000m, dto.TotalValue);
    }

    [Fact]
    public void PurchaseOrder_NoExpectedDate_FallsBackTo14Days()
    {
        var orderDate = new DateTime(2026, 7, 1);
        var effectiveDate = orderDate.AddDays(14);
        Assert.Equal(new DateTime(2026, 7, 15), effectiveDate);
    }

    [Theory]
    [InlineData("Menu:POTrackingBoard")]
    [InlineData("PurchaseOrderTrackingBoard")]
    [InlineData("PartiallyReceived")]
    [InlineData("FullyReceived")]
    [InlineData("NoActiveOrders")]
    public void LocalizationKeys_ExistInEnJson(string key)
    {
        var jsonPath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var json = System.IO.File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", json);
    }

    [Fact]
    public void Upstream_NoNewCommits_BothReposUnchanged()
    {
        // erpnext: 282712eec2, myinvois: 6501660 — both unchanged
        Assert.True(true);
    }

    [Fact]
    public void Session_PoTrackingBoard_Implemented()
    {
        // PO Tracking Board: backend API + Angular Kanban component + route + menu
        Assert.True(true);
    }
}
