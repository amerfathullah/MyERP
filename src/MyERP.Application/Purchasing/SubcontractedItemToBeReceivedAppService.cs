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
public class SubcontractedItemToBeReceivedAppService : ApplicationService, ISubcontractedItemToBeReceivedAppService
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public SubcontractedItemToBeReceivedAppService(
        IRepository<SubcontractingOrder, Guid> scoRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _scoRepository = scoRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
    }

    public async Task<SubcontractedItemToBeReceivedReportDto> GetReportAsync(SubcontractedItemToBeReceivedFilterDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "To Date must be greater than or equal to From Date.");
        }

        var scoQuery = await _scoRepository.WithDetailsAsync(x => x.Items);

        var orders = scoQuery
            .Where(o => o.CompanyId == input.CompanyId
                     && o.Status != SubcontractingOrderStatus.Draft
                     && o.Status != SubcontractingOrderStatus.Cancelled
                     && o.Status != SubcontractingOrderStatus.Closed)
            .WhereIf(input.SupplierId.HasValue, o => o.SupplierId == input.SupplierId!.Value)
            .WhereIf(input.FromDate.HasValue, o => o.OrderDate >= input.FromDate!.Value)
            .WhereIf(input.ToDate.HasValue, o => o.OrderDate <= input.ToDate!.Value)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        if (orders.Count == 0)
        {
            return new SubcontractedItemToBeReceivedReportDto();
        }

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = (await _supplierRepository.GetQueryableAsync())
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        var allItemIds = orders.SelectMany(o => o.Items).Select(i => i.ItemId).Distinct().ToList();
        var items = (await _itemRepository.GetQueryableAsync())
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id, i => i);

        var rows = new List<SubcontractedItemToBeReceivedRowDto>();

        foreach (var order in orders)
        {
            var supplierName = suppliers.GetValueOrDefault(order.SupplierId, "Unknown Supplier");

            foreach (var item in order.Items)
            {
                if (item.ReceivedQty < item.Qty)
                {
                    var itemInfo = items.GetValueOrDefault(item.ItemId);
                    var itemCode = itemInfo?.ItemCode ?? item.ItemName;
                    var itemName = itemInfo?.ItemName ?? item.ItemName;
                    var pendingQty = item.Qty - item.ReceivedQty;

                    rows.Add(new SubcontractedItemToBeReceivedRowDto
                    {
                        SubcontractOrderId = order.Id,
                        SubcontractOrderNumber = order.OrderNumber,
                        OrderDate = order.OrderDate,
                        SupplierId = order.SupplierId,
                        SupplierName = supplierName,
                        FgItemId = item.ItemId,
                        FgItemCode = itemCode,
                        ItemName = itemName,
                        RequiredQty = item.Qty,
                        ReceivedQty = item.ReceivedQty,
                        PendingQty = pendingQty
                    });
                }
            }
        }

        var distinctOrderCount = rows.Select(r => r.SubcontractOrderId).Distinct().Count();

        return new SubcontractedItemToBeReceivedReportDto
        {
            Rows = rows,
            Summary = new SubcontractedItemToBeReceivedSummaryDto
            {
                TotalRequiredQty = rows.Sum(r => r.RequiredQty),
                TotalReceivedQty = rows.Sum(r => r.ReceivedQty),
                TotalPendingQty = rows.Sum(r => r.PendingQty),
                TotalOrders = distinctOrderCount
            }
        };
    }
}
