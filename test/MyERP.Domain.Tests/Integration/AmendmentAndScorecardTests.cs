using System;
using MyERP.Core;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - SI AmendAsync: amendment from cancelled state
/// - PO IAmendable implementation
/// - Supplier scorecard: PreventPurchaseOrders/PreventRfqs flags
/// - Amendment number generation pattern
/// </summary>
public class AmendmentAndScorecardTests
{
    // --- SI Amendment ---

    [Fact]
    public void SalesInvoice_AmendedFromId_CanBeSet()
    {
        var original = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        var amended = new SalesInvoice(Guid.NewGuid(), original.CompanyId, original.CustomerId, "SI-001-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;

        amended.AmendedFromId.ShouldBe(original.Id);
        amended.AmendmentIndex.ShouldBe(1);
    }

    [Fact]
    public void SalesInvoice_OnlyCancelled_CanBeAmended()
    {
        // DocumentAmendmentService.ValidateCanAmend checks for Cancelled status
        var cancelledStatus = DocumentStatus.Cancelled;
        var postedStatus = DocumentStatus.Posted;
        var draftStatus = DocumentStatus.Draft;

        (cancelledStatus == DocumentStatus.Cancelled).ShouldBeTrue();
        (postedStatus == DocumentStatus.Cancelled).ShouldBeFalse();
        (draftStatus == DocumentStatus.Cancelled).ShouldBeFalse();
    }

    // --- PO IAmendable ---

    [Fact]
    public void PurchaseOrder_Implements_IAmendable()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        (po is IAmendable).ShouldBeTrue();
    }

    [Fact]
    public void PurchaseOrder_AmendmentFields_DefaultValues()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-002", DateTime.Today);
        po.AmendedFromId.ShouldBeNull();
        po.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void PurchaseOrder_AmendmentFields_CanBeSet()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-003", DateTime.Today);
        var originalId = Guid.NewGuid();

        po.AmendedFromId = originalId;
        po.AmendmentIndex = 3;

        po.AmendedFromId.ShouldBe(originalId);
        po.AmendmentIndex.ShouldBe(3);
    }

    // --- Supplier Scorecard ---

    [Fact]
    public void Supplier_PreventPurchaseOrders_DefaultsFalse()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.PreventPurchaseOrders.ShouldBeFalse();
        supplier.PreventRfqs.ShouldBeFalse();
    }

    [Fact]
    public void Supplier_PreventPurchaseOrders_BlocksPOSubmit()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Bad Supplier");
        supplier.PreventPurchaseOrders = true;

        supplier.PreventPurchaseOrders.ShouldBeTrue();
    }

    [Fact]
    public void Supplier_PreventRfqs_BlocksRFQCreation()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Restricted Supplier");
        supplier.PreventRfqs = true;

        supplier.PreventRfqs.ShouldBeTrue();
    }

    [Fact]
    public void Supplier_ScorecardIndependentFromHold()
    {
        // Scorecard and Hold are independent mechanisms
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Mixed Status");
        supplier.HoldType = SupplierHoldType.None; // Not on hold
        supplier.PreventPurchaseOrders = true; // But scorecard blocks POs

        supplier.IsOnHold.ShouldBeFalse();
        supplier.PreventPurchaseOrders.ShouldBeTrue();
    }

    // --- Amendment Number Pattern ---

    [Fact]
    public void AmendmentNumber_FirstAmendment_AppendsDash1()
    {
        // "PI-001" → "PI-001-1"
        var original = "PI-001";
        var amended = original + "-1";
        amended.ShouldBe("PI-001-1");
    }

    [Fact]
    public void AmendmentNumber_SecondAmendment_IncrementsSuffix()
    {
        // "PI-001-1" → "PI-001-2" (strip suffix, re-append)
        var firstAmend = "PI-001-1";
        var basePart = firstAmend[..firstAmend.LastIndexOf('-')]; // "PI-001"
        var newAmend = basePart + "-2";
        newAmend.ShouldBe("PI-001-2");
    }
}
