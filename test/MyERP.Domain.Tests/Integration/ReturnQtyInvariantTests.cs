using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for entity-level qty sign invariants on SI, DN, PR.
/// Per DO-NOT: "Allow returns with positive qty (must always be negative)".
/// </summary>
public class ReturnQtyInvariantTests
{
    // -- Sales Invoice --

    [Fact]
    public void SI_Normal_PositiveQty_Allowed()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 5, 100, 0);
        si.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void SI_Normal_ZeroQty_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Widget", 0, 100, 0));
    }

    [Fact]
    public void SI_Normal_NegativeQty_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Widget", -5, 100, 0));
    }

    [Fact]
    public void SI_Return_NegativeQty_Allowed()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        si.AddItem(Guid.NewGuid(), "Widget Return", -3, 100, 0);
        si.Items[0].Quantity.ShouldBe(-3);
    }

    [Fact]
    public void SI_Return_PositiveQty_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        Should.Throw<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Widget", 5, 100, 0));
    }

    // -- Delivery Note --

    [Fact]
    public void DN_Normal_PositiveQty_Allowed()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 50, 0);
        dn.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void DN_Normal_ZeroQty_Throws()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => dn.AddItem(Guid.NewGuid(), "Widget", 0, 50, 0));
    }

    [Fact]
    public void DN_Return_NegativeQty_Allowed()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.IsReturn = true;
        dn.AddItem(Guid.NewGuid(), "Widget Return", -5, 50, 0);
        dn.Items[0].Quantity.ShouldBe(-5);
    }

    [Fact]
    public void DN_Return_PositiveQty_Throws()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.IsReturn = true;
        Should.Throw<ArgumentException>(() => dn.AddItem(Guid.NewGuid(), "Widget", 5, 50, 0));
    }

    // -- Purchase Receipt --

    [Fact]
    public void PR_Normal_PositiveQty_Allowed()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Widget", 10, 30, 0);
        pr.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void PR_Normal_ZeroQty_Throws()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        Should.Throw<ArgumentException>(() => pr.AddItem(Guid.NewGuid(), "Widget", 0, 30, 0));
    }

    [Fact]
    public void PR_Return_NegativeQty_Allowed()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        pr.IsReturn = true;
        pr.AddItem(Guid.NewGuid(), "Widget Return", -5, 30, 0);
        pr.Items[0].Quantity.ShouldBe(-5);
    }

    [Fact]
    public void PR_Return_PositiveQty_Throws()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        pr.IsReturn = true;
        Should.Throw<ArgumentException>(() => pr.AddItem(Guid.NewGuid(), "Widget", 5, 30, 0));
    }
}
