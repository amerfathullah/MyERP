using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class StockReservationEntryTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 100m);
        sre.ReservedQty.ShouldBe(100m);
        sre.DeliveredQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(100m);
        sre.Status.ShouldBe(Core.DocumentStatus.Draft);
    }

    [Fact]
    public void Create_ZeroQty_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 0));
    }

    [Fact]
    public void Submit_Draft_Succeeds()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 50m);
        sre.Submit();
        sre.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void RecordDelivery_ReducesAvailable()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 100m);
        sre.Submit();
        sre.RecordDelivery(30m);
        sre.DeliveredQty.ShouldBe(30m);
        sre.AvailableQty.ShouldBe(70m);
    }

    [Fact]
    public void RecordDelivery_ExceedsReserved_Throws()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 50m);
        sre.Submit();
        sre.RecordDelivery(30m);
        Should.Throw<BusinessException>(() => sre.RecordDelivery(30m));
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 50m);
        sre.Submit();
        sre.Cancel();
        sre.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Draft_Throws()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 50m);
        Should.Throw<BusinessException>(() => sre.Cancel());
    }
}
