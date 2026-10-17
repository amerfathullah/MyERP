using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class RequestedItemsToTransferAppService : ApplicationService, IRequestedItemsToTransferAppService
{
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public RequestedItemsToTransferAppService(
        IRepository<MaterialRequest, Guid> materialRequestRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _materialRequestRepository = materialRequestRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<RequestedItemsToTransferReportDto> GetReportAsync(RequestedItemsToTransferFilterDto input)
    {
        var mrQuery = await _materialRequestRepository.WithDetailsAsync(x => x.Items);

        mrQuery = mrQuery.Where(mr =>
            mr.CompanyId == input.CompanyId &&
            mr.Status == DocumentStatus.Submitted &&
            (mr.RequestType == MaterialRequestType.MaterialTransfer || mr.RequestType == MaterialRequestType.MaterialIssue));

        if (input.FromDate.HasValue)
        {
            mrQuery = mrQuery.Where(mr => mr.RequestDate >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            mrQuery = mrQuery.Where(mr => mr.RequestDate <= input.ToDate.Value);
        }

        if (input.WarehouseId.HasValue && input.WarehouseId != Guid.Empty)
        {
            var whId = input.WarehouseId.Value;
            mrQuery = mrQuery.Where(mr => mr.SourceWarehouseId == whId || mr.TargetWarehouseId == whId || mr.Items.Any(i => i.WarehouseId == whId));
        }

        var requests = mrQuery
            .OrderBy(mr => mr.RequestDate)
            .ToList();

        // Get warehouses for display
        var warehouseIds = requests
            .SelectMany(r => new[] { r.SourceWarehouseId, r.TargetWarehouseId })
            .Where(w => w.HasValue)
            .Select(w => w!.Value)
            .Distinct()
            .ToList();

        var warehouseDict = new Dictionary<Guid, string>();
        if (warehouseIds.Count > 0)
        {
            var whList = await _warehouseRepository.GetListAsync(w => warehouseIds.Contains(w.Id));
            if (whList != null)
            {
                warehouseDict = whList.ToDictionary(w => w.Id, w => w.Name);
            }
        }

        // Get items for display
        var allItemIds = requests
            .SelectMany(r => r.Items)
            .Select(i => i.ItemId)
            .Distinct()
            .ToList();

        var itemDict = new Dictionary<Guid, (string Code, string? Desc)>();
        if (allItemIds.Count > 0)
        {
            var itemQueryable = await _itemRepository.GetQueryableAsync();
            if (itemQueryable != null)
            {
                var itemList = itemQueryable
                    .Where(i => allItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.ItemCode, i.Description })
                    .ToList();
                itemDict = itemList.ToDictionary(i => i.Id, i => (i.ItemCode, i.Description));
            }
        }

        var rows = new List<RequestedItemsToTransferRowDto>();
        var search = input.SearchText?.Trim().ToLowerInvariant();

        foreach (var req in requests)
        {
            foreach (var item in req.Items)
            {
                // Must have pending transfer quantity
                if (item.OrderedQuantity >= item.Quantity)
                {
                    continue;
                }

                if (input.ItemId.HasValue && input.ItemId != Guid.Empty && item.ItemId != input.ItemId.Value)
                {
                    continue;
                }

                var itemInfo = itemDict.TryGetValue(item.ItemId, out var info) ? info : (Code: string.Empty, Desc: (string?)null);
                var qtyToTransfer = Math.Max(0, item.Quantity - item.OrderedQuantity);
                var completionPct = item.Quantity > 0 ? Math.Round(item.OrderedQuantity / item.Quantity * 100, 2) : 0;

                var sourceWhName = req.SourceWarehouseId.HasValue && warehouseDict.TryGetValue(req.SourceWarehouseId.Value, out var sName) ? sName : null;
                var targetWhName = req.TargetWarehouseId.HasValue && warehouseDict.TryGetValue(req.TargetWarehouseId.Value, out var tName) ? tName : null;

                if (!string.IsNullOrEmpty(search))
                {
                    var matches =
                        req.RequestNumber.ToLowerInvariant().Contains(search) ||
                        itemInfo.Code.ToLowerInvariant().Contains(search) ||
                        item.ItemName.ToLowerInvariant().Contains(search) ||
                        (itemInfo.Desc?.ToLowerInvariant().Contains(search) ?? false);

                    if (!matches)
                    {
                        continue;
                    }
                }

                rows.Add(new RequestedItemsToTransferRowDto
                {
                    MaterialRequestId = req.Id,
                    RequestNumber = req.RequestNumber,
                    RequestDate = req.RequestDate,
                    RequiredByDate = req.RequiredByDate,
                    RequestType = req.RequestType.ToString(),
                    ItemId = item.ItemId,
                    ItemCode = itemInfo.Code,
                    ItemName = item.ItemName,
                    Description = itemInfo.Desc,
                    Uom = item.Uom,
                    RequestedQty = item.Quantity,
                    TransferredQty = item.OrderedQuantity,
                    QtyToTransfer = qtyToTransfer,
                    CompletionPercent = completionPct,
                    SourceWarehouseId = req.SourceWarehouseId,
                    SourceWarehouseName = sourceWhName,
                    TargetWarehouseId = req.TargetWarehouseId,
                    TargetWarehouseName = targetWhName,
                    WorkOrderId = req.WorkOrderId,
                    JobCardId = req.JobCardId
                });
            }
        }

        return new RequestedItemsToTransferReportDto
        {
            Rows = rows,
            TotalRequests = rows.Select(r => r.MaterialRequestId).Distinct().Count(),
            TotalItems = rows.Count,
            TotalQtyToTransfer = rows.Sum(r => r.QtyToTransfer)
        };
    }
}
