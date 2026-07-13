using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Sales;

public class ReturnValidationTests
{
    private static SalesInvoice CreateInvoice(decimal qty = 10m)
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-001", DateTime.UtcNow);
        invoice.AddItem(Guid.NewGuid(), "Widget", qty, 100m, 6m);
        return invoice;
    }

    [Fact]
    public void SalesInvoice_IsReturn_DefaultFalse()
    {
        var invoice = CreateInvoice();
        invoice.IsReturn.ShouldBeFalse();
    }

    [Fact]
    public void SalesInvoice_CanSetIsReturn()
    {
        var invoice = CreateInvoice();
        invoice.IsReturn = true;
        invoice.IsReturn.ShouldBeTrue();
    }

    [Fact]
    public void SalesInvoice_ReturnAgainstId_SetCorrectly()
    {
        var originalId = Guid.NewGuid();
        var invoice = CreateInvoice();
        invoice.IsReturn = true;
        invoice.ReturnAgainstId = originalId;
        invoice.ReturnAgainstId.ShouldBe(originalId);
    }

    [Fact]
    public void SalesInvoice_ExchangeRate_DefaultIsOne()
    {
        var invoice = CreateInvoice();
        invoice.ExchangeRate.ShouldBe(1m);
    }

    [Fact]
    public void SalesInvoice_PaymentTermsTemplateId_Nullable()
    {
        var invoice = CreateInvoice();
        invoice.PaymentTermsTemplateId.ShouldBeNull();
        var templateId = Guid.NewGuid();
        invoice.PaymentTermsTemplateId = templateId;
        invoice.PaymentTermsTemplateId.ShouldBe(templateId);
    }

    [Fact]
    public void ReturnInvoice_NegativeQty_GrandTotalIsNegative()
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, -30m);
        // GrandTotal = qty * price + tax = (-5 * 100) + (-30) = -530
        invoice.GrandTotal.ShouldBeLessThan(0);
    }

    [Fact]
    public void ReturnInvoice_OutstandingAmount_IsNegative()
    {
        var invoice = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, 0m);
        invoice.GrandTotal = -500m;  // Set explicitly for test
        invoice.OutstandingAmount.ShouldBe(-500m);
    }
}
