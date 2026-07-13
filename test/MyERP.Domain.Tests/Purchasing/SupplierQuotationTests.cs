using System;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class SupplierQuotationTests
{
    private static SupplierQuotation CreateSQ() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    [Fact]
    public void Create_SetsDefaults()
    {
        var sq = CreateSQ();
        sq.Status.ShouldBe(Core.DocumentStatus.Draft);
        sq.Currency.ShouldBe("MYR");
        sq.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_CalculatesTotals()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 10, 5.50m, "Widget");
        sq.NetTotal.ShouldBe(55m);
        sq.GrandTotal.ShouldBe(55m);
    }

    [Fact]
    public void AddMultipleItems_SumsTotals()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 10, 5m);
        sq.AddItem(Guid.NewGuid(), 20, 3m);
        sq.NetTotal.ShouldBe(110m); // 50 + 60
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 10, 5m);
        sq.Submit();
        sq.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var sq = CreateSQ();
        Should.Throw<BusinessException>(() => sq.Submit());
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 10, 5m);
        sq.Submit();
        sq.Cancel();
        sq.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 10, 5m);
        sq.Submit();
        Should.Throw<BusinessException>(() => sq.AddItem(Guid.NewGuid(), 5, 3m));
    }
}
