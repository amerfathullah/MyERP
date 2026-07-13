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

    public StockBalanceAppService(IRepository<Bin, Guid> binRepository)
    {
        _binRepository = binRepository;
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

        return new PagedResultDto<StockBalanceDto>(
            totalCount,
            items.Select(b => new StockBalanceDto
            {
                Id = b.Id,
                ItemId = b.ItemId,
                WarehouseId = b.WarehouseId,
                ActualQty = b.ActualQty,
                OrderedQty = b.OrderedQty,
                PlannedQty = b.PlannedQty,
                ReservedQty = b.ReservedQty,
                IndentedQty = b.IndentedQty,
                ProjectedQty = b.ProjectedQty,
                StockValue = b.StockValue,
                ValuationRate = b.ValuationRate,
            }).ToList());
    }

    /// <summary>
    /// Get a single item's stock across all warehouses.
    /// </summary>
    public async Task<List<StockBalanceDto>> GetItemStockAsync(Guid itemId)
    {
        var query = await _binRepository.GetQueryableAsync();
        var bins = query.Where(b => b.ItemId == itemId && b.ActualQty != 0).ToList();

        return bins.Select(b => new StockBalanceDto
        {
            Id = b.Id,
            ItemId = b.ItemId,
            WarehouseId = b.WarehouseId,
            ActualQty = b.ActualQty,
            OrderedQty = b.OrderedQty,
            PlannedQty = b.PlannedQty,
            ReservedQty = b.ReservedQty,
            IndentedQty = b.IndentedQty,
            ProjectedQty = b.ProjectedQty,
            StockValue = b.StockValue,
            ValuationRate = b.ValuationRate,
        }).ToList();
    }
}

// DTOs

public class StockBalanceDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
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
