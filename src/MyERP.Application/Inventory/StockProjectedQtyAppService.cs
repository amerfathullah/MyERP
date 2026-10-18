using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Stock Projected Quantity application service.
/// Provides comprehensive stock projection (Actual + Planned + Indented + Ordered - Reserved)
/// and calculates shortage quantities against configured reorder levels per warehouse.
/// Maps to ERPNext stock/report/stock_projected_qty/stock_projected_qty.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockProjectedQtyAppService : ApplicationService, IStockProjectedQtyAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<ItemReorder, Guid> _reorderRepository;

    public StockProjectedQtyAppService(
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<ItemReorder, Guid> reorderRepository)
    {
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _binRepository = binRepository;
        _reorderRepository = reorderRepository;
    }

    public async Task<StockProjectedQtyReportDto> GetReportAsync(StockProjectedQtyFilterDto input)
    {
        var targetWarehouseIds = await ResolveWarehouseIdsAsync(input.WarehouseId, input.CompanyId);

        // 1. Warehouses
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        var warehouses = whQuery
            .Where(w => targetWarehouseIds.Contains(w.Id))
            .ToList();
        var whMap = warehouses.ToDictionary(w => w.Id, w => w.Name);

        if (whMap.Count == 0)
        {
            return new StockProjectedQtyReportDto();
        }

        // 2. Items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.MaintainStock && i.IsActive);

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.CompanyId == input.CompanyId.Value);
        }
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        if (input.ItemGroupId.HasValue && input.ItemGroupId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Brand))
        {
            itemQuery = itemQuery.Where(i => i.Brand == input.Brand.Trim());
        }

        var items = itemQuery.ToList();
        if (items.Count == 0)
        {
            return new StockProjectedQtyReportDto();
        }
        var itemMap = items.ToDictionary(i => i.Id);
        var itemIds = items.Select(i => i.Id).ToList();

        // 3. Bins
        var binQuery = await _binRepository.GetQueryableAsync();
        binQuery = binQuery.Where(b => itemIds.Contains(b.ItemId) && targetWarehouseIds.Contains(b.WarehouseId));
        var bins = binQuery.ToList();

        // 4. Item Reorder overrides
        var reorderQuery = await _reorderRepository.GetQueryableAsync();
        reorderQuery = reorderQuery.Where(r => itemIds.Contains(r.ItemId) && targetWarehouseIds.Contains(r.WarehouseId));
        var reorders = reorderQuery.ToList();
        var reorderMap = reorders.ToDictionary(r => (r.ItemId, r.WarehouseId));

        var rows = new List<StockProjectedQtyRowDto>();
        var searchTerm = input.SearchTerm?.Trim().ToLowerInvariant();

        foreach (var bin in bins)
        {
            if (!itemMap.TryGetValue(bin.ItemId, out var item)) continue;
            if (!whMap.TryGetValue(bin.WarehouseId, out var whName)) continue;

            // Determine reorder level & qty (per-warehouse override takes precedence)
            decimal reorderLevel;
            decimal reorderQty;
            if (reorderMap.TryGetValue((bin.ItemId, bin.WarehouseId), out var overrideReorder))
            {
                reorderLevel = overrideReorder.WarehouseReorderLevel;
                reorderQty = overrideReorder.WarehouseReorderQty;
            }
            else
            {
                reorderLevel = item.ReorderLevel;
                reorderQty = item.ReorderQty;
            }

            var totalReserved = bin.ReservedQty + bin.ReservedQtyForProduction + bin.ReservedQtyForSubContract + bin.ReservedQtyForProductionPlan;
            var projectedQty = bin.ProjectedQty;

            decimal shortageQty = 0m;
            if ((reorderLevel > 0m || reorderQty > 0m) && reorderLevel > projectedQty)
            {
                shortageQty = reorderLevel - projectedQty;
            }
            var hasShortage = shortageQty > 0m;

            if (input.ShowOnlyShortage && !hasShortage)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var match = item.ItemCode.ToLowerInvariant().Contains(searchTerm) ||
                            item.ItemName.ToLowerInvariant().Contains(searchTerm) ||
                            whName.ToLowerInvariant().Contains(searchTerm) ||
                            (item.Brand != null && item.Brand.ToLowerInvariant().Contains(searchTerm)) ||
                            (item.ItemGroup != null && item.ItemGroup.ToLowerInvariant().Contains(searchTerm));
                if (!match) continue;
            }

            rows.Add(new StockProjectedQtyRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemGroup = item.ItemGroup,
                Brand = item.Brand,
                Uom = item.Uom,
                WarehouseId = bin.WarehouseId,
                WarehouseName = whName,
                ActualQty = bin.ActualQty,
                PlannedQty = bin.PlannedQty,
                IndentedQty = bin.IndentedQty,
                OrderedQty = bin.OrderedQty,
                ReservedQty = bin.ReservedQty,
                ReservedQtyForProduction = bin.ReservedQtyForProduction,
                ReservedQtyForSubContract = bin.ReservedQtyForSubContract,
                ReservedQtyForProductionPlan = bin.ReservedQtyForProductionPlan,
                TotalReservedQty = totalReserved,
                ProjectedQty = projectedQty,
                ReorderLevel = reorderLevel,
                ReorderQty = reorderQty,
                ShortageQty = shortageQty,
                HasShortage = hasShortage,
            });
        }

        rows = rows.OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ToList();

        return new StockProjectedQtyReportDto
        {
            Rows = rows,
            ShortageCount = rows.Count(r => r.HasShortage),
            TotalActualQty = rows.Sum(r => r.ActualQty),
            TotalProjectedQty = rows.Sum(r => r.ProjectedQty),
            TotalShortageQty = rows.Sum(r => r.ShortageQty),
        };
    }

    private async Task<List<Guid>> ResolveWarehouseIdsAsync(Guid? warehouseId, Guid? companyId)
    {
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        if (companyId.HasValue && companyId.Value != Guid.Empty)
        {
            warehouseQuery = warehouseQuery.Where(w => w.CompanyId == companyId.Value);
        }
        var allWarehouses = warehouseQuery.ToList();

        if (!warehouseId.HasValue || warehouseId.Value == Guid.Empty)
        {
            return allWarehouses.Where(w => !w.IsGroup && w.IsActive).Select(w => w.Id).ToList();
        }

        var selected = allWarehouses.FirstOrDefault(w => w.Id == warehouseId.Value);
        if (selected == null)
        {
            return new List<Guid> { warehouseId.Value };
        }

        if (!selected.IsGroup)
        {
            return new List<Guid> { selected.Id };
        }

        var result = new HashSet<Guid>();
        void AddChildren(Guid parentId)
        {
            foreach (var child in allWarehouses.Where(w => w.ParentWarehouseId == parentId && w.IsActive))
            {
                if (child.IsGroup)
                {
                    AddChildren(child.Id);
                }
                else
                {
                    result.Add(child.Id);
                }
            }
        }
        AddChildren(selected.Id);

        return result.ToList();
    }
}
