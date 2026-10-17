using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Item Shortage Report application service.
/// Identifies active items where projected stock quantity is negative in warehouse bins.
/// Maps to ERPNext stock/report/item_shortage_report/item_shortage_report.py.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class ItemShortageReportAppService : ApplicationService, IItemShortageReportAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public ItemShortageReportAppService(
        IRepository<Bin, Guid> binRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _binRepository = binRepository;
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
    }

    public async Task<ItemShortageReportDto> GetReportAsync(ItemShortageReportRequestDto input)
    {
        var company = await _companyRepository.FindAsync(input.CompanyId);
        var companyName = company?.Name ?? string.Empty;

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var companyWhQuery = whQuery.Where(w => w.CompanyId == input.CompanyId);
        if (input.WarehouseId.HasValue)
        {
            companyWhQuery = companyWhQuery.Where(w => w.Id == input.WarehouseId.Value);
        }

        var warehouses = companyWhQuery.ToList();
        if (warehouses.Count == 0)
        {
            return new ItemShortageReportDto
            {
                CompanyId = input.CompanyId,
                CompanyName = companyName
            };
        }

        var warehouseIds = warehouses.Select(w => w.Id).ToHashSet();
        var warehouseDict = warehouses.ToDictionary(w => w.Id, w => w.Name ?? w.WarehouseCode ?? string.Empty);

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var activeItems = itemQuery.Where(i => i.IsActive).ToList();
        var itemDict = activeItems.ToDictionary(i => i.Id);

        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery.Where(b => warehouseIds.Contains(b.WarehouseId)).ToList();

        var shortageBins = bins
            .Where(b => b.ProjectedQty < 0 && itemDict.ContainsKey(b.ItemId))
            .OrderBy(b => b.ProjectedQty)
            .ToList();

        var rows = shortageBins.Select(b =>
        {
            var item = itemDict[b.ItemId];
            return new ItemShortageReportRowDto
            {
                WarehouseId = b.WarehouseId,
                WarehouseName = warehouseDict.TryGetValue(b.WarehouseId, out var whName) ? whName : string.Empty,
                ItemId = b.ItemId,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                ActualQty = b.ActualQty,
                OrderedQty = b.OrderedQty,
                PlannedQty = b.PlannedQty,
                ReservedQty = b.ReservedQty,
                ReservedQtyForProduction = b.ReservedQtyForProduction,
                ProjectedQty = b.ProjectedQty,
                CompanyId = input.CompanyId,
                CompanyName = companyName
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(input.SearchText))
        {
            var search = input.SearchText.Trim();
            rows = rows.Where(r =>
                r.ItemCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.WarehouseName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(r.Description) && r.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        var chartData = rows.Take(10).Select(r => new ItemShortageChartItemDto
        {
            ItemCode = r.ItemCode,
            ProjectedQty = r.ProjectedQty
        }).ToList();

        return new ItemShortageReportDto
        {
            CompanyId = input.CompanyId,
            CompanyName = companyName,
            Rows = rows,
            ChartData = chartData,
            TotalShortageItems = rows.Count,
            TotalProjectedDeficit = rows.Sum(r => r.ProjectedQty),
            TotalActualQty = rows.Sum(r => r.ActualQty),
            TotalOrderedQty = rows.Sum(r => r.OrderedQty),
            TotalPlannedQty = rows.Sum(r => r.PlannedQty),
            TotalReservedQty = rows.Sum(r => r.ReservedQty + r.ReservedQtyForProduction)
        };
    }
}
