using System;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Edge case tests for BomOperation, ItemStandardCost, RepostItemValuation, SCIO, and SerialBatchBundle.
/// </summary>
public class NewEntityEdgeCaseTests
{
    #region BomOperation Scheduling

    [Fact]
    public void BomOperation_ZeroTimeInMins_SetupOnlyOp()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, timeInMins: 0m);
        op.FixedTime = 30m;
        Assert.Equal(30m, op.GetTotalTime(1));
        Assert.Equal(30m, op.GetTotalTime(100));
    }

    [Fact]
    public void BomOperation_ExactBatchMultiple_NoExtraJC()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 5m);
        op.BatchSize = 25;
        Assert.Equal(4, op.GetJobCardCount(100));
        Assert.Equal(1, op.GetJobCardCount(25));
    }

    [Fact]
    public void BomOperation_IsSubcontracted_DefaultFalse()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 5m);
        Assert.False(op.IsSubcontracted);
    }

    [Fact]
    public void BOM_RecalculateCost_EmptyOperations_Zero()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-E-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Wire", 20, 3m));
        bom.RecalculateCost();
        Assert.Equal(60m, bom.TotalMaterialCost);
        Assert.Equal(0m, bom.OperatingCost);
    }

    [Fact]
    public void BOM_AddOperation_EqualSequence_Throws()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-E-002", Guid.NewGuid());
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 20, 10m));
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bom.AddOperation(new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 20, 5m)));
    }

    #endregion

    #region StandardCost Boundaries

    [Fact]
    public void StandardCost_TodayDate_Allowed()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, DateTime.UtcNow.Date);
        Assert.Equal(DateTime.UtcNow.Date, isc.EffectiveDate);
    }

    [Fact]
    public void StandardCost_NegativeRate_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                -5m, DateTime.UtcNow.Date));
    }

    [Fact]
    public void StandardCost_ValidateNoSle_NullPasses()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, DateTime.UtcNow.Date);
        isc.ValidateAgainstLastSle(null); // Should not throw
    }

    [Fact]
    public void StandardCost_DoubleSubmit_Throws()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, DateTime.UtcNow.Date);
        isc.Submit();
        Assert.Throws<Volo.Abp.BusinessException>(() => isc.Submit());
    }

    [Fact]
    public void StandardCost_CancelFromDraft_Throws()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10m, DateTime.UtcNow.Date);
        Assert.Throws<Volo.Abp.BusinessException>(() => isc.Cancel(false));
    }

    [Fact]
    public void StandardCost_FavorablePPV_Negative()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, DateTime.UtcNow.Date);
        Assert.Equal(-150m, isc.CalculatePpv(85m, 10)); // Bought cheaper
    }

    #endregion

    #region RepostItemValuation Boundaries

    [Fact]
    public void Repost_StartFromInProgress_Throws()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid());
        riv.StartProcessing();
        Assert.Throws<Volo.Abp.BusinessException>(() => riv.StartProcessing());
    }

    [Fact]
    public void Repost_DifferentItems_NotCovered()
    {
        var cid = Guid.NewGuid();
        var r1 = new RepostItemValuation(Guid.NewGuid(), cid,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        var r2 = new RepostItemValuation(Guid.NewGuid(), cid,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        Assert.False(r1.IsCoveredBy(r2));
    }

    [Fact]
    public void Repost_DifferentCompany_NeverCovered()
    {
        var r1 = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.EntireCompany, DateTime.UtcNow.AddDays(-30));
        var r2 = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow, Guid.NewGuid());
        Assert.False(r2.IsCoveredBy(r1));
    }

    [Fact]
    public void Repost_MarkSkipped_SetsReason()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemWise, DateTime.UtcNow, Guid.NewGuid());
        riv.MarkSkipped("Covered by XYZ");
        Assert.Equal(RepostStatus.Skipped, riv.Status);
        Assert.Contains("XYZ", riv.ErrorLog);
    }

    #endregion

    #region SCIO Boundaries

    [Fact]
    public void SCIO_ZeroQtyItem_NoOverflow()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(), "SCIO-E-001",
            DateTime.UtcNow, Guid.NewGuid());
        var item1 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 0, 10m);
        var item2 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 100, 10m);
        scio.AddItem(item1);
        scio.AddItem(item2);
        scio.Submit();
        item2.ReceivedQty = 50;
        scio.UpdateReceivedStatus(); // No divide-by-zero
        Assert.Equal(SubcontractingInwardOrderStatus.PartiallyReceived, scio.Status);
    }

    [Fact]
    public void SCIO_CancelFromCompleted_Succeeds()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(), "SCIO-E-002",
            DateTime.UtcNow, Guid.NewGuid());
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 10, 5m);
        scio.AddItem(item);
        scio.Submit();
        item.ReceivedQty = 10;
        scio.UpdateReceivedStatus();
        scio.Cancel();
        Assert.Equal(SubcontractingInwardOrderStatus.Cancelled, scio.Status);
    }

    [Fact]
    public void SCIO_DoubleSubmit_Throws()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(), "SCIO-E-003",
            DateTime.UtcNow, Guid.NewGuid());
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 10, 5m));
        scio.Submit();
        Assert.Throws<Volo.Abp.BusinessException>(() => scio.Submit());
    }

    [Fact]
    public void SCIO_PendingQty_NeverNegative()
    {
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 50, 10m);
        item.ReceivedQty = 60; // Over-receipt
        Assert.Equal(0m, item.PendingReceiptQty);
    }

    #endregion

    #region Bundle Boundaries

    [Fact]
    public void Bundle_Empty_ZeroTotals()
    {
        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(),
            DateTime.UtcNow);
        Assert.Equal(0m, bundle.TotalQty);
        Assert.Equal(0m, bundle.AvgRate);
    }

    [Fact]
    public void Bundle_SingleEntry_AvgEqualsRate()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 99.50m, serialNo: "SN-1"));
        Assert.Equal(99.50m, bundle.AvgRate);
    }

    [Fact]
    public void Bundle_ZeroRate_ValidForTransfer()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 10, 0m, batchId: Guid.NewGuid()));
        Assert.Equal(0m, bundle.TotalAmount);
    }

    [Fact]
    public void Bundle_ValidateQtyMatch_NegativeUsesAbs()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Outward, "SI", Guid.NewGuid(), DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 7, 10m));
        bundle.ValidateQtyMatch(-7); // abs(-7) == 7
        Assert.Throws<Volo.Abp.BusinessException>(() => bundle.ValidateQtyMatch(-5));
    }

    #endregion
}
