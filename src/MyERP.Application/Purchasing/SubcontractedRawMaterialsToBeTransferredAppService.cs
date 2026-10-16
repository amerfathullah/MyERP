using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractedRawMaterialsToBeTransferredAppService : ApplicationService, ISubcontractedRawMaterialsToBeTransferredAppService
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public SubcontractedRawMaterialsToBeTransferredAppService(
        IRepository<SubcontractingOrder, Guid> scoRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _scoRepository = scoRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<SubcontractedRawMaterialsToBeTransferredReportDto> GetReportAsync(SubcontractedRawMaterialsToBeTransferredFilterDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "To Date must be greater than or equal to From Date.");
        }

        var scoQuery = await _scoRepository.WithDetailsAsync(x => x.SuppliedItems);

        var orders = scoQuery
            .Where(o => o.CompanyId == input.CompanyId
                     && o.Status != SubcontractingOrderStatus.Draft
                     && o.Status != SubcontractingOrderStatus.Cancelled
                     && o.Status != SubcontractingOrderStatus.Closed
                     && o.PerReceived < 100m)
            .WhereIf(input.SupplierId.HasValue, o => o.SupplierId == input.SupplierId!.Value)
            .WhereIf(input.SubcontractOrderId.HasValue, o => o.Id == input.SubcontractOrderId!.Value)
            .WhereIf(input.FromDate.HasValue, o => o.OrderDate >= input.FromDate!.Value)
            .WhereIf(input.ToDate.HasValue, o => o.OrderDate <= input.ToDate!.Value)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        if (orders.Count == 0)
        {
            return new SubcontractedRawMaterialsToBeTransferredReportDto();
        }

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = (await _supplierRepository.GetQueryableAsync())
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        var allItemIds = orders.SelectMany(o => o.SuppliedItems).Select(i => i.ItemId).Distinct().ToList();
        var items = (await _itemRepository.GetQueryableAsync())
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id, i => i);

        var warehouseIds = orders.SelectMany(o => o.SuppliedItems)
            .Where(i => i.ReserveWarehouseId.HasValue)
            .Select(i => i.ReserveWarehouseId!.Value)
            .Distinct()
            .ToList();
        var warehouses = (await _warehouseRepository.GetQueryableAsync())
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionary(w => w.Id, w => w.Name);

        var rows = new List<SubcontractedRawMaterialsToBeTransferredRowDto>();

        foreach (var order in orders)
        {
            var supplierName = suppliers.GetValueOrDefault(order.SupplierId, "Unknown Supplier");

            foreach (var item in order.SuppliedItems)
            {
                if (input.RawMaterialItemId.HasValue && item.ItemId != input.RawMaterialItemId.Value)
                    continue;

                if (item.TransferredQty < item.RequiredQty)
                {
                    var itemInfo = items.GetValueOrDefault(item.ItemId);
                    var itemCode = itemInfo?.ItemCode ?? item.ItemName;
                    var itemName = itemInfo?.ItemName ?? item.ItemName;
                    var pendingQty = item.RequiredQty - item.TransferredQty;
                    var warehouseName = item.ReserveWarehouseId.HasValue
                        ? warehouses.GetValueOrDefault(item.ReserveWarehouseId.Value, string.Empty)
                        : string.Empty;

                    rows.Add(new SubcontractedRawMaterialsToBeTransferredRowDto
                    {
                        SubcontractOrderId = order.Id,
                        SubcontractOrderNumber = order.OrderNumber,
                        OrderDate = order.OrderDate,
                        SupplierId = order.SupplierId,
                        SupplierName = supplierName,
                        RmItemId = item.ItemId,
                        RmItemCode = itemCode,
                        RmItemName = itemName,
                        RequiredQty = item.RequiredQty,
                        TransferredQty = item.TransferredQty,
                        ReturnedQty = 0m,
                        PendingQty = pendingQty,
                        ReserveWarehouseId = item.ReserveWarehouseId,
                        ReserveWarehouseName = warehouseName,
                    });
                }
            }
        }

        return new SubcontractedRawMaterialsToBeTransferredReportDto
        {
            Rows = rows,
            Summary = new SubcontractedRawMaterialsToBeTransferredSummaryDto
            {
                TotalOrders = rows.Select(r => r.SubcontractOrderId).Distinct().Count(),
                TotalRequiredQty = rows.Sum(r => r.RequiredQty),
                TotalTransferredQty = rows.Sum(r => r.TransferredQty),
                TotalReturnedQty = rows.Sum(r => r.ReturnedQty),
                TotalPendingQty = rows.Sum(r => r.PendingQty),
            }
        };
    }
}
