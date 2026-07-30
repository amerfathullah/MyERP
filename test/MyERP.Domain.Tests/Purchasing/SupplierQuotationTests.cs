using System;
using System.IO;
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

    [Fact]
    public void Submit_SetsStatusToSubmitted()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 5, 100m);
        sq.Submit();
        sq.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Cancel_FromDraft_Throws()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 1, 10m);
        Should.Throw<BusinessException>(() => sq.Cancel());
    }

    [Fact]
    public void GrandTotal_EqualsNetTotal_WhenNoTax()
    {
        var sq = CreateSQ();
        sq.AddItem(Guid.NewGuid(), 10, 100m); // Net = 1000
        sq.GrandTotal.ShouldBe(1000m);
    }

    [Fact]
    public void ValidTill_DefaultsToNull()
    {
        var sq = CreateSQ();
        sq.ValidTill.ShouldBeNull();
    }

    [Fact]
    public void ValidTill_CanBeSet()
    {
        var sq = CreateSQ();
        var future = DateTime.UtcNow.AddDays(30);
        sq.ValidTill = future;
        sq.ValidTill.ShouldBe(future);
    }

    [Fact]
    public void SupplierQuotationId_OnPO_TracksSource()
    {
        var poId = Guid.NewGuid();
        var sqId = Guid.NewGuid();
        var po = new PurchaseOrder(poId, Guid.NewGuid(), Guid.NewGuid(), "PO-0001", DateTime.UtcNow);
        po.SupplierQuotationId = sqId;
        po.SupplierQuotationId.ShouldBe(sqId);
    }

    [Theory]
    [InlineData("PurchaseOrderCreated")]
    [InlineData("CreatePurchaseOrder")]
    [InlineData("ValidTill")]
    [InlineData("SupplierQuotations")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var enJsonPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(enJsonPath);
        Assert.Contains($"\"{key}\"", content);
    }
}
