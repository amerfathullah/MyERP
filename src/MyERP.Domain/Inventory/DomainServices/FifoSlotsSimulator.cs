using System;
using System.Collections.Generic;
using System.Linq;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Simulates FIFO/LIFO inventory queues from Stock Ledger Entries to compute stock age and value distribution.
/// Maps to ERPNext stock/report/stock_ageing/stock_ageing.py (FIFOSlots class).
/// 
/// Key Invariants & Gotchas:
/// - FIFO consumes from queue head (index 0).
/// - LIFO consumes from queue tail (index ^1).
/// - Per ERPNext PR #59062 (commit 5a63b36c3b): LIFO batch items walk batch slots newest-first (from tail).
/// - Per ERPNext PR #59058 (commit 990a43ed16): batch and serial receipt dates are scoped to (batch/serial, warehouse)
///   so inter-warehouse transfers age from the date of arrival in the target warehouse.
/// - Unfilled outward stock creates negative queue slots that absorb subsequent inward arrivals.
/// - Moving Average valuation updates all slot values based on the current valuation rate.
/// </summary>
public class FifoSlotsSimulator
{
    public record StockSlot
    {
        public decimal Qty { get; set; }
        public DateTime Date { get; set; }
        public decimal StockValue { get; set; }
        public Guid? BatchId { get; set; }
        public string? SerialNo { get; set; }
        public bool UseBatchwiseValuation { get; set; }
    }

    public record SimulationEntry
    {
        public Guid ItemId { get; init; }
        public Guid WarehouseId { get; init; }
        public DateTime PostingDate { get; init; }
        public DateTime CreationTime { get; init; }
        public decimal ActualQty { get; init; }
        public decimal StockValueDifference { get; init; }
        public decimal ValuationRate { get; init; }
        public Guid? BatchId { get; init; }
        public string? SerialNo { get; init; }
        public bool HasBatchNo { get; init; }
        public bool HasSerialNo { get; init; }
        public bool UseBatchwiseValuation { get; init; }
        public string ValuationMethod { get; init; } = "FIFO";
    }

    public record AgeBucketDefinition(string Label, int MinDays, int? MaxDays);

    public record BucketValue(int BucketIndex, string Label, decimal Qty, decimal StockValue);

    public record ItemAgeingResult
    {
        public Guid ItemId { get; init; }
        public Guid? WarehouseId { get; init; }
        public decimal TotalQty { get; init; }
        public decimal TotalStockValue { get; init; }
        public decimal AverageAgeDays { get; init; }
        public int OldestDays { get; init; }
        public int NewestDays { get; init; }
        public List<BucketValue> Buckets { get; init; } = new();
    }

    public static List<AgeBucketDefinition> ParseRanges(string? rangeString)
    {
        var rawNumbers = (rangeString ?? "30, 60, 90, 120")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : -1)
            .Where(n => n > 0)
            .OrderBy(n => n)
            .Distinct()
            .ToList();

        if (rawNumbers.Count == 0)
        {
            rawNumbers = new List<int> { 30, 60, 90, 120 };
        }

        var buckets = new List<AgeBucketDefinition>();
        var prev = 0;
        foreach (var num in rawNumbers)
        {
            var label = prev == 0 ? $"0-{num}d" : $"{prev + 1}-{num}d";
            buckets.Add(new AgeBucketDefinition(label, prev == 0 ? 0 : prev + 1, num));
            prev = num;
        }

