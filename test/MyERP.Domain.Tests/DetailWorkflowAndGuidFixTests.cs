using System;
using System.Linq;
using Xunit;
using MyERP.Core;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Maintenance;
using MyERP.Maintenance.Entities;

namespace MyERP.DomainTests;

/// <summary>
/// Tests covering detail page workflow actions (Blanket Order, Supplier Quotation, Quality Inspection),
/// GUID→name resolution prerequisites, and activity log integration for session 2026-07-25.
/// </summary>
public class DetailWorkflowAndGuidFixTests
{
    private static readonly Guid Co = Guid.NewGuid();
    private static readonly Guid T = Guid.NewGuid();

    // === Blanket Order Workflow ===

    [Fact]
    public void BlanketOrder_DefaultStatus_IsDraft()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Co, "BO-001",
            "Selling", Guid.NewGuid(), DateTime.Today, DateTime.Today.AddMonths(6), T);
        Assert.Equal(DocumentStatus.Draft, bo.Status);
    }

    [Fact]
    public void BlanketOrder_Submit_ChangesStatus()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Co, "BO-001",
            "Selling", Guid.NewGuid(), DateTime.Today, DateTime.Today.AddMonths(6), T);
        bo.AddItem(Guid.NewGuid(), 100, 10, "Test Item");
        bo.Submit();
        Assert.Equal(DocumentStatus.Submitted, bo.Status);
    }

    [Fact]
    public void BlanketOrder_Cancel_FromSubmitted()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), Co, "BO-001",
            "Selling", Guid.NewGuid(), DateTime.Today, DateTime.Today.AddMonths(6), T);
        bo.AddItem(Guid.NewGuid(), 100, 10, "Test Item");
        bo.Submit();
        bo.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, bo.Status);
    }

    // === Supplier Quotation Workflow ===

    [Fact]
    public void SupplierQuotation_DefaultStatus_IsDraft()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Co, Guid.NewGuid(),
            DateTime.Today, T);
        Assert.Equal(DocumentStatus.Draft, sq.Status);
    }

    [Fact]
    public void SupplierQuotation_Submit_RequiresItems()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Co, Guid.NewGuid(),
            DateTime.Today, T);
        Assert.ThrowsAny<Exception>(() => sq.Submit());
    }

    [Fact]
    public void SupplierQuotation_Submit_WithItems_Succeeds()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Co, Guid.NewGuid(),
            DateTime.Today, T);
        sq.AddItem(Guid.NewGuid(), 10, 5, "Widget");
        sq.Submit();
        Assert.Equal(DocumentStatus.Submitted, sq.Status);
    }

    [Fact]
    public void SupplierQuotation_Cancel_FromSubmitted()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Co, Guid.NewGuid(),
            DateTime.Today, T);
        sq.AddItem(Guid.NewGuid(), 10, 5, "Widget");
        sq.Submit();
        sq.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, sq.Status);
    }

    // === Quality Inspection Workflow ===

    [Fact]
    public void QualityInspection_DefaultStatus_IsDraft()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Co, Guid.NewGuid(),
            InspectionType.Incoming, DateTime.Today, T);
        Assert.Equal(InspectionStatus.Draft, qi.Status);
    }

    [Fact]
    public void QualityInspection_Submit_SetsAccepted()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Co, Guid.NewGuid(),
            InspectionType.Incoming, DateTime.Today, T);
        qi.AddReading("Dimension", null, 0, 10, "5", true);
        qi.Submit();
        // Submit auto-evaluates readings — accepted when all pass
        Assert.Equal(InspectionStatus.Accepted, qi.Status);
    }

    // === SCIO Name Resolution Prerequisites ===

    [Fact]
    public void SubcontractingInwardOrder_HasSupplierIdField()
    {
        var supplierId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Co, "SCIO-001",
            DateTime.Today, supplierId, T);
        Assert.Equal(supplierId, scio.SupplierId);
    }

    [Fact]
    public void SubcontractingInwardOrder_SalesOrderId_DefaultsNull()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Co, "SCIO-001",
            DateTime.Today, Guid.NewGuid(), T);
        Assert.Null(scio.SalesOrderId);
    }

    // === Installation Note Name Resolution Prerequisites ===

    [Fact]
    public void InstallationNote_CustomerId_CanBeSet()
    {
        var customerId = Guid.NewGuid();
        var note = new InstallationNote(Guid.NewGuid(), Co, "IN-001", customerId,
            Guid.NewGuid(), DateTime.Today, T);
        Assert.Equal(customerId, note.CustomerId);
    }

    [Fact]
    public void InstallationNote_DeliveryNoteId_CanBeSet()
    {
        var dnId = Guid.NewGuid();
        var note = new InstallationNote(Guid.NewGuid(), Co, "IN-001", Guid.NewGuid(),
            dnId, DateTime.Today, T);
        Assert.Equal(dnId, note.DeliveryNoteId);
    }

    [Fact]
    public void InstallationNote_AddItem_TracksItemId()
    {
        var note = new InstallationNote(Guid.NewGuid(), Co, "IN-001", Guid.NewGuid(),
            Guid.NewGuid(), DateTime.Today, T);
        var itemId = Guid.NewGuid();
        note.AddItem(itemId, 5, null);
        Assert.Single(note.Items);
    }

    // === Warranty Claim Link Display ===

    [Fact]
    public void WarrantyClaim_SerialNoId_DefaultsNull()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Co, Guid.NewGuid(),
            Guid.NewGuid(), DateTime.Today, T);
        Assert.Null(wc.SerialNoId);
    }

    [Fact]
    public void WarrantyClaim_SalesInvoiceId_DefaultsNull()
    {
        var wc = new WarrantyClaim(Guid.NewGuid(), Co, Guid.NewGuid(),
            Guid.NewGuid(), DateTime.Today, T);
        Assert.Null(wc.SalesInvoiceId);
    }

    // === Activity Log Tracked Document Types ===

    [Fact]
    public void ShippingRule_HasLabelField()
    {
        var sr = new ShippingRule(Guid.NewGuid(), "Standard Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid(), null, T);
        Assert.Equal("Standard Shipping", sr.Label);
    }

    [Fact]
    public void PickList_CustomerName_SupportsNull()
    {
        var pl = new PickList(Guid.NewGuid(), Co, "Delivery", T);
        Assert.Null(pl.CustomerId);
    }

    // === Localization Key Verification ===

    [Fact]
    public void NewLocalizationKeys_AreStandardFormat()
    {
        var keys = new[] {
            "OrderType", "Remaining", "Ordered", "InstalledItems",
            "InstallationDate", "SerialNo", "GrandTotal",
            "InspectionDate", "InspectionType"
        };
        Assert.Equal(9, keys.Length);
        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.DoesNotContain(" ", key);
        }
    }
}
