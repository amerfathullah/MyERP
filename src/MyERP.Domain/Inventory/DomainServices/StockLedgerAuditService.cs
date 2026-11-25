using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Weekly Stock Ledger Audit engine per ERPNext PR #63403 (<c>stock_ledger_audit.py</c> and
/// <c>stock_ledger_invariant_check.py</c>):
/// - Identifies <c>(ItemId, WarehouseId)</c> pairs with SLEs in the current fiscal year, skipping pairs
///   that already have a pending (<c>Queued</c> / <c>InProgress</c>) stock repost (either
///   <c>Item and Warehouse</c> or non-accounting-only <c>Transaction</c> repost).
/// - Checks SLE invariants from the opening balance before the audit window and returns the first
///   incorrect SLE (qty balance, value balance, valuation rate tolerance, and optional FIFO queue checks).
/// - Deduplicates against existing scheduler-created reposts at the exact <c>(ItemId, WarehouseId, PostingDateTime)</c>.
/// </summary>
public class StockLedgerAuditService : DomainService
{
    /// <summary>
    /// Computes the set of <c>(ItemId, WarehouseId)</c> pairs currently covered by pending stock reposts
    /// (excluding accounting-only transaction reposts) per ERPNext PR #63403
    /// (<c>StockLedgerAudit.get_item_warehouses_in_repost</c>).
    /// </summary>
    public static HashSet<(Guid ItemId, Guid WarehouseId)> GetItemWarehousesInPendingRepost(
        IEnumerable<PendingRepostSnapshot>? pendingReposts)
    {
        var result = new HashSet<(Guid ItemId, Guid WarehouseId)>();
        if (pendingReposts == null)
        {
            return result;
        }

        foreach (var r in pendingReposts)
        {
            if (!r.IsSubmitted || !r.IsQueuedOrInProgress)
                continue;

            if (r.IsTransactionBased && r.RepostOnlyAccountingLedgers)
                continue;

            result.Add((r.ItemId, r.WarehouseId));
        }

        return result;
    }

    /// <summary>
    /// Evaluates whether a single SLE's invariant differences pass within <paramref name="floatPrecision"/>
    /// and <paramref name="currencyPrecision"/> per ERPNext <c>is_sle_has_correct_data</c>.
    /// </summary>
    public static bool IsSleCorrect(
        decimal differenceInQty,
        decimal diffValueDiff,
        decimal? valuationDiff,
        decimal qtyAfterTransaction,
        decimal fifoQtyDiff = 0m,
        decimal fifoValueDiff = 0m,
        decimal fifoDifferenceDiff = 0m,
        int floatPrecision = 3,
        int currencyPrecision = 2)
    {
        var valueTolerance = (decimal)Math.Pow(10, -currencyPrecision);
        var valDrift = valuationDiff.HasValue ? Math.Abs(valuationDiff.Value * qtyAfterTransaction) : 0m;

        return Math.Round(differenceInQty, floatPrecision, MidpointRounding.AwayFromZero) == 0m
            && Math.Round(diffValueDiff, currencyPrecision, MidpointRounding.AwayFromZero) == 0m
            && Math.Round(fifoQtyDiff, floatPrecision, MidpointRounding.AwayFromZero) == 0m
            && Math.Round(fifoValueDiff, currencyPrecision, MidpointRounding.AwayFromZero) == 0m
            && Math.Round(fifoDifferenceDiff, currencyPrecision, MidpointRounding.AwayFromZero) == 0m
            && valDrift < valueTolerance;
    }

