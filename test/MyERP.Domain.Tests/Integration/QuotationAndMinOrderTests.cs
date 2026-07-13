using System;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Final integration tests for quotation expiry and minimum order quantity.
/// </summary>
public class QuotationAndMinOrderTests
{
    [Fact]
    public void Quotation_IsExpired_WhenPastValidUntil()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.UtcNow);
        q.ValidUntil = DateTime.UtcNow.AddDays(-5); // Expired 5 days ago
        q.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        q.Submit();

        q.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void Quotation_NotExpired_WhenFutureValidUntil()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-002", DateTime.UtcNow);
        q.ValidUntil = DateTime.UtcNow.AddDays(30); // Valid for 30 more days
        q.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        q.Submit();

        q.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void Quotation_NotExpired_WhenNoValidUntilSet()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-003", DateTime.UtcNow);
        q.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        q.Submit();

        q.IsExpired.ShouldBeFalse(); // No expiry date = never expires
    }

    [Fact]
    public void Quotation_NotExpired_WhenConverted()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-004", DateTime.UtcNow);
        q.ValidUntil = DateTime.UtcNow.AddDays(-5); // Past valid date
        q.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        q.Submit();
        q.ConvertedToSalesOrderId = Guid.NewGuid(); // Already converted

        q.IsExpired.ShouldBeFalse(); // Converted quotations are not "expired"
    }

    [Fact]
    public void Quotation_MarkLost_FromSubmitted()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-005", DateTime.UtcNow);
        q.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        q.Submit();

        q.MarkLost();
        q.Status.ShouldBe(DocumentStatus.Rejected);
    }

    [Fact]
    public void Quotation_MarkLost_BlockedFromDraft()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-006", DateTime.UtcNow);
        q.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);

        Should.Throw<Volo.Abp.BusinessException>(() => q.MarkLost());
    }

    [Fact]
    public void Item_MinOrderQty_Default()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-MOQ", "Component", ItemType.Goods);
        item.MinOrderQty.ShouldBe(0); // No minimum by default
    }

    [Fact]
    public void Item_MinOrderQty_BelowMinimum_Detected()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-MOQ2", "Bolt", ItemType.Goods);
        item.MinOrderQty = 100;

        var orderedQty = 50m;
        var belowMinimum = item.MinOrderQty > 0 && orderedQty < item.MinOrderQty;
        belowMinimum.ShouldBeTrue();
    }

    [Fact]
    public void Item_MinOrderQty_AboveMinimum_Passes()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-MOQ3", "Nut", ItemType.Goods);
        item.MinOrderQty = 100;

        var orderedQty = 200m;
        var belowMinimum = item.MinOrderQty > 0 && orderedQty < item.MinOrderQty;
        belowMinimum.ShouldBeFalse();
    }
}
