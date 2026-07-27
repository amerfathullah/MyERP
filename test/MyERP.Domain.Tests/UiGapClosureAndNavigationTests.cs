using System;
using System.Linq;
using Xunit;
using MyERP.Core;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Inventory.Entities;

namespace MyERP.DomainTests;

/// <summary>
/// Tests covering POS Closing VoucherLedger prerequisites, list→detail navigation entity properties,
/// RFQ supplier quote status, Proforma Invoice email lifecycle, Dunning customer display,
/// Batch/SerialNo detail navigation prerequisites, and Job Card operation sequence.
/// </summary>
public class UiGapClosureAndNavigationTests
{
    // === POS Closing — VoucherLedger Prerequisite ===

    [Fact]
    public void PosClosingEntry_Submitted_Enables_VoucherLedger()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", 1000m, 1050m);
        entry.AddInvoice(Guid.NewGuid(), "SI-001", 1200m);
        entry.Submit();
        Assert.Equal(PosClosingStatus.Submitted, entry.Status);
    }

    [Fact]
    public void PosClosingEntry_Draft_Does_Not_Show_VoucherLedger()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosClosingStatus.Draft, entry.Status);
    }

    [Fact]
    public void PosClosingEntry_TotalDifference_Sums_All_Payment_Variances()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AddPayment(Guid.NewGuid(), "Cash", 500m, 520m);  // expected - closing = -20
        entry.AddPayment(Guid.NewGuid(), "Card", 300m, 290m);  // expected - closing = +10
        Assert.Equal(-10m, entry.TotalDifference); // net = -20 + 10 = -10
    }

    [Fact]
    public void PosClosingEntry_ConsolidatedSalesInvoiceId_Defaults_Null()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(entry.ConsolidatedSalesInvoiceId);
    }

    // === RFQ Supplier Quote Status ===

    [Fact]
    public void RfqSupplier_QuoteStatus_Defaults_Pending()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A", "a@test.com");
        Assert.Equal("Pending", rfq.Suppliers[0].QuoteStatus);
    }

    [Fact]
    public void RfqSupplier_MarkQuoteReceived_Changes_Status()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        var supplierId = Guid.NewGuid();
        rfq.AddSupplier(supplierId, "Supplier A", "a@test.com");
        rfq.Suppliers[0].MarkQuoteReceived();
        Assert.Equal("Received", rfq.Suppliers[0].QuoteStatus);
    }

    [Fact]
    public void RfqSupplier_EmailSent_Defaults_False()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Guid.NewGuid(), "RFQ-001", DateTime.Today, Guid.NewGuid());
        rfq.AddSupplier(Guid.NewGuid(), "Supplier A", "a@test.com");
        Assert.False(rfq.Suppliers[0].EmailSent);
    }

    // === Proforma Invoice ===

    [Fact]
    public void ProformaInvoice_SentOn_Defaults_Null()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        Assert.Null(pi.SentOn);
        Assert.Null(pi.EmailedTo);
    }

    [Fact]
    public void ProformaInvoice_Status_Issued_After_Submit()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        pi.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget", 10, 100m);
        pi.Submit();
        Assert.Equal(ProformaInvoiceStatus.Issued, pi.Status);
    }

    [Fact]
    public void ProformaInvoice_Cancel_From_Issued()
    {
        var pi = new ProformaInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        pi.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget", 10, 100m);
        pi.Submit();
        pi.Cancel();
        Assert.Equal(ProformaInvoiceStatus.Cancelled, pi.Status);
    }

    // === Batch Detail ===

    [Fact]
    public void Batch_BatchNo_Is_Navigable_Property()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        Assert.Equal("BATCH-001", batch.BatchNo);
    }

    [Fact]
    public void Batch_ExpiryDate_Nullable_For_Non_Perishable()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002");
        Assert.Null(batch.ExpiryDate);
    }

    [Fact]
    public void Batch_IsDisabled_Defaults_False()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");
        Assert.False(batch.IsDisabled);
    }

    // === Serial No Detail ===

    [Fact]
    public void SerialNo_SerialNumber_Is_Primary_Display_Field()
    {
        var sn = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-0001", Guid.NewGuid());
        Assert.Equal("SN-0001", sn.SerialNumber);
    }

    [Fact]
    public void SerialNo_MaintenanceStatus_Defaults_To_OutOfWarranty()
    {
        var sn = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-0002", Guid.NewGuid());
        Assert.Equal("Out of Warranty", sn.MaintenanceStatus);
    }

    // === Job Card — List Navigation ===

    [Fact]
    public void JobCard_SequenceId_Is_Primary_Display_Field()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, 10);
        Assert.Equal(10, jc.SequenceId);
    }

    [Fact]
    public void JobCard_CompletedQty_Defaults_Zero()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50, 10);
        Assert.Equal(0, jc.CompletedQty);
    }

    [Fact]
    public void JobCard_TotalTimeInMins_Defaults_Zero()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50, 10);
        Assert.Equal(0, jc.TotalTimeInMins);
    }

    // === Dunning — Customer Display ===

    [Fact]
    public void Dunning_DunningLevel_Starts_At_One()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 1, Guid.NewGuid());
        Assert.Equal(1, dunning.DunningLevel);
    }

    [Fact]
    public void Dunning_GrandTotal_Includes_Fee_And_Interest()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 1, Guid.NewGuid());
        dunning.DunningFee = 50m;
        dunning.InterestAmount = 120m;
        dunning.TotalOutstanding = 5000m;
        Assert.Equal(5170m, dunning.GrandTotal);
    }

    // === Supplier Quotation — RFQ Linkage ===

    [Fact]
    public void SupplierQuotation_RequestForQuotationId_Can_Be_Set()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        var rfqId = Guid.NewGuid();
        sq.RequestForQuotationId = rfqId;
        Assert.Equal(rfqId, sq.RequestForQuotationId);
    }

    // === Cross-Entity: VoucherLedger visibility ===

    [Fact]
    public void WorkOrder_Submitted_Has_Status_For_VoucherLedger()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        Assert.True((int)wo.Status >= 1);
    }

    [Fact]
    public void MaterialRequest_Submitted_Has_Status_For_VoucherLedger()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.Today, Guid.NewGuid());
        mr.AddItem(Guid.NewGuid(), "Item A", 10, "Unit");
        mr.Submit();
        Assert.Equal(DocumentStatus.Submitted, mr.Status);
    }

    [Fact]
    public void Budget_Submitted_Has_Status_For_VoucherLedger()
    {
        var budget = new Accounting.Entities.Budget(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CostCenter", Guid.NewGuid());
        budget.AddAccount(Guid.NewGuid(), 50000m);
        budget.Submit();
        Assert.Equal(DocumentStatus.Submitted, budget.Status);
    }
}
