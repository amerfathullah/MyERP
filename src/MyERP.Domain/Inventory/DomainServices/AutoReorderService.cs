using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Auto-reorder service — checks stock levels and creates Material Requests
/// for items that have fallen below their reorder level.
/// 
/// ERPNext equivalent: stock/reorder_item.py (runs as scheduled job)
/// 
/// Trigger: called after stock movements (DN submit, SE post) or via scheduled job.
/// Logic: projected_qty (from Bin) &lt; reorder_level → create MR for reorder_qty.
/// </summary>
public class AutoReorderService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<MaterialRequest, Guid> _mrRepository;
    private readonly IGuidGenerator _guidGenerator;

    public AutoReorderService(
        IRepository<Item, Guid> itemRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<MaterialRequest, Guid> mrRepository,
        IGuidGenerator guidGenerator)
    {
        _itemRepository = itemRepository;
        _binRepository = binRepository;
        _mrRepository = mrRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Check all items with reorder settings and create Material Requests for items below reorder level.
    /// Returns the list of created MR IDs.
    /// </summary>
    public async Task<List<Guid>> CheckAndReorderAsync(Guid companyId, Guid? tenantId = null)
    {
        var createdMRs = new List<Guid>();

        // Get all items with reorder configured
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var reorderItems = itemQuery
            .Where(i => i.CompanyId == companyId
                && i.IsActive
                && i.MaintainStock
                && i.ReorderLevel > 0
                && i.ReorderQty > 0)
            .ToList();

        if (!reorderItems.Any()) return createdMRs;

        var binQuery = await _binRepository.GetQueryableAsync();
        var itemsNeedingReorder = new List<(Item item, Guid warehouseId, decimal projectedQty)>();

        foreach (var item in reorderItems)
        {
            // Check each warehouse where this item has a Bin
            var bins = binQuery
                .Where(b => b.ItemId == item.Id)
                .ToList();

            // If item has a default warehouse but no Bin yet, treat as zero stock
            if (!bins.Any() && item.DefaultWarehouseId.HasValue)
            {
                itemsNeedingReorder.Add((item, item.DefaultWarehouseId.Value, 0));
                continue;
            }

            foreach (var bin in bins)
            {
                if (bin.ProjectedQty < item.ReorderLevel)
                {
                    itemsNeedingReorder.Add((item, bin.WarehouseId, bin.ProjectedQty));
                }
            }
        }

        if (!itemsNeedingReorder.Any()) return createdMRs;

        // Group by warehouse to create one MR per warehouse
        var grouped = itemsNeedingReorder.GroupBy(x => x.warehouseId);

        foreach (var group in grouped)
        {
            var mr = new MaterialRequest(
                _guidGenerator.Create(),
                companyId,
                $"REORDER-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{group.Key.ToString()[..8]}",
                MaterialRequestType.Purchase,
                DateTime.UtcNow,
                tenantId);

            foreach (var (item, warehouseId, projectedQty) in group)
            {
                mr.AddItem(item.Id, item.ItemName, item.ReorderQty, item.Uom, warehouseId);
            }

            await _mrRepository.InsertAsync(mr, autoSave: true);
            createdMRs.Add(mr.Id);
        }

        return createdMRs;
    }

    /// <summary>
    /// Check a single item+warehouse after a stock movement.
    /// More efficient than full scan for real-time triggers.
    /// </summary>
    public async Task<Guid?> CheckSingleItemAsync(Guid itemId, Guid warehouseId, Guid companyId, Guid? tenantId = null)
    {
        var item = await _itemRepository.GetAsync(itemId);
        if (!item.IsActive || !item.MaintainStock || item.ReorderLevel <= 0 || item.ReorderQty <= 0)
            return null;

        var binQuery = await _binRepository.GetQueryableAsync();
        var bin = binQuery.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);

        var projectedQty = bin?.ProjectedQty ?? 0;
        if (projectedQty >= item.ReorderLevel)
            return null;

        // Check if there's already a pending MR for this item+warehouse
        var mrQuery = await _mrRepository.GetQueryableAsync();
        var existingMR = mrQuery
            .Where(mr => mr.CompanyId == companyId
                && mr.Status == Core.DocumentStatus.Draft
                && mr.Items.Any(i => i.ItemId == itemId && i.WarehouseId == warehouseId))
            .Any();

        if (existingMR)
            return null; // Already has a pending reorder

        var mr = new MaterialRequest(
            _guidGenerator.Create(),
            companyId,
            $"REORDER-{item.ItemCode}-{DateTime.UtcNow:yyyyMMddHHmm}",
            MaterialRequestType.Purchase,
            DateTime.UtcNow,
            tenantId);

        mr.AddItem(item.Id, item.ItemName, item.ReorderQty, item.Uom, warehouseId);

        await _mrRepository.InsertAsync(mr, autoSave: true);
        return mr.Id;
    }
}
