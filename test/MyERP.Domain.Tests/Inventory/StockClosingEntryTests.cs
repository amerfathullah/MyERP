using System;
using System.Linq;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class StockClosingEntryTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    private StockClosingEntry CreateEntry(DateTime? toDate = null)
    {
        return new StockClosingEntry(Guid.NewGuid(), _companyId,
            toDate ?? new DateTime(2026, 6, 30));
    }

    [Fact]
    public void ClosingEntry_DefaultState()
    {
        var entry = CreateEntry();
        Assert.Equal(StockClosingStatus.Draft, entry.Status);
        Assert.Equal(new DateTime(2026, 6, 30), entry.ToDate);
        Assert.Equal(0, entry.TotalEntries);
        Assert.Equal(0m, entry.TotalStockValue);
        Assert.Null(entry.PreviousClosingEntryId);
        Assert.Null(entry.ScannedFromDate);
        Assert.Empty(entry.Balances);
    }

    [Fact]
    public void ClosingEntry_AddBalance()
    {
        var entry = CreateEntry();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        entry.AddBalance(itemId, whId, 100m, 5000m, 50m);

        Assert.Single(entry.Balances);
        var bal = entry.Balances.First();
        Assert.Equal(itemId, bal.ItemId);
        Assert.Equal(whId, bal.WarehouseId);
        Assert.Equal(100m, bal.Qty);
        Assert.Equal(5000m, bal.StockValue);
        Assert.Equal(50m, bal.ValuationRate);
    }

    [Fact]
    public void ClosingEntry_AddBalance_WithFifoQueue()
    {
        var entry = CreateEntry();
        var fifo = "[{\"qty\":50,\"rate\":48},{\"qty\":50,\"rate\":52}]";
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m, fifo);

        Assert.Equal(fifo, entry.Balances.First().FifoQueue);
    }

    [Fact]
    public void ClosingEntry_Submit()
    {
        var entry = CreateEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 200m, 8000m, 40m);

        entry.Submit();

        Assert.Equal(StockClosingStatus.Submitted, entry.Status);
        Assert.Equal(2, entry.TotalEntries);
        Assert.Equal(13_000m, entry.TotalStockValue);
    }

    [Fact]
    public void ClosingEntry_Submit_NoBalances_Throws()
    {
        var entry = CreateEntry();
        var ex = Assert.Throws<BusinessException>(() => entry.Submit());
        Assert.Equal("MyERP:05028", ex.Code);
    }

    [Fact]
    public void ClosingEntry_Submit_FromNonDraft_Throws()
    {
        var entry = CreateEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();

        Assert.Throws<BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void ClosingEntry_Cancel()
    {
        var entry = CreateEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();
        entry.Cancel();

        Assert.Equal(StockClosingStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void ClosingEntry_Cancel_FromDraft_Throws()
    {
        var entry = CreateEntry();
        Assert.Throws<BusinessException>(() => entry.Cancel());
    }

    [Fact]
    public void ClosingEntry_AddBalance_AfterSubmit_Throws()
    {
        var entry = CreateEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();

        Assert.Throws<BusinessException>(() =>
            entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 50m, 2500m, 50m));
    }

    [Fact]
    public void ClosingEntry_CoversDate_Submitted()
    {
        var entry = CreateEntry(new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();

        Assert.True(entry.CoversDate(new DateTime(2026, 6, 30)));  // Exact date
        Assert.True(entry.CoversDate(new DateTime(2026, 6, 1)));   // Before
        Assert.True(entry.CoversDate(new DateTime(2026, 1, 1)));   // Much before
        Assert.False(entry.CoversDate(new DateTime(2026, 7, 1)));  // After
    }

    [Fact]
    public void ClosingEntry_CoversDate_Draft_NeverCovers()
    {
        var entry = CreateEntry(new DateTime(2026, 6, 30));
        Assert.False(entry.CoversDate(new DateTime(2026, 6, 15))); // Draft never covers
    }

    [Fact]
    public void ClosingEntry_CoversDate_Cancelled_NeverCovers()
    {
        var entry = CreateEntry(new DateTime(2026, 6, 30));
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();
        entry.Cancel();

        Assert.False(entry.CoversDate(new DateTime(2026, 6, 15))); // Cancelled
    }

    [Fact]
    public void ClosingEntry_IncrementalReference()
    {
        var prevId = Guid.NewGuid();
        var entry = CreateEntry(new DateTime(2026, 6, 30));
        entry.PreviousClosingEntryId = prevId;
        entry.ScannedFromDate = new DateTime(2026, 4, 1);

        Assert.Equal(prevId, entry.PreviousClosingEntryId);
        Assert.Equal(new DateTime(2026, 4, 1), entry.ScannedFromDate);
    }

    [Fact]
    public void ClosingBalance_Properties()
    {
        var bal = new StockClosingBalance(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 250m, 12_500m, 50m,
            "[{\"qty\":250,\"rate\":50}]");

        Assert.Equal(250m, bal.Qty);
        Assert.Equal(12_500m, bal.StockValue);
        Assert.Equal(50m, bal.ValuationRate);
        Assert.NotNull(bal.FifoQueue);
    }

    [Fact]
    public void ClosingEntry_MultipleItemWarehouse()
    {
        var entry = CreateEntry();
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        entry.AddBalance(item1, wh1, 100m, 5000m, 50m);
        entry.AddBalance(item1, wh2, 50m, 2500m, 50m);  // Same item, different warehouse
        entry.AddBalance(item2, wh1, 200m, 10000m, 50m); // Different item, same warehouse

        Assert.Equal(3, entry.Balances.Count);
        entry.Submit();
        Assert.Equal(17_500m, entry.TotalStockValue);
    }
}
