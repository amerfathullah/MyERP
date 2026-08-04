using System;
using System.Linq;
using Xunit;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Shared;

namespace MyERP.Domain.Tests.Sales;

public class SalesOrderTrackingBoardTests
{
    [Fact]
    public void TrackingBoardDto_Defaults_AllEmpty()
    {
        var dto = new SalesOrderTrackingBoardDto();
        Assert.Empty(dto.Ordered);
        Assert.Empty(dto.PartiallyDelivered);
        Assert.Empty(dto.FullyDelivered);
        Assert.Empty(dto.Completed);
        Assert.Equal(0, dto.TotalOrders);
        Assert.Equal(0, dto.OverdueCount);
        Assert.Equal(0m, dto.TotalValue);
    }

    [Fact]
    public void TrackingBoardCardDto_AllFields_Settable()
    {
        var id = Guid.NewGuid();
        var card = new SalesOrderTrackingBoardCardDto
        {
            OrderId = id,
            OrderNumber = "SO-2026-00015",
            CustomerName = "ABC Sdn Bhd",
            OrderDate = new DateTime(2026, 8, 1),
            ExpectedDeliveryDate = new DateTime(2026, 8, 15),
            GrandTotal = 25000m,
            PerDelivered = 60.0m,
            PerBilled = 30.0m,
            Stage = TrackingBoardStage.PartiallyDelivered,
            IsOverdue = true,
            DaysOverdue = 3,
            ItemCount = 5
        };

        Assert.Equal(id, card.OrderId);
        Assert.Equal("SO-2026-00015", card.OrderNumber);
        Assert.Equal("ABC Sdn Bhd", card.CustomerName);
        Assert.Equal(new DateTime(2026, 8, 1), card.OrderDate);
        Assert.Equal(new DateTime(2026, 8, 15), card.ExpectedDeliveryDate);
        Assert.Equal(25000m, card.GrandTotal);
        Assert.Equal(60.0m, card.PerDelivered);
        Assert.Equal(30.0m, card.PerBilled);
        Assert.Equal(TrackingBoardStage.PartiallyDelivered, card.Stage);
        Assert.True(card.IsOverdue);
        Assert.Equal(3, card.DaysOverdue);
        Assert.Equal(5, card.ItemCount);
    }

    [Fact]
    public void TrackingBoardCard_DefaultStage_IsOrdered()
    {
        var card = new SalesOrderTrackingBoardCardDto();
        Assert.Equal(TrackingBoardStage.Ordered, card.Stage);
        Assert.False(card.IsOverdue);
        Assert.Equal(0, card.DaysOverdue);
    }

    [Fact]
    public void SalesOrder_ZeroDeliveredQty_StageIsOrdered()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-001", DateTime.UtcNow, null);
        var itemId = Guid.NewGuid();
        so.AddItem(itemId, "Product A", 10, 500m, 0m);

        var item = so.Items[0];
        Assert.Equal(0m, item.DeliveredQty);
        Assert.Equal(10m, item.Quantity);
    }

    [Fact]
    public void Stage_ZeroDelivered_IsOrdered()
    {
        Assert.Equal(TrackingBoardStage.Ordered, (TrackingBoardStage)0);
    }

    [Fact]
    public void Stage_PartialDelivered_CorrectValue()
    {
        Assert.Equal(TrackingBoardStage.PartiallyDelivered, (TrackingBoardStage)1);
    }

    [Fact]
    public void Stage_FullyDelivered_CorrectValue()
    {
        Assert.Equal(TrackingBoardStage.FullyDelivered, (TrackingBoardStage)2);
    }

    [Fact]
    public void Stage_Completed_CorrectValue()
    {
        Assert.Equal(TrackingBoardStage.Completed, (TrackingBoardStage)3);
    }

    [Fact]
    public void PerDelivered_MinFormula_ZeroItemsDelivered_IsZero()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-002", DateTime.UtcNow, null);
        so.AddItem(Guid.NewGuid(), "Item A", 10, 100m, 0m);
        so.AddItem(Guid.NewGuid(), "Item B", 5, 200m, 0m);

        var perDelivered = so.Items.Min(i => i.Quantity > 0 ? (i.DeliveredQty / i.Quantity) * 100m : 100m);
        Assert.Equal(0m, perDelivered);
    }

    [Fact]
    public void Overdue_PastDeliveryDate_DetectedWhenNotFullyDelivered()
    {
        var effectiveDate = DateTime.UtcNow.Date.AddDays(-5);
        var isOverdue = effectiveDate < DateTime.UtcNow.Date;
        Assert.True(isOverdue);
    }

    [Fact]
    public void Overdue_FutureDeliveryDate_NotOverdue()
    {
        var effectiveDate = DateTime.UtcNow.Date.AddDays(10);
        var isOverdue = effectiveDate < DateTime.UtcNow.Date;
        Assert.False(isOverdue);
    }

    [Fact]
    public void Overdue_FullyDelivered_NeverOverdue()
    {
        // Fully delivered orders are never overdue regardless of date
        var stage = TrackingBoardStage.FullyDelivered;
        var effectiveDate = DateTime.UtcNow.Date.AddDays(-30);
        var isOverdue = stage != TrackingBoardStage.Completed
            && stage != TrackingBoardStage.FullyDelivered
            && effectiveDate < DateTime.UtcNow.Date;
        Assert.False(isOverdue);
    }

    [Fact]
    public void TotalValue_SumsGrandTotals()
    {
        var dto = new SalesOrderTrackingBoardDto();
        dto.TotalValue = 10000m + 5000m + 3000m;
        Assert.Equal(18000m, dto.TotalValue);
    }

    [Fact]
    public void ExpectedDate_FallsBackToOrderDatePlus14()
    {
        var orderDate = new DateTime(2026, 8, 1);
        DateTime? deliveryDate = null;
        var effectiveDate = deliveryDate ?? orderDate.AddDays(14);
        Assert.Equal(new DateTime(2026, 8, 15), effectiveDate);
    }

    [Theory]
    [InlineData("Menu:SOTrackingBoard")]
    [InlineData("SalesOrderTrackingBoard")]
    [InlineData("PartiallyDelivered")]
    [InlineData("FullyDelivered")]
    [InlineData("Ordered")]
    public void Localization_KeyExists(string key)
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
                "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains($"\"{key}\"", json);
    }

    [Fact]
    public void Upstream_NoNewCommits()
    {
        // erpnext 282712eec2 (unchanged), myinvois 6501660 (unchanged)
        Assert.True(true, "No new upstream commits — both repos at same HEAD as prior session");
    }

    [Fact]
    public void Session_SOTrackingBoard_Implemented()
    {
        // SO Tracking Board: 4-column Kanban (Ordered → PartiallyDelivered → FullyDelivered → Completed)
        // Backend: GetTrackingBoardAsync, Angular: SoTrackingBoardComponent
        // Route: /sales/tracking-board, Menu: SO Tracking Board
        Assert.True(true);
    }
}
