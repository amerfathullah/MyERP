using System;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

public class NewEntityFlowTests
{
    #region SerialAndBatchBundle Flow

    [Fact]
    public void Bundle_InwardReceipt_MultiSerial_Flow()
    {
        var bundleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), itemId, warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow);
        bundle.HasSerialNo = true;

        // Receive 3 serial numbers at different rates
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 100m, serialNo: "SN-001"));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 110m, serialNo: "SN-002"));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 105m, serialNo: "SN-003"));

        Assert.Equal(3, bundle.TotalQty);
        Assert.Equal(315m, bundle.TotalAmount);
        Assert.Equal(105m, bundle.AvgRate); // 315/3
        bundle.ValidateQtyMatch(3); // Should not throw
    }

    [Fact]
    public void Bundle_OutwardDelivery_BatchAllocation_Flow()
    {
        var bundleId = Guid.NewGuid();
        var batchId1 = Guid.NewGuid();
        var batchId2 = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.HasBatchNo = true;

        // Deliver from two batches
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 7, 50m, batchId: batchId1));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 3, 52m, batchId: batchId2));

        Assert.Equal(10, bundle.TotalQty);
        Assert.Equal(506m, bundle.TotalAmount); // 7*50 + 3*52
        Assert.Equal(2, bundle.Entries.Count);
    }

    [Fact]
    public void Bundle_CancelAndReuse_PreventsDuplicates()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 5, 10m, serialNo: "SN-X"));

        bundle.Cancel();

        // Cancelled bundle blocks further additions
        Assert.True(bundle.IsCancelled);
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 1, 10m)));
    }

    #endregion

    #region ItemStandardCost Flow

    [Fact]
    public void StandardCost_CreateSubmitCancel_Lifecycle()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, DateTime.UtcNow.Date.AddDays(-10));
        Assert.Equal(DocumentStatus.Draft, isc.Status);

        isc.Submit();
        Assert.Equal(DocumentStatus.Submitted, isc.Status);

        isc.Cancel(hasStockActivityOnOrAfter: false);
        Assert.Equal(DocumentStatus.Cancelled, isc.Status);
    }

    [Fact]
    public void StandardCost_PPV_Calculation_AccurateForMultipleTransactions()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            50m, DateTime.UtcNow.Date);

        // Purchase at various actual rates
        var ppv1 = isc.CalculatePpv(actualRate: 55m, qty: 100); // Unfavorable: actual > standard
        var ppv2 = isc.CalculatePpv(actualRate: 48m, qty: 200); // Favorable: actual < standard
        var ppv3 = isc.CalculatePpv(actualRate: 50m, qty: 50);  // No variance

        Assert.Equal(500m, ppv1);   // (55-50)*100
        Assert.Equal(-400m, ppv2);  // (48-50)*200
        Assert.Equal(0m, ppv3);     // (50-50)*50
    }

    [Fact]
    public void StandardCost_PreviousRate_Tracks_RateHistory()
    {
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        // First standard cost
        var isc1 = new ItemStandardCost(Guid.NewGuid(), companyId, itemId,
            50m, DateTime.UtcNow.Date.AddDays(-30));
        isc1.Submit();
        Assert.Null(isc1.PreviousRate);

        // Second standard cost (rate change)
        var isc2 = new ItemStandardCost(Guid.NewGuid(), companyId, itemId,
            55m, DateTime.UtcNow.Date.AddDays(-1));
        isc2.PreviousRate = isc1.StandardRate; // Set by AppService from query
        isc2.Submit();

        Assert.Equal(50m, isc2.PreviousRate);
        Assert.Equal(55m, isc2.StandardRate);
    }

    #endregion

    #region RepostItemValuation Flow

    [Fact]
    public void Repost_FullLifecycle_QueuedToCompleted()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var riv = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-5), itemId, whId);

        Assert.Equal(RepostStatus.Queued, riv.Status);

        riv.StartProcessing();
        Assert.Equal(RepostStatus.InProgress, riv.Status);

        riv.Complete(150);
        Assert.Equal(RepostStatus.Completed, riv.Status);
        Assert.Equal(150, riv.TotalAffectedEntries);
    }

    [Fact]
    public void Repost_Dedup_NarrowerCoveredByBroader()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        // Broad repost (entire company, earlier date)
        var broad = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.EntireCompany, DateTime.UtcNow.AddDays(-10));

        // Narrow repost (specific item+warehouse, later date)
        var narrow = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-5), itemId, whId);

        Assert.True(narrow.IsCoveredBy(broad));
        Assert.False(broad.IsCoveredBy(narrow)); // Broad is NOT covered by narrow
    }

    [Fact]
    public void Repost_Fail_CapturesErrorWithRetryOption()
    {
        var riv = new RepostItemValuation(Guid.NewGuid(), Guid.NewGuid(),
            RepostMethod.ItemWise, DateTime.UtcNow, Guid.NewGuid());
        riv.StartProcessing();
        riv.CurrentIndex = 45;

        riv.Fail("Connection timeout after processing 45 entries");

        Assert.Equal(RepostStatus.Failed, riv.Status);
        Assert.Equal(45, riv.CurrentIndex); // Progress preserved for resume
        Assert.Contains("timeout", riv.ErrorLog);
    }

    #endregion

    #region SubcontractingInwardOrder Flow

    [Fact]
    public void SCIO_FullLifecycle_DraftToCompleted()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-TEST-001", DateTime.UtcNow, Guid.NewGuid());

        var item1 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 50, 20m);
        var item2 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 30, 35m);
        scio.AddItem(item1);
        scio.AddItem(item2);

        Assert.Equal(2050m, scio.NetTotal); // 50*20 + 30*35

        scio.Submit();
        Assert.Equal(SubcontractingInwardOrderStatus.Open, scio.Status);

        // Partial receipt of first item
        item1.ReceivedQty = 25;
        scio.UpdateReceivedStatus();
        Assert.Equal(SubcontractingInwardOrderStatus.PartiallyReceived, scio.Status);
        Assert.Equal(0m, scio.PerReceived); // Min(50%, 0%) = 0% (item2 not received)

        // Full receipt of second item
        item2.ReceivedQty = 30;
        scio.UpdateReceivedStatus();
        Assert.Equal(50m, scio.PerReceived); // Min(50%, 100%) = 50%

        // Full receipt of first item
        item1.ReceivedQty = 50;
        scio.UpdateReceivedStatus();
        Assert.Equal(SubcontractingInwardOrderStatus.Completed, scio.Status);
        Assert.Equal(100m, scio.PerReceived);
    }

    [Fact]
    public void SCIO_AddItemBlockedAfterSubmit()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-002", DateTime.UtcNow, Guid.NewGuid());
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 10, 5m));
        scio.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 5, 3m)));
    }

    [Fact]
    public void SCIO_CancelFromOpen()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-003", DateTime.UtcNow, Guid.NewGuid());
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 10, 5m));
        scio.Submit();

        scio.Cancel();
        Assert.Equal(SubcontractingInwardOrderStatus.Cancelled, scio.Status);
    }

    #endregion

    #region BomOperation Flow

    [Fact]
    public void BomOperation_CostCalculation_FromWorkstationRate()
    {
        var bomOp = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10, timeInMins: 30m, Guid.NewGuid());

        bomOp.CalculateCost(workstationHourRate: 120m); // RM 120/hr

        Assert.Equal(60m, bomOp.OperatingCost); // 30/60 * 120 = 60
    }

    [Fact]
    public void BomOperation_TotalTime_IncludesFixedTime()
    {
        var bomOp = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10, timeInMins: 5m);
        bomOp.FixedTime = 15m; // 15 min setup

        var totalFor10 = bomOp.GetTotalTime(10);   // 15 + 5*10 = 65
        var totalFor1 = bomOp.GetTotalTime(1);     // 15 + 5*1 = 20

        Assert.Equal(65m, totalFor10);
        Assert.Equal(20m, totalFor1);
    }

    [Fact]
    public void BomOperation_JobCardCount_BatchSplitting()
    {
        var bomOp = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10, timeInMins: 5m);
        bomOp.BatchSize = 25;

        Assert.Equal(4, bomOp.GetJobCardCount(100)); // ceil(100/25) = 4
        Assert.Equal(1, bomOp.GetJobCardCount(20));  // ceil(20/25) = 1
        Assert.Equal(2, bomOp.GetJobCardCount(30));  // ceil(30/25) = 2
    }

    [Fact]
    public void BomOperation_ZeroBatchSize_SingleJobCard()
    {
        var bomOp = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            10, timeInMins: 5m);
        bomOp.BatchSize = 0; // Default — no splitting

        Assert.Equal(1, bomOp.GetJobCardCount(1000)); // Always 1 regardless of qty
    }

    [Fact]
    public void BOM_AddOperations_SequenceEnforcement()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());

        var op1 = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 10, 15m);
        var op2 = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 20, 10m);

        bom.AddOperation(op1);
        bom.AddOperation(op2);

        Assert.Equal(2, bom.Operations.Count);
    }

    [Fact]
    public void BOM_AddOperations_NonMonotonicThrows()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());

        var op1 = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 20, 15m);
        bom.AddOperation(op1);

        // Sequence 10 is less than existing max (20) — should throw
        var op2 = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 10, 10m);
        Assert.Throws<Volo.Abp.BusinessException>(() => bom.AddOperation(op2));
    }

    [Fact]
    public void BOM_RecalculateCost_IncludesOperatingCost()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-003", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Steel", 10, 5m));

        var op = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 30m);
        op.CalculateCost(100m); // 30/60 * 100 = 50
        bom.Operations.Add(op);

        bom.RecalculateCost();

        Assert.Equal(50m, bom.TotalMaterialCost); // 10*5
        Assert.Equal(50m, bom.OperatingCost);     // from operation
        Assert.Equal(100m, bom.TotalCost);        // 50 + 50
    }

    #endregion
}
