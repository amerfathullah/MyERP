using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Purchasing;

public class DebitNoteTests
{
    private static PurchaseInvoice CreatePI(decimal qty = 10m, decimal rate = 100m)
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Widget", qty, rate, 0m);
        return pi;
    }

    [Fact]
    public void PurchaseInvoice_IsReturn_DefaultFalse()
    {
        var pi = CreatePI();
        pi.IsReturn.ShouldBeFalse();
    }

    [Fact]
    public void PurchaseInvoice_CanSetIsReturn()
    {
        var pi = CreatePI();
        pi.IsReturn = true;
        pi.IsReturn.ShouldBeTrue();
    }

    [Fact]
    public void PurchaseInvoice_ReturnAgainstId_Nullable()
    {
        var pi = CreatePI();
        pi.ReturnAgainstId.ShouldBeNull();
        pi.ReturnAgainstId = Guid.NewGuid();
        pi.ReturnAgainstId.ShouldNotBeNull();
    }

    [Fact]
    public void PurchaseInvoice_Return_NegativeQty_NegativeTotal()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-001", DateTime.UtcNow);
        pi.IsReturn = true;
        pi.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, 0m);
        pi.GrandTotal.ShouldBeLessThan(0m);
    }

    [Fact]
    public void PurchaseInvoice_OutstandingAmount_Calculated()
    {
        var pi = CreatePI();
        pi.GrandTotal = 1000m;
        pi.AmountPaid = 0m;
        pi.OutstandingAmount.ShouldBe(1000m);
    }

    [Fact]
    public void PurchaseInvoice_OutstandingReduced_ByDebitNote()
    {
        // Simulate debit note reducing original PI outstanding
        var pi = CreatePI();
        pi.GrandTotal = 1000m;
        pi.AmountPaid = 0m;
        pi.OutstandingAmount.ShouldBe(1000m);

        // Debit note adds returnAmount to AmountPaid
        pi.AmountPaid += 300m; // debit note for 300
        pi.OutstandingAmount.ShouldBe(700m);
    }

    [Fact]
    public void PurchaseInvoice_FullyPaid_ZeroOutstanding()
    {
        var pi = CreatePI();
        pi.GrandTotal = 1000m;
        pi.AmountPaid = 1000m;
        pi.OutstandingAmount.ShouldBe(0m);
    }
}
