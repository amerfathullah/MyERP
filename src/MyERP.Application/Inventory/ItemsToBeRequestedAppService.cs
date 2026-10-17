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
/// Items To Be Requested Report service.
/// Identifies items where projected quantity in bin is negative, requiring procurement requisitions.
/// Ported from ERPNext stock/report/items_to_be_requested.
/// </summary>
[Authorize(MyERPPermissions.MaterialRequests.Default)]
public class ItemsToBeRequestedAppService : ApplicationService, IItemsToBeRequestedAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;

    public ItemsToBeRequestedAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository)
    {
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _itemGroupRepository = itemGroupRepository;
    }

    public async Task<ItemsToBeRequestedReportDto> GetReportAsync(ItemsToBeRequestedFilterDto input)
    {
        var binQueryable = await _binRepository.GetQueryableAsync();

        // ProjectedQty calculation: actual + ordered + indented + planned - reserved - reserved_prod - reserved_subcontract - reserved_pp
        var negativeBinQuery = binQueryable.Where(b =>
            (b.ActualQty + b.OrderedQty + b.IndentedQty + b.PlannedQty
             - b.ReservedQty - b.ReservedQtyForProduction - b.ReservedQtyForSubContract - b.ReservedQtyForProductionPlan) < 0m);

        if (input.WarehouseId.HasValue)
        {
            negativeBinQuery = negativeBinQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        }

        var candidateBins = negativeBinQuery.ToList();
        if (candidateBins.Count == 0)
        {
            return new ItemsToBeRequestedReportDto
            {
                CompanyId = input.CompanyId,
                WarehouseId = input.WarehouseId
            };
        }

        var candidateItemIds = candidateBins.Select(b => b.ItemId).Distinct().ToList();
        var candidateWarehouseIds = candidateBins.Select(b => b.WarehouseId).Distinct().ToList();

        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var itemsQuery = itemQueryable.Where(i => candidateItemIds.Contains(i.Id) && i.MaintainStock && i.IsActive);
        if (input.ItemGroupId.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }
        var items = itemsQuery.ToList().ToDictionary(i => i.Id);

        var warehouseQueryable = await _warehouseRepository.GetQueryableAsync();
        var whQuery = warehouseQueryable.Where(w => candidateWarehouseIds.Contains(w.Id) && w.IsActive && !w.IsGroup);
        if (input.CompanyId.HasValue)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        var warehouses = whQuery.ToList().ToDictionary(w => w.Id);

        var groupIds = items.Values.Where(i => i.ItemGroupId.HasValue).Select(i => i.ItemGroupId!.Value).Distinct().ToList();
        var groupQueryable = await _itemGroupRepository.GetQueryableAsync();
        var itemGroups = groupQueryable.Where(g => groupIds.Contains(g.Id)).ToList().ToDictionary(g => g.Id, g => g.Name);

        var reportItems = new List<ItemsToBeRequestedItemDto>();

        foreach (var bin in candidateBins)
        {
            if (!items.TryGetValue(bin.ItemId, out var item))
                continue;

            if (!warehouses.TryGetValue(bin.WarehouseId, out var warehouse))
                continue;

            var projectedQty = bin.ActualQty + bin.OrderedQty + bin.IndentedQty + bin.PlannedQty
                - bin.ReservedQty - bin.ReservedQtyForProduction - bin.ReservedQtyForSubContract - bin.ReservedQtyForProductionPlan;

            if (projectedQty >= 0m)
                continue;

            var shortfallQty = Math.Abs(projectedQty);
            var buyingPrice = item.StandardBuyingPrice ?? 0m;
            var estimatedCost = shortfallQty * buyingPrice;

            var itemGroupName = item.ItemGroup ?? (item.ItemGroupId.HasValue && itemGroups.TryGetValue(item.ItemGroupId.Value, out var gName) ? gName : string.Empty);

            reportItems.Add(new ItemsToBeRequestedItemDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemGroup = itemGroupName,
                ItemGroupId = item.ItemGroupId,
                Uom = item.Uom,
                WarehouseId = warehouse.Id,
                WarehouseName = warehouse.Name,
                CompanyId = warehouse.CompanyId,
                ActualQty = bin.ActualQty,
                IndentedQty = bin.IndentedQty,
                ReservedQty = bin.ReservedQty,
                OrderedQty = bin.OrderedQty,
                PlannedQty = bin.PlannedQty,
                ReservedQtyForProduction = bin.ReservedQtyForProduction,
                ProjectedQty = projectedQty,
                ShortfallQty = shortfallQty,
                ReorderLevel = item.ReorderLevel,
                SafetyStock = item.SafetyStock,
                StandardBuyingPrice = buyingPrice,
                EstimatedShortfallCost = estimatedCost,
                DefaultMaterialRequestType = item.DefaultMaterialRequestType.ToString()
            });
        }

        // Apply optional search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            reportItems = reportItems.Where(r =>
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Order by ProjectedQty ascending (most negative / urgent deficit first per ERPNext)
        reportItems = reportItems.OrderBy(r => r.ProjectedQty).ToList();

        return new ItemsToBeRequestedReportDto
        {
            CompanyId = input.CompanyId,
            WarehouseId = input.WarehouseId,
            Items = reportItems,
            TotalDeficitItems = reportItems.Count,
            TotalShortfallQty = reportItems.Sum(r => r.ShortfallQty),
            TotalEstimatedCost = reportItems.Sum(r => r.EstimatedShortfallCost)
        };
    }
}
