using System;
using System.Linq;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.SubcontractingAndWiringTests;

public class SubcontractingAndWiringTests
{
    // ========== SubcontractingManager Tests ==========

    private static SubcontractingOrder CreateSco()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id,
            Guid.NewGuid(), "FG Widget", 100m, 50m));
        return sco;
    }

    [Fact]
    public void CalculateRmConsumption_ProportionalRatio()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 50m);

        result.Length.ShouldBe(1);
        result[0].ConsumedQty.ShouldBe(100m); // 200 × (50/100) = 100
    }

    [Fact]
    public void CalculateRmConsumption_FullReceipt()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 100m);

        result[0].ConsumedQty.ShouldBe(200m); // Full consumption
    }

    [Fact]
    public void CalculateRmConsumption_MultipleRm()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = CreateSco();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Steel", 200m));
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, Guid.NewGuid(), "RM Paint", 50m));

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 25m);

        result.Length.ShouldBe(2);
        result[0].ConsumedQty.ShouldBe(50m);  // 200 × (25/100)
        result[1].ConsumedQty.ShouldBe(12.5m); // 50 × (25/100)
    }

    [Fact]
    public void CalculateRmConsumption_ZeroFgQty_ReturnsEmpty()
    {
        var mgr = new SubcontractingManager(null!, null!);
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-001",
            DateTime.UtcNow, Guid.NewGuid());
        // No FG items → total qty is 0

        var result = mgr.CalculateRmConsumption(sco, receivedFgQty: 10m);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void SubcontractingOrder_PerReceived_DefaultsZero()
    {
        var sco = CreateSco();
        sco.PerReceived.ShouldBe(0);
    }

    [Fact]
    public void SubcontractingOrder_PartialReceipt_TracksQty()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.Items.First().ReceivedQty = 40;

        var pending = sco.Items.First().Qty - sco.Items.First().ReceivedQty;
        pending.ShouldBe(60m);
    }

    // ========== SubcontractingRmConsumption DTO ==========

    [Fact]
    public void SubcontractingRmConsumption_StoresValues()
    {
        var c = new SubcontractingRmConsumption
        {
            ItemId = Guid.NewGuid(),
            RequiredQty = 200m,
            ConsumedQty = 100m,
            WarehouseId = Guid.NewGuid()
        };
        c.ConsumedQty.ShouldBe(100m);
        c.WarehouseId.ShouldNotBeNull();
    }

    // ========== SCO Status Transitions ==========

    [Fact]
    public void SubcontractingOrder_Submit_FromDraft()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);
    }

    [Fact]
    public void SubcontractingOrder_Close_FromOpen()
    {
        var sco = CreateSco();
        sco.Submit();
        sco.MarkPartiallyReceived();
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    [Fact]
    public void SubcontractingOrder_Cancel_FromDraft()
    {
        var sco = CreateSco();
        sco.Cancel();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Cancelled);
    }

    [Fact]
    public void SubcontractingOrder_DoubleCancel_Throws()
    {
        var sco = CreateSco();
        sco.Cancel();
        Should.Throw<BusinessException>(() => sco.Cancel());
    }
}
