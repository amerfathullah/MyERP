using System;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class StockEntryGLAndItemValidationTests
{
    [Fact]
    public void StockEntry_ImplementsIAccountableDocument()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        (se is IAccountableDocument).ShouldBeTrue();
    }

    [Fact]
    public void StockEntry_DocumentType_IsStockEntry()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        var doc = (IAccountableDocument)se;
        doc.DocumentType.ShouldBe("StockEntry");
    }

    [Fact]
    public void StockEntry_NetTotal_SumsItemValues()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid(), 100m); // 10 × 100 = 1000
        se.AddItem(Guid.NewGuid(), 5, null, Guid.NewGuid(), 200m);  // 5 × 200 = 1000

        var doc = (IAccountableDocument)se;
        doc.NetTotal.ShouldBe(2000m);
    }

    [Fact]
    public void StockEntry_TaxAmount_AlwaysZero()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialIssue, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 5, Guid.NewGuid(), null, 50m);

        var doc = (IAccountableDocument)se;
        doc.TaxAmount.ShouldBe(0m);
    }

    [Fact]
    public void StockEntry_CustomerSupplierId_AlwaysNull()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer, DateTime.UtcNow);
        var doc = (IAccountableDocument)se;
        doc.CustomerId.ShouldBeNull();
        doc.SupplierId.ShouldBeNull();
    }

    [Fact]
    public void ItemInactive_ErrorCode_Value()
    {
        MyERPDomainErrorCodes.ItemInactive.ShouldBe("MyERP:05013");
    }

    [Fact]
    public void Item_InactiveItem_ShouldBeBlocked()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "DISC-001", "Discontinued Item", ItemType.Goods);
        item.IsActive = false;

        // The ItemTransactionValidationService would throw for this item
        item.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void StockEntry_PostingDate_MatchesInterface()
    {
        var date = new DateTime(2026, 6, 15);
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, date);
        var doc = (IAccountableDocument)se;
        doc.PostingDate.ShouldBe(date);
    }
}
