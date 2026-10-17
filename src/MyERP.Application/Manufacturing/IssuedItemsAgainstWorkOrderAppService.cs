using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Issued Items Against Work Order — tracks all raw materials transferred/issued to active Work Orders,
/// including current projected stock at the source warehouse.
/// Maps to ERPNext manufacturing/report/issued_items_against_work_order/issued_items_against_work_order.json.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class IssuedItemsAgainstWorkOrderAppService : ApplicationService, IIssuedItemsAgainstWorkOrderAppService
{
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Bin, Guid> _binRepository;

    public IssuedItemsAgainstWorkOrderAppService(
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Bin, Guid> binRepository)
    {
        _stockEntryRepository = stockEntryRepository;
        _workOrderRepository = workOrderRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _binRepository = binRepository;
    }

    public async Task<IssuedItemsAgainstWorkOrderReportDto> GetReportAsync(IssuedItemsAgainstWorkOrderRequestDto input)
    {
        var fromDate = input.FromDate?.Date ?? DateTime.UtcNow.AddMonths(-1).Date;
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

        var seQuery = await _stockEntryRepository.WithDetailsAsync(se => se.Items);
        var query = seQuery
            .Where(se => se.CompanyId == input.CompanyId)
            .Where(se => se.WorkOrderId.HasValue && !se.IsReturn)
            .Where(se => se.Status == DocumentStatus.Submitted || se.Status == DocumentStatus.Posted)
            .Where(se => se.PostingDate >= fromDate && se.PostingDate <= toDate)
            .Where(se => se.EntryType == StockEntryType.MaterialTransfer || se.EntryType == StockEntryType.MaterialIssue);

        if (input.WorkOrderId.HasValue)
        {
            query = query.Where(se => se.WorkOrderId == input.WorkOrderId.Value);
        }

        var stockEntries = query.OrderBy(se => se.PostingDate).ToList();
        if (!stockEntries.Any())
        {
            return new IssuedItemsAgainstWorkOrderReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = fromDate,
                ToDate = toDate
            };
        }

        var woIds = stockEntries.Select(se => se.WorkOrderId!.Value).Distinct().ToList();
        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var workOrders = woQuery.Where(w => woIds.Contains(w.Id)).ToList();
        var woMap = workOrders.ToDictionary(w => w.Id, w => w);

        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouses = whQuery.Where(w => w.CompanyId == input.CompanyId).ToList();
        var whMap = warehouses.ToDictionary(w => w.Id, w => w);

        var allItemIds = workOrders.Select(w => w.ItemId)
            .Concat(stockEntries.SelectMany(se => se.Items.Select(i => i.ItemId)))
            .Distinct()
            .ToList();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => allItemIds.Contains(i.Id)).ToList();
        var itemMap = items.ToDictionary(i => i.Id, i => i);

        var sourceWhIds = stockEntries
            .SelectMany(se => se.Items.Where(i => i.SourceWarehouseId.HasValue).Select(i => i.SourceWarehouseId!.Value))
            .Distinct()
            .ToList();

        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery
            .Where(b => sourceWhIds.Contains(b.WarehouseId))
            .ToList();

        var binMap = bins
            .GroupBy(b => (b.ItemId, b.WarehouseId))
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<IssuedItemsAgainstWorkOrderRowDto>();

        foreach (var se in stockEntries)
        {
            var wo = woMap.GetValueOrDefault(se.WorkOrderId!.Value);
            var woNumber = wo?.WorkOrderNumber ?? string.Empty;
            var fgItem = wo != null ? itemMap.GetValueOrDefault(wo.ItemId) : null;
            var fgCode = fgItem?.ItemCode ?? string.Empty;
            var fgName = fgItem?.ItemName ?? string.Empty;
            var fgId = wo?.ItemId ?? Guid.Empty;

            foreach (var item in se.Items)
            {
                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value)
                {
                    continue;
                }

                if (input.SourceWarehouseId.HasValue && item.SourceWarehouseId != input.SourceWarehouseId.Value)
                {
                    continue;
                }

                var rmItem = itemMap.GetValueOrDefault(item.ItemId);
                var rmCode = rmItem?.ItemCode ?? string.Empty;
                var rmName = rmItem?.ItemName ?? string.Empty;
                var desc = rmItem?.Description;

                var srcWh = item.SourceWarehouseId.HasValue ? whMap.GetValueOrDefault(item.SourceWarehouseId.Value) : null;
                var tgtWh = item.TargetWarehouseId.HasValue ? whMap.GetValueOrDefault(item.TargetWarehouseId.Value) : null;

                decimal projectedQty = 0;
                decimal actualQty = 0;
                if (item.SourceWarehouseId.HasValue && binMap.TryGetValue((item.ItemId, item.SourceWarehouseId.Value), out var bin))
                {
                    projectedQty = bin.ProjectedQty;
                    actualQty = bin.ActualQty;
                }

                var entryNumber = se.EntryNumber ?? se.Id.ToString("N")[..8];

                rows.Add(new IssuedItemsAgainstWorkOrderRowDto
                {
                    WorkOrderId = se.WorkOrderId!.Value,
                    WorkOrderNumber = woNumber,
                    IssueDate = se.PostingDate,
                    StockEntryId = se.Id,
                    StockEntryNumber = entryNumber,
                    FinishedGoodItemId = fgId,
                    FinishedGoodCode = fgCode,
                    FinishedGoodName = fgName,
                    ItemId = item.ItemId,
                    ItemCode = rmCode,
                    ItemName = rmName,
                    Description = desc,
                    Quantity = item.Quantity,
                    Uom = item.StockUom,
                    Amount = item.Amount,
                    SourceWarehouseId = item.SourceWarehouseId,
                    SourceWarehouseName = srcWh?.Name,
                    TargetWarehouseId = item.TargetWarehouseId,
                    TargetWarehouseName = tgtWh?.Name,
                    ProjectedQtyAtSource = Math.Round(projectedQty, 2),
                    ActualQtyAtSource = Math.Round(actualQty, 2)
                });
            }
        }

        var orderedRows = rows
            .OrderBy(r => r.IssueDate)
            .ThenBy(r => r.WorkOrderNumber)
            .ThenBy(r => r.ItemCode)
            .ToList();

        return new IssuedItemsAgainstWorkOrderReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = fromDate,
            ToDate = toDate,
            Rows = orderedRows,
            TotalWorkOrders = orderedRows.Select(r => r.WorkOrderId).Distinct().Count(),
            TotalIssuedLines = orderedRows.Count,
            TotalQuantity = orderedRows.Sum(r => r.Quantity),
            TotalAmount = Math.Round(orderedRows.Sum(r => r.Amount), 2)
        };
    }
}
