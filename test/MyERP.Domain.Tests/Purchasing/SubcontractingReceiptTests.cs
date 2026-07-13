using System;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class SubcontractingReceiptTests
{
    [Fact]
    public void Create_SetsDefaultStatus()
    {
        var scr = CreateSCR();
        scr.Status.ShouldBe(SubcontractingReceiptStatus.Draft);
    }

    [Fact]
    public void AddItem_CalculatesNetTotal()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG Widget", 50, 12));
        scr.NetTotal.ShouldBe(600m);
    }

    [Fact]
    public void Submit_WithItems_Succeeds()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG Widget", 50, 12));
        scr.Submit();
        scr.Status.ShouldBe(SubcontractingReceiptStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_Throws()
    {
        var scr = CreateSCR();
        Should.Throw<BusinessException>(() => scr.Submit());
    }

    [Fact]
    public void AddItem_AfterSubmit_Throws()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG", 10, 5));
        scr.Submit();
        Should.Throw<BusinessException>(() =>
            scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG2", 5, 3)));
    }

    [Fact]
    public void Cancel_FromSubmitted_Succeeds()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG", 10, 5));
        scr.Submit();
        scr.Cancel();
        scr.Status.ShouldBe(SubcontractingReceiptStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG", 10, 5));
        scr.Submit();
        scr.Cancel();
        Should.Throw<BusinessException>(() => scr.Cancel());
    }

    [Fact]
    public void SCO_MarkPartiallyReceived_FromOpen()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Widget", 100, 5));
        sco.Submit();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Open);

        sco.MarkPartiallyReceived();
        sco.Status.ShouldBe(SubcontractingOrderStatus.PartiallyReceived);
    }

    [Fact]
    public void SCO_MarkPartiallyReceived_FromNonOpen_Throws()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Widget", 100, 5));
        // Still Draft
        Should.Throw<BusinessException>(() => sco.MarkPartiallyReceived());
    }

    [Fact]
    public void SCO_Close_FromPartiallyReceived_Succeeds()
    {
        var sco = CreateSCO();
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "Widget", 100, 5));
        sco.Submit();
        sco.MarkPartiallyReceived();
        sco.Close();
        sco.Status.ShouldBe(SubcontractingOrderStatus.Closed);
    }

    [Fact]
    public void SCR_MultipleItems_TotalSums()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "A", 10, 5));
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "B", 20, 3));
        scr.NetTotal.ShouldBe(110m); // 50 + 60
    }

    private static SubcontractingReceipt CreateSCR() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SCR-001", DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid());

    private static SubcontractingOrder CreateSCO() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SCO-001", DateTime.UtcNow, Guid.NewGuid());
}
