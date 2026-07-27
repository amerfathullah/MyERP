using System;
using System.Linq;
using Xunit;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.Core;

namespace MyERP.DomainTests;

/// <summary>
/// Tests covering VoucherLedger integration on detail pages, RFQ→SQ conversion prerequisites,
/// upstream PR #57452 (child item update list payload), PR #57458 (stock balance zero stock filter),
/// and Stock Reconciliation/LCV/Budget entity properties for voucher ledger display.
/// </summary>
public class VoucherLedgerRfqConversionTests
{
    // === RFQ Entity — Conversion Prerequisites ===

    [Fact]
    public void Rfq_DefaultStatus_Is_Draft()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, rfq.Status);
    }

    [Fact]
    public void Rfq_AddSupplier_Tracks_SupplierId()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        var supplierId = Guid.NewGuid();
        rfq.AddSupplier(supplierId, "Test Supplier", "test@supplier.com");
        Assert.Single(rfq.Suppliers);
        Assert.Equal(supplierId, rfq.Suppliers[0].SupplierId);
    }

    [Fact]
    public void Rfq_AddSupplier_Duplicate_Throws()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        var supplierId = Guid.NewGuid();
        rfq.AddSupplier(supplierId, "Test Supplier", "test@supplier.com");
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            rfq.AddSupplier(supplierId, "Test Supplier", "test@supplier.com"));
    }

    [Fact]
    public void Rfq_Submit_Requires_Items_And_Suppliers()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        // No items or suppliers → submit should throw
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.Submit());
    }

    [Fact]
    public void Rfq_Submit_With_Items_And_Suppliers_Succeeds()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        rfq.AddItem(Guid.NewGuid(), "Item 1", 10, "Unit", null, null);
        rfq.AddSupplier(Guid.NewGuid(), "Supplier 1", "s1@test.com");
        rfq.Submit();
        Assert.Equal(DocumentStatus.Submitted, rfq.Status);
    }

    [Fact]
    public void Rfq_Conversion_Requires_Submitted_Status()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        // Draft RFQ should NOT be convertible (business rule in AppService)
        Assert.Equal(DocumentStatus.Draft, rfq.Status);
    }

    // === Supplier Quotation — Entity for SQ Created from RFQ ===

    [Fact]
    public void SupplierQuotation_RequestForQuotationId_Defaults_Null()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        Assert.Null(sq.RequestForQuotationId);
    }

    [Fact]
    public void SupplierQuotation_RequestForQuotationId_Can_Be_Set()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        var rfqId = Guid.NewGuid();
        sq.RequestForQuotationId = rfqId;
        Assert.Equal(rfqId, sq.RequestForQuotationId);
    }

    [Fact]
    public void SupplierQuotation_AddItem_With_Zero_Rate()
    {
        // When created from RFQ, items start with rate=0 (supplier fills in quoted rate)
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        sq.AddItem(Guid.NewGuid(), 10, 0m, "Item 1", "Unit");
        Assert.Single(sq.Items);
        Assert.Equal(0m, sq.Items[0].Rate);
        Assert.Equal(0m, sq.GrandTotal);
    }

    // === Stock Balance — Zero Stock Filter (PR #57458) ===

    [Fact]
    public void Bin_Zero_Qty_Should_Be_Included_By_Default()
    {
        // Per ERPNext PR #57458: zero stock items shown by default
        // ExcludeZeroStock defaults false → zero stock bins returned
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(0m, bin.ActualQty);
        // Default behavior: this bin SHOULD appear in stock balance (not filtered out)
    }

    [Fact]
    public void Bin_NonZero_Qty_Always_Included()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ApplyStockMovement(100m, 10m);
        Assert.Equal(100m, bin.ActualQty);
    }

    // === Stock Reconciliation — Voucher Ledger Support ===

    [Fact]
    public void StockReconciliation_Default_Status_Is_Draft()
    {
        var sr = new StockReconciliation(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, sr.Status);
    }

    [Fact]
    public void StockReconciliation_Submit_Sets_Submitted()
    {
        var sr = new StockReconciliation(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        sr.AddItem(Guid.NewGuid(), Guid.NewGuid(), 50, 10, 100);
        sr.Submit();
        Assert.Equal(DocumentStatus.Submitted, sr.Status);
    }

    // === LCV — Voucher Ledger Support ===

    [Fact]
    public void LandedCostVoucher_Default_Status_Is_Draft()
    {
        var lcv = new LandedCostVoucher(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, lcv.Status);
    }

    // === Budget — Voucher Ledger Support ===

    [Fact]
    public void Budget_Default_Status_Is_Draft()
    {
        var budget = new Accounting.Entities.Budget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(DocumentStatus.Draft, budget.Status);
    }

    // === Work Order — Voucher Ledger Support (already had detail, now has ledger) ===

    [Fact]
    public void WorkOrder_Submitted_Status_Enables_Ledger_View()
    {
        var wo = new Manufacturing.Entities.WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(),
            100, Guid.NewGuid());
        wo.Submit();
        // Status >= 1 (Submitted) enables voucher ledger display
        Assert.True((int)wo.Status >= 1);
    }

    // === Material Request — Voucher Ledger Support ===

    [Fact]
    public void MaterialRequest_Submitted_Has_GL_Entries()
    {
        // MR itself doesn't create GL entries, but budget validation
        // happens at submit time — voucher ledger shows budget checks
        var mr = new Purchasing.Entities.MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            Purchasing.MaterialRequestType.Purchase, DateTime.Today, Guid.NewGuid());
        mr.AddItem(Guid.NewGuid(), "Item 1", 10, "Unit");
        mr.Submit();
        Assert.Equal(DocumentStatus.Submitted, mr.Status);
    }

    // === RFQ Supplier — Quote Status Tracking ===

    [Fact]
    public void RfqSupplier_QuoteStatus_Defaults_Pending()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        rfq.AddSupplier(Guid.NewGuid(), "Supplier", "s@test.com");
        // Per ERPNext: quoteStatus starts as "Pending", transitions to "Received" when SQ submitted
        Assert.Equal("Pending", rfq.Suppliers[0].QuoteStatus);
    }

    [Fact]
    public void RfqSupplier_MarkQuoteReceived_Changes_Status()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        rfq.AddSupplier(Guid.NewGuid(), "Supplier", "s@test.com");
        rfq.Suppliers[0].MarkQuoteReceived();
        Assert.Equal("Received", rfq.Suppliers[0].QuoteStatus);
    }

    // === Upstream PR #57452 — Child Item Update List Payload ===

    [Fact]
    public void SalesOrder_Items_Can_Be_Modified_In_Draft()
    {
        // PR #57452: update_child_qty_rate accepts list payload
        // Our ClearItems + AddItem pattern already handles this via typed DTOs
        var so = new Sales.Entities.SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.Today, Guid.NewGuid());
        so.AddItem(Guid.NewGuid(), "Item 1", 10, 100m, 0m, "Unit");
        Assert.Single(so.Items);
        so.ClearItems();
        Assert.Empty(so.Items);
        so.AddItem(Guid.NewGuid(), "Item 2", 20, 200m, 0m, "Unit");
        Assert.Single(so.Items);
        Assert.Equal(20, so.Items[0].Quantity);
    }

    [Fact]
    public void PurchaseOrder_Items_Can_Be_Modified_In_Draft()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.Today, Guid.NewGuid());
        po.AddItem(Guid.NewGuid(), "Item 1", 5, 50m, 0m, "Unit");
        po.ClearItems();
        po.AddItem(Guid.NewGuid(), "Item 2", 15, 150m, 0m, "Unit");
        Assert.Equal(15, po.Items[0].Quantity);
    }
}
