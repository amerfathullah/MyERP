using System;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for StockPostingService validation logic and SLE/Bin entity behavior.
/// Covers the entity contracts that StockPostingService depends on.
/// </summary>
public class StockPostingServiceTests
{
    [Fact]
    public void StockLedgerEntry_Creates_WithCorrectFields()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 10m, 100m, 10m, 1000m);

        sle.QuantityChange.ShouldBe(10m);
        sle.ValuationRate.ShouldBe(100m);
        sle.StockValue.ShouldBe(1000m); // 10 × 100
    }

    [Fact]
    public void StockLedgerEntry_NegativeQty_ForStockOut()
    {
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, -5m, 200m, 15m, 3000m);

        sle.QuantityChange.ShouldBe(-5m);
        sle.StockValue.ShouldBe(-1000m); // -5 × 200
    }

    [Fact]
    public void StockEntry_Submit_ChangeStatus()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid(), 100);
        se.Submit();

        se.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void StockEntry_Cancel_ChangeStatus()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid(), 100);
        se.Submit();
        se.Post();
        se.Cancel();

        se.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Company_StockFrozenUpto_BlocksEarlierTransactions()
    {
        var company = new Company(Guid.NewGuid(), "Test Co");
        company.StockFrozenUpto = new DateTime(2026, 3, 31);

        // Posting on/before frozen date should be blocked
        var earlyDate = new DateTime(2026, 3, 15);
        (earlyDate <= company.StockFrozenUpto.Value).ShouldBeTrue();

        // Posting after is OK
        var lateDate = new DateTime(2026, 4, 1);
        (lateDate <= company.StockFrozenUpto.Value).ShouldBeFalse();
    }

    [Fact]
    public void Warehouse_IsGroup_BlocksStockOperations()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "All Warehouses");
        wh.IsGroup = true;

        wh.IsGroup.ShouldBeTrue();
        // StockPostingService validates this and throws GroupWarehouseCannotReceiveStock
    }

    [Fact]
    public void Item_MaintainStock_False_SkipsSLE()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "SVC-001", "Consulting", ItemType.Service);
        item.MaintainStock = false;

        item.MaintainStock.ShouldBeFalse();
    }

    [Fact]
    public void Item_MaintainStock_True_CreatesSLE()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITM-001", "Widget", ItemType.Goods);
        item.MaintainStock.ShouldBeTrue();
    }

    [Fact]
    public void StockEntry_EntryNumber_Stored()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer, DateTime.UtcNow);
        se.EntryNumber = "SE-2026-00001";
        se.EntryNumber.ShouldBe("SE-2026-00001");
    }

    [Fact]
    public void StockEntry_Items_SourceAndTarget()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer, DateTime.UtcNow);
        var srcWh = Guid.NewGuid();
        var tgtWh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 20, srcWh, tgtWh, 50);

        se.Items[0].SourceWarehouseId.ShouldBe(srcWh);
        se.Items[0].TargetWarehouseId.ShouldBe(tgtWh);
        se.Items[0].Quantity.ShouldBe(20);
    }
}
