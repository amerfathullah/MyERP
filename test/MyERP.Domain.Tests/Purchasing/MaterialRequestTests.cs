using System;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class MaterialRequestTests
{
    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var mr = CreateMaterialRequest();

        mr.Status.ShouldBe(Core.DocumentStatus.Draft);
        mr.RequestType.ShouldBe(MaterialRequestType.Purchase);
    }

    [Fact]
    public void AddItem_ShouldAddToCollection()
    {
        var mr = CreateMaterialRequest();

        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");

        mr.Items.Count.ShouldBe(1);
        mr.Items[0].Quantity.ShouldBe(10);
    }

    [Fact]
    public void Submit_WithItems_ShouldSucceed()
    {
        var mr = CreateMaterialRequest();
        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");

        mr.Submit();

        mr.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_ShouldThrow()
    {
        var mr = CreateMaterialRequest();

        Assert.Throws<BusinessException>(() => mr.Submit());
    }

    [Fact]
    public void Cancel_FromSubmitted_ShouldSucceed()
    {
        var mr = CreateMaterialRequest();
        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");
        mr.Submit();

        mr.Cancel();

        mr.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldThrow()
    {
        var mr = CreateMaterialRequest();

        Assert.Throws<BusinessException>(() => mr.Cancel());
    }

    [Fact]
    public void AddItem_AfterSubmit_ShouldThrow()
    {
        var mr = CreateMaterialRequest();
        mr.AddItem(Guid.NewGuid(), "Steel Bar", 10, "Kg");
        mr.Submit();

        Assert.Throws<BusinessException>(() =>
            mr.AddItem(Guid.NewGuid(), "Bolt M8", 50, "Unit"));
    }

    private static MaterialRequest CreateMaterialRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "MR-0001",
            MaterialRequestType.Purchase, DateTime.UtcNow, Guid.NewGuid());
}
