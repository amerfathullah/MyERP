using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class PickListTests
{
    private static PickList CreatePickList() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Delivery");

    [Fact]
    public void Create_SetsDefaults()
    {
        var pl = CreatePickList();
        pl.Status.ShouldBe(Core.DocumentStatus.Draft);
        pl.Purpose.ShouldBe("Delivery");
        pl.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_Succeeds()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pl.Items.Count.ShouldBe(1);
        pl.Items[0].PendingQty.ShouldBe(100m);
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_Empty_Throws()
    {
        var pl = CreatePickList();
        Should.Throw<BusinessException>(() => pl.Submit());
    }

    [Fact]
    public void RecordTransfer_ReducesPending()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 100m);
        pl.Submit();
        pl.Items[0].RecordTransfer(40m);
        pl.Items[0].TransferredQty.ShouldBe(40m);
        pl.Items[0].PendingQty.ShouldBe(60m);
        pl.IsPartiallyTransferred.ShouldBeTrue();
        pl.IsFullyTransferred.ShouldBeFalse();
    }

    [Fact]
    public void RecordTransfer_Full_MarksComplete()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Items[0].RecordTransfer(50m);
        pl.IsFullyTransferred.ShouldBeTrue();
    }

    [Fact]
    public void RecordTransfer_Excess_Throws()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        Should.Throw<BusinessException>(() => pl.Items[0].RecordTransfer(60m));
    }

    [Fact]
    public void Cancel_WithTransferred_Throws()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Items[0].RecordTransfer(10m);
        Should.Throw<BusinessException>(() => pl.Cancel());
    }

    [Fact]
    public void Cancel_NoTransfers_Succeeds()
    {
        var pl = CreatePickList();
        pl.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50m);
        pl.Submit();
        pl.Cancel();
        pl.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }
}