    /// <summary>
    /// Scans SLEs ordered by <c>(PostingDateTime, CreationTime)</c> from the opening balance and returns
    /// the first entry that violates stock ledger invariants, or <c>null</c> when all entries are consistent.
    /// Per ERPNext PR #63403 (<c>StockLedgerAudit.get_first_incorrect_sle</c>).
    /// </summary>
    public static StockLedgerAuditEntry? FindFirstIncorrectSle(
        IReadOnlyList<StockLedgerAuditEntry>? orderedEntries,
        decimal openingQty,
        decimal openingStockValue,
        bool isFifoValuation = false,
        int floatPrecision = 3,
        int currencyPrecision = 2)
    {
        if (orderedEntries == null || orderedEntries.Count == 0)
        {
            return null;
        }

        var balanceQty = openingQty;
        var balanceStockValue = openingStockValue;
        decimal? prevFifoStockValue = null;

        foreach (var sle in orderedEntries)
        {
            balanceQty += sle.ActualQty;
            balanceStockValue += sle.StockValueDifference;

            // Non-batch, non-bundle, non-adjustment Stock Reconciliation resets balance qty directly
            if (sle.IsStockReconciliation
                && !sle.HasBatchNo
                && !sle.HasSerialAndBatchBundle
                && !sle.IsAdjustmentEntry)
            {
                balanceQty = sle.ReconciliationQty ?? sle.QtyAfterTransaction;
            }

            var balanceValueByQty = sle.QtyAfterTransaction != 0m
                ? sle.StockValue / sle.QtyAfterTransaction
                : (decimal?)null;

            var differenceInQty = sle.QtyAfterTransaction - balanceQty;
            var valuationDiff = balanceValueByQty.HasValue
                ? sle.ValuationRate - balanceValueByQty.Value
                : (decimal?)null;
            var diffValueDiff = balanceStockValue - sle.StockValue;

            decimal fifoQtyDiff = 0m;
            decimal fifoValueDiff = 0m;
            decimal fifoDifferenceDiff = 0m;
            decimal? currentFifoStockValue = null;

            var maintainsFifo = isFifoValuation
                && !sle.HasSerialAndBatchBundle
                && !sle.HasSerialNo
                && !(sle.HasBatchNo && sle.UseBatchwiseValuation);

            if (maintainsFifo && sle.FifoQueue != null)
            {
                decimal fifoQty = 0m;
                decimal fifoValue = 0m;
                foreach (var (lotQty, lotRate) in sle.FifoQueue)
                {
                    fifoQty += lotQty;
                    fifoValue += lotQty * lotRate;
                }

                currentFifoStockValue = fifoValue;
                fifoQtyDiff = sle.QtyAfterTransaction - fifoQty;
                fifoValueDiff = sle.StockValue - fifoValue;

                if (prevFifoStockValue.HasValue)
                {
                    var fifoStockDiff = Math.Round(fifoValue, currencyPrecision, MidpointRounding.AwayFromZero)
                        - Math.Round(prevFifoStockValue.Value, currencyPrecision, MidpointRounding.AwayFromZero);
                    fifoDifferenceDiff = fifoStockDiff - sle.StockValueDifference;
                }
            }

            prevFifoStockValue = currentFifoStockValue;

            if (!IsSleCorrect(
                differenceInQty,
                diffValueDiff,
                valuationDiff,
                sle.QtyAfterTransaction,
                fifoQtyDiff,
                fifoValueDiff,
                fifoDifferenceDiff,
                floatPrecision,
                currencyPrecision))
            {
                return sle;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns true if the weekly audit should queue a new scheduler-created <c>RepostItemValuation</c>
    /// for <paramref name="incorrectSle"/> (not already covered by an existing pending repost or prior
    /// scheduler repost at the same <c>(ItemId, WarehouseId, PostingDateTime)</c>).
    /// </summary>
    public static bool ShouldCreateSchedulerRepost(
        StockLedgerAuditEntry? incorrectSle,
        IReadOnlySet<(Guid ItemId, Guid WarehouseId)>? itemWarehousesInPendingRepost,
        IReadOnlySet<(Guid ItemId, Guid WarehouseId, DateTime PostingDateTime)>? existingSchedulerReposts)
    {
        if (incorrectSle == null)
            return false;

        if (itemWarehousesInPendingRepost != null
            && itemWarehousesInPendingRepost.Contains((incorrectSle.ItemId, incorrectSle.WarehouseId)))
        {
            return false;
        }

        if (existingSchedulerReposts != null
            && existingSchedulerReposts.Contains((incorrectSle.ItemId, incorrectSle.WarehouseId, incorrectSle.PostingDateTime)))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether any active Stock Ledger Entry for another voucher comes strictly after
    /// the target voucher's posting timestamp and earliest SLE creation time (including cancelled SLEs)
    /// per ERPNext PR #68732 (<c>stock_controller.py: future_sle_exists</c> / <c>get_voucher_sle_creation</c>):
    /// - Entries with <c>PostingDateTime &gt; targetPostingDateTime</c> are future.
    /// - Entries at the exact same <c>PostingDateTime</c> are future ONLY when
    ///   <c>CreationTime &gt; voucherFirstSleCreationTime</c>.
    /// </summary>
    public static bool HasFutureStockLedgerEntry(
        Guid itemId,
        Guid warehouseId,
        string voucherType,
        Guid voucherId,
        DateTime targetPostingDateTime,
        IEnumerable<FutureSleCandidate>? ledgerEntries)
    {
        if (ledgerEntries == null)
            return false;

        var entries = ledgerEntries.ToList();
        var voucherFirstCreation = entries
            .Where(e => string.Equals(e.VoucherType, voucherType, StringComparison.OrdinalIgnoreCase)
                && e.VoucherId == voucherId)
            .Select(e => (DateTime?)e.CreationTime)
            .OrderBy(dt => dt)
            .FirstOrDefault();

        return entries.Any(e =>
            !e.IsCancelled
            && e.ItemId == itemId
            && e.WarehouseId == warehouseId
            && e.VoucherId != voucherId
            && (e.PostingDateTime > targetPostingDateTime
                || (voucherFirstCreation.HasValue
                    && e.PostingDateTime == targetPostingDateTime
                    && e.CreationTime > voucherFirstCreation.Value)));
    }

    /// <summary>
    /// Determines whether an active Stock Valuation Adjustment Entry exists after the target SLE
    /// per ERPNext PR #67378 (<c>valuation_adjustment.py: validate_no_later_adjustment_entry</c> / <c>get_later_than</c>):
    /// - Any active Adjustment Entry with <c>PostingDateTime &gt; targetPostingDateTime</c> blocks the operation.
    /// - On cancellation (<paramref name="isCancellation"/> = true), an active Adjustment Entry at the EXACT same
    ///   <c>PostingDateTime</c> with <c>CreationTime &gt; originalEntryEarliestCreationTime</c> also blocks cancellation.
    /// </summary>
    public static bool HasLaterAdjustmentEntry(
        Guid itemId,
        Guid warehouseId,
        Guid voucherId,
        DateTime targetPostingDateTime,
        bool isCancellation,
        DateTime? originalEntryEarliestCreationTime,
        IEnumerable<AdjustmentSleCandidate>? adjustmentEntries)
    {
        if (adjustmentEntries == null)
        {
            return false;
        }

        return adjustmentEntries.Any(e =>
            !e.IsCancelled
            && e.IsAdjustmentEntry
            && e.ItemId == itemId
            && e.WarehouseId == warehouseId
            && e.VoucherId != voucherId
            && (e.PostingDateTime > targetPostingDateTime
                || (isCancellation
                    && originalEntryEarliestCreationTime.HasValue
                    && e.PostingDateTime == targetPostingDateTime
                    && e.CreationTime > originalEntryEarliestCreationTime.Value)));
    }

    /// <summary>
    /// Computes the index in <paramref name="orderedRows"/> where the Valuation Rate Breakdown window
    /// should restart (the last non-adjustment Stock Reconciliation or zero-qty crossing)
    /// per ERPNext PR #66536 (<c>valuation_rate_breakdown.py: get_restart_index</c>).
    /// </summary>
    public static int FindValuationBreakdownRestartIndex(
        IReadOnlyList<ValuationBreakdownSleRow>? orderedRows,
        ValuationBreakdownSleRow? previousRow)
    {
        if (orderedRows == null || orderedRows.Count == 0)
        {
            return 0;
        }

        var start = 0;
        for (var i = 0; i < orderedRows.Count; i++)
        {
            var row = orderedRows[i];
            var before = i > 0 ? orderedRows[i - 1] : previousRow;
            var isReconciliation = row.IsStockReconciliation && !row.IsAdjustmentEntry;
            var beforeQtyZero = (before?.QtyAfterTransaction ?? 0m) == 0m;
            if (isReconciliation || beforeQtyZero)
            {
                start = i;
            }
        }

        return start;
    }

    /// <summary>
    /// Builds step-by-step Valuation Rate Breakdown entries with before/after balances, FIFO/LIFO lot
    /// diffs (<c>UsedLots</c>, <c>AddedLots</c>), per-row ledger invariant problems (<c>qty</c>,
    /// <c>value</c>, <c>rate</c>, <c>lots</c>, <c>outgoing_rate</c>), and earliest repost-start entry
    /// per ERPNext PR #66536 (<c>valuation_rate_breakdown.py</c> &amp; <c>ledger_checks.py</c>).
    /// </summary>
    public static ValuationBreakdownResult BuildValuationRateBreakdown(
        IReadOnlyList<ValuationBreakdownSleRow>? orderedRows,
        ValuationBreakdownSleRow? previousRow,
        string valuationMethod,
        bool valuedBySerialOrBatch = false,
        IReadOnlyList<ValuationBreakdownSleRow>? olderRowsNewestFirst = null,
        int floatPrecision = 3,
        decimal roundingGap = 0.01m)
    {
        var steps = new List<ValuationBreakdownStep>();
        if (orderedRows == null || orderedRows.Count == 0)
        {
            return new ValuationBreakdownResult(steps, null);
        }

        var isQueueMethod = !valuedBySerialOrBatch
            && (string.Equals(valuationMethod, "FIFO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(valuationMethod, "LIFO", StringComparison.OrdinalIgnoreCase));
        var isMovingAverage = !valuedBySerialOrBatch
            && string.Equals(valuationMethod, "Moving Average", StringComparison.OrdinalIgnoreCase);

        var before = previousRow;
        Guid? repostFromEntryId = null;

        for (var i = 0; i < orderedRows.Count; i++)
        {
            var row = orderedRows[i];
            var qtyBefore = before?.QtyAfterTransaction ?? 0m;
            var valueBefore = before?.StockValue ?? 0m;
            var rateBefore = before?.ValuationRate ?? 0m;
            var lotsBefore = isQueueMethod ? (before?.Lots ?? Array.Empty<(decimal Qty, decimal Rate)>()) : Array.Empty<(decimal, decimal)>();
            var lotsAfter = isQueueMethod ? (row.Lots ?? Array.Empty<(decimal Qty, decimal Rate)>()) : Array.Empty<(decimal, decimal)>();

            var kind = (row.IsStockReconciliation && !row.IsAdjustmentEntry)
                ? "reconciliation"
                : row.ActualQty == 0m
                    ? "revaluation"
                    : row.ActualQty > 0m ? "in" : "out";

            var stepRate = row.ActualQty > 0m
                ? row.IncomingRate
                : row.ActualQty != 0m
                    ? Math.Abs(row.StockValueDifference / row.ActualQty)
                    : 0m;

            var (usedLots, addedLots) = isQueueMethod
                ? ComputeLotChanges(lotsBefore, lotsAfter)
                : (new List<(decimal Qty, decimal Rate)>(), new List<(decimal Qty, decimal Rate)>());

            var problems = row.AwaitingRecalculation
                ? new List<string>()
                : CheckValuationBreakdownRowProblems(row, before, isQueueMethod, isMovingAverage, floatPrecision, roundingGap);

            if (problems.Count > 0 && !repostFromEntryId.HasValue)
            {
                var startRow = row;
                if (i == 0 && previousRow != null
                    && HasValuationStateProblem(previousRow, isQueueMethod, floatPrecision, roundingGap))
                {
                    startRow = previousRow;
                    if (olderRowsNewestFirst != null)
                    {
                        foreach (var older in olderRowsNewestFirst)
                        {
                            if (!HasValuationStateProblem(older, isQueueMethod, floatPrecision, roundingGap))
                            {
                                break;
                            }

                            startRow = older;
                        }
                    }
                }

                repostFromEntryId = startRow.EntryId;
            }

            steps.Add(new ValuationBreakdownStep(
                row.EntryId,
                kind,
                row.ActualQty,
                stepRate,
                row.StockValueDifference,
                qtyBefore,
                valueBefore,
                rateBefore,
                row.QtyAfterTransaction,
                row.StockValue,
                row.ValuationRate,
                usedLots,
                addedLots,
                problems));

            before = row;
        }

        return new ValuationBreakdownResult(steps, repostFromEntryId);
    }

    private static (List<(decimal Qty, decimal Rate)> Used, List<(decimal Qty, decimal Rate)> Added) ComputeLotChanges(
        IReadOnlyList<(decimal Qty, decimal Rate)> lotsBefore,
        IReadOnlyList<(decimal Qty, decimal Rate)> lotsAfter)
    {
        var beforeByRate = new Dictionary<decimal, decimal>();
        foreach (var (q, r) in lotsBefore)
            beforeByRate[r] = beforeByRate.GetValueOrDefault(r, 0m) + q;

        var afterByRate = new Dictionary<decimal, decimal>();
        foreach (var (q, r) in lotsAfter)
            afterByRate[r] = afterByRate.GetValueOrDefault(r, 0m) + q;

        var used = beforeByRate
            .Where(kv => kv.Value > afterByRate.GetValueOrDefault(kv.Key, 0m))
            .Select(kv => (kv.Value - afterByRate.GetValueOrDefault(kv.Key, 0m), kv.Key))
            .ToList();

        var added = afterByRate
            .Where(kv => kv.Value > beforeByRate.GetValueOrDefault(kv.Key, 0m))
            .Select(kv => (kv.Value - beforeByRate.GetValueOrDefault(kv.Key, 0m), kv.Key))
            .ToList();

        return (used, added);
    }

    private static List<string> CheckValuationBreakdownRowProblems(
        ValuationBreakdownSleRow row,
        ValuationBreakdownSleRow? before,
        bool isQueueMethod,
        bool isMovingAverage,
        int floatPrecision,
        decimal roundingGap)
    {
        var problems = new List<string>();
        var isQtyReset = row.IsStockReconciliation && !row.HasSerialAndBatchBundle && !row.IsAdjustmentEntry;

        if (!isQtyReset)
        {
            var expectedQty = (before?.QtyAfterTransaction ?? 0m) + row.ActualQty;
            if (Math.Round(expectedQty - row.QtyAfterTransaction, floatPrecision, MidpointRounding.AwayFromZero) != 0m)
            {
                problems.Add("qty");
            }
        }

        var expectedVal = (before?.StockValue ?? 0m) + row.StockValueDifference;
        if (Math.Abs(expectedVal - row.StockValue) >= roundingGap)
        {
            problems.Add("value");
        }

        if (Math.Abs(row.ValuationRate * row.QtyAfterTransaction - row.StockValue) >= roundingGap)
        {
            problems.Add("rate");
        }

        if (isQueueMethod && !row.HasSerialAndBatchBundle && row.Lots != null)
        {
            var lotQty = row.Lots.Sum(l => l.Qty);
            var lotVal = row.Lots.Sum(l => l.Qty * l.Rate);
            var sameQty = Math.Round(lotQty - row.QtyAfterTransaction, floatPrecision, MidpointRounding.AwayFromZero) == 0m;
            if (!sameQty || Math.Abs(lotVal - row.StockValue) >= roundingGap)
            {
                problems.Add("lots");
            }
        }

        if (isMovingAverage
            && row.ActualQty < 0m
            && !row.IsReturn
            && !isQtyReset
            && !row.IsAdjustmentEntry
            && (before?.QtyAfterTransaction ?? 0m) > 0m)
        {
            var expectedDiff = row.ActualQty * (before?.ValuationRate ?? 0m);
            if (Math.Abs(expectedDiff - row.StockValueDifference) >= roundingGap)
            {
                problems.Add("outgoing_rate");
            }
        }

        return problems;
    }

    private static bool HasValuationStateProblem(
        ValuationBreakdownSleRow row,
        bool isQueueMethod,
        int floatPrecision,
        decimal roundingGap)
    {
        if (Math.Abs(row.ValuationRate * row.QtyAfterTransaction - row.StockValue) >= roundingGap)
        {
            return true;
        }

        if (isQueueMethod && !row.HasSerialAndBatchBundle && row.Lots != null)
        {
            var lotQty = row.Lots.Sum(l => l.Qty);
            var lotVal = row.Lots.Sum(l => l.Qty * l.Rate);
            var sameQty = Math.Round(lotQty - row.QtyAfterTransaction, floatPrecision, MidpointRounding.AwayFromZero) == 0m;
            if (!sameQty || Math.Abs(lotVal - row.StockValue) >= roundingGap)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record ValuationBreakdownSleRow(
    Guid EntryId,
    decimal ActualQty,
    decimal IncomingRate,
    decimal QtyAfterTransaction,
    decimal ValuationRate,
    decimal StockValue,
    decimal StockValueDifference,
    bool IsStockReconciliation = false,
    bool IsAdjustmentEntry = false,
    bool HasSerialAndBatchBundle = false,
    bool IsReturn = false,
    bool AwaitingRecalculation = false,
    IReadOnlyList<(decimal Qty, decimal Rate)>? Lots = null);

public sealed record ValuationBreakdownStep(
    Guid EntryId,
    string Kind,
    decimal Qty,
    decimal Rate,
    decimal ValueChange,
    decimal QtyBefore,
    decimal ValueBefore,
    decimal RateBefore,
    decimal QtyAfter,
    decimal ValueAfter,
    decimal RateAfter,
    IReadOnlyList<(decimal Qty, decimal Rate)> UsedLots,
    IReadOnlyList<(decimal Qty, decimal Rate)> AddedLots,
    IReadOnlyList<string> LedgerProblems);

public sealed record ValuationBreakdownResult(
    IReadOnlyList<ValuationBreakdownStep> Steps,
    Guid? RepostFromEntryId);

public sealed record AdjustmentSleCandidate(
    Guid ItemId,
    Guid WarehouseId,
    Guid VoucherId,
    DateTime PostingDateTime,
    DateTime CreationTime,
    bool IsAdjustmentEntry,
    bool IsCancelled);

public sealed record FutureSleCandidate(
    Guid ItemId,
    Guid WarehouseId,
    string? VoucherType,
    Guid? VoucherId,
    DateTime PostingDateTime,
    DateTime CreationTime,
    bool IsCancelled);

public sealed record PendingRepostSnapshot(
    Guid ItemId,
    Guid WarehouseId,
    bool IsSubmitted,
    bool IsQueuedOrInProgress,
    bool IsTransactionBased,
    bool RepostOnlyAccountingLedgers);

public sealed record StockLedgerAuditEntry(
    Guid EntryId,
    Guid ItemId,
    Guid WarehouseId,
    DateTime PostingDateTime,
    decimal ActualQty,
    decimal QtyAfterTransaction,
    decimal ValuationRate,
    decimal StockValue,
    decimal StockValueDifference,
    bool IsStockReconciliation = false,
    decimal? ReconciliationQty = null,
    bool IsAdjustmentEntry = false,
    bool HasBatchNo = false,
    bool UseBatchwiseValuation = false,
    bool HasSerialNo = false,
    bool HasSerialAndBatchBundle = false,
    IReadOnlyList<(decimal Qty, decimal Rate)>? FifoQueue = null);
