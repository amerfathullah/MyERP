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
}

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
