using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class NewEntityTests
{
    #region SerialAndBatchBundle

    [Fact]
    public void SerialAndBatchBundle_Create_DefaultState()
    {
        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(BundleTransactionType.Inward, bundle.TypeOfTransaction);
        Assert.Equal(0, bundle.TotalQty);
        Assert.Equal(0, bundle.AvgRate);
        Assert.Equal(0, bundle.TotalAmount);
        Assert.False(bundle.IsCancelled);
        Assert.False(bundle.IsRejected);
        Assert.Empty(bundle.Entries);
    }

    [Fact]
    public void SerialAndBatchBundle_AddEntry_RecalculatesTotals()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(),
            DateTime.UtcNow);

        var entry1 = new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 5, 10.0m, serialNo: "SN001");
        var entry2 = new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 3, 12.0m, serialNo: "SN002");

        bundle.AddEntry(entry1);
        bundle.AddEntry(entry2);

        Assert.Equal(8, bundle.TotalQty);
        Assert.Equal(86m, bundle.TotalAmount); // 5*10 + 3*12
        Assert.Equal(10.75m, bundle.AvgRate); // 86/8
        Assert.Equal(2, bundle.Entries.Count);
    }

    [Fact]
    public void SerialAndBatchBundle_Cancel_PreservesAuditTrail()
    {
        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(),
            DateTime.UtcNow);

        bundle.Cancel();

        Assert.True(bundle.IsCancelled);
    }

    [Fact]
    public void SerialAndBatchBundle_AddEntry_BlockedWhenCancelled()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.Cancel();

        var entry = new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 10.0m);
        Assert.Throws<Volo.Abp.BusinessException>(() => bundle.AddEntry(entry));
    }

    [Fact]
    public void SerialAndBatchBundle_ValidateQtyMatch_Succeeds()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 10, 5.0m));

        bundle.ValidateQtyMatch(10); // Should not throw
    }

    [Fact]
    public void SerialAndBatchBundle_ValidateQtyMatch_ThrowsOnMismatch()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 10, 5.0m));

        Assert.Throws<Volo.Abp.BusinessException>(() => bundle.ValidateQtyMatch(8));
    }

    [Fact]
    public void SerialAndBatchEntry_StockValueDifference_Computed()
    {
        var entry = new SerialAndBatchEntry(Guid.NewGuid(), Guid.NewGuid(), 5, 20.0m,
            serialNo: "SN-001", batchId: Guid.NewGuid());

        Assert.Equal(100m, entry.StockValueDifference);
        Assert.Equal("SN-001", entry.SerialNo);
        Assert.NotNull(entry.BatchId);
    }

    #endregion

    #region ItemStandardCost

    [Fact]
    public void ItemStandardCost_Create_ValidState()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            25.50m, DateTime.UtcNow.Date.AddDays(-1));

        Assert.Equal(25.50m, isc.StandardRate);
        Assert.Equal(DocumentStatus.Draft, isc.Status);
        Assert.Null(isc.PreviousRate);
        Assert.Null(isc.RevaluationStockReconciliationId);
    }

    [Fact]
    public void ItemStandardCost_FutureDate_Throws()
    {
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                10m, DateTime.UtcNow.Date.AddDays(5)));
    }

    [Fact]
    public void ItemStandardCost_ZeroRate_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                0m, DateTime.UtcNow.Date));
    }

    [Fact]
    public void ItemStandardCost_Submit_ChangesStatus()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            15m, DateTime.UtcNow.Date);

        isc.Submit();

        Assert.Equal(DocumentStatus.Submitted, isc.Status);
    }

    [Fact]
    public void ItemStandardCost_Cancel_WithNoActivity_Succeeds()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            15m, DateTime.UtcNow.Date);
        isc.Submit();

        isc.Cancel(hasStockActivityOnOrAfter: false);

        Assert.Equal(DocumentStatus.Cancelled, isc.Status);
    }

    [Fact]
    public void ItemStandardCost_Cancel_WithActivity_Throws()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            15m, DateTime.UtcNow.Date);
        isc.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() => isc.Cancel(hasStockActivityOnOrAfter: true));
    }

    [Fact]
    public void ItemStandardCost_ValidateAgainstLastSle_ThrowsWhenBefore()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            15m, DateTime.UtcNow.Date.AddDays(-5));

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            isc.ValidateAgainstLastSle(DateTime.UtcNow.Date.AddDays(-3))); // SLE is after effective date
    }

    [Fact]
    public void ItemStandardCost_ValidateAgainstLastSle_PassesWhenAfter()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            15m, DateTime.UtcNow.Date.AddDays(-1));

        isc.ValidateAgainstLastSle(DateTime.UtcNow.Date.AddDays(-5)); // SLE is before effective date — OK
    }

    [Fact]
    public void ItemStandardCost_CalculatePpv()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, DateTime.UtcNow.Date);

        var ppv = isc.CalculatePpv(actualRate: 12m, qty: 5);

        Assert.Equal(10m, ppv); // (12-10)*5 = 10 favorable variance
    }

    #endregion

    #region RepostItemValuation

    [Fact]
    public void RepostItemValuation_Create_DefaultState()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(RepostStatus.Queued, riv.Status);
        Assert.Equal(RepostMethod.ItemAndWarehouse, riv.BasedOn);
        Assert.True(riv.RepostGlEntries);
        Assert.False(riv.IsDeduplicated);
        Assert.Equal(0, riv.TotalAffectedEntries);
    }

    [Fact]
    public void RepostItemValuation_StartProcessing()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.EntireCompany, DateTime.UtcNow);

        riv.StartProcessing();

        Assert.Equal(RepostStatus.InProgress, riv.Status);
    }

    [Fact]
    public void RepostItemValuation_Complete_SetsTotal()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemWise, DateTime.UtcNow, Guid.NewGuid());
        riv.StartProcessing();

        riv.Complete(42);

        Assert.Equal(RepostStatus.Completed, riv.Status);
        Assert.Equal(42, riv.TotalAffectedEntries);
        Assert.Equal(42, riv.CurrentIndex);
    }

    [Fact]
    public void RepostItemValuation_Fail_CapturesError()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        riv.StartProcessing();

        riv.Fail("DB timeout during repost");

        Assert.Equal(RepostStatus.Failed, riv.Status);
        Assert.Equal("DB timeout during repost", riv.ErrorLog);
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_EntireCompanyCovers()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var specific = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, itemId, whId);

        var broad = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.EntireCompany, DateTime.UtcNow.AddDays(-1));

        Assert.True(specific.IsCoveredBy(broad));
    }

    [Fact]
    public void RepostItemValuation_IsCoveredBy_CompletedDoesNotCover()
    {
        var companyId = Guid.NewGuid();
        var specific = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid());

        var completed = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.EntireCompany, DateTime.UtcNow.AddDays(-1));
        completed.StartProcessing();
        completed.Complete(10);

        Assert.False(specific.IsCoveredBy(completed)); // Completed = already done
    }

    #endregion

    #region SubcontractingInwardOrder

    [Fact]
    public void SubcontractingInwardOrder_Create_DefaultState()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Guid.NewGuid(),
            "SCIO-001", DateTime.UtcNow, Guid.NewGuid());

        Assert.Equal(SubcontractingInwardOrderStatus.Draft, scio.Status);
        Assert.Equal("SCIO-001", scio.OrderNumber);
        Assert.Equal(0, scio.PerReceived);
        Assert.Equal(0, scio.PerBilled);
        Assert.Empty(scio.Items);
    }

    [Fact]
    public void SubcontractingInwardOrder_AddItem_Succeeds()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-002", DateTime.UtcNow, Guid.NewGuid());

        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId,
            Guid.NewGuid(), 100, 25.0m);
        scio.AddItem(item);

        Assert.Single(scio.Items);
        Assert.Equal(2500m, scio.NetTotal);
    }

    [Fact]
    public void SubcontractingInwardOrder_Submit_RequiresItems()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Guid.NewGuid(),
            "SCIO-003", DateTime.UtcNow, Guid.NewGuid());

        Assert.Throws<Volo.Abp.BusinessException>(() => scio.Submit());
    }

    [Fact]
    public void SubcontractingInwardOrder_Submit_ChangesStatus()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-004", DateTime.UtcNow, Guid.NewGuid());
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 50, 10m));

        scio.Submit();

        Assert.Equal(SubcontractingInwardOrderStatus.Open, scio.Status);
    }

    [Fact]
    public void SubcontractingInwardOrder_UpdateReceivedStatus_Partial()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-005", DateTime.UtcNow, Guid.NewGuid());
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 100, 10m);
        scio.AddItem(item);
        scio.Submit();

        item.ReceivedQty = 50;
        scio.UpdateReceivedStatus();

        Assert.Equal(SubcontractingInwardOrderStatus.PartiallyReceived, scio.Status);
        Assert.Equal(50m, scio.PerReceived);
    }

    [Fact]
    public void SubcontractingInwardOrder_UpdateReceivedStatus_Complete()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-006", DateTime.UtcNow, Guid.NewGuid());
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 100, 10m);
        scio.AddItem(item);
        scio.Submit();

        item.ReceivedQty = 100;
        scio.UpdateReceivedStatus();

        Assert.Equal(SubcontractingInwardOrderStatus.Completed, scio.Status);
        Assert.Equal(100m, scio.PerReceived);
    }

    [Fact]
    public void SubcontractingInwardOrder_Close_FromOpen()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-007", DateTime.UtcNow, Guid.NewGuid());
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 50, 10m));
        scio.Submit();

        scio.Close();

        Assert.Equal(SubcontractingInwardOrderStatus.Closed, scio.Status);
    }

    [Fact]
    public void SubcontractingInwardOrder_Close_BlockedFromDraft()
    {
        var scio = new SubcontractingInwardOrder(Guid.NewGuid(), Guid.NewGuid(),
            "SCIO-008", DateTime.UtcNow, Guid.NewGuid());

        Assert.Throws<Volo.Abp.BusinessException>(() => scio.Close());
    }

    [Fact]
    public void SubcontractingInwardOrderItem_PendingReceiptQty()
    {
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 100, 15m);
        item.ReceivedQty = 40;

        Assert.Equal(60m, item.PendingReceiptQty);
        Assert.Equal(1500m, item.Amount);
    }

    #endregion
}
