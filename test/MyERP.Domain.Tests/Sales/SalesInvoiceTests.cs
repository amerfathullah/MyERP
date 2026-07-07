using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class SalesInvoiceTests
{
    [Fact]
    public void Submit_WithItems_ShouldChangeStatus()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 60m);

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
    public void Submit_AlreadySubmitted_ShouldThrow()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 6m);
        invoice.Submit();

        Assert.Throws<BusinessException>(() => invoice.Submit());
    }

    [Fact]
    public void Post_AfterSubmit_ShouldChangeStatus()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 6m);
        invoice.Submit();

        invoice.Post();

        invoice.Status.ShouldBe(DocumentStatus.Posted);
    }

    [Fact]
    public void Cancel_AfterPost_ShouldChangeStatus()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 6m);
        invoice.Submit();
        invoice.Post();

        invoice.Cancel();

        invoice.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddItem_ShouldRecalculateTotals()
    {
        var invoice = CreateInvoice();

        invoice.AddItem(Guid.NewGuid(), "Item A", 2, 500m, 60m);  // 1000 + 60
        invoice.AddItem(Guid.NewGuid(), "Item B", 1, 200m, 12m);  // 200 + 12

        invoice.NetTotal.ShouldBe(1200m);
        invoice.TaxAmount.ShouldBe(72m);
        invoice.GrandTotal.ShouldBe(1272m);
    }

    [Fact]
    public void AddItem_AfterSubmit_ShouldThrow()
    {
        var invoice = CreateInvoice();
        invoice.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 6m);
        invoice.Submit();

        Assert.Throws<BusinessException>(() =>
            invoice.AddItem(Guid.NewGuid(), "New Item", 1, 50m, 3m));
    }

    private static SalesInvoice CreateInvoice()
    {
        return new SalesInvoice(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            invoiceNumber: "INV-2026-00001",
            issueDate: DateTime.Today);
    }
}
