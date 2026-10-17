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
/// Work Order Stock Report — analyzes raw material availability and buildable quantity for active Work Orders.
/// Maps to ERPNext manufacturing/report/work_order_stock_report/work_order_stock_report.py.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class WorkOrderStockReportAppService : ApplicationService, IWorkOrderStockReportAppService
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public WorkOrderStockReportAppService(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _workOrderRepository = workOrderRepository;
        _bomRepository = bomRepository;
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<WorkOrderStockReportDto> GetReportAsync(WorkOrderStockReportRequestDto input)
    {
        var woQuery = await _workOrderRepository.WithDetailsAsync(w => w.RequiredItems);
        var query = woQuery
            .Where(w => w.CompanyId == input.CompanyId)
            .Where(w => w.Status != WorkOrderStatus.Completed 
                     && w.Status != WorkOrderStatus.Cancelled 
                     && w.Status != WorkOrderStatus.Closed);

        if (input.WorkOrderId.HasValue)
        {
            query = query.Where(w => w.Id == input.WorkOrderId.Value);
        }

        if (input.ItemId.HasValue)
        {
            query = query.Where(w => w.ItemId == input.ItemId.Value);
        }

        if (input.WarehouseId.HasValue)
        {
            query = query.Where(w => w.SourceWarehouseId == input.WarehouseId.Value 
                                  || w.RequiredItems.Any(r => r.SourceWarehouseId == input.WarehouseId.Value));
        }

        var workOrders = query.OrderBy(w => w.WorkOrderNumber).ToList();
        if (!workOrders.Any())
        {
            return new WorkOrderStockReportDto
            {
                CompanyId = input.CompanyId
            };
        }

        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        var companyWarehouses = warehouseQuery.Where(w => w.CompanyId == input.CompanyId).ToList();
        var warehouseMap = companyWarehouses.ToDictionary(w => w.Id, w => w);
        var companyWarehouseIds = companyWarehouses.Select(w => w.Id).ToList();

        var bomIds = workOrders.Select(w => w.BomId).Distinct().ToList();
        var bomQuery = await _bomRepository.WithDetailsAsync(b => b.Items);
        var boms = bomQuery.Where(b => bomIds.Contains(b.Id)).ToList();
        var bomMap = boms.ToDictionary(b => b.Id, b => b);

        var allItemIds = workOrders.Select(w => w.ItemId)
            .Concat(workOrders.SelectMany(w => w.RequiredItems.Select(r => r.ItemId)))
            .Concat(boms.SelectMany(b => b.Items.Select(i => i.ItemId)))
            .Distinct()
            .ToList();

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => allItemIds.Contains(i.Id)).ToList();
        var itemMap = items.ToDictionary(i => i.Id, i => i);

        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery.Where(b => companyWarehouseIds.Contains(b.WarehouseId)).ToList();
        var binMap = bins
            .GroupBy(b => (b.ItemId, b.WarehouseId))
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));
        var itemTotalStockMap = bins
            .GroupBy(b => b.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));

        var rows = new List<WorkOrderStockReportRowDto>();

        foreach (var wo in workOrders)
        {
            var bom = bomMap.GetValueOrDefault(wo.BomId);
            var prodItem = itemMap.GetValueOrDefault(wo.ItemId);
            var prodItemCode = prodItem?.ItemCode ?? string.Empty;
            var prodItemName = prodItem?.ItemName ?? string.Empty;
            var bomNumber = bom?.BomNumber ?? string.Empty;
            var bomDesc = bom?.Notes ?? prodItemName;

            var defaultWhId = wo.SourceWarehouseId ?? input.WarehouseId;
            var defaultWhName = defaultWhId.HasValue ? warehouseMap.GetValueOrDefault(defaultWhId.Value)?.Name : null;

            var qtyToBuild = Math.Max(0, wo.Quantity - wo.ProducedQuantity);
            var componentDetails = new List<WorkOrderStockItemDetailDto>();

            var reqItemsCount = 0;
            var inStockCount = 0;
            var minBuildableQty = qtyToBuild;

            if (wo.RequiredItems.Any())
            {
                foreach (var reqItem in wo.RequiredItems)
                {
                    reqItemsCount++;
                    var rmItem = itemMap.GetValueOrDefault(reqItem.ItemId);
                    var rmCode = rmItem?.ItemCode ?? string.Empty;
                    var rmName = rmItem?.ItemName ?? reqItem.ItemName;

                    var itemWhId = reqItem.SourceWarehouseId ?? defaultWhId;
                    decimal actualStock;
                    if (itemWhId.HasValue)
                    {
                        actualStock = binMap.GetValueOrDefault((reqItem.ItemId, itemWhId.Value));
                    }
                    else
                    {
                        actualStock = itemTotalStockMap.GetValueOrDefault(reqItem.ItemId);
                    }

                    decimal perUnitReq;
                    if (bom != null && bom.Quantity > 0)
                    {
                        var bomLines = bom.Items.Where(bi => bi.ItemId == reqItem.ItemId).ToList();
                        if (bomLines.Any())
                        {
                            perUnitReq = bomLines.Sum(bi => bi.StockQty) / bom.Quantity;
                        }
                        else
                        {
                            perUnitReq = wo.Quantity > 0 ? reqItem.RequiredQuantity / wo.Quantity : reqItem.RequiredQuantity;
                        }
                    }
                    else
                    {
                        perUnitReq = wo.Quantity > 0 ? reqItem.RequiredQuantity / wo.Quantity : reqItem.RequiredQuantity;
                    }

                    if (perUnitReq <= 0)
                    {
                        perUnitReq = 1m;
                    }

                    var itemBuildableQty = Math.Floor(actualStock / perUnitReq);
                    var remainingItemReq = perUnitReq * qtyToBuild;
                    var isInStock = actualStock >= remainingItemReq;

                    if (isInStock)
                    {
                        inStockCount++;
                    }

                    if (itemBuildableQty < minBuildableQty)
                    {
                        minBuildableQty = itemBuildableQty;
                    }

                    componentDetails.Add(new WorkOrderStockItemDetailDto
                    {
                        ItemId = reqItem.ItemId,
                        ItemCode = rmCode,
                        ItemName = rmName,
                        RequiredQty = Math.Round(remainingItemReq, 2),
                        AvailableQty = Math.Round(actualStock, 2),
                        BuildableQty = Math.Round(itemBuildableQty, 2),
                        IsInStock = isInStock
                    });
                }
            }
            else if (bom != null && bom.Items.Any())
            {
                foreach (var bItem in bom.Items)
                {
                    reqItemsCount++;
                    var rmItem = itemMap.GetValueOrDefault(bItem.ItemId);
                    var rmCode = rmItem?.ItemCode ?? string.Empty;
                    var rmName = rmItem?.ItemName ?? bItem.ItemName;

                    var itemWhId = bItem.SourceWarehouseId ?? defaultWhId;
                    decimal actualStock;
                    if (itemWhId.HasValue)
                    {
                        actualStock = binMap.GetValueOrDefault((bItem.ItemId, itemWhId.Value));
                    }
                    else
                    {
                        actualStock = itemTotalStockMap.GetValueOrDefault(bItem.ItemId);
                    }

                    var perUnitReq = bom.Quantity > 0 ? bItem.StockQty / bom.Quantity : bItem.StockQty;
                    if (perUnitReq <= 0)
                    {
                        perUnitReq = 1m;
                    }

                    var itemBuildableQty = Math.Floor(actualStock / perUnitReq);
                    var remainingItemReq = perUnitReq * qtyToBuild;
                    var isInStock = actualStock >= remainingItemReq;

                    if (isInStock)
                    {
                        inStockCount++;
                    }

                    if (itemBuildableQty < minBuildableQty)
                    {
                        minBuildableQty = itemBuildableQty;
                    }

                    componentDetails.Add(new WorkOrderStockItemDetailDto
                    {
                        ItemId = bItem.ItemId,
                        ItemCode = rmCode,
                        ItemName = rmName,
                        RequiredQty = Math.Round(remainingItemReq, 2),
                        AvailableQty = Math.Round(actualStock, 2),
                        BuildableQty = Math.Round(itemBuildableQty, 2),
                        IsInStock = isInStock
                    });
                }
            }

            var finalBuildableQty = reqItemsCount > 0 ? Math.Max(0, minBuildableQty) : qtyToBuild;
            var isReadyToBuild = reqItemsCount > 0 ? (inStockCount == reqItemsCount) : true;

            if (input.ReadyToBuildOnly && !isReadyToBuild)
            {
                continue;
            }

            rows.Add(new WorkOrderStockReportRowDto
            {
                WorkOrderId = wo.Id,
                WorkOrderNumber = wo.WorkOrderNumber,
                Status = wo.Status.ToString(),
                BomId = wo.BomId,
                BomNumber = bomNumber,
                Description = bomDesc,
                ProductionItemId = wo.ItemId,
                ProductionItemCode = prodItemCode,
                ProductionItemName = prodItemName,
                SourceWarehouseId = defaultWhId,
                SourceWarehouseName = defaultWhName,
                QtyToBuild = qtyToBuild,
                RequiredItemsCount = reqItemsCount,
                InStockItemsCount = inStockCount,
                BuildableQty = Math.Round(finalBuildableQty, 2),
                ReadyToBuild = isReadyToBuild,
                ComponentDetails = componentDetails
            });
        }

        return new WorkOrderStockReportDto
        {
            CompanyId = input.CompanyId,
            Rows = rows,
            TotalWorkOrders = rows.Count,
            ReadyToBuildCount = rows.Count(r => r.ReadyToBuild),
            NotReadyCount = rows.Count(r => !r.ReadyToBuild),
            TotalBuildableQty = rows.Sum(r => r.BuildableQty)
        };
    }
}
