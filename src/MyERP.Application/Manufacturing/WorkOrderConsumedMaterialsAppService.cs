using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Work Order Consumed Materials report — analyzes raw materials transferred, consumed, returned, and extra consumed across Work Orders.
/// Maps to ERPNext manufacturing/report/work_order_consumed_materials/work_order_consumed_materials.py.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class WorkOrderConsumedMaterialsAppService : ApplicationService, IWorkOrderConsumedMaterialsAppService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public WorkOrderConsumedMaterialsAppService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _workOrderRepository = workOrderRepository;
        _stockEntryRepository = stockEntryRepository;
        _itemRepository = itemRepository;
    }

    public async Task<WorkOrderConsumedMaterialsReportDto> GetReportAsync(WorkOrderConsumedMaterialsRequestDto input)
    {
        var fromDate = input.FromDate?.Date ?? DateTime.UtcNow.AddMonths(-1).Date;
        var toDate = input.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

        var woQuery = await _workOrderRepository.WithDetailsAsync(wo => wo.RequiredItems);
        var query = woQuery
            .Where(w => w.CompanyId == input.CompanyId)
            .Where(w => w.CreationTime >= fromDate && w.CreationTime <= toDate);

        if (input.WorkOrderId.HasValue)
        {
            query = query.Where(w => w.Id == input.WorkOrderId.Value);
        }

        if (input.ProductionItemId.HasValue)
        {
            query = query.Where(w => w.ItemId == input.ProductionItemId.Value);
        }

        if (input.Status.HasValue)
        {
            query = query.Where(w => w.Status == input.Status.Value);
        }
        else
        {
            query = query.Where(w => w.Status == WorkOrderStatus.InProcess
                                  || w.Status == WorkOrderStatus.Completed
                                  || w.Status == WorkOrderStatus.Stopped);
        }

        var workOrders = query.OrderByDescending(w => w.CreationTime).ToList();
        if (!workOrders.Any())
        {
            return new WorkOrderConsumedMaterialsReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = fromDate,
                ToDate = toDate
            };
        }

        var woIds = workOrders.Select(w => w.Id).ToList();

        var seQuery = await _stockEntryRepository.WithDetailsAsync(se => se.Items);
        var returnEntries = seQuery
            .Where(se => se.WorkOrderId.HasValue && woIds.Contains(se.WorkOrderId.Value))
            .Where(se => se.IsReturn && (se.Status == DocumentStatus.Submitted || se.Status == DocumentStatus.Posted))
            .ToList();

        var returnQtyMap = new Dictionary<(Guid WorkOrderId, Guid ItemId), decimal>();
        foreach (var se in returnEntries)
        {
            foreach (var item in se.Items)
            {
                var key = (se.WorkOrderId!.Value, item.ItemId);
                returnQtyMap[key] = returnQtyMap.GetValueOrDefault(key) + item.Quantity;
            }
        }

        var allItemIds = workOrders.Select(w => w.ItemId)
            .Concat(workOrders.SelectMany(w => w.RequiredItems.Select(r => r.ItemId)))
            .Distinct()
            .ToList();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => allItemIds.Contains(i.Id)).ToList();
        var itemMap = items.ToDictionary(i => i.Id, i => i);

        var rows = new List<WorkOrderConsumedMaterialsRowDto>();

        foreach (var wo in workOrders)
        {
            var prodItem = itemMap.GetValueOrDefault(wo.ItemId);
            var prodItemCode = prodItem?.ItemCode ?? string.Empty;
            var prodItemName = prodItem?.ItemName ?? string.Empty;

            foreach (var reqItem in wo.RequiredItems)
            {
                var rmItem = itemMap.GetValueOrDefault(reqItem.ItemId);
                var rmCode = rmItem?.ItemCode ?? string.Empty;
                var rmName = rmItem?.ItemName ?? reqItem.ItemName;

                decimal extraConsumedQty;
                if (reqItem.IsAdditionalItem)
                {
                    extraConsumedQty = reqItem.ConsumedQuantity;
                }
                else if (reqItem.ConsumedQuantity > reqItem.RequiredQuantity)
                {
                    extraConsumedQty = reqItem.ConsumedQuantity - reqItem.RequiredQuantity;
                }
                else
                {
                    extraConsumedQty = 0m;
                }

                var returnedQty = returnQtyMap.GetValueOrDefault((wo.Id, reqItem.ItemId));

                if (input.ShowExtraConsumedMaterialsOnly && extraConsumedQty <= 0)
                {
                    continue;
                }

                rows.Add(new WorkOrderConsumedMaterialsRowDto
                {
                    WorkOrderId = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    Status = wo.Status.ToString(),
                    ProductionItemId = wo.ItemId,
                    ProductionItemCode = prodItemCode,
                    ProductionItemName = prodItemName,
                    QtyToProduce = wo.Quantity,
                    ProducedQty = wo.ProducedQuantity,
                    RawMaterialItemId = reqItem.ItemId,
                    RawMaterialItemCode = rmCode,
                    RawMaterialName = rmName,
                    StockUom = reqItem.StockUom ?? "Unit",
                    RequiredQty = reqItem.RequiredQuantity,
                    TransferredQty = reqItem.TransferredQuantity,
                    ConsumedQty = reqItem.ConsumedQuantity,
                    ExtraConsumedQty = extraConsumedQty,
                    ReturnedQty = returnedQty,
                    IsAdditionalItem = reqItem.IsAdditionalItem
                });
            }
        }

        var totalWorkOrders = rows.Select(r => r.WorkOrderId).Distinct().Count();
        var totalRawMaterials = rows.Count;
        var totalExtraItems = rows.Count(r => r.ExtraConsumedQty > 0);
        var totalRequiredQty = rows.Sum(r => r.RequiredQty);
        var totalConsumedQty = rows.Sum(r => r.ConsumedQty);
        var totalExtraConsumedQty = rows.Sum(r => r.ExtraConsumedQty);
        var totalReturnedQty = rows.Sum(r => r.ReturnedQty);

        return new WorkOrderConsumedMaterialsReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = fromDate,
            ToDate = toDate,
            Rows = rows,
            TotalWorkOrders = totalWorkOrders,
            TotalRawMaterialLines = totalRawMaterials,
            TotalExtraConsumedItems = totalExtraItems,
            TotalRequiredQty = totalRequiredQty,
            TotalConsumedQty = totalConsumedQty,
            TotalExtraConsumedQty = totalExtraConsumedQty,
            TotalReturnedQty = totalReturnedQty
        };
    }
}
