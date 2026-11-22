using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Parity tests for ERPNext upstream PRs #67377, #67386, #67451, #67337, #67452,
/// #67470, #67370, #67373, and #67368 (commits 6228e47a34..54978a862d).
/// </summary>
public class UpstreamBatch39ParityTests
{
    [Fact]
    public void PR67377_BuyingPriceFallback_ConvertsToStockUom()
    {
        // Buying price = 120 per Box (12 Units/Box) -> 10 per Unit
        StockValuationService.ConvertBuyingPriceToStockUom(120m, 12m).ShouldBe(10m);
        StockValuationService.ConvertBuyingPriceToStockUom(0m, 12m).ShouldBe(0m);
        StockValuationService.ConvertBuyingPriceToStockUom(50m, 0m).ShouldBe(50m);
    }

    [Fact]
    public void PR67386_StockEntryAdditionalCostSplit_RoundsRunningTotalAndGivesZeroToZeroWeightRow()
    {
        // 100 split equally across 3 rows -> 33.33, 33.34, 33.33 (sums to exact 100.00)
        var shares = StockEntryManager.SplitByWeights(100m, new[] { 1m / 3m, 1m / 3m, 1m / 3m }, precision: 2);
        shares.Sum().ShouldBe(100m);

        // Trailing zero-weight row gets 0, not rounding residue!
        var withZero = StockEntryManager.SplitByWeights(100m, new[] { 1m / 3m, 1m / 3m, 1m / 3m, 0m }, precision: 2);
        withZero[3].ShouldBe(0m);
        withZero.Sum().ShouldBe(100m);

        // DistributeAdditionalCosts on StockEntry gives 0 to zero-value trailing row
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.MaterialReceipt, DateTime.Today)
        {
            TotalAdditionalCosts = 100m
        };
        var whId = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 1m, targetWarehouseId: whId, valuationRate: 10m);
        se.AddItem(Guid.NewGuid(), 1m, targetWarehouseId: whId, valuationRate: 10m);
        se.AddItem(Guid.NewGuid(), 1m, targetWarehouseId: whId, valuationRate: 10m);
        se.AddItem(Guid.NewGuid(), 1m, targetWarehouseId: whId, valuationRate: 0m);

        StockEntryManager.DistributeAdditionalCosts(se);
        se.Items[3].AdditionalCost.ShouldBe(0m);
        se.Items.Sum(i => i.AdditionalCost).ShouldBe(100m);

        // SplitAdditionalCostsByAccount preserves exact sum per expense account
        var acc1 = Guid.NewGuid();
        var acc2 = Guid.NewGuid();
        var row1 = Guid.NewGuid();
        var row2 = Guid.NewGuid();
        var row3 = Guid.NewGuid();
        var byAccount = StockEntryManager.SplitAdditionalCostsByAccount(
            new[] { (row1, 10m), (row2, 10m), (row3, 10m) },
            new[] { (acc1, 10m, 10m), (acc2, 20m, 20m) },
            precision: 2);

        byAccount.Where(x => x.ExpenseAccountId == acc1).Sum(x => x.BaseAmount).ShouldBe(10m);
        byAccount.Where(x => x.ExpenseAccountId == acc2).Sum(x => x.BaseAmount).ShouldBe(20m);
    }

    [Fact]
    public void PR67337_ExplodedBomItems_MergesDuplicateItemAtWeightedAverageRate()
    {
        var itemId = Guid.NewGuid();
        var opA = Guid.NewGuid();
        var opB = Guid.NewGuid();

        // Same item with same null operation at two rates: 2 @ 10 + 3 @ 20 = 80 / 5 = 16
        var merged = BomValidationService.MergeExplodedBomItemsAtWeightedRate(new[]
        {
            (itemId, (Guid?)null, 2m, 10m),
            (itemId, (Guid?)null, 3m, 20m),
            (itemId, (Guid?)opA, 4m, 30m),
            (itemId, (Guid?)opB, 1m, 50m),
        });

        merged.Count.ShouldBe(3);
        merged[0].StockQty.ShouldBe(5m);
        merged[0].Rate.ShouldBe(16m);
        merged[0].Amount.ShouldBe(80m);
        merged[1].OperationId.ShouldBe(opA);
        merged[2].OperationId.ShouldBe(opB);
    }

    [Fact]
    public void PR67452_SalesOrderUnreservedQty_SubtractsBothDeliveredAndReservedStock()
    {
        // Order 10 Boxes (120 stock units), 3 Boxes delivered (36 stock units), 24 stock units still reserved
        // -> remaining unreserved = 120 - 36 - 24 = 60 stock units = 5 Boxes
        StockReservationManager.CalculateDialogUnreservedStockQty(
            stockQty: 120m, stockReservedQty: 24m, dispatchedQtyInOrderUom: 3m, conversionFactor: 12m)
            .ShouldBe(60m);

        StockReservationManager.CalculateSalesOrderUnreservedOrderQty(
            stockQty: 120m, stockReservedQty: 24m, deliveredQtyInOrderUom: 3m, conversionFactor: 12m)
            .ShouldBe(5m);

        // Fully delivered + reserved -> 0
        StockReservationManager.CalculateSalesOrderUnreservedOrderQty(
            stockQty: 120m, stockReservedQty: 60m, deliveredQtyInOrderUom: 5m, conversionFactor: 12m)
            .ShouldBe(0m);
    }

    [Fact]
    public void PR67470_JobCardSlotConsumption_UsesDatetimeComparisonAndBatchSpan()
    {
        var start = new DateTime(2026, 11, 22, 8, 0, 0);
        var slotEnd = new DateTime(2026, 11, 22, 12, 0, 0);

        // Exact 240 mins fits cleanly into 8:00-12:00 with 0 remaining
        var (endExact, remExact) = WorkstationSchedulingService.ConsumeWorkingHourSlot(start, slotEnd, 240m);
        endExact.ShouldBe(slotEnd);
        remExact.ShouldBe(0m);

        // 300 mins consumes 240 mins and leaves 60 mins for next slot
        var (endPartial, remPartial) = WorkstationSchedulingService.ConsumeWorkingHourSlot(start, slotEnd, 300m);
        endPartial.ShouldBe(slotEnd);
        remPartial.ShouldBe(60m);

        // Out-of-order batch cards -> operation spans earliest start to latest end
        var span = WorkstationSchedulingService.ComputeBatchOperationTimeSpan(new[]
        {
            (new DateTime(2026, 11, 22, 10, 0, 0), new DateTime(2026, 11, 22, 14, 0, 0)),
            (new DateTime(2026, 11, 22, 8, 0, 0), new DateTime(2026, 11, 22, 11, 0, 0)),
        });
        span.ShouldNotBeNull();
        span.Value.PlannedStartTime.ShouldBe(new DateTime(2026, 11, 22, 8, 0, 0));
        span.Value.PlannedEndTime.ShouldBe(new DateTime(2026, 11, 22, 14, 0, 0));
    }

    [Fact]
    public void PR67370_SubcontractingReturnedSerialAndBatch_RemovedFromSupplierPoolAndRejectedOnConsume()
    {
        var batchId = Guid.NewGuid();
        var t1 = new DateTime(2026, 11, 20, 9, 0, 0);
        var t2 = new DateTime(2026, 11, 20, 10, 0, 0);

        var (serials, batches) = SubcontractingManager.BuildSupplierMaterialLotsChronological(new[]
        {
            (t2, t2, true, (IEnumerable<string>?)new[] { "SN-1", "SN-2" }, (IReadOnlyDictionary<Guid, decimal>?)new Dictionary<Guid, decimal> { [batchId] = 2m }),
            (t1, t1, false, (IEnumerable<string>?)new[] { "SN-1", "SN-2" }, (IReadOnlyDictionary<Guid, decimal>?)new Dictionary<Guid, decimal> { [batchId] = 5m }),
        });

        serials.ShouldBeEmpty();
        batches[batchId].ShouldBe(3m);

        // Even when availableSerialNosAtSupplier is empty, consuming a returned serial MUST throw!
        var ex = Should.Throw<BusinessException>(() =>
            SubcontractingManager.ValidateConsumedSerialNosAgainstSupplierPool(
                "Subcontracting Order", "SCO-001", serials, new[] { "SN-1" }));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("SN-1");
    }

    [Fact]
    public void PR67373_SubcontractingConsumedAlternativeItem_AttributedToRowSentOnIgnoringNegativeNetReturn()
    {
        var orderId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var row1 = Guid.NewGuid();
        var row2 = Guid.NewGuid();
        var rmOrig = Guid.NewGuid();
        var rmAlt = Guid.NewGuid();

        var allocated = SubcontractingManager.AllocateConsumedQtyOnOrderRows(
            new[]
            {
                (row1, fgItemId, orderId),
                (row2, fgItemId, orderId),
            },
            new Dictionary<Guid, IReadOnlyDictionary<Guid, decimal>>
            {
                // Row 1 had a component return leaving negative net qty for rmOrig, and 6 of rmAlt sent
                [row1] = new Dictionary<Guid, decimal> { [rmOrig] = -2m, [rmAlt] = 6m },
                [row2] = new Dictionary<Guid, decimal> { [rmOrig] = 5m },
            },
            new Dictionary<(Guid, Guid, Guid), decimal>
            {
                [(rmAlt, fgItemId, orderId)] = 4m,
                [(rmOrig, fgItemId, orderId)] = 3m,
            });

        // Row 1 gets 4 consumed from rmAlt (negative rmOrig ignored, not subtracted!)
        allocated[row1].ShouldBe(4m);
        // Row 2 gets 3 consumed from rmOrig
        allocated[row2].ShouldBe(3m);
    }

    [Fact]
    public void PR67368_ReturnOfComponents_RefusesExceedingSupplierQtyAcrossRows()
    {
        var rmId = Guid.NewGuid();
        var qtyAtSupplier = new Dictionary<Guid, decimal> { [rmId] = 5m };

        // Two rows returning 3 + 3 = 6 > 5 allowed -> throws on row 2
        var ex = Should.Throw<BusinessException>(() =>
            SubcontractingRmTransferService.ValidateReturnedQtyWithinSupplierBalance(
                "SCO-009",
                qtyAtSupplier,
                new[]
                {
                    (1, rmId, "RM-STEEL", 3m),
                    (2, rmId, "RM-STEEL", 3m),
                }));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty)
            .ShouldBe("Row #2: Only 5 of Item RM-STEEL can be returned against Subcontracting Order SCO-009");

        // Returning exactly 5 passes
        Should.NotThrow(() =>
            SubcontractingRmTransferService.ValidateReturnedQtyWithinSupplierBalance(
                "SCO-009",
                qtyAtSupplier,
                new[] { (1, rmId, "RM-STEEL", 5m) }));
    }
}
