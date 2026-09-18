using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Stock Ageing Report application service.
/// Uses FifoSlotsSimulator to calculate exact stock age based on SLE FIFO/LIFO queue simulation.
/// Maps to ERPNext stock/report/stock_ageing/stock_ageing.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockAgeingAppService : ApplicationService, IStockAgeingAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;

    public StockAgeingAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<Batch, Guid> batchRepository)
    {
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _itemGroupRepository = itemGroupRepository;
        _batchRepository = batchRepository;
    }

    public async Task<StockAgeingReportDto> GetReportAsync(StockAgeingFilterDto input)
    {
        var toDate = input.ToDate?.Date ?? DateTime.UtcNow.Date;
        var bucketDefs = FifoSlotsSimulator.ParseRanges(input.Ranges);

        // 1. Fetch relevant warehouses for the company
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouses = whQuery
            .Where(w => w.CompanyId == input.CompanyId)
            .ToList();
        var warehouseMap = warehouses.ToDictionary(w => w.Id, w => w.Name ?? w.WarehouseCode);
        var companyWarehouseIds = warehouses.Select(w => w.Id).ToHashSet();

        if (input.WarehouseId.HasValue)
        {
            companyWarehouseIds.RemoveWhere(id => id != input.WarehouseId.Value);
        }

        if (companyWarehouseIds.Count == 0)
        {
            return new StockAgeingReportDto
            {
                ToDate = toDate,
                Buckets = bucketDefs.Select((b, i) => new StockAgeingBucketDefinitionDto
                {
                    BucketIndex = i,
                    Label = b.Label,
                    MinDays = b.MinDays,
                    MaxDays = b.MaxDays
                }).ToList()
            };
        }

        // 2. Fetch items matching company / filters
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemQueryable = itemQuery.Where(i => i.IsActive && i.MaintainStock);

        if (input.ItemId.HasValue)
        {
            itemQueryable = itemQueryable.Where(i => i.Id == input.ItemId.Value);
        }

        if (input.ItemGroupId.HasValue)
        {
            itemQueryable = itemQueryable.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            var txt = input.FilterText.Trim().ToLower();
            itemQueryable = itemQueryable.Where(i => i.ItemCode.ToLower().Contains(txt) || i.ItemName.ToLower().Contains(txt));
        }

        var items = itemQueryable.ToList();
        if (items.Count == 0)
        {
            return new StockAgeingReportDto
            {
                ToDate = toDate,
                Buckets = bucketDefs.Select((b, i) => new StockAgeingBucketDefinitionDto
                {
                    BucketIndex = i,
                    Label = b.Label,
                    MinDays = b.MinDays,
                    MaxDays = b.MaxDays
                }).ToList()
            };
        }

        var itemMap = items.ToDictionary(i => i.Id);
        var itemIds = itemMap.Keys.ToHashSet();

        // 3. Resolve Item Group and Brand labels
        var itemGroupQuery = await _itemGroupRepository.GetQueryableAsync();
        var itemGroupMap = itemGroupQuery
            .Select(g => new { g.Id, g.Name })
            .ToDictionary(g => g.Id, g => g.Name);

        // 4. Fetch batches for batchwise valuation
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
                     && companyWarehouseIds.Contains(s.WarehouseId))
            .OrderBy(s => s.PostingDate)
            .ThenBy(s => s.CreationTime)
            .ToList();

        // 6. Convert SLEs to SimulationEntries
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

        // 7. Run simulation
        var simulationResults = FifoSlotsSimulator.Simulate(
            simulationEntries,
            toDate,
            input.ShowWarehouseWiseStock,
            input.Ranges);

        // 8. Build report rows
        var rows = new List<StockAgeingRowDto>();

        foreach (var sim in simulationResults)
        {
            var item = itemMap.GetValueOrDefault(sim.ItemId);
            if (item == null) continue;

            if (!input.IncludeZeroStock && sim.TotalQty <= 0) continue;

            var warehouseName = sim.WarehouseId.HasValue
                ? warehouseMap.GetValueOrDefault(sim.WarehouseId.Value, "—")
                : null;

            var valuationRate = sim.TotalQty > 0 ? Math.Round(sim.TotalStockValue / sim.TotalQty, 4) : 0m;

            rows.Add(new StockAgeingRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Description = item.Description,
                ItemGroup = item.ItemGroupId.HasValue ? itemGroupMap.GetValueOrDefault(item.ItemGroupId.Value) : null,
                WarehouseId = sim.WarehouseId,
                WarehouseName = warehouseName,
                TotalQty = sim.TotalQty,
                ValuationRate = valuationRate,
                TotalStockValue = sim.TotalStockValue,
                AverageAgeDays = sim.AverageAgeDays,
                OldestDays = sim.OldestDays,
                NewestDays = sim.NewestDays,
                StockUom = item.Uom,
                Buckets = sim.Buckets.Select(b => new StockAgeingBucketValueDto
                {
                    BucketIndex = b.BucketIndex,
                    Label = b.Label,
                    Qty = b.Qty,
                    StockValue = b.StockValue
                }).ToList()
            });
        }

        // Sort by average age descending (oldest stock first)
        rows = rows.OrderByDescending(r => r.AverageAgeDays).ToList();

        var totalStockValue = rows.Sum(r => r.TotalStockValue);
        var totalQty = rows.Sum(r => r.TotalQty);
        var overallAvgAge = totalQty > 0
            ? Math.Round(rows.Sum(r => r.AverageAgeDays * r.TotalQty) / totalQty, 1)
            : 0m;

        var agedOver90 = rows.Count(r => r.AverageAgeDays > 90);

        return new StockAgeingReportDto
        {
            ToDate = toDate,
            Buckets = bucketDefs.Select((b, i) => new StockAgeingBucketDefinitionDto
            {
                BucketIndex = i,
                Label = b.Label,
                MinDays = b.MinDays,
                MaxDays = b.MaxDays
            }).ToList(),
            Rows = rows,
            TotalItems = rows.Count,
            TotalStockValue = totalStockValue,
            OverallAverageAgeDays = overallAvgAge,
            AgedOver90Count = agedOver90
        };
    }
}
