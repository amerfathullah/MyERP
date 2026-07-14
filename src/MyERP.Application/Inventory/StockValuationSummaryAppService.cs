using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Stock Valuation Summary — shows current stock value per item per warehouse.
/// Used for balance sheet preparation (Inventory asset = total stock value).
/// Per ERPNext: stock_balance report with valuation_rate × actual_qty per Bin.
/// </summary>
public class StockValuationSummaryAppService : ApplicationService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public StockValuationSummaryAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// Gets stock valuation for all items in a company, grouped by item + warehouse.
    /// Returns total inventory asset value for balance sheet reconciliation.
    /// </summary>
    public async Task<StockValuationSummaryDto> GetSummaryAsync(Guid companyId, Guid? warehouseId = null)
    {
        var binQuery = await _binRepository.GetQueryableAsync();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var whQuery = await _warehouseRepository.GetQueryableAsync();

        // Get all bins with positive stock
        var bins = binQuery
            .Where(b => b.ActualQty > 0)
            .ToList();

        // Filter by warehouse if specified
        if (warehouseId.HasValue)
        {
            var whIds = whQuery.Where(w => w.Id == warehouseId.Value || w.ParentWarehouseId == warehouseId.Value)
                .Select(w => w.Id).ToList();
            bins = bins.Where(b => whIds.Contains(b.WarehouseId)).ToList();
        }

        // Get item and warehouse names for display
        var itemIds = bins.Select(b => b.ItemId).Distinct().ToList();
        var warehouseIds = bins.Select(b => b.WarehouseId).Distinct().ToList();

        var items = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.Uom })
            .ToDictionary(i => i.Id);

        var warehouses = whQuery.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionary(w => w.Id);

        var rows = bins.Select(b =>
        {
            var item = items.GetValueOrDefault(b.ItemId);
            var wh = warehouses.GetValueOrDefault(b.WarehouseId);
            var stockValue = b.StockValue > 0 ? b.StockValue : b.ActualQty * b.ValuationRate;

            return new StockValuationRowDto
            {
                ItemId = b.ItemId,
                ItemCode = item?.ItemCode ?? "Unknown",
                ItemName = item?.ItemName ?? "Unknown",
                Uom = item?.Uom ?? "Unit",
                WarehouseId = b.WarehouseId,
                WarehouseName = wh?.Name ?? "Unknown",
                Quantity = b.ActualQty,
                ValuationRate = b.ValuationRate,
                StockValue = stockValue,
            };
        })
        .OrderBy(r => r.ItemCode)
        .ThenBy(r => r.WarehouseName)
        .ToList();

        return new StockValuationSummaryDto
        {
            CompanyId = companyId,
            TotalStockValue = rows.Sum(r => r.StockValue),
            TotalItems = rows.Select(r => r.ItemId).Distinct().Count(),
            TotalWarehouses = rows.Select(r => r.WarehouseId).Distinct().Count(),
            Rows = rows,
        };
    }
}

public class StockValuationSummaryDto
{
    public Guid CompanyId { get; set; }
    public decimal TotalStockValue { get; set; }
    public int TotalItems { get; set; }
    public int TotalWarehouses { get; set; }
    public List<StockValuationRowDto> Rows { get; set; } = new();
}

public class StockValuationRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Uom { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
}
