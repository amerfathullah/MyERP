using System;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class SubcontractingOrderTests
{
    [Fact]
    public void Create_SetsDefaultStatus()
    {
        var sco = CreateSCO();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Draft);
    }

    [Fact]
    public void AddItem_CalculatesTotals()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Widget", 100, 5));

        sco.NetTotal.ShouldBe(500m);
        sco.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Part A", 50, 10));

        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var sco = CreateSCO();
        Should.Throw<BusinessException>(() => sco.Submit());
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Part", 10, 20));
        sco.Submit();

        Should.Throw<BusinessException>(() =>
            sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Part B", 5, 15)));
    }

    [Fact]
    public void Close_FromOpen_Succeeds()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Part", 10, 20));
        sco.Submit();
        sco.Close();

        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    [Fact]
    public void Cancel_Succeeds()
    {
        var sco = CreateSCO();
        sco.Cancel();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Cancelled);
    }

    [Fact]
    public void AddSuppliedItem_TracksRawMaterials()
    {
        var sco = CreateSCO();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Steel", 500));

        sco.SuppliedItems.Count.ShouldBe(1);
        sco.SuppliedItems[0].RequiredQty.ShouldBe(500m);
    }

    private static SubcontractingOrder CreateSCO() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SCO-001", DateTime.UtcNow, Guid.NewGuid());
}
