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
    /// Uses INSERT ON CONFLICT pattern to handle concurrent creates.
    /// </summary>
    public async Task<Bin> GetOrCreateAsync(Guid itemId, Guid warehouseId, Guid? tenantId = null)
    {
        var query = await _binRepository.GetQueryableAsync();
        var bin = query.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);

        if (bin != null)
            return bin;

        bin = new Bin(GuidGenerator.Create(), itemId, warehouseId, tenantId);
        await _binRepository.InsertAsync(bin);
        return bin;
    }

    /// <summary>
    /// Apply a stock movement to the Bin (called after SLE creation).
    /// Retries up to 3 times on concurrency conflicts (concurrent bin updates).
    /// </summary>
    public async Task ApplyStockMovementAsync(Guid itemId, Guid warehouseId, decimal qtyChange, decimal valueChange, Guid? tenantId = null)
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
    /// Update ordered qty (from Purchase Order submit/cancel).
    /// </summary>
    public async Task UpdateOrderedQtyAsync(Guid itemId, Guid warehouseId, decimal orderedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.OrderedQty += orderedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty (from Sales Order submit/cancel/delivery).
    /// </summary>
    public async Task UpdateReservedQtyAsync(Guid itemId, Guid warehouseId, decimal reservedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQty += reservedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update planned qty (from Work Order submit/cancel/production).
    /// </summary>
    public async Task UpdatePlannedQtyAsync(Guid itemId, Guid warehouseId, decimal plannedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.PlannedQty += plannedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update indented qty (from Material Request submit/cancel/fulfill).
    /// </summary>
    public async Task UpdateIndentedQtyAsync(Guid itemId, Guid warehouseId, decimal indentedQtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.IndentedQty += indentedQtyChange;
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty for production (from Work Order RM reservation).
    /// Formula: MAX(0, required_qty - transferred_qty) for each open WO item.
    /// </summary>
    public async Task UpdateReservedQtyForProductionAsync(Guid itemId, Guid warehouseId, decimal qtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQtyForProduction = Math.Max(0, bin.ReservedQtyForProduction + qtyChange);
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty for subcontracting (from SCO RM transfer tracking).
    /// Formula: MAX(0, required_qty - transferred_qty) for each open SCO supplied item.
    /// </summary>
    public async Task UpdateReservedQtyForSubContractAsync(Guid itemId, Guid warehouseId, decimal qtyChange, Guid? tenantId = null)
    {
        var bin = await GetOrCreateAsync(itemId, warehouseId, tenantId);
        bin.ReservedQtyForSubContract = Math.Max(0, bin.ReservedQtyForSubContract + qtyChange);
        await _binRepository.UpdateAsync(bin);
    }

    /// <summary>
    /// Update reserved qty for production plan (from Production Plan MR reservation).
    /// </summary>
    public async Task UpdateReservedQtyForProductionPlanAsync(Guid itemId, Guid warehouseId, decimal qtyChange, Guid? tenantId = null)
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
}
