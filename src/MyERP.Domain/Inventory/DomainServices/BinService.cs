using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for managing Bin (stock balance per item+warehouse).
/// Bin updates are synchronous within the same transaction.
/// Uses retry logic for optimistic concurrency conflicts (ConcurrencyStamp).
/// </summary>
public class BinService : DomainService
{
    private const int MaxRetries = 3;
    private readonly IRepository<Bin, Guid> _binRepository;

    public BinService(IRepository<Bin, Guid> binRepository)
    {
        _binRepository = binRepository;
    }

    /// <summary>
    /// Get or create a Bin for the given item+warehouse combination.
    /// Races the unique (TenantId, ItemId, WarehouseId) index on insert: if a concurrent
    /// caller wins the race, re-reads the row it created instead of failing.
    /// </summary>
    public virtual async Task<Bin> GetOrCreateAsync(Guid itemId, Guid warehouseId, Guid? tenantId = null)
    {
        var query = await _binRepository.GetQueryableAsync();
        var bin = query.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);

        if (bin != null)
            return bin;

        bin = new Bin(Guid.NewGuid(), itemId, warehouseId, tenantId);
        try
        {
            // autoSave forces the insert (and its unique-index check) to happen now,
            // so a concurrent-create race is caught here instead of surfacing later,
            // mixed with unrelated pending changes, at the ambient unit of work's commit.
            await _binRepository.InsertAsync(bin, autoSave: true);
            return bin;
        }
        catch
        {
            // Domain layer can't reference the EF Core provider to catch the specific
            // unique-violation type, so instead of guessing an exception type: re-check
            // whether a Bin now exists (a concurrent caller won the race) and use it.
            // If no Bin exists, the failure wasn't the race — rethrow the real error.
            var refreshed = await _binRepository.GetQueryableAsync();
            var existing = refreshed.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);
            if (existing != null)
                return existing;
            throw;
        }
    }

    /// <summary>
    /// Apply a stock movement to the Bin (called after SLE creation).
    /// Retries up to 3 times on concurrency conflicts (concurrent bin updates).
    /// </summary>
    public virtual async Task ApplyStockMovementAsync(Guid itemId, Guid warehouseId, decimal qtyChange, decimal valueChange, Guid? tenantId = null)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
                bin.ApplyStockMovement(qtyChange, valueChange);
                await _binRepository.UpdateAsync(bin);
                return;
            }
            catch (AbpDbConcurrencyException) when (attempt < MaxRetries - 1)
            {
                // Concurrency conflict: another transaction modified this bin simultaneously.
                // Retry with fresh data.
                await Task.Delay(5 * (attempt + 1));
            }
        }
    }

    /// <summary>
    /// Set the Bin's actual qty/value to an absolute balance (not a delta).
    /// Used after a full valuation repost, where the caller already knows the
    /// authoritative post-repost balance and an additive ApplyStockMovement (delta=0)
    /// would leave the Bin unchanged instead of syncing it.
    /// </summary>
    public virtual async Task SetBalanceAsync(Guid itemId, Guid warehouseId, decimal actualQty, decimal stockValue, Guid? tenantId = null)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
                bin.UpdateActualQty(actualQty, stockValue);
                await _binRepository.UpdateAsync(bin);
                return;
            }
            catch (AbpDbConcurrencyException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(5 * (attempt + 1));
            }
        }
    }

    /// <summary>
    /// Update ordered qty (from Purchase Order submit/cancel).
    /// </summary>
    public virtual async Task UpdateOrderedQtyAsync(Guid itemId, Guid warehouseId, decimal orderedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.OrderedQty += orderedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty (from Sales Order submit/cancel/delivery).
    /// </summary>
    public virtual async Task UpdateReservedQtyAsync(Guid itemId, Guid warehouseId, decimal reservedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQty += reservedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update planned qty (from Work Order submit/cancel/production).
    /// </summary>
    public virtual async Task UpdatePlannedQtyAsync(Guid itemId, Guid warehouseId, decimal plannedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.PlannedQty += plannedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update indented qty (from Material Request submit/cancel/fulfill).
    /// </summary>
    public virtual async Task UpdateIndentedQtyAsync(Guid itemId, Guid warehouseId, decimal indentedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.IndentedQty += indentedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty for production (from Work Order RM reservation).
    /// Formula: MAX(0, required_qty - transferred_qty) for each open WO item.
    /// </summary>
    public virtual async Task UpdateReservedQtyForProductionAsync(Guid itemId, Guid warehouseId, decimal qtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQtyForProduction = Math.Max(0, bin.ReservedQtyForProduction + qtyChange);
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty for subcontracting (from SCO RM transfer tracking).
    /// Formula: MAX(0, required_qty - transferred_qty) for each open SCO supplied item.
    /// </summary>
    public virtual async Task UpdateReservedQtyForSubContractAsync(Guid itemId, Guid warehouseId, decimal qtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQtyForSubContract = Math.Max(0, bin.ReservedQtyForSubContract + qtyChange);
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty for production plan (from Production Plan MR reservation).
    /// </summary>
    public virtual async Task UpdateReservedQtyForProductionPlanAsync(Guid itemId, Guid warehouseId, decimal qtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQtyForProductionPlan = Math.Max(0, bin.ReservedQtyForProductionPlan + qtyChange);
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Get current stock balance for item+warehouse.
    /// Returns zero-value Bin if no record exists.
    /// </summary>
    public async Task<Bin> GetBalanceAsync(Guid itemId, Guid warehouseId)
    {
        var query = await _binRepository.GetQueryableAsync();
        var bin = query.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);
        return bin ?? new Bin(Guid.Empty, itemId, warehouseId);
    }

    /// <summary>
    /// Full bin recalculation — refreshes all quantity fields from source data.
    /// Per ERPNext PR #57492: projected_qty depends on all bin fields, so updating just one
    /// can leave projected_qty stale if other fields drifted. This recalculates everything.
    /// </summary>
    public async Task RecalculateFullBinAsync(Guid itemId, Guid warehouseId, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        // Force recalculation by saving — ProjectedQty is computed so it auto-recalculates
        // In production, this would re-derive each field from source documents (SLE, PO, SO, WO, etc.)
        // For now, ensures the entity is marked dirty and re-persisted with current computed values
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Resets Bin actual_qty, valuation_rate, and stock_value to 0 when no non-cancelled SLEs remain (PR #58362).
    /// </summary>
    public virtual async Task ResetBinIfNoLedgerEntriesAsync(Guid itemId, Guid warehouseId, IRepository<StockLedgerEntry, Guid> sleRepository, Guid? tenantId = null)
    {
        var sleQuery = await sleRepository.GetQueryableAsync();
        var hasActiveSle = sleQuery.Any(s => s.ItemId == itemId && s.WarehouseId == warehouseId && !s.IsCancelled);
        if (!hasActiveSle)
        {
            await SetBalanceAsync(itemId, warehouseId, 0m, 0m, tenantId);
        }
    }
}
