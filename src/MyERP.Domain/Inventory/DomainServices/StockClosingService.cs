using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for generating period-end stock closing snapshots.
/// Per ERPNext: stock closing entries avoid full SLE scans for balance reports.
///
/// Algorithm:
/// 1. Find the latest submitted closing for the company (incremental base)
/// 2. Determine scan range: previous_closing.ToDate+1 to new closing ToDate
/// 3. Load opening balances from previous closing (or empty if first)
/// 4. Apply all SLE delta for the scan range (group by item+warehouse)
/// 5. For FIFO/LIFO items, persist the queue state as JSON
/// 6. Write StockClosingBalance rows and submit
///
/// Per DO-NOT rules:
/// - Must use incremental logic (reprocessing all SLE is too slow)
/// - Stock Balance report uses latest closing as opening (optimization)
/// </summary>
public class StockClosingService : DomainService
{
    private readonly IRepository<StockClosingEntry, Guid> _closingRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;

    public StockClosingService(
        IRepository<StockClosingEntry, Guid> closingRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository)
    {
        _closingRepository = closingRepository;
        _sleRepository = sleRepository;
    }

    /// <summary>
    /// Find the latest submitted stock closing for a company.
    /// Returns null if no prior closing exists (first closing ever).
    /// </summary>
    public async Task<StockClosingEntry?> GetLatestClosingAsync(Guid companyId)
    {
        var query = await _closingRepository.GetQueryableAsync();
        return query
            .Where(c => c.CompanyId == companyId && c.Status == StockClosingStatus.Submitted)
            .OrderByDescending(c => c.ToDate)
            .FirstOrDefault();
    }

    /// <summary>
    /// Check if a date is covered by an existing submitted stock closing.
    /// If covered, reposting for that date is blocked.
    /// </summary>
    public async Task<bool> IsDateCoveredByClosingAsync(Guid companyId, DateTime date)
    {
        var query = await _closingRepository.GetQueryableAsync();
        return query.Any(c => c.CompanyId == companyId
                           && c.Status == StockClosingStatus.Submitted
                           && c.ToDate >= date);
    }

    /// <summary>
    /// Generate a stock closing entry for a company up to the specified date.
    /// Uses incremental logic: builds on the latest previous closing.
    ///
    /// Per ERPNext:
    /// - Reads opening balances from previous closing entry
    /// - Scans only the SLE delta since the previous closing
    /// - Groups by (item, warehouse) and aggregates qty/value
    /// - For FIFO/LIFO items, persists the stock queue as JSON
    /// </summary>
    public async Task<StockClosingEntry> GenerateClosingAsync(
        Guid companyId, DateTime toDate, Guid? tenantId = null)
    {
        // 1. Find previous closing (incremental base)
        var previousClosing = await GetLatestClosingAsync(companyId);

        // 2. Determine scan range
        var scanFromDate = previousClosing?.ToDate.AddDays(1) ?? DateTime.MinValue;

        // 3. Load opening balances from previous closing
        var balances = new Dictionary<(Guid ItemId, Guid WarehouseId), StockBalanceAccumulator>();
        if (previousClosing != null)
        {
            foreach (var prev in previousClosing.Balances)
            {
                balances[(prev.ItemId, prev.WarehouseId)] = new StockBalanceAccumulator
                {
                    Qty = prev.Qty,
                    StockValue = prev.StockValue,
                    ValuationRate = prev.ValuationRate,
                    FifoQueue = prev.FifoQueue,
                };
            }
        }

        // 4. Apply SLE delta for the scan range
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var deltaEntries = sleQuery
            .Where(s => s.CompanyId == companyId
                     && s.PostingDate >= scanFromDate
                     && s.PostingDate <= toDate)
            .OrderBy(s => s.PostingDate)
            .ThenBy(s => s.CreationTime)
            .ToList();

        foreach (var sle in deltaEntries)
        {
            var key = (sle.ItemId, sle.WarehouseId);
            if (!balances.TryGetValue(key, out var acc))
            {
                acc = new StockBalanceAccumulator();
                balances[key] = acc;
            }

            acc.Qty += sle.QuantityChange;
            acc.StockValue += sle.StockValue;
            acc.ValuationRate = acc.Qty != 0 ? acc.StockValue / acc.Qty : 0;

            // Preserve FIFO/LIFO queue from SLE if available
            if (!string.IsNullOrEmpty(sle.StockQueue))
                acc.FifoQueue = sle.StockQueue;
        }

        // 5. Create the closing entry
        var closing = new StockClosingEntry(
            GuidGenerator.Create(), companyId, toDate, tenantId);
        closing.PreviousClosingEntryId = previousClosing?.Id;
        closing.ScannedFromDate = scanFromDate == DateTime.MinValue ? null : scanFromDate;

        // 6. Add balance snapshots
        foreach (var kvp in balances)
        {
            if (kvp.Value.Qty == 0 && kvp.Value.StockValue == 0)
                continue; // Skip zero-balance entries

            closing.AddBalance(
                kvp.Key.ItemId,
                kvp.Key.WarehouseId,
                kvp.Value.Qty,
                kvp.Value.StockValue,
                kvp.Value.ValuationRate,
                kvp.Value.FifoQueue);
        }

        await _closingRepository.InsertAsync(closing);
        return closing;
    }

    /// <summary>
    /// Get stock balance for reporting, using the latest closing as optimization.
    /// Returns the opening balance as of the closing date, or empty if no closing.
    /// </summary>
    public async Task<Dictionary<(Guid ItemId, Guid WarehouseId), StockBalanceAccumulator>>
        GetOpeningBalancesFromClosingAsync(Guid companyId, DateTime asOfDate)
    {
        var query = await _closingRepository.GetQueryableAsync();
        var bestClosing = query
            .Where(c => c.CompanyId == companyId
                     && c.Status == StockClosingStatus.Submitted
                     && c.ToDate <= asOfDate)
            .OrderByDescending(c => c.ToDate)
            .FirstOrDefault();

        var result = new Dictionary<(Guid, Guid), StockBalanceAccumulator>();
        if (bestClosing == null)
            return result;

        foreach (var b in bestClosing.Balances)
        {
            result[(b.ItemId, b.WarehouseId)] = new StockBalanceAccumulator
            {
                Qty = b.Qty,
                StockValue = b.StockValue,
                ValuationRate = b.ValuationRate,
                FifoQueue = b.FifoQueue,
            };
        }

        return result;
    }

    /// <summary>Accumulator for per-item per-warehouse stock balance during closing generation.</summary>
    public class StockBalanceAccumulator
    {
        public decimal Qty { get; set; }
        public decimal StockValue { get; set; }
        public decimal ValuationRate { get; set; }
        public string? FifoQueue { get; set; }
    }
}
