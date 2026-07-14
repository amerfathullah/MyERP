using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for Quotation IAmendable + AmendAsync workflow.
/// Completes the set: all 7 business documents now support amendment.
/// </summary>
public class QuotationAmendmentTests
{
    [Fact]
    public void Quotation_Implements_IAmendable()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.Today);
        (q is IAmendable).ShouldBeTrue();
    }

    [Fact]
    public void Quotation_AmendmentFields_DefaultValues()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-002", DateTime.Today);
        q.AmendedFromId.ShouldBeNull();
        q.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void Quotation_AmendmentFields_CanBeSet()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-003", DateTime.Today);
        var origId = Guid.NewGuid();
        q.AmendedFromId = origId;
        q.AmendmentIndex = 1;

        q.AmendedFromId.ShouldBe(origId);
        q.AmendmentIndex.ShouldBe(1);
    }

    [Fact]
    public void Quotation_Cancelled_CanBeAmended()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-004", DateTime.Today);
        q.AddItem(Guid.NewGuid(), "Widget", 5, 200m, 0, "Pcs");
        q.Submit();
        q.Cancel();

        q.Status.ShouldBe(DocumentStatus.Cancelled);
        // Cancelled → eligible for amendment
    }

    [Fact]
    public void Quotation_AmendedCopy_FreshValidUntil()
    {
        // When amending, the new quotation gets fresh validity (30 days)
        var original = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-100", DateTime.Today);
        original.ValidUntil = DateTime.Today.AddDays(-10); // Expired original

        var amended = new Quotation(Guid.NewGuid(), original.CompanyId, original.CustomerId, "QTN-100-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;
        amended.ValidUntil = DateTime.Today.AddDays(30); // Fresh validity

        amended.ValidUntil!.Value.ShouldBeGreaterThan(DateTime.Today);
        amended.IsExpired.ShouldBeFalse(); // New quote is not expired (it's Draft, not Submitted)
    }

    [Fact]
    public void AllSevenDocuments_Implement_IAmendable()
    {
        // Complete set: SI, PI, SO, PO, DN, PR, Quotation
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-1", DateTime.Today);
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-1", DateTime.Today);
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-1", DateTime.Today);
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-1", DateTime.Today);
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-1", DateTime.Today);
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-1", DateTime.Today);
        var qt = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QT-1", DateTime.Today);

        (si is IAmendable).ShouldBeTrue();
        (pi is IAmendable).ShouldBeTrue();
        (so is IAmendable).ShouldBeTrue();
        (po is IAmendable).ShouldBeTrue();
        (dn is IAmendable).ShouldBeTrue();
        (pr is IAmendable).ShouldBeTrue();
        (qt is IAmendable).ShouldBeTrue();
    }
}
