using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class StockBalanceAppService : ApplicationService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public StockBalanceAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// Get stock balance report — all Bins with their projected quantities.
    /// </summary>
    public async Task<PagedResultDto<StockBalanceDto>> GetStockBalanceAsync(GetStockBalanceRequestDto input)
    {
        var query = await _binRepository.GetQueryableAsync();

        if (input.ItemId.HasValue)
            query = query.Where(b => b.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == input.WarehouseId.Value);

        // Only show bins with non-zero quantities
        query = query.Where(b => b.ActualQty != 0 || b.OrderedQty != 0 || b.ReservedQty != 0 || b.PlannedQty != 0);

        var totalCount = query.Count();
        var items = query
            .OrderBy(b => b.ItemId)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = items.Select(ObjectMapper.Map<Bin, StockBalanceDto>).ToList();

        // Resolve item and warehouse names
        var itemIds = dtos.Select(d => d.ItemId).Distinct().ToList();
        var warehouseIds = dtos.Select(d => d.WarehouseId).Distinct().ToList();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id, i => $"{i.ItemCode} — {i.ItemName}");

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouseNames = whQuery.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        foreach (var dto in dtos)
        {
            dto.ItemName = itemNames.GetValueOrDefault(dto.ItemId, dto.ItemId.ToString()[..8]);
            dto.WarehouseName = warehouseNames.GetValueOrDefault(dto.WarehouseId, dto.WarehouseId.ToString()[..8]);
        }

        return new PagedResultDto<StockBalanceDto>(totalCount, dtos);
    }

    /// <summary>
    /// Get a single item's stock across all warehouses.
    /// </summary>
    public async Task<List<StockBalanceDto>> GetItemStockAsync(Guid itemId)
    {
        var query = await _binRepository.GetQueryableAsync();
        var bins = query.Where(b => b.ItemId == itemId && b.ActualQty != 0).ToList();

        var dtos = bins.Select(ObjectMapper.Map<Bin, StockBalanceDto>).ToList();

        // Resolve warehouse names
        var warehouseIds = dtos.Select(d => d.WarehouseId).Distinct().ToList();
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouseNames = whQuery.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        foreach (var dto in dtos)
        {
            dto.WarehouseName = warehouseNames.GetValueOrDefault(dto.WarehouseId, dto.WarehouseId.ToString()[..8]);
        }

        return dtos;
    }

    /// <summary>
    /// Batch stock availability check for multiple items — used by transaction forms
    /// to show real-time stock alongside item selection. Per ERPNext update_bin_details.
    /// Returns projected qty per item (across all company warehouses).
    /// </summary>
    public async Task<List<ItemAvailabilityDto>> GetItemsAvailabilityAsync(GetItemsAvailabilityInput input)
    {
        if (input.ItemIds == null || input.ItemIds.Count == 0)
            return new List<ItemAvailabilityDto>();

        var query = await _binRepository.GetQueryableAsync();

        // Filter by company warehouses if companyId provided
        IQueryable<Bin> binQuery = query.Where(b => input.ItemIds.Contains(b.ItemId));

        if (input.WarehouseId.HasValue)
        {
            binQuery = binQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        }

        var bins = binQuery.ToList();

        // Aggregate per item (sum across all matching warehouses)
        var grouped = bins.GroupBy(b => b.ItemId).Select(g => new ItemAvailabilityDto
        {
            ItemId = g.Key,
            ActualQty = g.Sum(b => b.ActualQty),
            ReservedQty = g.Sum(b => b.ReservedQty),
            OrderedQty = g.Sum(b => b.OrderedQty),
            ProjectedQty = g.Sum(b => b.ProjectedQty),
            AvailableQty = g.Sum(b => b.ActualQty) - g.Sum(b => b.ReservedQty),
        }).ToList();

        // Items not in Bin table (zero stock) — include with zero values
        var foundItemIds = grouped.Select(g => g.ItemId).ToHashSet();
        foreach (var itemId in input.ItemIds.Where(id => !foundItemIds.Contains(id)))
        {
            grouped.Add(new ItemAvailabilityDto { ItemId = itemId });
        }

        return grouped;
    }
}

// DTOs

public class StockBalanceDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? ItemName { get; set; }
    public string? WarehouseName { get; set; }
    public decimal ActualQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal IndentedQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal StockValue { get; set; }
    public decimal ValuationRate { get; set; }
}

public class GetStockBalanceRequestDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
}

/// <summary>Input for batch item availability check.</summary>
public class GetItemsAvailabilityInput
{
    public List<Guid> ItemIds { get; set; } = new();
    public Guid? WarehouseId { get; set; }
}

/// <summary>Per-item stock availability summary (aggregated across warehouses).</summary>
public class ItemAvailabilityDto
{
    public Guid ItemId { get; set; }
    public decimal ActualQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal ProjectedQty { get; set; }
    /// <summary>Available for new orders = Actual - Reserved</summary>
    public decimal AvailableQty { get; set; }
}
