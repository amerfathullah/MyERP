using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class BillOfMaterialsTests
{
    [Fact]
    public void RecalculateCost_SumsItemAmounts()
    {
        var bom = CreateBom();
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel Bar", 10, 25));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Bolt M8", 50, 2));

        bom.RecalculateCost();

        bom.TotalMaterialCost.ShouldBe(350m); // (10*25) + (50*2)
    }

    [Fact]
    public void BomItem_Recalculate_SetsAmount()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Wire", 5, 10);
        item.Quantity = 8;

        item.Recalculate();

        item.Amount.ShouldBe(80m);
    }

    private static BillOfMaterials CreateBom() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "BOM-0001", Guid.NewGuid(), Guid.NewGuid());
}

public class WorkOrderTests
{
    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var wo = CreateWorkOrder();

        wo.Status.ShouldBe(WorkOrderStatus.Draft);
        wo.PercentComplete.ShouldBe(0);
    }

    [Fact]
    public void Submit_FromDraft_ShouldSucceed()
    {
        var wo = CreateWorkOrder();

        wo.Submit();

        wo.Status.ShouldBe(WorkOrderStatus.Submitted);
    }

    [Fact]
    public void Start_FromSubmitted_ShouldSucceed()
    {
        var wo = CreateWorkOrder();
        wo.Submit();

        wo.Start();

        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
        wo.ActualStartDate.ShouldNotBeNull();
    }

    [Fact]
    public void Start_FromDraft_ShouldThrow()
    {
        var wo = CreateWorkOrder();

        Assert.Throws<BusinessException>(() => wo.Start());
    }

    [Fact]
    public void RecordProduction_PartialQuantity()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();

        wo.RecordProduction(5);

        wo.ProducedQuantity.ShouldBe(5);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
        wo.PercentComplete.ShouldBe(50);
    }

    [Fact]
    public void RecordProduction_CompletesWhenFull()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();

        wo.RecordProduction(10);

        wo.ProducedQuantity.ShouldBe(10);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
        wo.ActualEndDate.ShouldNotBeNull();
    }

    [Fact]
    public void Stop_FromInProcess_ShouldSucceed()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();

        wo.Stop();

        wo.Status.ShouldBe(WorkOrderStatus.Stopped);
    }

    [Fact]
    public void Cancel_FromCompleted_ShouldThrow()
    {
        var wo = CreateWorkOrder();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(10);

        Assert.Throws<BusinessException>(() => wo.Cancel());
    }

    [Fact]
    public void RecordMaterialTransfer_UpdatesStatus()
    {
        var wo = CreateWorkOrder();
        wo.Submit();

        wo.RecordMaterialTransfer(5);

        wo.MaterialTransferred.ShouldBe(5);
        wo.Status.ShouldBe(WorkOrderStatus.NotStarted);
    }

    private static WorkOrder CreateWorkOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "WO-0001", Guid.NewGuid(), Guid.NewGuid(), 10, Guid.NewGuid());
}
