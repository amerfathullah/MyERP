using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// BOM Variance Report — compares planned raw material requirements against actual consumption
/// across completed/overproduced Work Orders.
/// ERPNext equivalent: manufacturing/report/bom_variance_report/bom_variance_report.py
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class BomVarianceReportAppService : ApplicationService, IBomVarianceReportAppService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public BomVarianceReportAppService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _workOrderRepository = workOrderRepository;
        _bomRepository = bomRepository;
        _itemRepository = itemRepository;
    }

    public async Task<BomVarianceReportDto> GetReportAsync(BomVarianceReportRequestDto input)
    {
        var woQuery = await _workOrderRepository.WithDetailsAsync(w => w.RequiredItems);
        var query = woQuery
            .Where(w => w.CompanyId == input.CompanyId)
            .Where(w => w.Status != WorkOrderStatus.Cancelled && w.Status != WorkOrderStatus.Draft);

        if (input.OnlyOverproduced)
        {
            query = query.Where(w => w.ProducedQuantity > w.Quantity);
        }

        if (input.BomId.HasValue)
        {
            query = query.Where(w => w.BomId == input.BomId.Value);
        }

        if (input.WorkOrderId.HasValue)
        {
            query = query.Where(w => w.Id == input.WorkOrderId.Value);
        }

        if (input.FromDate.HasValue)
        {
            var from = input.FromDate.Value.Date;
            query = query.Where(w => w.CreationTime.Date >= from);
        }

        if (input.ToDate.HasValue)
        {
            var to = input.ToDate.Value.Date;
            query = query.Where(w => w.CreationTime.Date <= to);
        }

        var workOrders = query.OrderBy(w => w.CreationTime).ToList();

        if (workOrders.Count == 0)
        {
            return new BomVarianceReportDto
            {
                CompanyId = input.CompanyId,
                Rows = new(),
                TotalWorkOrders = 0,
                TotalOrderedQty = 0,
                TotalProducedQty = 0,
                TotalRequiredQty = 0,
                TotalConsumedQty = 0,
                TotalVarianceQty = 0
            };
        }

        var bomIds = workOrders.Select(w => w.BomId).Distinct().ToList();
        var boms = await _bomRepository.GetListAsync(b => bomIds.Contains(b.Id));
        var bomLookup = boms.ToDictionary(b => b.Id);

        var itemIds = workOrders.Select(w => w.ItemId)
            .Concat(workOrders.SelectMany(w => w.RequiredItems.Select(ri => ri.ItemId)))
            .Distinct()
            .ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemLookup = items.ToDictionary(i => i.Id);

        var rows = new List<BomVarianceReportRowDto>();

        foreach (var wo in workOrders)
        {
            bomLookup.TryGetValue(wo.BomId, out var bom);
            itemLookup.TryGetValue(wo.ItemId, out var fgItem);

            var bomNumber = bom?.BomNumber ?? wo.BomId.ToString()[..8];
            var fgCode = fgItem?.ItemCode ?? wo.ItemId.ToString()[..8];
            var fgName = fgItem?.ItemName ?? "Unknown Item";

            if (wo.RequiredItems.Count > 0)
            {
                foreach (var ri in wo.RequiredItems)
                {
                    itemLookup.TryGetValue(ri.ItemId, out var rmItem);
                    var rmCode = rmItem?.ItemCode ?? ri.ItemId.ToString()[..8];
                    var rmName = rmItem?.ItemName ?? ri.ItemName;

                    rows.Add(new BomVarianceReportRowDto
                    {
                        WorkOrderId = wo.Id,
                        WorkOrderNumber = wo.WorkOrderNumber,
                        BomId = wo.BomId,
                        BomNumber = bomNumber,
                        FinishedGoodItemId = wo.ItemId,
                        FinishedGoodItemCode = fgCode,
                        FinishedGoodItemName = fgName,
                        OrderedQty = wo.Quantity,
                        ProducedQty = wo.ProducedQuantity,
                        RawMaterialItemId = ri.ItemId,
                        RawMaterialItemCode = rmCode,
                        RawMaterialItemName = rmName,
                        RequiredQty = ri.RequiredQuantity,
                        ConsumedQty = ri.ConsumedQuantity,
                        VarianceQty = ri.ConsumedQuantity - ri.RequiredQuantity
                    });
                }
            }
            else
            {
                rows.Add(new BomVarianceReportRowDto
                {
                    WorkOrderId = wo.Id,
                    WorkOrderNumber = wo.WorkOrderNumber,
                    BomId = wo.BomId,
                    BomNumber = bomNumber,
                    FinishedGoodItemId = wo.ItemId,
                    FinishedGoodItemCode = fgCode,
                    FinishedGoodItemName = fgName,
                    OrderedQty = wo.Quantity,
                    ProducedQty = wo.ProducedQuantity,
                    RawMaterialItemId = Guid.Empty,
                    RawMaterialItemCode = "—",
                    RawMaterialItemName = "—",
                    RequiredQty = 0,
                    ConsumedQty = 0,
                    VarianceQty = 0
                });
            }
        }

        return new BomVarianceReportDto
        {
            CompanyId = input.CompanyId,
            Rows = rows,
            TotalWorkOrders = workOrders.Count,
            TotalOrderedQty = workOrders.Sum(w => w.Quantity),
            TotalProducedQty = workOrders.Sum(w => w.ProducedQuantity),
            TotalRequiredQty = rows.Sum(r => r.RequiredQty),
            TotalConsumedQty = rows.Sum(r => r.ConsumedQty),
            TotalVarianceQty = rows.Sum(r => r.VarianceQty)
        };
    }
}
