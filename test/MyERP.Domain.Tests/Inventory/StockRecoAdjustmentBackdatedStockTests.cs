using System;
using System.Collections.Generic;
using System.Reflection;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for ERPNext PR #59269 (commit 719b53f2ad):
/// An adjustment entry restates value, so backdated stock posted before it must survive
/// across both FIFO and Moving Average revaluation paths.
/// Also verifies FifoValuation.RestateRate and stranded value write-offs.
/// </summary>
public class StockRecoAdjustmentBackdatedStockTests
{
    [Fact]
    public void FifoValuation_RestateRate_UpdatesRateWithoutChangingQty()
    {
        var queue = new FifoValuation();
        queue.AddStock(5, 100);
        queue.AddStock(5, 50);

        queue.TotalQty.ShouldBe(10);
        queue.TotalValue.ShouldBe(750); // 5*100 + 5*50

        queue.RestateRate(80);

        queue.TotalQty.ShouldBe(10);
        queue.TotalValue.ShouldBe(800); // 10*80
        queue.ValuationRate.ShouldBe(80);
    }

    [Fact]
    public void AdjustmentEntry_DoesNotZeroOutBackdatedStock_Fifo()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 9, 1);

        // Day -7: Backdated receipt inserted between Day -10 and Day -5
        var backdatedReceipt = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            baseDate.AddDays(-7), quantityChange: 4, valuationRate: 50,
            balanceQuantity: 4, balanceValue: 200)
        {
            VoucherType = "StockEntry",
        };

        // Day -5: Stock Reconciliation adjustment entry (was stranded value write-off)
        var recoAdjustment = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            baseDate.AddDays(-5), quantityChange: 0, valuationRate: 0,
            balanceQuantity: 0, balanceValue: 0)
        {
            VoucherType = "StockReconciliation",
            IsAdjustmentEntry = true,
        };

        var entries = new List<StockLedgerEntry> { backdatedReceipt, recoAdjustment };

        // Revaluate entries through FIFO replay
        InvokeRevaluateFifoLifo(entries, priorEntry: null, isLifo: false);

        // Assert backdated stock survives through the adjustment entry
        recoAdjustment.BalanceQuantity.ShouldBe(4);
        recoAdjustment.BalanceValue.ShouldBe(200);
        recoAdjustment.ValuationRate.ShouldBe(50);
        recoAdjustment.StockValueDifference.ShouldBe(0);

        // Queue is preserved with 4 units @ 50
        var queue = FifoValuation.Deserialize(recoAdjustment.StockQueue);
        queue.TotalQty.ShouldBe(4);
        queue.TotalValue.ShouldBe(200);
    }

    [Fact]
    public void AdjustmentEntry_DoesNotZeroOutBackdatedStock_MovingAverage()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 9, 1);

        // Day -7: Backdated receipt inserted between Day -10 and Day -5
        var backdatedReceipt = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            baseDate.AddDays(-7), quantityChange: 4, valuationRate: 50,
            balanceQuantity: 4, balanceValue: 200)
        {
            VoucherType = "StockEntry",
        };

        // Day -5: Stock Reconciliation adjustment entry (was stranded value write-off)
        var recoAdjustment = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            baseDate.AddDays(-5), quantityChange: 0, valuationRate: 0,
            balanceQuantity: 0, balanceValue: 0)
        {
            VoucherType = "StockReconciliation",
            IsAdjustmentEntry = true,
        };

        var entries = new List<StockLedgerEntry> { backdatedReceipt, recoAdjustment };

        // Revaluate entries through Moving Average replay
        InvokeRevaluateMovingAverage(entries, priorEntry: null);

        // Assert backdated stock survives through the adjustment entry in Moving Average
        recoAdjustment.BalanceQuantity.ShouldBe(4);
        recoAdjustment.BalanceValue.ShouldBe(200);
        recoAdjustment.ValuationRate.ShouldBe(50);
        recoAdjustment.StockValueDifference.ShouldBe(0);
    }

    [Fact]
    public void AdjustmentEntry_ClearsStrandedValueAtZeroQty()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var baseDate = new DateTime(2026, 9, 1);

        // Prior entry has 0 qty but 500 stranded value
        var prior = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            baseDate.AddDays(-1), quantityChange: -10, valuationRate: 100,
            balanceQuantity: 0, balanceValue: 500)
        {
            StockQueue = new FifoValuation().Serialize()
        };

        // Adjustment entry to write off the stranded value
        var recoAdjustment = new StockLedgerEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            baseDate, quantityChange: 0, valuationRate: 0,
            balanceQuantity: 0, balanceValue: 0)
        {
            VoucherType = "StockReconciliation",
            IsAdjustmentEntry = true,
        };

        var entries = new List<StockLedgerEntry> { recoAdjustment };

        // FIFO replay
        InvokeRevaluateFifoLifo(entries, priorEntry: prior, isLifo: false);

        recoAdjustment.BalanceQuantity.ShouldBe(0);
        recoAdjustment.BalanceValue.ShouldBe(0);
        recoAdjustment.StockValueDifference.ShouldBe(-500);

        // MA replay
        recoAdjustment.BalanceValue = 0;
        recoAdjustment.BalanceQuantity = 0;
        InvokeRevaluateMovingAverage(entries, priorEntry: prior);

        recoAdjustment.BalanceQuantity.ShouldBe(0);
        recoAdjustment.BalanceValue.ShouldBe(0);
        recoAdjustment.StockValueDifference.ShouldBe(-500);
    }

    private static void InvokeRevaluateFifoLifo(List<StockLedgerEntry> entries, StockLedgerEntry? priorEntry, bool isLifo)
    {
        var method = typeof(StockValuationService).GetMethod(
            "RevaluateFifoLifo",
            BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object?[] { entries, priorEntry, isLifo });
    }

    private static void InvokeRevaluateMovingAverage(List<StockLedgerEntry> entries, StockLedgerEntry? priorEntry)
    {
        var method = typeof(StockValuationService).GetMethod(
            "RevaluateMovingAverage",
            BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object?[] { entries, priorEntry });
    }
}
