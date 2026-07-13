using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// FIFO/LIFO valuation queue. Maintains cost layers as a list of [qty, rate] bins.
/// Implements the exact ERPNext algorithm from stock/valuation.py.
/// </summary>
public class FifoValuation
{
    private const decimal NearZeroThreshold = 1e-7m;

    private readonly List<StockBin> _queue;
    private readonly bool _isLifo;

    public FifoValuation(List<StockBin>? existingQueue = null, bool isLifo = false)
    {
        _queue = existingQueue ?? new List<StockBin>();
        _isLifo = isLifo;
    }

    /// <summary>Total quantity across all bins.</summary>
    public decimal TotalQty => RoundNearZero(_queue.Sum(b => b.Qty));

    /// <summary>Total value across all bins.</summary>
    public decimal TotalValue => RoundNearZero(_queue.Sum(b => b.Qty * b.Rate));

    /// <summary>Current valuation rate (weighted average of queue).</summary>
    public decimal ValuationRate => TotalQty > 0 ? TotalValue / TotalQty : 0;

    /// <summary>
    /// Adds stock to the queue. Handles merging, negative bin recovery.
    /// </summary>
    public void AddStock(decimal qty, decimal rate)
    {
        if (qty <= 0) return;

        if (_queue.Count == 0)
        {
            _queue.Add(new StockBin(qty, rate));
            return;
        }

        var lastBin = _queue[^1];

        if (lastBin.Qty < 0)
        {
            // Negative stock existed — recover
            var combined = lastBin.Qty + qty;
            if (combined > 0)
            {
                _queue[^1] = new StockBin(combined, rate);
            }
            else
            {
                _queue[^1] = new StockBin(combined, lastBin.Rate);
            }
        }
        else if (Math.Abs(lastBin.Rate - rate) < NearZeroThreshold)
        {
            // Same rate — merge into last bin
            _queue[^1] = new StockBin(lastBin.Qty + qty, rate);
        }
        else
        {
            // Different rate — new bin
            _queue.Add(new StockBin(qty, rate));
        }
    }

    /// <summary>
    /// Removes stock from the queue using FIFO (front) or LIFO (back) order.
    /// Returns consumed bins for valuation rate calculation.
    /// </summary>
    public List<StockBin> RemoveStock(decimal qty, decimal outgoingRate = 0)
    {
        var consumedBins = new List<StockBin>();
        if (qty <= 0) return consumedBins;

        var remaining = qty;

        while (remaining > 0)
        {
            if (_queue.Count == 0)
            {
                // Going negative — create negative bin
                _queue.Add(new StockBin(-remaining, outgoingRate));
                consumedBins.Add(new StockBin(remaining, outgoingRate));
                break;
            }

            // FIFO: consume from front (index 0), LIFO: consume from back (last index)
            int targetIndex;
            if (_isLifo)
            {
                targetIndex = _queue.Count - 1;
            }
            else
            {
                // For FIFO: if outgoing rate > 0, try rate-matched consumption first
                targetIndex = 0;
                if (outgoingRate > 0)
                {
                    var matchIndex = _queue.FindIndex(b => Math.Abs(b.Rate - outgoingRate) < NearZeroThreshold && b.Qty > 0);
                    if (matchIndex >= 0)
                        targetIndex = matchIndex;
                }
            }

            var bin = _queue[targetIndex];

            if (bin.Qty <= 0)
            {
                // Skip empty/negative bins
                _queue.RemoveAt(targetIndex);
                continue;
            }

            if (remaining >= bin.Qty)
            {
                // Consume entire bin
                consumedBins.Add(new StockBin(bin.Qty, bin.Rate));
                remaining -= bin.Qty;
                _queue.RemoveAt(targetIndex);

                if (_queue.Count == 0 && remaining > 0)
                {
                    // Queue exhausted — go negative
                    var negRate = outgoingRate > 0 ? outgoingRate : bin.Rate;
                    _queue.Add(new StockBin(-remaining, negRate));
                    consumedBins.Add(new StockBin(remaining, negRate));
                    break;
                }
            }
            else
            {
                // Partial consumption
                consumedBins.Add(new StockBin(remaining, bin.Rate));
                _queue[targetIndex] = new StockBin(bin.Qty - remaining, bin.Rate);
                remaining = 0;
            }
        }

        return consumedBins;
    }

    /// <summary>
    /// Calculates the outgoing rate from consumed bins.
    /// </summary>
    public static decimal GetOutgoingRate(List<StockBin> consumedBins)
    {
        var totalQty = consumedBins.Sum(b => b.Qty);
        if (totalQty <= 0) return 0;
        return consumedBins.Sum(b => b.Qty * b.Rate) / totalQty;
    }

    /// <summary>Serializes the queue to JSON for SLE storage.</summary>
    public string Serialize()
    {
        var data = _queue.Select(b => new[] { b.Qty, b.Rate }).ToList();
        return JsonSerializer.Serialize(data);
    }

    /// <summary>Deserializes a queue from JSON stored in SLE.StockQueue.</summary>
    public static FifoValuation Deserialize(string? json, bool isLifo = false)
    {
        if (string.IsNullOrEmpty(json))
            return new FifoValuation(isLifo: isLifo);

        var data = JsonSerializer.Deserialize<List<decimal[]>>(json) ?? new List<decimal[]>();
        var bins = data.Select(arr => new StockBin(arr[0], arr.Length > 1 ? arr[1] : 0)).ToList();
        return new FifoValuation(bins, isLifo);
    }

    private static decimal RoundNearZero(decimal value)
        => Math.Abs(value) < NearZeroThreshold ? 0 : value;
}

/// <summary>A single bin (cost layer) in the FIFO/LIFO queue.</summary>
public record struct StockBin(decimal Qty, decimal Rate);
