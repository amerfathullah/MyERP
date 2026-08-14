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
public class StockBalanceAppService : ApplicationService, IStockBalanceAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;

    public StockBalanceAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Batch, Guid> batchRepository)
    {
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _sleRepository = sleRepository;
        _batchRepository = batchRepository;
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

        // Per ERPNext PR #57458: include zero stock items by default
        if (input.ExcludeZeroStock)
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

    /// <summary>
    /// Batch-Wise Stock Balance: shows per-batch qty across warehouses.
    /// Per ERPNext stock/report/batch_wise_balance_history: aggregates SLE by batch.
    /// </summary>
    public async Task<BatchWiseBalanceReportDto> GetBatchWiseBalanceAsync(GetBatchWiseBalanceRequestDto input)
    {
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => s.BatchId != null && !s.IsCancelled);

        if (input.ItemId.HasValue)
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);
        if (input.WarehouseId.HasValue)
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);
        if (input.FromDate.HasValue)
            sleQuery = sleQuery.Where(s => s.PostingDate >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            sleQuery = sleQuery.Where(s => s.PostingDate <= input.ToDate.Value);

        // Aggregate by (item, batch, warehouse) — net qty from SLE
        var grouped = sleQuery
            .GroupBy(s => new { s.ItemId, s.BatchId, s.WarehouseId })
            .Select(g => new
            {
                g.Key.ItemId,
                BatchId = g.Key.BatchId!.Value,
                g.Key.WarehouseId,
                Balance = g.Sum(s => s.QuantityChange),
                StockValue = g.Sum(s => s.QuantityChange * s.ValuationRate),
            })
            .ToList();

        // Filter out zero-balance batches unless requested
        if (!input.IncludeZeroBalance)
            grouped = grouped.Where(g => g.Balance != 0).ToList();

        // Resolve names
        var itemIds = grouped.Select(g => g.ItemId).Distinct().ToList();
        var batchIds = grouped.Select(g => g.BatchId).Distinct().ToList();
        var warehouseIds = grouped.Select(g => g.WarehouseId).Distinct().ToList();

        var itemQ = await _itemRepository.GetQueryableAsync();
        var itemMap = itemQ.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id, i => $"{i.ItemCode} — {i.ItemName}");

        var batchQ = await _batchRepository.GetQueryableAsync();
        var batchMap = batchQ.Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo, b.ExpiryDate, b.IsDisabled }).ToList()
            .ToDictionary(b => b.Id);

        var whQ = await _warehouseRepository.GetQueryableAsync();
        var whMap = whQ.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        var rows = grouped.Select(g =>
        {
            var batch = batchMap.GetValueOrDefault(g.BatchId);
            return new BatchWiseBalanceRowDto
            {
                ItemId = g.ItemId,
                ItemName = itemMap.GetValueOrDefault(g.ItemId, "—"),
                BatchId = g.BatchId,
                BatchNo = batch?.BatchNo ?? "—",
                WarehouseId = g.WarehouseId,
                WarehouseName = whMap.GetValueOrDefault(g.WarehouseId, "—"),
                Balance = g.Balance,
                StockValue = g.StockValue,
                ExpiryDate = batch?.ExpiryDate,
                IsExpired = batch?.ExpiryDate.HasValue == true && batch.ExpiryDate < DateTime.UtcNow.Date,
                IsDisabled = batch?.IsDisabled ?? false,
            };
        })
        .OrderBy(r => r.ItemName).ThenBy(r => r.BatchNo).ThenBy(r => r.WarehouseName)
        .ToList();

        return new BatchWiseBalanceReportDto
        {
            Rows = rows,
            TotalBatches = rows.Select(r => r.BatchId).Distinct().Count(),
            TotalQuantity = rows.Sum(r => r.Balance),
            TotalStockValue = rows.Sum(r => r.StockValue),
            ExpiredBatchCount = rows.Count(r => r.IsExpired),
        };
    }
}

