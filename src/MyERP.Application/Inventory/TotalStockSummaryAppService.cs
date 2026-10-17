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

[Authorize(MyERPPermissions.StockEntries.Default)]
public class TotalStockSummaryAppService : ApplicationService, ITotalStockSummaryAppService
{
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public TotalStockSummaryAppService(
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

    public async Task<TotalStockSummaryReportDto> GetReportAsync(TotalStockSummaryFilterDto input)
    {
        var groupBy = string.Equals(input.GroupBy, "Company", StringComparison.OrdinalIgnoreCase) ? "Company" : "Warehouse";

        var binQuery = await _binRepository.GetQueryableAsync();
        binQuery = binQuery.Where(b => b.ActualQty != 0);

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var companyQuery = await _companyRepository.GetQueryableAsync();

        if (input.WarehouseId.HasValue && input.WarehouseId != Guid.Empty)
        {
            binQuery = binQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        }

        if (input.CompanyId.HasValue && input.CompanyId != Guid.Empty)
        {
            var companyWhIds = whQuery.Where(w => w.CompanyId == input.CompanyId.Value).Select(w => w.Id);
            binQuery = binQuery.Where(b => companyWhIds.Contains(b.WarehouseId));
        }

        if (input.ItemGroupId.HasValue && input.ItemGroupId != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }

        var bins = binQuery.ToList();
        if (bins.Count == 0)
        {
            return new TotalStockSummaryReportDto
            {
                GroupBy = groupBy
            };
        }

        var itemIds = bins.Select(b => b.ItemId).Distinct().ToList();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id)).ToDictionary(i => i.Id);

        var whIds = bins.Select(b => b.WarehouseId).Distinct().ToList();
        var warehouses = whQuery.Where(w => whIds.Contains(w.Id)).ToDictionary(w => w.Id);

        var companyIds = warehouses.Values.Select(w => w.CompanyId).Distinct().ToList();
        var companies = companyQuery.Where(c => companyIds.Contains(c.Id)).ToDictionary(c => c.Id);

        var rows = new List<TotalStockSummaryRowDto>();

        if (groupBy == "Company")
        {
            var grouped = bins
                .Where(b => items.ContainsKey(b.ItemId) && warehouses.ContainsKey(b.WarehouseId))
                .GroupBy(b => new
                {
                    CompanyId = warehouses[b.WarehouseId].CompanyId,
                    b.ItemId
                });

            foreach (var g in grouped)
            {
                var item = items[g.Key.ItemId];
                var companyName = companies.TryGetValue(g.Key.CompanyId, out var comp) ? comp.Name : "Unknown Company";
                var currentQty = g.Sum(b => b.ActualQty);
                var stockValue = g.Sum(b => b.StockValue);

                rows.Add(new TotalStockSummaryRowDto
                {
                    GroupName = companyName,
                    GroupId = g.Key.CompanyId,
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Description = item.Description,
                    ItemGroup = item.ItemGroup,
                    Uom = item.Uom,
                    CurrentQty = currentQty,
                    StockValue = stockValue,
                    ValuationRate = currentQty != 0 ? Math.Round(stockValue / currentQty, 4) : 0m
                });
            }
        }
        else
        {
            var grouped = bins
                .Where(b => items.ContainsKey(b.ItemId) && warehouses.ContainsKey(b.WarehouseId))
                .GroupBy(b => new
                {
                    b.WarehouseId,
                    b.ItemId
                });

            foreach (var g in grouped)
            {
                var item = items[g.Key.ItemId];
                var wh = warehouses[g.Key.WarehouseId];
                var currentQty = g.Sum(b => b.ActualQty);
                var stockValue = g.Sum(b => b.StockValue);

                rows.Add(new TotalStockSummaryRowDto
                {
                    GroupName = wh.Name,
                    GroupId = wh.Id,
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    Description = item.Description,
                    ItemGroup = item.ItemGroup,
                    Uom = item.Uom,
                    CurrentQty = currentQty,
                    StockValue = stockValue,
                    ValuationRate = currentQty != 0 ? Math.Round(stockValue / currentQty, 4) : 0m
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                r.GroupName.ToLowerInvariant().Contains(term) ||
                (!string.IsNullOrEmpty(r.Description) && r.Description.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        rows = rows.OrderBy(r => r.GroupName).ThenBy(r => r.ItemCode).ToList();

        return new TotalStockSummaryReportDto
        {
            GroupBy = groupBy,
            Rows = rows,
            TotalItems = rows.Select(r => r.ItemId).Distinct().Count(),
            TotalQuantity = rows.Sum(r => r.CurrentQty),
            TotalStockValue = rows.Sum(r => r.StockValue)
        };
    }
}