        buckets.Add(new AgeBucketDefinition($">{prev}d", prev + 1, null));
        return buckets;
    }

    /// <summary>
    /// Processes stock entries in temporal order (PostingDate, CreationTime) and computes
    /// the remaining FIFO/LIFO slots and age breakdown as of toDate.
    /// </summary>
    public static List<ItemAgeingResult> Simulate(
        IEnumerable<SimulationEntry> entries,
        DateTime toDate,
        bool showWarehouseWise = true,
        string? rangeString = null)
    {
        var bucketDefs = ParseRanges(rangeString);
        var asOf = toDate.Date;

        // Group by (ItemId, WarehouseId) or ItemId depending on showWarehouseWise
        var groupedEntries = showWarehouseWise
            ? entries.GroupBy(e => (e.ItemId, WarehouseId: (Guid?)e.WarehouseId))
            : entries.GroupBy(e => (e.ItemId, WarehouseId: (Guid?)null));

        var results = new List<ItemAgeingResult>();

        // PR #59058: scope batch and serial receipt dates to the warehouse
        var batchDateLookup = new Dictionary<(Guid BatchId, Guid WarehouseId), DateTime>();
        var serialDateLookup = new Dictionary<(string SerialNo, Guid WarehouseId), DateTime>();
        // PR #59061: track (batch_id, warehouse) with negative slots to skip scan when nothing negative
        var batchesWithNegativeSlots = new HashSet<(Guid BatchId, Guid WarehouseId)>();

        foreach (var group in groupedEntries)
        {
            var sortedEntries = group
                .OrderBy(e => e.PostingDate)
                .ThenBy(e => e.CreationTime)
                .ToList();

            if (sortedEntries.Count == 0) continue;

            var queue = new List<StockSlot>();
            var isLifo = string.Equals(sortedEntries.First().ValuationMethod, "LIFO", StringComparison.OrdinalIgnoreCase);
            var isMovingAverage = string.Equals(sortedEntries.First().ValuationMethod, "MovingAverage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sortedEntries.First().ValuationMethod, "Moving Average", StringComparison.OrdinalIgnoreCase);
            var lastValuationRate = sortedEntries.Last().ValuationRate;

            foreach (var entry in sortedEntries)
            {
                if (entry.ActualQty > 0)
                {
                    // Inward stock
                    AddInward(entry, queue, batchDateLookup, serialDateLookup, batchesWithNegativeSlots);
                }
                else if (entry.ActualQty < 0)
                {
                    // Outward stock
                    ConsumeOutward(entry, queue, isLifo, batchesWithNegativeSlots);
                }
                else if (entry.StockValueDifference != 0 && queue.Count > 0)
                {
                    // Stock reconciliation or rate adjustment without qty change
                    AdjustValue(entry.StockValueDifference, queue);
                }

                if (isMovingAverage && entry.ValuationRate > 0)
                {
                    // Recompute slot values for Moving Average
                    foreach (var slot in queue)
                    {
                        if (slot.Qty > 0)
                            slot.StockValue = slot.Qty * entry.ValuationRate;
                    }
                }
            }

            // Remove empty or zero-qty slots
            queue.RemoveAll(s => s.Qty <= 0);

            if (queue.Count == 0) continue;

            var totalQty = queue.Sum(s => s.Qty);
            var totalValue = queue.Sum(s => s.StockValue);
            if (totalQty <= 0) continue;

            // Compute ages
            decimal weightedAgeSum = 0m;
            int oldestDays = 0;
            int newestDays = int.MaxValue;

            var bucketTotals = bucketDefs.Select((b, idx) => new
            {
                Index = idx,
                Def = b,
                Qty = 0m,
                Value = 0m
            }).ToDictionary(x => x.Index, x => (x.Def, Qty: 0m, Value: 0m));

            foreach (var slot in queue)
            {
                var age = Math.Max(0, (int)(asOf - slot.Date.Date).TotalDays);
                weightedAgeSum += slot.Qty * age;

                if (age > oldestDays) oldestDays = age;
                if (age < newestDays) newestDays = age;

                // Match bucket
                var bucketIndex = bucketDefs.Count - 1; // default to last (>max)
                for (int i = 0; i < bucketDefs.Count; i++)
                {
                    var def = bucketDefs[i];
                    if (def.MaxDays.HasValue && age <= def.MaxDays.Value)
                    {
                        bucketIndex = i;
                        break;
                    }
                }

                var current = bucketTotals[bucketIndex];
                bucketTotals[bucketIndex] = (current.Def, current.Qty + slot.Qty, current.Value + slot.StockValue);
            }

            if (newestDays == int.MaxValue) newestDays = 0;
            var averageAge = totalQty > 0 ? Math.Round(weightedAgeSum / totalQty, 2) : 0m;

            var bucketsList = bucketDefs.Select((b, idx) => new BucketValue(
                idx,
                b.Label,
                Math.Round(bucketTotals[idx].Qty, 4),
                Math.Round(bucketTotals[idx].Value, 2)
            )).ToList();

            results.Add(new ItemAgeingResult
            {
                ItemId = group.Key.ItemId,
                WarehouseId = group.Key.WarehouseId,
                TotalQty = Math.Round(totalQty, 4),
                TotalStockValue = Math.Round(totalValue, 2),
                AverageAgeDays = averageAge,
                OldestDays = oldestDays,
                NewestDays = newestDays,
                Buckets = bucketsList
            });
        }

        return results;
    }

    private static void AddInward(
        SimulationEntry entry,
        List<StockSlot> queue,
        Dictionary<(Guid BatchId, Guid WarehouseId), DateTime> batchDateLookup,
        Dictionary<(string SerialNo, Guid WarehouseId), DateTime> serialDateLookup,
        HashSet<(Guid BatchId, Guid WarehouseId)> batchesWithNegativeSlots)
    {
        var incomingQty = entry.ActualQty;
        var incomingValue = Math.Abs(entry.StockValueDifference);
        if (incomingValue == 0 && entry.ValuationRate > 0)
        {
            incomingValue = incomingQty * entry.ValuationRate;
        }

        // PR #59061: Skip negative batch scan when nothing is negative for this (batch, warehouse)
        if (entry.BatchId.HasValue && batchesWithNegativeSlots.Contains((entry.BatchId.Value, entry.WarehouseId)))
        {
            var negativeSlotMayRemain = false;

            for (int i = 0; i < queue.Count && incomingQty > 0; i++)
            {
                var slot = queue[i];
                if (slot.Qty < 0 && slot.BatchId == entry.BatchId.Value && slot.UseBatchwiseValuation == entry.UseBatchwiseValuation)
                {
                    var absNeg = Math.Abs(slot.Qty);
                    var qtyToAdjust = Math.Min(incomingQty, absNeg);
                    var valueToAdjust = incomingQty > 0 && incomingValue > 0
                        ? (qtyToAdjust == incomingQty ? incomingValue : incomingValue * (qtyToAdjust / incomingQty))
                        : 0m;

                    slot.Qty += qtyToAdjust;
                    slot.Date = entry.PostingDate;
                    slot.StockValue += valueToAdjust;

                    incomingQty -= qtyToAdjust;
                    incomingValue -= valueToAdjust;

                    if (slot.Qty == 0)
                    {
                        queue.RemoveAt(i);
                        i--;
                    }
                    else if (slot.Qty < 0)
                    {
                        negativeSlotMayRemain = true;
                    }

                    if (incomingQty <= 0)
                    {
                        // Check if any negative slot remains further in the queue
                        if (queue.Any(s => s.Qty < 0 && s.BatchId == entry.BatchId.Value && s.UseBatchwiseValuation == entry.UseBatchwiseValuation))
                        {
                            negativeSlotMayRemain = true;
                        }
                        break;
                    }
                }
            }

            if (!negativeSlotMayRemain)
            {
                batchesWithNegativeSlots.Remove((entry.BatchId.Value, entry.WarehouseId));
            }
        }

        // Absorb general/non-batch negative stock slots if incomingQty remains
        for (int i = 0; i < queue.Count && incomingQty > 0; i++)
        {
            if (queue[i].Qty < 0 && !queue[i].BatchId.HasValue)
            {
                var absNeg = Math.Abs(queue[i].Qty);
                if (incomingQty >= absNeg)
                {
                    incomingQty -= absNeg;
                    queue.RemoveAt(i);
                    i--;
                }
                else
                {
                    queue[i].Qty += incomingQty;
                    incomingQty = 0;
                }
            }
        }

        if (incomingQty <= 0) return;

        // Resolve date per PR #59058 (scoped to warehouse)
        DateTime slotDate = entry.PostingDate;
        if (entry.BatchId.HasValue)
        {
            var key = (entry.BatchId.Value, entry.WarehouseId);
            if (!batchDateLookup.TryGetValue(key, out slotDate))
            {
                slotDate = entry.PostingDate;
                batchDateLookup[key] = slotDate;
            }
        }
        else if (!string.IsNullOrEmpty(entry.SerialNo))
        {
            var key = (entry.SerialNo.ToUpperInvariant(), entry.WarehouseId);
            if (!serialDateLookup.TryGetValue(key, out slotDate))
            {
                slotDate = entry.PostingDate;
                serialDateLookup[key] = slotDate;
            }
        }

        queue.Add(new StockSlot
        {
            Qty = incomingQty,
            Date = slotDate,
            StockValue = incomingValue,
            BatchId = entry.BatchId,
            SerialNo = entry.SerialNo,
            UseBatchwiseValuation = entry.UseBatchwiseValuation
        });
    }

    private static void ConsumeOutward(
        SimulationEntry entry,
        List<StockSlot> queue,
        bool isLifo,
        HashSet<(Guid BatchId, Guid WarehouseId)> batchesWithNegativeSlots)
    {
        var neededQty = Math.Abs(entry.ActualQty);

        // 1. If serial number specified, consume specific serial slot
        if (!string.IsNullOrEmpty(entry.SerialNo))
        {
            var serialIndex = queue.FindIndex(s => string.Equals(s.SerialNo, entry.SerialNo, StringComparison.OrdinalIgnoreCase));
            if (serialIndex >= 0)
            {
                queue.RemoveAt(serialIndex);
                return;
            }
        }

        // 2. If batch specified, consume from batch slots
        if (entry.BatchId.HasValue)
        {
            // Per PR #59062: LIFO batch walk reads from tail (newest first)
            var candidateIndices = new List<int>();
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].BatchId == entry.BatchId.Value)
                    candidateIndices.Add(i);
            }

            if (isLifo)
                candidateIndices.Reverse();

            foreach (var idx in candidateIndices)
            {
                if (neededQty <= 0) break;
                var slot = queue[idx];
                if (slot.Qty <= 0) continue;

                var consume = Math.Min(neededQty, slot.Qty);
                var valueRatio = slot.Qty > 0 ? consume / slot.Qty : 1m;
                slot.StockValue -= slot.StockValue * valueRatio;
                slot.Qty -= consume;
                neededQty -= consume;
            }

            queue.RemoveAll(s => s.Qty == 0 && s.BatchId == entry.BatchId.Value);
            if (neededQty <= 0) return;
        }

        // 3. General consumption (FIFO from head, LIFO from tail)
        while (neededQty > 0 && queue.Any(s => s.Qty > 0))
        {
            var targetSlot = isLifo
                ? queue.Last(s => s.Qty > 0)
                : queue.First(s => s.Qty > 0);

            var consume = Math.Min(neededQty, targetSlot.Qty);
            var valueRatio = targetSlot.Qty > 0 ? consume / targetSlot.Qty : 1m;
            targetSlot.StockValue -= targetSlot.StockValue * valueRatio;
            targetSlot.Qty -= consume;
            neededQty -= consume;

            if (targetSlot.Qty <= 0)
            {
                queue.Remove(targetSlot);
            }
        }

        // 4. If negative stock remains, record negative buffer slot
        if (neededQty > 0)
        {
            var negValDiff = entry.StockValueDifference != 0
                ? -Math.Abs(entry.StockValueDifference)
                : (entry.ValuationRate > 0 ? -neededQty * entry.ValuationRate : 0m);

            queue.Insert(0, new StockSlot
            {
                Qty = -neededQty,
                Date = entry.PostingDate,
                StockValue = negValDiff,
                BatchId = entry.BatchId,
                SerialNo = entry.SerialNo,
                UseBatchwiseValuation = entry.UseBatchwiseValuation
            });

            // PR #59061: record warehouse owes stock on that batch
            if (entry.BatchId.HasValue)
            {
                batchesWithNegativeSlots.Add((entry.BatchId.Value, entry.WarehouseId));
            }
        }
    }

    private static void AdjustValue(decimal diff, List<StockSlot> queue)
    {
        var positiveSlots = queue.Where(s => s.Qty > 0).ToList();
        if (positiveSlots.Count == 0) return;

        var totalQty = positiveSlots.Sum(s => s.Qty);
        foreach (var slot in positiveSlots)
        {
            var portion = totalQty > 0 ? (slot.Qty / totalQty) * diff : 0m;
            slot.StockValue = Math.Max(0m, slot.StockValue + portion);
        }
    }
}
