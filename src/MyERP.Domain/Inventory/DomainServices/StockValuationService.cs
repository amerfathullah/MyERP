using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Calculates stock valuation rates using Weighted Average method.
/// Called when stock entries are posted to ensure accurate inventory costing.
/// 
/// ERPNext equivalent: stock reposting on submit (stock_ledger.py / repost_item_valuation).
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
    /// Creates a stock ledger entry with proper valuation rate calculated.
    /// For stock-in: uses the incoming rate.
    /// For stock-out: uses the current weighted average valuation rate.
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
        var currentBalance = await GetCurrentBalanceAsync(itemId, warehouseId);

        decimal valuationRate;
        decimal newBalanceQty;
        decimal newBalanceValue;

        if (quantityChange > 0)
        {
            // Stock IN — calculate new weighted average
            valuationRate = incomingRate;
            newBalanceQty = currentBalance.Quantity + quantityChange;
            newBalanceValue = currentBalance.Value + (quantityChange * incomingRate);
        }
        else
        {
            // Stock OUT — use current weighted average rate
            valuationRate = currentBalance.Quantity > 0
                ? currentBalance.Value / currentBalance.Quantity
                : incomingRate;
            newBalanceQty = currentBalance.Quantity + quantityChange; // quantityChange is negative
            newBalanceValue = newBalanceQty > 0
                ? newBalanceQty * valuationRate
                : 0;
        }

        // Prevent negative valuation
        if (newBalanceQty < 0)
            newBalanceQty = 0;
        if (newBalanceValue < 0)
            newBalanceValue = 0;

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
        };

        await _ledgerRepository.InsertAsync(entry);
        return entry;
    }

    /// <summary>
    /// Gets the current stock balance for an item in a specific warehouse.
    /// Uses the most recent ledger entry's running balance.
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
        var query = await _ledgerRepository.GetQueryableAsync();
        var entries = query
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate >= fromDate)
            .OrderBy(e => e.PostingDate)
            .ThenBy(e => e.CreationTime)
            .ToList();

        if (entries.Count == 0) return;

        // Get balance just before the from date
        var priorEntry = query
            .Where(e => e.ItemId == itemId && e.WarehouseId == warehouseId && e.PostingDate < fromDate)
            .OrderByDescending(e => e.PostingDate)
            .ThenByDescending(e => e.CreationTime)
            .FirstOrDefault();

        var runningQty = priorEntry?.BalanceQuantity ?? 0;
        var runningValue = priorEntry?.BalanceValue ?? 0;

        foreach (var entry in entries)
        {
            if (entry.QuantityChange > 0)
            {
                // Stock in: valuation rate stays as-is (incoming rate)
                runningQty += entry.QuantityChange;
                runningValue += entry.QuantityChange * entry.ValuationRate;
            }
            else
            {
                // Stock out: recalculate using current weighted average
                var avgRate = runningQty > 0 ? runningValue / runningQty : entry.ValuationRate;
                entry.ValuationRate = Math.Round(avgRate, 4);
                entry.StockValue = entry.QuantityChange * avgRate;
                runningQty += entry.QuantityChange;
                runningValue = runningQty > 0 ? runningQty * avgRate : 0;
            }

            entry.BalanceQuantity = Math.Max(0, Math.Round(runningQty, 4));
            entry.BalanceValue = Math.Max(0, Math.Round(runningValue, 2));
        }

        await _ledgerRepository.UpdateManyAsync(entries);
    }
}

public record StockBalance(decimal Quantity, decimal Value)
{
    public decimal ValuationRate => Quantity > 0 ? Value / Quantity : 0;
}
