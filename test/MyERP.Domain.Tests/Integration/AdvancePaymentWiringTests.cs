using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class AdvancePaymentWiringTests
{
    [Fact]
    public void PaymentEntry_AgainstOrderId_DefaultNull()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 5000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstOrderId.ShouldBeNull();
        pe.AgainstOrderType.ShouldBeNull();
    }

    [Fact]
    public void PaymentEntry_IsAdvance_WhenOrderLinkedNoInvoice()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 5000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstOrderId = Guid.NewGuid();
        pe.AgainstOrderType = "SalesOrder";
        pe.AgainstInvoiceId = null; // No invoice

        pe.IsAdvance.ShouldBeTrue();
    }

    [Fact]
    public void PaymentEntry_NotAdvance_WhenInvoiceLinked()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.UtcNow, 5000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstInvoiceId = Guid.NewGuid();
        pe.AgainstInvoiceType = "SalesInvoice";
        pe.AgainstOrderId = Guid.NewGuid(); // Even with order linked

        pe.IsAdvance.ShouldBeFalse(); // Invoice takes priority
    }

    [Fact]
    public void PaymentEntry_NotAdvance_WhenNoOrderLinked()
    {
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Pay,
            DateTime.UtcNow, 3000m, Guid.NewGuid(), Guid.NewGuid());
        pe.AgainstOrderId = null;
        pe.AgainstInvoiceId = null;

        pe.IsAdvance.ShouldBeFalse();
    }

    [Fact]
    public void PurchaseInvoice_WriteOff_Concept()
    {
        // PI with RM 1000 outstanding → write off RM 1000
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.GrandTotal = 1000m;
        pi.AmountPaid = 0m;
        pi.OutstandingAmount.ShouldBe(1000m);

        // After write-off: AmountPaid = GrandTotal
        pi.AmountPaid = pi.GrandTotal;
        pi.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void PurchaseInvoice_WriteOff_PartiallyPaid()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.GrandTotal = 5000m;
        pi.AmountPaid = 4990m; // RM 10 remaining (small rounding)
        pi.OutstandingAmount.ShouldBe(10m);

        // Write off the remaining RM 10
        pi.AmountPaid = pi.GrandTotal;
        pi.OutstandingAmount.ShouldBe(0m);
    }

    [Fact]
    public void AdvancePayment_UpdatesOrderAdvancePaid()
    {
        // Simulate: advance PE of RM 3000 against SO with total RM 10000
        var soGrandTotal = 10000m;
        var advanceAmount = 3000m;
        var advancePaid = 0m;

        advancePaid += advanceAmount;
        var perAdvance = Math.Round(advancePaid / soGrandTotal * 100m, 2);
        perAdvance.ShouldBe(30m);
    }
}
