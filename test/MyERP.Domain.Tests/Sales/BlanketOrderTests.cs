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
    public void UnrecordOrder_RestoresRemaining()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 1000, 5m);
        bo.Submit();
        bo.Items[0].RecordOrder(300m);
        bo.Items[0].UnrecordOrder(300m);
        bo.Items[0].OrderedQty.ShouldBe(0m);
        bo.Items[0].RemainingQty.ShouldBe(1000m);
    }

    [Fact]
    public void UnrecordOrder_NeverGoesBelowZero()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 1000, 5m);
        bo.Submit();
        bo.Items[0].RecordOrder(300m);
        bo.Items[0].UnrecordOrder(500m);
        bo.Items[0].OrderedQty.ShouldBe(0m);
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

    [Fact]
    public void AddItem_ZeroOrNegativeQuantity_Throws()
    {
        var bo = CreateBO();
        Should.Throw<BusinessException>(() => bo.AddItem(Guid.NewGuid(), 0, 5m));
        Should.Throw<BusinessException>(() => bo.AddItem(Guid.NewGuid(), -10, 5m));
    }

    [Fact]
    public void MultiCurrency_ComputesBaseRate()
    {
        var bo = CreateBO();
        bo.Currency = "USD";
        bo.ExchangeRate = 4.45m;
        var itemId = Guid.NewGuid();

        bo.AddItem(itemId, 100, 10m, "USD Widget");
        bo.Items[0].Rate.ShouldBe(10m);
        bo.Items[0].BaseRate.ShouldBe(44.5m);
    }

    [Fact]
    public void Close_WhenSubmitted_ChangesStatusToClosed()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 100, 10m);
        bo.Submit();
        bo.Close();
        bo.Status.ShouldBe(Core.DocumentStatus.Closed);
    }

    [Fact]
    public void Reopen_WhenClosed_ChangesStatusToSubmitted()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 100, 10m);
        bo.Submit();
        bo.Close();
        bo.Reopen();
        bo.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void CloseItem_MarksItemClosed_AndClosesOrderIfAllClosed()
    {
        var bo = CreateBO();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        bo.AddItem(item1Id, 50, 10m);
        bo.AddItem(item2Id, 50, 20m);
        bo.Submit();

        bo.CloseItem(item1Id);
        bo.Items[0].IsClosed.ShouldBeTrue();
        bo.Items[1].IsClosed.ShouldBeFalse();
        bo.Status.ShouldBe(Core.DocumentStatus.Submitted); // Still submitted because item 2 is open

        bo.CloseItem(item2Id);
        bo.Items[1].IsClosed.ShouldBeTrue();
        bo.Status.ShouldBe(Core.DocumentStatus.Closed); // All items closed -> order closed
    }

    [Fact]
    public void ReopenItem_MarksItemOpen_AndReopensOrderIfOrderWasClosed()
    {
        var bo = CreateBO();
        var item1Id = Guid.NewGuid();
        bo.AddItem(item1Id, 50, 10m);
        bo.Submit();
        bo.CloseItem(item1Id);
        bo.Status.ShouldBe(Core.DocumentStatus.Closed);

        bo.ReopenItem(item1Id);
        bo.Items[0].IsClosed.ShouldBeFalse();
        bo.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void RecordOrder_WhenItemClosed_Throws()
    {
        var bo = CreateBO();
        var item1Id = Guid.NewGuid();
        bo.AddItem(item1Id, 50, 10m);
        bo.Submit();
        bo.CloseItem(item1Id);

        Should.Throw<BusinessException>(() => bo.Items[0].RecordOrder(10m));
    }

    [Fact]
    public void ValidateCanBeOrdered_Expired_Throws()
    {
        var bo = CreateBO(); // 2026-01-01 to 2026-12-31
        bo.AddItem(Guid.NewGuid(), 50, 10m);
        bo.Submit();

        // Within validity
        bo.ValidateCanBeOrdered(new DateTime(2026, 6, 1));

        // After validity
        Should.Throw<BusinessException>(() => bo.ValidateCanBeOrdered(new DateTime(2027, 1, 1)));
    }

    [Fact]
    public void ValidateCanBeOrdered_WhenClosed_Throws()
    {
        var bo = CreateBO();
        bo.AddItem(Guid.NewGuid(), 50, 10m);
        bo.Submit();
        bo.Close();

        Should.Throw<BusinessException>(() => bo.ValidateCanBeOrdered(new DateTime(2026, 6, 1)));
    }
}
