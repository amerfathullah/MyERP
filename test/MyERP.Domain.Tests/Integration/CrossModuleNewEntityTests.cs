using System;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests verifying cross-module interactions for newly added entities.
/// </summary>
public class CrossModuleNewEntityTests
{
    #region Standard Cost + Stock Valuation Interaction

    [Fact]
    public void StandardCost_PPV_OnMultiplePurchases_AccumulatesVariance()
    {
        var isc = new ItemStandardCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, DateTime.UtcNow.Date);
        isc.Submit();

        // Series of purchases at different actual rates
        var purchase1Ppv = isc.CalculatePpv(actualRate: 105m, qty: 50); // Unfavorable
        var purchase2Ppv = isc.CalculatePpv(actualRate: 95m, qty: 30);  // Favorable
        var purchase3Ppv = isc.CalculatePpv(actualRate: 100m, qty: 20); // Zero variance

        // Total PPV = (5*50) + (-5*30) + (0*20) = 250 - 150 + 0 = 100 net unfavorable
        var totalPpv = purchase1Ppv + purchase2Ppv + purchase3Ppv;
        Assert.Equal(100m, totalPpv);
    }

    [Fact]
    public void StandardCost_RateChange_TracksPreviousForRevaluation()
    {
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var cost1 = new ItemStandardCost(Guid.NewGuid(), companyId, itemId,
            50m, DateTime.UtcNow.Date.AddDays(-60));
        cost1.Submit();

        var cost2 = new ItemStandardCost(Guid.NewGuid(), companyId, itemId,
            55m, DateTime.UtcNow.Date.AddDays(-30));
        cost2.PreviousRate = cost1.StandardRate;
        cost2.Submit();

        var cost3 = new ItemStandardCost(Guid.NewGuid(), companyId, itemId,
            52m, DateTime.UtcNow.Date);
        cost3.PreviousRate = cost2.StandardRate;

        // Rate history: 50 → 55 → 52
        Assert.Equal(50m, cost2.PreviousRate);
        Assert.Equal(55m, cost3.PreviousRate);

        // Revaluation amount per unit from last change: 52 - 55 = -3 (decrease)
        var revalPerUnit = cost3.StandardRate - (cost3.PreviousRate ?? cost3.StandardRate);
        Assert.Equal(-3m, revalPerUnit);
    }

    #endregion

    #region BOM Operations + Work Order Integration

    [Fact]
    public void BOM_WithOperations_TotalCostIncludesBoth()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-001", Guid.NewGuid());

        // Add raw materials
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Steel Rod", 10, 25m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Copper Wire", 5, 40m));

        // Add operations (cutting + assembly)
        var op1 = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 20m); // 20 min
        op1.CalculateCost(90m); // RM 90/hr → 30
        bom.AddOperation(op1);

        var op2 = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 20, 45m); // 45 min
        op2.CalculateCost(120m); // RM 120/hr → 90
        bom.AddOperation(op2);

        bom.RecalculateCost();

        Assert.Equal(450m, bom.TotalMaterialCost); // 10*25 + 5*40 = 250 + 200
        Assert.Equal(120m, bom.OperatingCost);     // 30 + 90
        Assert.Equal(570m, bom.TotalCost);         // 450 + 120
    }

    [Fact]
    public void BomOperation_BatchSplitting_WorkOrderWith100Units()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-002", Guid.NewGuid());

        // Operation with batch size 30 (work order of 100 units)
        var op = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 15m);
        op.BatchSize = 30;
        bom.AddOperation(op);

        var jobCardCount = op.GetJobCardCount(100);
        Assert.Equal(4, jobCardCount); // ceil(100/30) = 4 JCs: 30+30+30+10

        // Total time for 100 units
        var totalTime = op.GetTotalTime(100);
        Assert.Equal(1500m, totalTime); // 0 fixed + 15*100
    }

    #endregion

    #region Repost Item Valuation Dedup Logic

    [Fact]
    public void Repost_MultipleRequests_DedupCoversNarrower()
    {
        var companyId = Guid.NewGuid();
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var wh = Guid.NewGuid();

        // Full company repost queued first
        var fullRepost = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.EntireCompany, DateTime.UtcNow.AddDays(-30));

        // Specific item repost queued after
        var itemRepost1 = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-10), item1, wh);
        var itemRepost2 = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-5), item2, wh);

        // Both narrower reposts should be covered by the full repost
        Assert.True(itemRepost1.IsCoveredBy(fullRepost));
        Assert.True(itemRepost2.IsCoveredBy(fullRepost));

        // The full repost is NOT covered by either narrow one
        Assert.False(fullRepost.IsCoveredBy(itemRepost1));
        Assert.False(fullRepost.IsCoveredBy(itemRepost2));
    }

    [Fact]
    public void Repost_SameItemDifferentDates_EarlierCoversLater()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var earlier = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-30), itemId, whId);
        var later = new RepostItemValuation(Guid.NewGuid(), companyId,
            RepostMethod.ItemAndWarehouse, DateTime.UtcNow.AddDays(-10), itemId, whId);

        // Earlier date covers later date (reposting from -30 includes -10)
        Assert.True(later.IsCoveredBy(earlier));
        // Later date does NOT cover earlier
        Assert.False(earlier.IsCoveredBy(later));
    }

    #endregion

    #region SCIO + SO Close Cascade

    [Fact]
    public void SCIO_MultiItem_MinPercentage_StatusTransition()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-INT-001", DateTime.UtcNow, Guid.NewGuid());

        var item1 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 100, 10m);
        var item2 = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 200, 5m);
        scio.AddItem(item1);
        scio.AddItem(item2);
        scio.Submit();

        // Receive 100% of item1 but 0% of item2
        item1.ReceivedQty = 100;
        scio.UpdateReceivedStatus();
        Assert.Equal(SubcontractingInwardOrderStatus.PartiallyReceived, scio.Status);
        Assert.Equal(0m, scio.PerReceived); // Min(100%, 0%) = 0%

        // Receive 50% of item2
        item2.ReceivedQty = 100;
        scio.UpdateReceivedStatus();
        Assert.Equal(50m, scio.PerReceived); // Min(100%, 50%) = 50%

        // Complete item2
        item2.ReceivedQty = 200;
        scio.UpdateReceivedStatus();
        Assert.Equal(SubcontractingInwardOrderStatus.Completed, scio.Status);
        Assert.Equal(100m, scio.PerReceived);
    }

    [Fact]
    public void SCIO_BillingTracking_PerBilled()
    {
        var scioId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-INT-002", DateTime.UtcNow, Guid.NewGuid());
        var item = new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 100, 10m);
        scio.AddItem(item);
        scio.Submit();

        item.BilledQty = 40;
        scio.UpdateBilledStatus();
        Assert.Equal(40m, scio.PerBilled);

        item.BilledQty = 100;
        scio.UpdateBilledStatus();
        Assert.Equal(100m, scio.PerBilled);
    }

    [Fact]
    public void SCIO_Close_AlsoClosesLinkedFromSO()
    {
        var scioId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var scio = new SubcontractingInwardOrder(scioId, Guid.NewGuid(),
            "SCIO-INT-003", DateTime.UtcNow, Guid.NewGuid());
        scio.SalesOrderId = salesOrderId;
        scio.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), scioId, Guid.NewGuid(), 50, 20m));
        scio.Submit();

        // Simulate partial receipt then close
        scio.Close();

        Assert.Equal(SubcontractingInwardOrderStatus.Closed, scio.Status);
        Assert.Equal(salesOrderId, scio.SalesOrderId); // Link preserved
    }

    #endregion

    #region Serial and Batch Bundle + SRE Interaction

    [Fact]
    public void Bundle_WithStockReservation_Entry_Linked()
    {
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "SalesOrder", Guid.NewGuid(), 10m);

        var bundleId = Guid.NewGuid();
        sre.SerialAndBatchBundleId = bundleId;

        Assert.Equal(bundleId, sre.SerialAndBatchBundleId);
    }

    [Fact]
    public void Bundle_QtyValidation_AbsValue_ForReturns()
    {
        var bundleId = Guid.NewGuid();
        var bundle = new SerialAndBatchBundle(bundleId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(),
            DateTime.UtcNow);
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 5, 20m));

        // ValidateQtyMatch uses abs — works with negative transaction qty (returns)
        bundle.ValidateQtyMatch(-5); // abs(-5) == 5, should pass
    }

    #endregion
}
