using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class WarehouseWiseItemBalanceAgeAndValueAppService : ApplicationService, IWarehouseWiseItemBalanceAgeAndValueAppService
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;

    public WarehouseWiseItemBalanceAgeAndValueAppService(
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Batch, Guid> batchRepository)
    {
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
        _sleRepository = sleRepository;
        _batchRepository = batchRepository;
    }

    public async Task<WarehouseWiseItemBalanceAgeAndValueReportDto> GetReportAsync(WarehouseWiseItemBalanceAgeAndValueFilterDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new UserFriendlyException("Company is required.");
        }

        var toDate = input.ToDate?.Date ?? DateTime.UtcNow.Date;

        // 1. Fetch leaf warehouses for company
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouses = whQuery
            .Where(w => w.CompanyId == input.CompanyId && !w.IsGroup && w.IsActive)
            .OrderBy(w => w.Name)
            .ToList();

        if (input.WarehouseId.HasValue && input.WarehouseId != Guid.Empty)
        {
            warehouses = warehouses.Where(w => w.Id == input.WarehouseId.Value).ToList();
        }

        if (warehouses.Count == 0)
        {
            return new WarehouseWiseItemBalanceAgeAndValueReportDto
            {
                CompanyId = input.CompanyId,
                AsOfDate = toDate
            };
        }

        var warehouseIds = warehouses.Select(w => w.Id).ToHashSet();
        var warehouseColumns = warehouses.Select(w => new WarehouseColumnDto
        {
            WarehouseId = w.Id,
            WarehouseName = w.Name
        }).ToList();

        // 2. Fetch items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemQueryable = itemQuery.Where(i => i.IsActive && i.MaintainStock);

        if (input.ItemId.HasValue && input.ItemId != Guid.Empty)
        {
            itemQueryable = itemQueryable.Where(i => i.Id == input.ItemId.Value);
        }

        if (input.ItemGroupId.HasValue && input.ItemGroupId != Guid.Empty)
        {
            itemQueryable = itemQueryable.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }

        var items = itemQueryable.ToList();
        if (items.Count == 0)
        {
            return new WarehouseWiseItemBalanceAgeAndValueReportDto
            {
                CompanyId = input.CompanyId,
                AsOfDate = toDate,
                WarehouseColumns = warehouseColumns
            };
        }

        var itemMap = items.ToDictionary(i => i.Id);
        var itemIds = itemMap.Keys.ToHashSet();

        // 3. Resolve item groups
        var itemGroupQuery = await _itemGroupRepository.GetQueryableAsync();
        var itemGroupMap = itemGroupQuery
            .Select(g => new { g.Id, g.Name })
            .ToDictionary(g => g.Id, g => g.Name);

        // 4. Batchwise valuation mapping
        var batchQuery = await _batchRepository.GetQueryableAsync();
        var batchUseValuationMap = batchQuery
            .Where(b => itemIds.Contains(b.ItemId))
            .Select(b => new { b.Id, b.UseBatchwiseValuation })
            .ToDictionary(b => b.Id, b => b.UseBatchwiseValuation);

        // 5. Fetch SLEs up to toDate
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var sles = sleQuery
            .Where(s => s.CompanyId == input.CompanyId
                     && !s.IsCancelled
                     && s.PostingDate <= toDate
                     && itemIds.Contains(s.ItemId)
                     && warehouseIds.Contains(s.WarehouseId))
            .OrderBy(s => s.PostingDate)
            .ThenBy(s => s.CreationTime)
            .ToList();

        // 6. Run FifoSlotsSimulator for item age and stock values
        var simulationEntries = sles.Select(sle =>
        {
            var item = itemMap[sle.ItemId];
            var useBatchwise = sle.BatchId.HasValue && batchUseValuationMap.GetValueOrDefault(sle.BatchId.Value, false);

            return new FifoSlotsSimulator.SimulationEntry
            {
                ItemId = sle.ItemId,
                WarehouseId = sle.WarehouseId,
                PostingDate = sle.PostingDate,
                CreationTime = sle.CreationTime,
                ActualQty = sle.QuantityChange,
                StockValueDifference = sle.StockValueDifference,
                ValuationRate = sle.ValuationRate,
                BatchId = sle.BatchId,
                SerialNo = sle.SerialNoId?.ToString(),
                HasBatchNo = item.HasBatchNo,
                HasSerialNo = item.HasSerialNo,
                UseBatchwiseValuation = useBatchwise,
                ValuationMethod = item.ValuationMethod.ToString()
            };
        });

        var simResults = FifoSlotsSimulator.Simulate(
            simulationEntries,
            toDate,
            showWarehouseWise: false,
            rangeString: null);

        var simResultMap = simResults.ToDictionary(r => r.ItemId);

        // 7. Calculate warehouse balances per item
        var itemWhBalances = new Dictionary<Guid, Dictionary<Guid, decimal>>();
        foreach (var sle in sles)
        {
            if (!itemWhBalances.TryGetValue(sle.ItemId, out var whDict))
            {
                whDict = new Dictionary<Guid, decimal>();
                itemWhBalances[sle.ItemId] = whDict;
            }

            whDict[sle.WarehouseId] = whDict.GetValueOrDefault(sle.WarehouseId, 0m) + sle.QuantityChange;
        }

        // 8. Build rows
        var rows = new List<WarehouseWiseItemBalanceAgeAndValueRowDto>();

        foreach (var item in items)
        {
            var sim = simResultMap.GetValueOrDefault(item.Id);
            var totalQty = sim?.TotalQty ?? 0m;
            var totalVal = sim?.TotalStockValue ?? 0m;
            var avgAge = sim?.AverageAgeDays ?? 0m;

            var whBalances = itemWhBalances.GetValueOrDefault(item.Id, new Dictionary<Guid, decimal>());

            if (input.FilterTotalZeroQty && totalQty == 0m)
            {
                continue;
            }

            rows.Add(new WarehouseWiseItemBalanceAgeAndValueRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemGroup = item.ItemGroupId.HasValue ? itemGroupMap.GetValueOrDefault(item.ItemGroupId.Value) : item.ItemGroup,
                Brand = item.Brand,
                StockUom = item.Uom,
                TotalStockValue = totalVal,
                AverageAgeDays = avgAge,
                TotalQty = totalQty,
                WarehouseBalances = warehouseColumns.ToDictionary(
                    c => c.WarehouseId,
                    c => whBalances.GetValueOrDefault(c.WarehouseId, 0m))
            });
        }

        // 9. Search term filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            rows = rows.Where(r =>
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                (!string.IsNullOrEmpty(r.ItemGroup) && r.ItemGroup.ToLowerInvariant().Contains(term)) ||
                (!string.IsNullOrEmpty(r.Brand) && r.Brand.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        rows = rows.OrderBy(r => r.ItemCode).ToList();

        var totalItems = rows.Count;
        var overallTotalQty = rows.Sum(r => r.TotalQty);
        var overallTotalVal = rows.Sum(r => r.TotalStockValue);
        var overallAvgAge = rows.Count > 0 ? Math.Round(rows.Average(r => r.AverageAgeDays), 2) : 0m;

        return new WarehouseWiseItemBalanceAgeAndValueReportDto
        {
            CompanyId = input.CompanyId,
            AsOfDate = toDate,
            WarehouseColumns = warehouseColumns,
            Rows = rows,
            TotalItems = totalItems,
            TotalQuantity = overallTotalQty,
            TotalStockValue = overallTotalVal,
            AverageAgeDays = overallAvgAge
        };
    }
}
