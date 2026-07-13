using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Calculates stock valuation rates using the item's configured method (FIFO, LIFO, Moving Average).
/// Dispatches to the appropriate algorithm based on Item.ValuationMethod.
/// 
/// ERPNext equivalent: stock/valuation.py + stock_ledger.py
/// </summary>
public class StockValuationService : DomainService
{
    private readonly IRepository<StockLedgerEntry, Guid> _ledgerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public StockValuationService(
        IRepository<StockLedgerEntry, Guid> ledgerRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _ledgerRepository = ledgerRepository;
        _itemRepository = itemRepository;
    }

    /// <summary>
    /// Creates a stock ledger entry with proper valuation rate calculated based on item's valuation method.
    /// Validates negative stock constraint before allowing stock-out.
    /// </summary>
    public async Task<StockLedgerEntry> CreateLedgerEntryAsync(
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        DateTime postingDate,
        decimal quantityChange,
        decimal incomingRate,
        string? voucherType = null,
        Guid? voucherId = null,
        Guid? tenantId = null)
    {
        var item = await _itemRepository.GetAsync(itemId);
        var previousSle = await GetPreviousSleAsync(itemId, warehouseId, postingDate);

        decimal valuationRate;
        decimal newBalanceQty;
        decimal newBalanceValue;
        string? stockQueue = null;

        switch (item.ValuationMethod)
        {
            case ValuationMethod.FIFO:
            case ValuationMethod.LIFO:
                (valuationRate, newBalanceQty, newBalanceValue, stockQueue) =
                    CalculateFifoLifo(previousSle, quantityChange, incomingRate, item.ValuationMethod == ValuationMethod.LIFO);
                break;

            case ValuationMethod.StandardCost:
                (valuationRate, newBalanceQty, newBalanceValue) =
                    CalculateStandardCost(previousSle, quantityChange, item.StandardBuyingPrice ?? 0);
                break;

            case ValuationMethod.WeightedAverage:
            default:
                (valuationRate, newBalanceQty, newBalanceValue) =
                    CalculateMovingAverage(previousSle, quantityChange, incomingRate);
                break;
        }

        // Negative stock validation: block stock-out that would go negative
        // unless item explicitly allows it
        if (quantityChange < 0 && newBalanceQty < -0.0001m && !item.AllowNegativeStock)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("itemId", itemId)
                .WithData("warehouseId", warehouseId)
                .WithData("requested", Math.Abs(quantityChange))
                .WithData("available", previousSle?.BalanceQuantity ?? 0);
        }

        var entry = new StockLedgerEntry(
            GuidGenerator.Create(),
            companyId,
            itemId,
            warehouseId,
            postingDate,
            quantityChange,
            Math.Round(valuationRate, 4),
            Math.Round(newBalanceQty, 4),
            Math.Round(newBalanceValue, 2),
            tenantId)
        {
            VoucherType = voucherType,
            VoucherId = voucherId,
            StockQueue = stockQueue,
        };

        await _ledgerRepository.InsertAsync(entry);

        // Backdated entry detection: if there are future entries, revalue them
        // This ensures all downstream valuations are recalculated (mandatory per ERPNext rules)
        var query = await _ledgerRepository.GetQueryableAsync();
        var hasFutureEntries = query.Any(e =>
            e.ItemId == itemId && e.WarehouseId == warehouseId
            && e.PostingDate > postingDate && e.Id != entry.Id);

        if (hasFutureEntries)
        {
            await RevaluateFromDateAsync(itemId, warehouseId, postingDate);
        }

