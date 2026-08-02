using System;
using MyERP.Accounting.DomainServices;
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

    [Fact]
    public void IAccountableDocument_DocumentType_IsSubcontractingReceipt()
    {
        var scr = CreateSCR();
        IAccountableDocument doc = scr;
        doc.DocumentType.ShouldBe("SubcontractingReceipt");
    }

    [Fact]
    public void IAccountableDocument_GrandTotal_EqualsNetTotal()
    {
        var scr = CreateSCR();
        scr.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), scr.Id, Guid.NewGuid(), "FG", 10, 8));
        IAccountableDocument doc = scr;
        doc.GrandTotal.ShouldBe(80m);
        doc.GrandTotal.ShouldBe(scr.NetTotal);
    }

    [Fact]
    public void IAccountableDocument_TaxAmount_IsZero()
    {
        var scr = CreateSCR();
        IAccountableDocument doc = scr;
        doc.TaxAmount.ShouldBe(0m);
    }

    [Fact]
    public void IAccountableDocument_CustomerId_IsNull()
    {
        var scr = CreateSCR();
        IAccountableDocument doc = scr;
        doc.CustomerId.ShouldBeNull();
    }

    [Fact]
    public void IAccountableDocument_SupplierId_IsMapped()
    {
        var supplierId = Guid.NewGuid();
        var scr = new SubcontractingReceipt(Guid.NewGuid(), Guid.NewGuid(), "SCR-002", DateTime.UtcNow,
            supplierId, Guid.NewGuid());
        IAccountableDocument doc = scr;
        doc.SupplierId.ShouldBe(supplierId);
    }

    [Fact]
    public void IAccountableDocument_ExchangeRate_DefaultsToOne()
    {
        var scr = CreateSCR();
        IAccountableDocument doc = scr;
        doc.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void IAccountableDocument_CurrencyCode_DefaultsMYR()
    {
        var scr = CreateSCR();
        IAccountableDocument doc = scr;
        doc.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void IAccountableDocument_PostingDate_FromEntity()
    {
        var date = new DateTime(2026, 6, 15);
        var scr = new SubcontractingReceipt(Guid.NewGuid(), Guid.NewGuid(), "SCR-003", date,
            Guid.NewGuid(), Guid.NewGuid());
        IAccountableDocument doc = scr;
        doc.PostingDate.ShouldBe(date);
    }

    [Fact]
    public void IAccountableDocument_CompanyId_FromEntity()
    {
        var companyId = Guid.NewGuid();
        var scr = new SubcontractingReceipt(Guid.NewGuid(), companyId, "SCR-004", DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid());
        IAccountableDocument doc = scr;
        doc.CompanyId.ShouldBe(companyId);
    }

    private static SubcontractingReceipt CreateSCR() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SCR-001", DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid());

    private static SubcontractingOrder CreateSCO() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SCO-001", DateTime.UtcNow, Guid.NewGuid());
}
