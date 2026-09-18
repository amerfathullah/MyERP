using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class RequestedItemsToOrderAndReceiveAppService : ApplicationService, IRequestedItemsToOrderAndReceiveAppService
{
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public RequestedItemsToOrderAndReceiveAppService(
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _materialRequestRepository = materialRequestRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<RequestedItemsToOrderAndReceiveReportDto> GetReportAsync(RequestedItemsFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var mrQuery = await _materialRequestRepository.GetQueryableAsync();
        var requests = mrQuery
            .Where(mr => mr.CompanyId == input.CompanyId
                      && mr.Status == DocumentStatus.Submitted
                      && mr.RequestDate >= from
                      && mr.RequestDate <= to)
            .WhereIf(input.MaterialRequestId.HasValue, mr => mr.Id == input.MaterialRequestId!.Value)
            .ToList();

        if (requests.Count == 0)
        {
            return new RequestedItemsToOrderAndReceiveReportDto();
        }

        var allItems = requests
            .SelectMany(mr => mr.Items.Select(item => new { Request = mr, Item = item }))
            .WhereIf(input.ItemId.HasValue, x => x.Item.ItemId == input.ItemId!.Value)
            .WhereIf(input.WarehouseId.HasValue, x => x.Item.WarehouseId == input.WarehouseId!.Value)
            .ToList();

        if (allItems.Count == 0)
        {
            return new RequestedItemsToOrderAndReceiveReportDto();
        }

        var itemIds = allItems.Select(x => x.Item.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemEntities = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var warehouseIds = allItems.Where(x => x.Item.WarehouseId.HasValue).Select(x => x.Item.WarehouseId!.Value).Distinct().ToList();
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouseMap = whQuery
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        // Group by (MaterialRequestId, ItemId)
        // Per ERPNext PR #59132 (commit 0f66c41819):
        // Sourcing each unit column separately can report one line's UOM beside another's Stock UOM.
        // UOM and Stock UOM describe a line, so the reported pair must come together off the FIRST
        // representative line by Idx.
        var groups = allItems
            .GroupBy(x => new { x.Request.Id, x.Item.ItemId })
            .ToList();

        var rows = new List<RequestedItemToOrderAndReceiveRowDto>();

        foreach (var g in groups)
        {
            var repLine = g.First();
            var totalQty = g.Sum(x => x.Item.Quantity);
            var totalStockQty = g.Sum(x => x.Item.StockQty);
            var totalOrdered = g.Sum(x => x.Item.OrderedQuantity);
            var totalReceived = g.Sum(x => x.Item.ReceivedQuantity);

            var itemEntity = itemEntities.GetValueOrDefault(g.Key.ItemId);
            var stockUom = itemEntity?.Uom ?? repLine.Item.Uom;
            var warehouseName = repLine.Item.WarehouseId.HasValue
                ? warehouseMap.GetValueOrDefault(repLine.Item.WarehouseId.Value)
                : null;

            rows.Add(new RequestedItemToOrderAndReceiveRowDto
            {
                MaterialRequestId = g.Key.Id,
                MaterialRequestNumber = repLine.Request.RequestNumber,
                TransactionDate = repLine.Request.RequestDate,
                RequiredDate = repLine.Request.RequiredByDate,
                ItemId = g.Key.ItemId,
                ItemCode = itemEntity?.ItemCode ?? g.Key.ItemId.ToString().Substring(0, 8),
                ItemName = repLine.Item.ItemName,
                Description = itemEntity?.Description,
                Qty = Math.Round(totalQty, 4),
                StockQty = Math.Round(totalStockQty, 4),
                OrderedQty = Math.Round(totalOrdered, 4),
                ReceivedQty = Math.Round(totalReceived, 4),
                QtyToOrder = Math.Max(0, Math.Round(totalStockQty - totalOrdered, 4)),
                QtyToReceive = Math.Max(0, Math.Round(totalStockQty - totalReceived, 4)),
                Uom = repLine.Item.Uom,
                StockUom = stockUom,
                WarehouseId = repLine.Item.WarehouseId,
                WarehouseName = warehouseName
            });
        }

        return new RequestedItemsToOrderAndReceiveReportDto
        {
            Rows = rows.OrderBy(r => r.TransactionDate).ThenBy(r => r.MaterialRequestNumber).ToList(),
            TotalQty = Math.Round(rows.Sum(r => r.Qty), 4),
            TotalOrderedQty = Math.Round(rows.Sum(r => r.OrderedQty), 4),
            TotalReceivedQty = Math.Round(rows.Sum(r => r.ReceivedQty), 4),
            TotalQtyToOrder = Math.Round(rows.Sum(r => r.QtyToOrder), 4),
            TotalQtyToReceive = Math.Round(rows.Sum(r => r.QtyToReceive), 4)
        };
    }
}
