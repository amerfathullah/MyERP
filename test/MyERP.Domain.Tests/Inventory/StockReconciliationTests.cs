using System;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class StockReconciliationTests
{
    private static StockReconciliation CreateSR()
        => new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var sr = CreateSR();
        sr.Status.ShouldBe(Core.DocumentStatus.Draft);
        sr.Items.ShouldBeEmpty();
        sr.DifferenceAmount.ShouldBe(0);
    }

    [Fact]
    public void AddItem_CalculatesDifference()
    {
        var sr = CreateSR();
        // Current: 10 qty × RM5 = RM50, New: 15 qty × RM5 = RM75 → diff = RM25
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 15, 5, 10, 5);
        sr.Items.Count.ShouldBe(1);
        sr.DifferenceAmount.ShouldBe(25m);
    }

    [Fact]
    public void AddItem_NegativeDifference()
    {
        var sr = CreateSR();
        // Current: 20 qty × RM10 = RM200, New: 15 qty × RM10 = RM150 → diff = -RM50
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 15, 10, 20, 10);
        sr.DifferenceAmount.ShouldBe(-50m);
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 5, 0, 0);
        sr.Submit();
        sr.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var sr = CreateSR();
        Should.Throw<BusinessException>(() => sr.Submit());
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var sr = CreateSR();
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 10, 5, 0, 0);
        sr.Submit();
        sr.Cancel();
        sr.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Draft_Throws()
    {
        var sr = CreateSR();
        Should.Throw<BusinessException>(() => sr.Cancel());
    }
}
