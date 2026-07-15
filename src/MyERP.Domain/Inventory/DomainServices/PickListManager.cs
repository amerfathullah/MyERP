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
/// Domain service for Pick List business rules.
/// Handles warehouse allocation, batch/serial selection, and partial transfer tracking.
/// Per DO-NOT: "Skip double-pick prevention in pick list allocation"
/// Per DO-NOT: "Block creation of a second Stock Entry from a Pick List (partial transfers supported)"
/// </summary>
public class PickListManager : DomainService
{
    private readonly IRepository<PickList, Guid> _pickListRepository;
    private readonly IRepository<Bin, Guid> _binRepository;

    public PickListManager(
        IRepository<PickList, Guid> pickListRepository,
        IRepository<Bin, Guid> binRepository)
    {
        _pickListRepository = pickListRepository;
        _binRepository = binRepository;
    }

    /// <summary>
    /// Allocates stock to pick list items from warehouse bins.
    /// Uses priority: specific warehouse first, then checks availability.
    /// Deducts already-picked quantities from other active pick lists.
    /// </summary>
    public async Task<PickAllocationResult> AllocateStockAsync(PickList pickList)
    {
        var result = new PickAllocationResult();

        foreach (var item in pickList.Items)
        {
            var availableQty = await GetAvailableQtyForPickAsync(
                item.ItemId, item.WarehouseId, pickList.Id);

            var allocatedQty = Math.Min(item.Qty, availableQty);
            result.Allocations.Add(new PickAllocation
            {
                ItemId = item.ItemId,
                WarehouseId = item.WarehouseId,
                RequestedQty = item.Qty,
                AllocatedQty = allocatedQty,
                ShortageQty = item.Qty - allocatedQty
            });

            if (allocatedQty < item.Qty)
                result.HasShortage = true;
        }

        return result;
    }

    /// <summary>
    /// Gets available qty for picking, deducting quantities from other active pick lists.
    /// Per DO-NOT: "Skip double-pick prevention" — must query active pick lists.
    /// </summary>
    public async Task<decimal> GetAvailableQtyForPickAsync(
        Guid itemId, Guid warehouseId, Guid excludePickListId)
    {
        // Get bin balance
        var binQueryable = await _binRepository.GetQueryableAsync();
        var bin = binQueryable.FirstOrDefault(b =>
            b.ItemId == itemId && b.WarehouseId == warehouseId);

        var actualQty = bin?.ActualQty ?? 0;

        // Deduct quantities already picked by other active pick lists
        var pickQueryable = await _pickListRepository.GetQueryableAsync();
        var alreadyPicked = pickQueryable
            .Where(pl => pl.Id != excludePickListId
                && pl.Status == Core.DocumentStatus.Submitted)
            .SelectMany(pl => pl.Items)
            .Where(pi => pi.ItemId == itemId && pi.WarehouseId == warehouseId)
            .Sum(pi => pi.Qty - pi.TransferredQty);

        return Math.Max(0, actualQty - alreadyPicked);
    }

    /// <summary>
    /// Calculates pending transfer quantities for a pick list.
    /// Returns items that still have qty to transfer (picked_qty - transferred_qty > 0).
    /// </summary>
    public IReadOnlyList<PendingTransfer> GetPendingTransfers(PickList pickList)
    {
        return pickList.Items
            .Where(i => i.PendingQty > 0)
            .Select(i => new PendingTransfer
            {
                PickListItemId = i.Id,
                ItemId = i.ItemId,
                WarehouseId = i.WarehouseId,
                PendingQty = i.PendingQty,
                BatchId = i.BatchId
            })
            .ToList();
    }

    /// <summary>
    /// Validates that a stock entry can be created from the pick list.
    /// Per DO-NOT: partial transfers ARE supported (not blocked).
    /// Returns false if nothing pending.
    /// </summary>
    public bool HasPendingTransfers(PickList pickList)
    {
        return pickList.Items.Any(i => i.PendingQty > 0);
    }
}

public class PickAllocationResult
{
    public List<PickAllocation> Allocations { get; set; } = new();
    public bool HasShortage { get; set; }
}

public class PickAllocation
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal ShortageQty { get; set; }
}

public class PendingTransfer
{
    public Guid PickListItemId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal PendingQty { get; set; }
    public Guid? BatchId { get; set; }
}
