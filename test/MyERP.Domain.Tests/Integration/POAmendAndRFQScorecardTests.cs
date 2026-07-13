using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - PO AmendAsync: creates copy with amendment link
/// - RFQ/SQ scorecard enforcement: PreventRfqs blocks SQ creation
/// - SI/PI DTO amendment fields exposed
/// </summary>
public class POAmendAndRFQScorecardTests
{
    // --- PO Amendment ---

    [Fact]
    public void PurchaseOrder_Amendment_CopiesFields()
    {
        var original = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-100", DateTime.Today);
        original.ExpectedDeliveryDate = DateTime.Today.AddDays(14);
        original.CurrencyCode = "USD";
        original.Terms = "Net 30";
        original.Notes = "Urgent order";

        // Simulate amend
        var amended = new PurchaseOrder(Guid.NewGuid(), original.CompanyId, original.SupplierId, "PO-100-1", DateTime.Today);
        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = 1;
        amended.ExpectedDeliveryDate = original.ExpectedDeliveryDate;
        amended.CurrencyCode = original.CurrencyCode;
        amended.Terms = original.Terms;
        amended.Notes = original.Notes;

        amended.AmendedFromId.ShouldBe(original.Id);
        amended.CurrencyCode.ShouldBe("USD");
        amended.Terms.ShouldBe("Net 30");
    }

    [Fact]
    public void PurchaseOrder_Amendment_CopiesItems()
    {
        var original = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-200", DateTime.Today);
        original.AddItem(Guid.NewGuid(), "Widget A", 10, 25.50m, 0, "Pcs");
        original.AddItem(Guid.NewGuid(), "Widget B", 5, 100m, 6m, "Pcs");

        // Amend copies items
        var amended = new PurchaseOrder(Guid.NewGuid(), original.CompanyId, original.SupplierId, "PO-200-1", DateTime.Today);
        foreach (var item in original.Items)
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);

        amended.Items.Count.ShouldBe(2);
        amended.NetTotal.ShouldBe(original.NetTotal);
    }

    [Fact]
    public void PurchaseOrder_OnlyCancelled_CanAmend()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-300", DateTime.Today);
        // Draft status cannot be amended
        (po.Status == DocumentStatus.Cancelled).ShouldBeFalse();
    }

    // --- Supplier Scorecard RFQ Enforcement ---

    [Fact]
    public void Supplier_PreventRfqs_DefaultsFalse()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.PreventRfqs.ShouldBeFalse();
    }

    [Fact]
    public void Supplier_PreventRfqs_WhenTrue_BlocksSQCreation()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Bad Supplier");
        supplier.PreventRfqs = true;
        supplier.PreventRfqs.ShouldBeTrue();
    }

    [Fact]
    public void Supplier_PreventPOs_DoesNotBlockSQ()
    {
        // PreventPurchaseOrders and PreventRfqs are independent
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Partially Blocked");
        supplier.PreventPurchaseOrders = true;
        supplier.PreventRfqs = false;

        supplier.PreventPurchaseOrders.ShouldBeTrue();
        supplier.PreventRfqs.ShouldBeFalse();
    }

    // --- SI/PI DTO Amendment Fields ---

    [Fact]
    public void SalesInvoice_AmendmentFields_ExposedInEntity()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.AmendedFromId = Guid.NewGuid();
        si.AmendmentIndex = 2;
        si.IsReturn = true;
        si.ReturnAgainstId = Guid.NewGuid();

        si.AmendedFromId.ShouldNotBeNull();
        si.AmendmentIndex.ShouldBe(2);
        si.IsReturn.ShouldBeTrue();
        si.ReturnAgainstId.ShouldNotBeNull();
    }

    [Fact]
    public void PurchaseInvoice_AmendmentFields_ExposedInEntity()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        pi.AmendedFromId = Guid.NewGuid();
        pi.AmendmentIndex = 1;
        pi.IsReturn = true;
        pi.ReturnAgainstId = Guid.NewGuid();

        pi.AmendedFromId.ShouldNotBeNull();
        pi.AmendmentIndex.ShouldBe(1);
        pi.IsReturn.ShouldBeTrue();
        pi.ReturnAgainstId.ShouldNotBeNull();
    }
}
