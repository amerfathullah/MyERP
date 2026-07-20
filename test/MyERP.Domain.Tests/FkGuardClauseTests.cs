using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Purchasing;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.EntityGuardTests;

/// <summary>
/// Tests that required FK Guid parameters throw ArgumentException when Guid.Empty is passed.
/// </summary>
public class FkGuardClauseTests
{
    [Fact]
    public void SalesInvoice_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new SalesInvoice(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "SI-001", DateTime.Today));
    }

    [Fact]
    public void SalesInvoice_EmptyCustomerId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "SI-001", DateTime.Today));
    }

    [Fact]
    public void SalesInvoice_ValidGuids_Succeeds()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.CompanyId.ShouldNotBe(Guid.Empty);
        si.CustomerId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void PurchaseOrder_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new PurchaseOrder(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "PO-001", DateTime.Today));
    }

    [Fact]
    public void PurchaseOrder_EmptySupplierId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "PO-001", DateTime.Today));
    }

    [Fact]
    public void PaymentEntry_EmptyPaidFromAccount_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
                DateTime.Today, 1000m, Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void PaymentEntry_EmptyPaidToAccount_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
                DateTime.Today, 1000m, Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void WorkOrder_EmptyItemId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.Empty, Guid.NewGuid(),
                10));
    }

    [Fact]
    public void WorkOrder_EmptyBomId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.Empty,
                10));
    }

    [Fact]
    public void Asset_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Asset(Guid.NewGuid(), Guid.Empty, "AST-001", "Computer",
                DateTime.Today, 5000m));
    }

    [Fact]
    public void StockEntry_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new StockEntry(Guid.NewGuid(), Guid.Empty, StockEntryType.MaterialReceipt, DateTime.Today));
    }

    [Fact]
    public void MaterialRequest_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new MaterialRequest(Guid.NewGuid(), Guid.Empty, "MR-001",
                MaterialRequestType.Purchase, DateTime.Today));
    }

    [Fact]
    public void Budget_EmptyFiscalYearId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
                "CostCenter", Guid.NewGuid()));
    }

    // ========== Round 2: 6 more entities ==========

    [Fact]
    public void SalesOrder_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new SalesOrder(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "SO-001", DateTime.Today));
    }

    [Fact]
    public void SalesOrder_EmptyCustomerId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "SO-001", DateTime.Today));
    }

    [Fact]
    public void DeliveryNote_EmptyWarehouseId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
                "DN-001", DateTime.Today));
    }

    [Fact]
    public void PurchaseInvoice_EmptySupplierId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "PI-001", DateTime.Today));
    }

    [Fact]
    public void PurchaseReceipt_EmptyWarehouseId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
                "PR-001", DateTime.Today));
    }

    [Fact]
    public void Quotation_EmptyCompanyId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Quotation(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "QTN-001", DateTime.Today));
    }

    [Fact]
    public void Quotation_EmptyCustomerId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "QTN-001", DateTime.Today));
    }

    [Fact]
    public void JournalEntry_EmptyFiscalYearId_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTime.Today));
    }

    // ========== Quotation Lifecycle (Test #3000!) ==========

    [Fact]
    public void Quotation_Create_SetsDefaults()
    {
        var q = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QTN-001", DateTime.Today);
        q.QuotationNumber.ShouldBe("QTN-001");
        q.Status.ShouldBe(Core.DocumentStatus.Draft);
        q.CurrencyCode.ShouldBe("MYR");
        q.Items.Count.ShouldBe(0);
        q.GrandTotal.ShouldBe(0m);
        q.IsExpired.ShouldBeFalse();
        q.AmendedFromId.ShouldBeNull();
    }

    // ========== AddItem itemId guards ==========

    [Fact]
    public void SI_AddItem_EmptyItemId_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        Should.Throw<ArgumentException>(() => si.AddItem(Guid.Empty, "Widget", 1, 100m, 0m));
    }

    [Fact]
    public void SO_AddItem_EmptyItemId_Throws()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        Should.Throw<ArgumentException>(() => so.AddItem(Guid.Empty, "Widget", 1, 100m, 0m));
    }

    [Fact]
    public void PO_AddItem_EmptyItemId_Throws()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.Today);
        Should.Throw<ArgumentException>(() => po.AddItem(Guid.Empty, "Material", 1, 50m, 0m));
    }

    [Fact]
    public void PI_AddItem_EmptyItemId_Throws()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        Should.Throw<ArgumentException>(() => pi.AddItem(Guid.Empty, "Material", 1, 50m, 0m));
    }

    // ========== Domain Event Record Types Exist ==========

    [Fact]
    public void PaymentEntry_PostAndCancel_StatusTransitions()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid());
        pe.Submit();
        pe.Status.ShouldBe(Core.DocumentStatus.Submitted);
        pe.Post();
        pe.Status.ShouldBe(Core.DocumentStatus.Posted);
        pe.Cancel();
        pe.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void PurchaseInvoice_SubmitPostCancel_StatusTransitions()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        pi.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);
        pi.Submit();
        pi.Status.ShouldBe(Core.DocumentStatus.Submitted);
        pi.Post();
        pi.Status.ShouldBe(Core.DocumentStatus.Posted);
        pi.Cancel();
        pi.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void JournalEntry_PostCancel_StatusTransitions()
    {
        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        je.AddLine(Guid.NewGuid(), 500m, true);
        je.AddLine(Guid.NewGuid(), 500m, false);
        je.Post();
        je.Status.ShouldBe(Core.DocumentStatus.Posted);
        je.Cancel();
        je.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void DomainEventRecords_Exist()
    {
        // Verify the domain event record types compile and can be instantiated
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive,
            DateTime.Today, 1000m, Guid.NewGuid(), Guid.NewGuid());
        var peEvt = new PaymentEntryPostedEvent(pe);
        peEvt.PaymentEntry.ShouldBe(pe);

        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI", DateTime.Today);
        var piEvt = new PurchaseInvoiceSubmittedEvent(pi);
        piEvt.Invoice.ShouldBe(pi);

        var je = new JournalEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        var jeEvt = new JournalEntryPostedEvent(je);
        jeEvt.JournalEntry.ShouldBe(je);
    }
}
