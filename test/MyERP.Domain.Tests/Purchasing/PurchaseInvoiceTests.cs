using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseInvoiceTests
{
    [Fact]
    public void Submit_WithItems_ShouldChangeStatus()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Raw Material", 50, 20m, 60m);

        invoice.Submit();

        invoice.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_ShouldThrow()
    {
        var invoice = CreateInvoice();

        Assert.Throws<BusinessException>(() => invoice.Submit());
    }

    [Fact]
    public void Post_AfterSubmit_ShouldChangeStatus()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Raw Material", 10, 50m, 30m);
        invoice.Submit();

        invoice.Post();

        invoice.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void Post_FromDraft_ShouldThrow()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Raw Material", 10, 50m, 30m);

        Assert.Throws<BusinessException>(() => invoice.Post());
    }

    [Fact]
    public void Cancel_AfterPost_ShouldChangeStatus()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Item", 1, 100m, 6m);
        invoice.Submit();
        invoice.Post();

        invoice.Cancel();

        invoice.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldThrow()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Item", 1, 100m, 6m);

        Assert.Throws<BusinessException>(() => invoice.Cancel());
    }

    [Fact]
    public void AddItem_ShouldRecalculateTotals()
    {
        var invoice = CreateInvoice();

        invoice.AddItem(Guid.NewGuid(), "Steel Plate", 5, 200m, 60m);  // 1000 + 60
        invoice.AddItem(Guid.NewGuid(), "Bolts", 100, 2m, 12m);       // 200 + 12

        invoice.NetTotal.ShouldBe(1200m);
        invoice.TaxAmount.ShouldBe(72m);
        invoice.GrandTotal.ShouldBe(1272m);
    }

    [Fact]
    public void OutstandingAmount_ShouldBeGrandTotalMinusPaid()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Service", 1, 1000m, 60m);

        invoice.OutstandingAmount.ShouldBe(1060m); // GrandTotal - 0 paid
    }

    [Fact]
    public void SetHold_WithFutureReleaseDate_ShouldBlockUntilThen()
    {
        var invoice = CreateInvoice();

        invoice.SetHold(true, "Awaiting quality dispute resolution", DateTime.UtcNow.Date.AddDays(3));

        invoice.OnHold.ShouldBeTrue();
        invoice.IsBlocked.ShouldBeTrue();
    }

    [Fact]
    public void SetHold_WithPastReleaseDate_ShouldThrow()
    {
        var invoice = CreateInvoice();

        Assert.Throws<BusinessException>(() =>
            invoice.SetHold(true, "reason", DateTime.UtcNow.Date.AddDays(-1)));
    }

    [Fact]
    public void SetHold_NoReleaseDate_ShouldBlockIndefinitely()
    {
        var invoice = CreateInvoice();

        invoice.SetHold(true, "reason", null);

        invoice.IsBlocked.ShouldBeTrue();
    }

    [Fact]
    public void SetHold_False_ShouldClearCommentAndReleaseDate()
    {
        var invoice = CreateInvoice();
        invoice.SetHold(true, "reason", DateTime.UtcNow.Date.AddDays(3));

        invoice.SetHold(false, null, null);

        invoice.OnHold.ShouldBeFalse();
        invoice.HoldComment.ShouldBeNull();
        invoice.ReleaseDate.ShouldBeNull();
        invoice.IsBlocked.ShouldBeFalse();
    }

    private static PurchaseInvoice CreateInvoice()
    {
        return new PurchaseInvoice(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            supplierId: Guid.NewGuid(),
            invoiceNumber: "PINV-2026-00001",
            issueDate: DateTime.Today);
    }
}
