using System;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class BlanketOrderTests
{
    private static BlanketOrder CreateBO() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "BO-001", "Selling", Guid.NewGuid(),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

    [Fact]
    public void Create_SetsDefaults()
    {
        var bo = CreateBO();
        bo.Status.ShouldBe(Core.DocumentStatus.Draft);
        bo.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_Succeeds()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 1000, 5.50m, "Widget A");
        bo.Items.Count.ShouldBe(1);
        bo.Items[0].RemainingQty.ShouldBe(1000);
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 500, 10m);
        bo.Submit();
        bo.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var bo = CreateBO();
        Should.Throw<BusinessException>(() => bo.Submit());
    }

    [Fact]
    public void RecordOrder_ReducesRemaining()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 1000, 5m);
        bo.Submit();
        bo.Items[0].RecordOrder(300m);
        bo.Items[0].OrderedQty.ShouldBe(300m);
        bo.Items[0].RemainingQty.ShouldBe(700m);
    }

    [Fact]
    public void RecordOrder_ExceedsAllowance_Throws()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 100, 5m);
        bo.Submit();
        // With 0% allowance, max = 100
        Should.Throw<BusinessException>(() => bo.Items[0].RecordOrder(101m, 0));
    }

    [Fact]
    public void RecordOrder_WithAllowance_AllowsOverage()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 100, 5m);
        bo.Submit();
        // With 10% allowance, max = 110
        bo.Items[0].RecordOrder(105m, 10);
        bo.Items[0].OrderedQty.ShouldBe(105m);
    }

    [Fact]
    public void Cancel_Succeeds()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 100, 5m);
        bo.Submit();
        bo.Cancel();
        bo.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 100, 5m);
        bo.Submit();
        Should.Throw<BusinessException>(() => bo.AddItem(Guid.NewGuid(), 50, 3m));
    }
}
