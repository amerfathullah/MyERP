using System;
using System.Linq;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests covering RFQ lifecycle, POS Closing, SerialAndBatchBundle, 
/// SubcontractingInwardOrder, ItemStandardCost, RepostItemValuation 
/// entity invariants and cross-cutting behaviors.
/// </summary>
public class EntityLifecycleInvariantTests
{
    private static readonly Guid Co = Guid.NewGuid();

    // ──── RequestForQuotation ────

    [Fact]
    public void RFQ_Create_DefaultsDraft()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        Assert.Equal(DocumentStatus.Draft, rfq.Status);
        Assert.Empty(rfq.Items);
        Assert.Empty(rfq.Suppliers);
    }

    [Fact]
    public void RFQ_AddItem_IncreasesCount()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Steel Rod", 100, "Kg");
        Assert.Single(rfq.Items);
        Assert.Equal(100m, rfq.Items[0].Qty);
    }

    [Fact]
    public void RFQ_AddItem_NegativeQty_Throws()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => rfq.AddItem(Guid.NewGuid(), "Item", -5, "Unit"));
    }

    [Fact]
    public void RFQ_AddSupplier_IncreasesCount()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        rfq.AddSupplier(Guid.NewGuid(), "Acme Corp", "acme@test.com");
        Assert.Single(rfq.Suppliers);
    }

    [Fact]
    public void RFQ_AddDuplicateSupplier_Throws()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        var supplierId = Guid.NewGuid();
        rfq.AddSupplier(supplierId, "Acme Corp");
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.AddSupplier(supplierId, "Acme Corp"));
    }

    [Fact]
    public void RFQ_Submit_RequiresItemsAndSuppliers()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        // No suppliers yet — should throw
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.Submit());
    }

    [Fact]
    public void RFQ_Submit_WithItemsAndSuppliers_Succeeds()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Acme");
        rfq.Submit();
        Assert.Equal(DocumentStatus.Submitted, rfq.Status);
    }

    [Fact]
    public void RFQ_AddItem_AfterSubmit_Throws()
    {
        var rfq = CreateSubmittedRfq();
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.AddItem(Guid.NewGuid(), "New", 5, "Unit"));
    }

    [Fact]
    public void RFQ_Cancel_Succeeds()
    {
        var rfq = CreateSubmittedRfq();
        rfq.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, rfq.Status);
    }

    [Fact]
    public void RFQ_Cancel_AlreadyCancelled_Throws()
    {
        var rfq = CreateSubmittedRfq();
        rfq.Cancel();
        Assert.Throws<Volo.Abp.BusinessException>(() => rfq.Cancel());
    }

    // ──── PosClosingEntry ────

    [Fact]
    public void PosClosing_Create_DefaultsDraft()
    {
        var pce = new PosClosingEntry(Guid.NewGuid(), Co, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosClosingStatus.Draft, pce.Status);
        Assert.Empty(pce.Payments);
        Assert.Empty(pce.Invoices);
    }

    [Fact]
    public void PosClosing_PostingDate_SetToToday()
    {
        var pce = new PosClosingEntry(Guid.NewGuid(), Co, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(DateTime.UtcNow.Date, pce.PostingDate);
    }

    // ──── SerialAndBatchBundle ────

    [Fact]
    public void SABB_Create_DefaultsNotCancelled()
    {
        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), Co, Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);
        Assert.False(bundle.IsCancelled);
        Assert.Equal(0m, bundle.TotalQty);
    }

    [Fact]
    public void SABB_Cancel_BlocksAddEntry()
    {
        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), Co, Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);
        var entry = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 5m, 100m);
        bundle.AddEntry(entry);
        bundle.Cancel();
        var entry2 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 3m, 50m);
        Assert.Throws<Volo.Abp.BusinessException>(() => bundle.AddEntry(entry2));
    }

    // ──── ItemStandardCost ────

    [Fact]
    public void ISC_Create_DefaultsDraft()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Co, Guid.NewGuid(), 100m, DateTime.UtcNow);
        Assert.Equal(DocumentStatus.Draft, isc.Status);
        Assert.Equal(100m, isc.StandardRate);
    }

    [Fact]
    public void ISC_FutureDate_Throws()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new ItemStandardCost(Guid.NewGuid(), Co, Guid.NewGuid(), 100m, DateTime.UtcNow.AddDays(5)));
    }

    [Fact]
    public void ISC_ZeroRate_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ItemStandardCost(Guid.NewGuid(), Co, Guid.NewGuid(), 0m, DateTime.UtcNow));
    }

    [Fact]
    public void ISC_Submit_SetsStatus()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Co, Guid.NewGuid(), 50m, DateTime.UtcNow);
        isc.Submit();
        Assert.Equal(DocumentStatus.Submitted, isc.Status);
    }

    // ──── RepostItemValuation ────

    [Fact]
    public void RIV_Create_DefaultsQueued()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Co, RepostMethod.ItemAndWarehouse,
            DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(RepostStatus.Queued, riv.Status);
    }

    [Fact]
    public void RIV_Start_SetsInProgress()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Co, RepostMethod.ItemAndWarehouse,
            DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing();
        Assert.Equal(RepostStatus.InProgress, riv.Status);
    }

    [Fact]
    public void RIV_Complete_SetsCompleted()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Co, RepostMethod.ItemAndWarehouse,
            DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing();
        riv.Complete(10);
        Assert.Equal(RepostStatus.Completed, riv.Status);
    }

    [Fact]
    public void RIV_Fail_SetsFailedWithError()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Co, RepostMethod.ItemAndWarehouse,
            DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing();
        riv.Fail("Concurrency conflict");
        Assert.Equal(RepostStatus.Failed, riv.Status);
    }

    [Fact]
    public void RIV_IsCoveredBy_BroaderRepost_ReturnsTrue()
    {
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var narrow = new RepostItemValuation(Guid.NewGuid(), Co, RepostMethod.ItemAndWarehouse,
            new DateTime(2026, 6, 15), itemId, whId);
        // A broader company-wide repost from earlier date
        var broader = new RepostItemValuation(Guid.NewGuid(), Co, RepostMethod.EntireCompany,
            new DateTime(2026, 6, 1));
        // Active (InProgress) repost covers queued ones
        broader.StartProcessing();
        Assert.True(narrow.IsCoveredBy(broader));
    }

    // ──── SubcontractingInwardOrder ────

    [Fact]
    public void SCIO_Create_DefaultsDraft()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Co, "SCIO-001",
            DateTime.UtcNow, Guid.NewGuid());
        Assert.Equal(SubcontractingInwardOrderStatus.Draft, scio.Status);
        Assert.Empty(scio.Items);
    }

    [Fact]
    public void SCIO_AddItem_IncreasesCount()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Co, "SCIO-001",
            DateTime.UtcNow, Guid.NewGuid());
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 100m, 50m);
        scio.AddItem(item);
        Assert.Single(scio.Items);
    }

    [Fact]
    public void SCIO_Submit_RequiresItems()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Co, "SCIO-001",
            DateTime.UtcNow, Guid.NewGuid());
        Assert.Throws<Volo.Abp.BusinessException>(() => scio.Submit());
    }

    [Fact]
    public void SCIO_Submit_WithItems_SetsOpen()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Co, "SCIO-001",
            DateTime.UtcNow, Guid.NewGuid());
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scio.Id, Guid.NewGuid(), 100m, 50m);
        scio.AddItem(item);
        scio.Submit();
        Assert.Equal(SubcontractingInwardOrderStatus.Open, scio.Status);
    }

    // ──── BomOperation ────

    [Fact]
    public void BomOp_GetTotalTime_WithBatchSize()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 30m)
        {
            BatchSize = 50,
            FixedTime = 10m
        };
        // Total time = fixed + (qty × timePerUnit) if batch-based, or just time+fixed
        var totalTime = op.GetTotalTime(100);
        Assert.True(totalTime > 0);
    }

    [Fact]
    public void BomOp_SequenceId_MustBePositive()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 60m);
        Assert.True(op.SequenceId > 0);
    }

    // ──── Company Frozen Dates ────

    [Fact]
    public void Company_StockFrozenUpto_DefaultsNull()
    {
        var company = new MyERP.Core.Entities.Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.StockFrozenUpto);
        Assert.Null(company.AccountsFrozenTillDate);
    }

    [Fact]
    public void Company_DefaultAccounts_AllNullable()
    {
        var company = new MyERP.Core.Entities.Company(Guid.NewGuid(), "Test Co");
        Assert.Null(company.DefaultReceivableAccountId);
        Assert.Null(company.DefaultPayableAccountId);
        Assert.Null(company.DefaultBankAccountId);
        Assert.Null(company.ExchangeGainLossAccountId);
    }

    // ──── AccountingDimension ────

    [Fact]
    public void AccountingDimension_FieldName_GeneratedFromDocType()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "CostCenter", "Cost Center");
        Assert.Contains("cost", dim.FieldName.ToLower());
    }

    [Fact]
    public void AccountingDimension_EnableDisable()
    {
        var dim = new AccountingDimension(Guid.NewGuid(), "CostCenter", "Cost Center");
        Assert.True(dim.IsEnabled);
        dim.Disable();
        Assert.False(dim.IsEnabled);
        dim.Enable();
        Assert.True(dim.IsEnabled);
    }

    // ──── Helpers ────

    private static RequestForQuotation CreateSubmittedRfq()
    {
        var rfq = new RequestForQuotation(Guid.NewGuid(), Co, "RFQ-001", DateTime.UtcNow);
        rfq.AddItem(Guid.NewGuid(), "Widget", 10, "Unit");
        rfq.AddSupplier(Guid.NewGuid(), "Acme");
        rfq.Submit();
        return rfq;
    }
}
