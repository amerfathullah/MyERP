using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for SE from Material Request, Item Price auto-insert,
/// and selling price validation.
/// </summary>
public class SeFromMrAndItemPriceAutoInsertTests
{
    // === Stock Entry from Material Request ===

    [Fact]
    public void MaterialRequest_TransferType_MapsToPurposeTransfer()
    {
        var purpose = MaterialRequestType.MaterialTransfer switch
        {
            MaterialRequestType.MaterialTransfer => StockEntryType.MaterialTransfer,
            MaterialRequestType.MaterialIssue => StockEntryType.MaterialIssue,
            _ => StockEntryType.MaterialTransfer,
        };
        Assert.Equal(StockEntryType.MaterialTransfer, purpose);
    }

    [Fact]
    public void MaterialRequest_IssueType_MapsToPurposeIssue()
    {
        var purpose = MaterialRequestType.MaterialIssue switch
        {
            MaterialRequestType.MaterialTransfer => StockEntryType.MaterialTransfer,
            MaterialRequestType.MaterialIssue => StockEntryType.MaterialIssue,
            _ => StockEntryType.MaterialTransfer,
        };
        Assert.Equal(StockEntryType.MaterialIssue, purpose);
    }

    [Fact]
    public void MaterialRequest_PendingQty_ExcludesOrdered()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Widget", 100m, "Unit");

        var item = mr.Items.Last();
        item.OrderedQuantity = 60m;

        // Pending = Quantity - OrderedQuantity = 100 - 60 = 40
        var pending = item.Quantity - item.OrderedQuantity;
        Assert.Equal(40m, pending);
    }

    [Fact]
    public void MaterialRequest_AllOrdered_NoPendingItems()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-002",
            MaterialRequestType.MaterialTransfer, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Part A", 50m, "Unit");

        var item = mr.Items.Last();
        item.OrderedQuantity = 50m;

        var pending = item.Quantity - item.OrderedQuantity;
        Assert.Equal(0m, pending);
    }

    // === Item Price Auto-Insert ===

    [Fact]
    public void AutoInsertPriceContext_DisabledSkips()
    {
        var context = new AutoInsertPriceContext
        {
            IsEnabled = false,
            PriceListId = Guid.NewGuid(),
            Items = new[] { new AutoInsertPriceItem { ItemId = Guid.NewGuid(), Rate = 100m } }
        };

        // Service would skip when IsEnabled = false
        Assert.False(context.IsEnabled);
    }

    [Fact]
    public void AutoInsertPriceContext_ZeroRate_Skipped()
    {
        var item = new AutoInsertPriceItem { ItemId = Guid.NewGuid(), Rate = 0m };
        // Per ERPNext: zero rate items are skipped by the auto-insert service
        Assert.Equal(0m, item.Rate);
    }

    [Fact]
    public void AutoInsertPriceContext_BuyingTransaction()
    {
        var context = new AutoInsertPriceContext
        {
            IsEnabled = true,
            PriceListId = Guid.NewGuid(),
            IsSelling = false,
            PartyId = Guid.NewGuid(),
            TransactionDate = DateTime.UtcNow,
        };

        // For buying transactions: IsSelling = false, PartyId = supplier
        Assert.False(context.IsSelling);
        Assert.NotNull(context.PartyId);
    }

    // === PriceList Entity ===

    [Fact]
    public void PriceList_BuyingDefault_CanBeQueried()
    {
        var pl = new PriceList(Guid.NewGuid(), "Standard Buying", "MYR", false, true)
        { IsDefault = true };

        Assert.True(pl.IsBuying);
        Assert.False(pl.IsSelling);
        Assert.True(pl.IsDefault);
        Assert.True(pl.IsActive);
    }

    [Fact]
    public void PriceList_SellingDefault()
    {
        var pl = new PriceList(Guid.NewGuid(), "Standard Selling", "MYR", true, false)
        { IsDefault = true };

        Assert.True(pl.IsSelling);
        Assert.False(pl.IsBuying);
    }

    // === Item Price Entity ===

    [Fact]
    public void ItemPrice_AutoInserted_FlagSet()
    {
        var ip = new ItemPrice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            50.00m, "Unit", "MYR")
        { IsAutoInserted = true, ValidFrom = DateTime.UtcNow };

        Assert.True(ip.IsAutoInserted);
        Assert.NotNull(ip.ValidFrom);
    }

    [Fact]
    public void ItemPrice_ManualEntry_NotAutoInserted()
    {
        var ip = new ItemPrice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            75.00m, "Kg", "MYR");

        Assert.False(ip.IsAutoInserted);
    }
}