        return entry;
    }

    /// <summary>
    /// Gets the current stock balance for an item in a specific warehouse.
    /// </summary>
    public async Task<StockBalance> GetCurrentBalanceAsync(Guid itemId, Guid warehouseId)
    {
        var query = await _ledgerRepository.GetQueryableAsync();
        var lastEntry = query
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.CreationTime)
            .FirstOrDefault();

        if (lastEntry == null)
            return new StockBalance(0, 0);

        return new StockBalance(lastEntry.BalanceQuantity, lastEntry.BalanceValue);
    }

    /// <summary>
    /// Recalculates valuation for all entries of an item in a warehouse from a given date.
    /// Used when backdated entries are inserted or rates need correction.
    /// </summary>
    public async Task RevaluateFromDateAsync(Guid itemId, Guid warehouseId, DateTime fromDate)
    {
        var item = await _itemRepository.GetAsync(itemId);
        var query = await _ledgerRepository.GetQueryableAsync();

        var entries = query
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate >= fromDate)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.CreationTime)
            .ToList();

        if (entries.Count == 0) return;

        // Get the SLE just before the revaluation start
        var priorEntry = query
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate < fromDate)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.CreationTime)
            .FirstOrDefault();

        switch (item.ValuationMethod)
        {
            case ValuationMethod.FIFO:
            case ValuationMethod.LIFO:
                RevaluateFifoLifo(entries, priorEntry, item.ValuationMethod == ValuationMethod.LIFO);
                break;
            case ValuationMethod.WeightedAverage:
            default:
                RevaluateMovingAverage(entries, priorEntry);
                break;
        }

        await _ledgerRepository.UpdateManyAsync(entries);
    }

    private async Task<StockLedgerEntry?> GetPreviousSleAsync(Guid itemId, Guid warehouseId, DateTime postingDate)
    {
        var query = await _ledgerRepository.GetQueryableAsync();
        return query
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate <= postingDate)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.CreationTime)
            .FirstOrDefault();
    }

    private static (decimal valuationRate, decimal balanceQty, decimal balanceValue, string stockQueue)
        CalculateFifoLifo(StockLedgerEntry? previousSle, decimal quantityChange, decimal incomingRate, bool isLifo)
    {
        var queue = FifoValuation.Deserialize(previousSle?.StockQueue, isLifo);

        decimal valuationRate;
        if (quantityChange > 0)
        {
            // Stock IN: add to queue
            queue.AddStock(quantityChange, incomingRate);
            valuationRate = incomingRate;
        }
        else
        {
            // Stock OUT: consume from queue (FIFO or LIFO)
            var consumed = queue.RemoveStock(Math.Abs(quantityChange), incomingRate);
            valuationRate = FifoValuation.GetOutgoingRate(consumed);
        }

        var balanceQty = queue.TotalQty;
        var balanceValue = queue.TotalValue;
        var stockQueueJson = queue.Serialize();

        return (valuationRate, balanceQty, balanceValue, stockQueueJson);
    }

    private static (decimal valuationRate, decimal balanceQty, decimal balanceValue)
        CalculateMovingAverage(StockLedgerEntry? previousSle, decimal quantityChange, decimal incomingRate)
    {
        var existingQty = previousSle?.BalanceQuantity ?? 0;
        var existingValue = previousSle?.BalanceValue ?? 0;
        var existingRate = existingQty > 0 ? existingValue / existingQty : 0;

        decimal valuationRate;
        decimal newBalanceQty;
        decimal newBalanceValue;

        if (quantityChange > 0)
        {
            // Stock IN: weighted average
            if (existingQty <= 0)
            {
                // Reset behavior: when stock crosses from negative to positive
                valuationRate = incomingRate;
            }
            else
            {
                newBalanceValue = (existingQty * existingRate) + (quantityChange * incomingRate);
                newBalanceQty = existingQty + quantityChange;
                valuationRate = newBalanceQty > 0 ? newBalanceValue / newBalanceQty : incomingRate;
                return (valuationRate, newBalanceQty, newBalanceValue);
            }

            newBalanceQty = existingQty + quantityChange;
            newBalanceValue = newBalanceQty * valuationRate;
        }
        else
        {
            // Stock OUT: use existing average rate
            valuationRate = existingRate > 0 ? existingRate : incomingRate;
            newBalanceQty = existingQty + quantityChange; // quantityChange is negative
            newBalanceValue = newBalanceQty > 0 ? newBalanceQty * valuationRate : 0;

            // Going negative: if was positive and outgoing rate specified, use outgoing rate
            if (existingQty >= 0 && newBalanceQty < 0 && incomingRate > 0)
            {
                valuationRate = incomingRate;
            }
        }

        return (valuationRate, newBalanceQty, newBalanceValue);
    }

    private static void RevaluateFifoLifo(List<StockLedgerEntry> entries, StockLedgerEntry? priorEntry, bool isLifo)
    {
        var queue = FifoValuation.Deserialize(priorEntry?.StockQueue, isLifo);

        foreach (var entry in entries)
        {
            if (entry.QuantityChange > 0)
            {
                queue.AddStock(entry.QuantityChange, entry.ValuationRate);
            }
            else
            {
                var consumed = queue.RemoveStock(Math.Abs(entry.QuantityChange), entry.ValuationRate);
                entry.ValuationRate = FifoValuation.GetOutgoingRate(consumed);
                entry.StockValue = entry.QuantityChange * entry.ValuationRate;
            }

            entry.BalanceQuantity = Math.Round(queue.TotalQty, 4);
            entry.BalanceValue = Math.Round(queue.TotalValue, 2);
            entry.StockQueue = queue.Serialize();
        }
    }

    private static void RevaluateMovingAverage(List<StockLedgerEntry> entries, StockLedgerEntry? priorEntry)
    {
        var runningQty = priorEntry?.BalanceQuantity ?? 0;
        var runningValue = priorEntry?.BalanceValue ?? 0;

        foreach (var entry in entries)
        {
            if (entry.QuantityChange > 0)
            {
                if (runningQty <= 0)
                {
                    // Reset behavior
                    runningQty = entry.QuantityChange;
                    runningValue = entry.QuantityChange * entry.ValuationRate;
                }
                else
                {
                    runningQty += entry.QuantityChange;
                    runningValue += entry.QuantityChange * entry.ValuationRate;
                }
            }
            else
            {
                var avgRate = runningQty > 0 ? runningValue / runningQty : entry.ValuationRate;
                entry.ValuationRate = Math.Round(avgRate, 4);
                entry.StockValue = entry.QuantityChange * avgRate;
                runningQty += entry.QuantityChange;
                runningValue = runningQty > 0 ? runningQty * avgRate : 0;
            }

            entry.BalanceQuantity = Math.Round(runningQty, 4);
            entry.BalanceValue = Math.Max(0, Math.Round(runningValue, 2));
        }
    }

    /// <summary>
    /// Standard Cost valuation: uses fixed standard rate regardless of actual purchase price.
    /// Per ERPNext: standard rate is set on the item and all movements use that rate.
    /// Purchase Price Variance (PPV) = (actual_rate - standard_rate) × qty, posted to PPV account.
    /// </summary>
    private static (decimal valuationRate, decimal balanceQty, decimal balanceValue)
        CalculateStandardCost(StockLedgerEntry? previousSle, decimal quantityChange, decimal standardRate)
    {
        var existingQty = previousSle?.BalanceQuantity ?? 0;
        var newBalanceQty = existingQty + quantityChange;
        // Standard Cost: balance value always = qty × standard_rate
        var newBalanceValue = newBalanceQty * standardRate;
        return (standardRate, newBalanceQty, Math.Max(0, newBalanceValue));
    }
}

public record StockBalance(decimal Quantity, decimal Value)
{
    public decimal ValuationRate => Quantity > 0 ? Value / Quantity : 0;
}
