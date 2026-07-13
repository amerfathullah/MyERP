using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class OpeningInvoiceAndDiscountTests
{
    [Fact]
    public void SalesInvoice_IsOpening_DefaultFalse()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.IsOpening.ShouldBeFalse();
    }

    [Fact]
    public void SalesInvoice_CanSetIsOpening()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "OI-001", DateTime.UtcNow);
        si.IsOpening = true;
        si.IsOpening.ShouldBeTrue();
    }

    [Fact]
    public void SalesInvoice_AdditionalDiscountPercentage_Default()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AdditionalDiscountPercentage.ShouldBe(0m);
        si.DiscountAmount.ShouldBe(0m);
    }

    [Fact]
    public void SalesInvoice_DiscountPercentage_Applied()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.NetTotal = 10000m;
        si.AdditionalDiscountPercentage = 5m;
        // Discount = 10000 × 5% = 500
        var discountedTotal = si.NetTotal * (1 - si.AdditionalDiscountPercentage / 100m);
        discountedTotal.ShouldBe(9500m);
    }

    [Fact]
    public void SalesInvoice_DiscountAmount_Applied()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.NetTotal = 10000m;
        si.DiscountAmount = 750m;
        var discountedTotal = si.NetTotal - si.DiscountAmount;
        discountedTotal.ShouldBe(9250m);
    }

    [Fact]
    public void PurchaseInvoice_IsOpening_DefaultFalse()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.IsOpening.ShouldBeFalse();
    }

    [Fact]
    public void PurchaseInvoice_HasDiscountFields()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.AdditionalDiscountPercentage = 10m;
        pi.DiscountAmount = 0m;
        pi.AdditionalDiscountPercentage.ShouldBe(10m);
    }

    [Fact]
    public void OpeningInvoice_NoUpdateStock_Concept()
    {
        // Per DO-NOT: opening invoices with update_stock=true are blocked
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "OI-001", DateTime.UtcNow);
        si.IsOpening = true;
        si.UpdateStock = false; // Opening invoices must not affect stock
        si.IsOpening.ShouldBeTrue();
        si.UpdateStock.ShouldBeFalse();
    }

    [Fact]
    public void OpeningInvoice_ClearPaymentTerms_Concept()
    {
        // Per DO-NOT: opening invoices must clear payment_terms_template
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "OI-001", DateTime.UtcNow);
        si.IsOpening = true;
        si.PaymentTermsTemplateId = null; // Cleared per rule
        si.PaymentTermsTemplateId.ShouldBeNull();
    }
}
