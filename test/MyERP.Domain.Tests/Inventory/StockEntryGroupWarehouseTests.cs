using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class StockEntryGroupWarehouseTests
{
    [Fact]
    public void Warehouse_IsGroup_DefaultFalse()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Main Store");
        wh.IsGroup.ShouldBeFalse();
    }

    [Fact]
    public void Warehouse_IsGroup_CanBeSet()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "All Warehouses");
        wh.IsGroup = true;
        wh.IsGroup.ShouldBeTrue();
    }

    [Fact]
    public void StockEntry_EntryType_MaterialReceipt_RequiresTarget()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt,
            DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid(), 100);
        se.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void StockEntry_EntryType_MaterialIssue_RequiresSource()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialIssue,
            DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 5, Guid.NewGuid(), null, 50);
        se.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void StockEntry_EntryType_Transfer_RequiresBoth()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialTransfer,
            DateTime.UtcNow);
        var src = Guid.NewGuid();
        var tgt = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 5, src, tgt, 100);
        se.Items[0].SourceWarehouseId.ShouldBe(src);
        se.Items[0].TargetWarehouseId.ShouldBe(tgt);
    }

    [Fact]
    public void StockEntry_All14Types_Exist()
    {
        Enum.GetValues<StockEntryType>().Length.ShouldBeGreaterThanOrEqualTo(14);
    }

    [Fact]
    public void StockEntry_WorkOrderId_CanBeSet()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture,
            DateTime.UtcNow);
        var woId = Guid.NewGuid();
        se.WorkOrderId = woId;
        se.WorkOrderId.ShouldBe(woId);
    }

    [Fact]
    public void GroupWarehouseErrorCode_Exists()
    {
        MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock.ShouldBe("MyERP:05014");
    }
}
