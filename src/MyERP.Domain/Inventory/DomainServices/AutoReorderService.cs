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
///
/// Per-warehouse override: if an Item has ItemReorder rows (per ERPNext's Item Reorder
/// child table), each row's WarehouseReorderLevel/Qty/MaterialRequestType replace the
/// Item's single global ReorderLevel/ReorderQty/DefaultMaterialRequestType for that
/// warehouse. A row's optional WarehouseGroupId ("Check Availability in Warehouse")
/// checks stock summed across that warehouse and all its descendants instead of just
/// the target warehouse's own bin — matching ERPNext's warehouse_group semantics.
/// </summary>
public class AutoReorderService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<MaterialRequest, Guid> _mrRepository;
    private readonly IGuidGenerator _guidGenerator;

    public AutoReorderService(
        IRepository<Item, Guid> itemRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<MaterialRequest, Guid> mrRepository,
        IGuidGenerator guidGenerator)
    {
        _itemRepository = itemRepository;
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
        _mrRepository = mrRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Check all items with reorder settings (global or per-warehouse) and create
    /// Material Requests for items below their reorder level. Returns the created MR IDs.
    /// </summary>
    public async Task<List<Guid>> CheckAndReorderAsync(Guid companyId, Guid? tenantId = null)
    {
        var createdMRs = new List<Guid>();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var candidateItems = itemQuery
            .Where(i => i.CompanyId == companyId && i.IsActive && i.MaintainStock)
            .Where(i => (i.ReorderLevel > 0 && i.ReorderQty > 0) || i.Reorders.Any())
            .ToList();

        if (!candidateItems.Any()) return createdMRs;

        var binQuery = await _binRepository.GetQueryableAsync();
        var childrenByParent = await GetWarehouseChildrenLookupAsync(companyId);

        var itemsNeedingReorder = new List<(Item item, Guid warehouseId, decimal reorderQty, MaterialRequestType mrType)>();

        foreach (var item in candidateItems)
        {
            var overriddenWarehouseIds = new HashSet<Guid>();

            // Per-warehouse overrides take priority over the item's global reorder settings.
            foreach (var reorder in item.Reorders)
            {
                overriddenWarehouseIds.Add(reorder.WarehouseId);

                var checkWarehouseIds = GetWarehouseGroupMembers(reorder.WarehouseGroupId ?? reorder.WarehouseId, childrenByParent);
                var projectedQty = binQuery
                    .Where(b => b.ItemId == item.Id && checkWarehouseIds.Contains(b.WarehouseId))
                    .Sum(b => (decimal?)b.ProjectedQty) ?? 0;

                if (projectedQty <= reorder.WarehouseReorderLevel)
                {
                    var deficiency = reorder.WarehouseReorderLevel - projectedQty;
                    var qtyToOrder = Math.Max(reorder.WarehouseReorderQty, deficiency);
                    itemsNeedingReorder.Add((item, reorder.WarehouseId, qtyToOrder, reorder.MaterialRequestType));
                }
            }

            // Global fallback: applies only to warehouses not already covered by an override row above.
            if (item.ReorderLevel > 0 && item.ReorderQty > 0)
            {
                var bins = binQuery
                    .Where(b => b.ItemId == item.Id && !overriddenWarehouseIds.Contains(b.WarehouseId))
                    .ToList();

                if (!bins.Any() && item.DefaultWarehouseId.HasValue && !overriddenWarehouseIds.Contains(item.DefaultWarehouseId.Value))
                {
                    var qtyToOrder = Math.Max(item.ReorderQty, item.ReorderLevel);
                    itemsNeedingReorder.Add((item, item.DefaultWarehouseId.Value, qtyToOrder, item.DefaultMaterialRequestType));
                    continue;
                }

                foreach (var bin in bins)
                {
                    if (bin.ProjectedQty <= item.ReorderLevel)
                    {
                        var deficiency = item.ReorderLevel - bin.ProjectedQty;
                        var qtyToOrder = Math.Max(item.ReorderQty, deficiency);
                        itemsNeedingReorder.Add((item, bin.WarehouseId, qtyToOrder, item.DefaultMaterialRequestType));
                    }
                }
            }
        }

        if (!itemsNeedingReorder.Any()) return createdMRs;

        // Group by (MR type, warehouse) — per ERPNext: reorders can be Purchase, Transfer, or Manufacture
        var grouped = itemsNeedingReorder
            .GroupBy(x => new { x.mrType, x.warehouseId });

        foreach (var group in grouped)
        {
            var mr = new MaterialRequest(
                _guidGenerator.Create(),
                companyId,
                $"REORDER-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{group.Key.warehouseId.ToString()[..8]}",
                group.Key.mrType,
                DateTime.UtcNow,
                tenantId);

            foreach (var (item, warehouseId, reorderQty, _) in group)
            {
                mr.AddItem(item.Id, item.ItemName, reorderQty, item.Uom, warehouseId);
            }

            await _mrRepository.InsertAsync(mr, autoSave: true);
            createdMRs.Add(mr.Id);
        }

        return createdMRs;
    }

    /// <summary>
    /// Check a single item+warehouse after a stock movement.
    /// More efficient than full scan for real-time triggers. Honors a per-warehouse
    /// ItemReorder override for this warehouse if one exists, else falls back to the
    /// item's global reorder level/qty.
    /// </summary>
    public async Task<Guid?> CheckSingleItemAsync(Guid itemId, Guid warehouseId, Guid companyId, Guid? tenantId = null)
    {
        var item = await _itemRepository.GetAsync(itemId);
        if (!item.IsActive || !item.MaintainStock)
            return null;

        var reorder = item.Reorders.FirstOrDefault(r => r.WarehouseId == warehouseId);

        decimal reorderLevel;
        decimal reorderQty;
        MaterialRequestType mrType;
        Guid checkRootWarehouseId;

        if (reorder != null)
        {
            reorderLevel = reorder.WarehouseReorderLevel;
            reorderQty = reorder.WarehouseReorderQty;
            mrType = reorder.MaterialRequestType;
            checkRootWarehouseId = reorder.WarehouseGroupId ?? reorder.WarehouseId;
        }
        else
        {
            if (item.ReorderLevel <= 0 || item.ReorderQty <= 0)
                return null;
            reorderLevel = item.ReorderLevel;
            reorderQty = item.ReorderQty;
            mrType = item.DefaultMaterialRequestType;
            checkRootWarehouseId = warehouseId;
        }

        var binQuery = await _binRepository.GetQueryableAsync();
        decimal projectedQty;
        if (checkRootWarehouseId == warehouseId)
        {
            var bin = binQuery.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);
            projectedQty = bin?.ProjectedQty ?? 0;
        }
        else
        {
            var childrenByParent = await GetWarehouseChildrenLookupAsync(companyId);
            var checkWarehouseIds = GetWarehouseGroupMembers(checkRootWarehouseId, childrenByParent);
            projectedQty = binQuery
                .Where(b => b.ItemId == itemId && checkWarehouseIds.Contains(b.WarehouseId))
                .Sum(b => (decimal?)b.ProjectedQty) ?? 0;
        }

        if (projectedQty > reorderLevel)
            return null;

        var deficiency = reorderLevel - projectedQty;
        var qtyToOrder = Math.Max(reorderQty, deficiency);

        // Check if there's already a pending MR for this item+warehouse
        var mrQuery = await _mrRepository.GetQueryableAsync();
        var existingMR = mrQuery
            .Where(mr => mr.CompanyId == companyId
                && mr.Status == Core.DocumentStatus.Draft
                && mr.Items.Any(i => i.ItemId == itemId && i.WarehouseId == warehouseId))
            .Any();

        if (existingMR)
            return null; // Already has a pending reorder

        var newMr = new MaterialRequest(
            _guidGenerator.Create(),
            companyId,
            $"REORDER-{item.ItemCode}-{DateTime.UtcNow:yyyyMMddHHmm}",
            mrType,
            DateTime.UtcNow,
            tenantId);

        newMr.AddItem(item.Id, item.ItemName, qtyToOrder, item.Uom, warehouseId);

        await _mrRepository.InsertAsync(newMr, autoSave: true);
        return newMr.Id;
    }

    /// <summary>Builds a parent→children lookup over the company's warehouses for group-descendant resolution.</summary>
    private async Task<ILookup<Guid?, Warehouse>> GetWarehouseChildrenLookupAsync(Guid companyId)
    {
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouses = warehouseQuery.Where(w => w.CompanyId == companyId).ToList();
        return warehouses.ToLookup(w => w.ParentWarehouseId);
    }

    /// <summary>Returns the warehouse itself plus all warehouses nested under it (BFS over ParentWarehouseId).</summary>
    private static List<Guid> GetWarehouseGroupMembers(Guid rootWarehouseId, ILookup<Guid?, Warehouse> childrenByParent)
    {
        var result = new List<Guid> { rootWarehouseId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootWarehouseId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in childrenByParent[current])
            {
                result.Add(child.Id);
                queue.Enqueue(child.Id);
            }
        }

        return result;
    }
}
