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
public class SubcontractOrderSummaryAppService : ApplicationService, ISubcontractOrderSummaryAppService
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public SubcontractOrderSummaryAppService(
        IRepository<SubcontractingOrder, Guid> scoRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _scoRepository = scoRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
    }

    public async Task<SubcontractOrderSummaryReportDto> GetReportAsync(SubcontractOrderSummaryFilterDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "To Date must be greater than or equal to From Date.");
        }

        var scoQuery = await _scoRepository.WithDetailsAsync(x => x.Items, x => x.SuppliedItems);

        var orders = scoQuery
            .Where(o => o.CompanyId == input.CompanyId
                     && o.Status != SubcontractingOrderStatus.Draft
                     && o.Status != SubcontractingOrderStatus.Cancelled)
            .WhereIf(input.SupplierId.HasValue, o => o.SupplierId == input.SupplierId!.Value)
            .WhereIf(input.SubcontractOrderId.HasValue, o => o.Id == input.SubcontractOrderId!.Value)
            .WhereIf(input.FromDate.HasValue, o => o.OrderDate >= input.FromDate!.Value)
            .WhereIf(input.ToDate.HasValue, o => o.OrderDate <= input.ToDate!.Value)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        if (orders.Count == 0)
        {
            return new SubcontractOrderSummaryReportDto();
        }

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = (await _supplierRepository.GetQueryableAsync())
            .Where(s => supplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionary(s => s.Id, s => s.Name);

        var allItemIds = orders.SelectMany(o => o.Items.Select(i => i.ItemId))
            .Union(orders.SelectMany(o => o.SuppliedItems.Select(i => i.ItemId)))
            .Distinct()
            .ToList();

        var items = (await _itemRepository.GetQueryableAsync())
            .Where(i => allItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName })
            .ToDictionary(i => i.Id, i => i);

        var rows = new List<SubcontractOrderSummaryRowDto>();

        foreach (var order in orders)
        {
            var supplierName = suppliers.GetValueOrDefault(order.SupplierId, "Unknown Supplier");
            var statusStr = order.Status.ToString();

            var fgItems = order.Items.ToList();
            var rmItems = order.SuppliedItems.ToList();

            if (fgItems.Count > 0 && rmItems.Count > 0)
            {
                for (int i = 0; i < Math.Max(fgItems.Count, rmItems.Count); i++)
                {
                    var fg = i < fgItems.Count ? fgItems[i] : null;
                    var rm = i < rmItems.Count ? rmItems[i] : null;

                    var fgInfo = fg != null ? items.GetValueOrDefault(fg.ItemId) : null;
                    var rmInfo = rm != null ? items.GetValueOrDefault(rm.ItemId) : null;

                    rows.Add(new SubcontractOrderSummaryRowDto
                    {
                        SubcontractOrderId = order.Id,
                        SubcontractOrderNumber = order.OrderNumber,
                        OrderDate = order.OrderDate,
                        SupplierId = order.SupplierId,
                        SupplierName = supplierName,
                        Status = statusStr,
                        FgItemId = fg?.ItemId,
                        FgItemCode = fgInfo?.ItemCode ?? fg?.ItemName,
                        FgItemName = fgInfo?.ItemName ?? fg?.ItemName,
                        OrderQty = fg?.Qty ?? 0m,
                        ReceivedQty = fg?.ReceivedQty ?? 0m,
                        RmItemId = rm?.ItemId,
                        RmItemCode = rmInfo?.ItemCode ?? rm?.ItemName,
                        RmItemName = rmInfo?.ItemName ?? rm?.ItemName,
                        RequiredQty = rm?.RequiredQty ?? 0m,
                        SuppliedQty = rm?.TransferredQty ?? 0m,
                        ConsumedQty = rm?.ConsumedQty ?? 0m,
                        ReturnedQty = 0m,
                    });
                }
            }
            else if (fgItems.Count > 0)
            {
                foreach (var fg in fgItems)
                {
                    var fgInfo = items.GetValueOrDefault(fg.ItemId);
                    rows.Add(new SubcontractOrderSummaryRowDto
                    {
                        SubcontractOrderId = order.Id,
                        SubcontractOrderNumber = order.OrderNumber,
                        OrderDate = order.OrderDate,
                        SupplierId = order.SupplierId,
                        SupplierName = supplierName,
                        Status = statusStr,
                        FgItemId = fg.ItemId,
                        FgItemCode = fgInfo?.ItemCode ?? fg.ItemName,
                        FgItemName = fgInfo?.ItemName ?? fg.ItemName,
                        OrderQty = fg.Qty,
                        ReceivedQty = fg.ReceivedQty,
                    });
                }
            }
            else if (rmItems.Count > 0)
            {
                foreach (var rm in rmItems)
                {
                    var rmInfo = items.GetValueOrDefault(rm.ItemId);
                    rows.Add(new SubcontractOrderSummaryRowDto
                    {
                        SubcontractOrderId = order.Id,
                        SubcontractOrderNumber = order.OrderNumber,
                        OrderDate = order.OrderDate,
                        SupplierId = order.SupplierId,
                        SupplierName = supplierName,
                        Status = statusStr,
                        RmItemId = rm.ItemId,
                        RmItemCode = rmInfo?.ItemCode ?? rm.ItemName,
                        RmItemName = rmInfo?.ItemName ?? rm.ItemName,
                        RequiredQty = rm.RequiredQty,
                        SuppliedQty = rm.TransferredQty,
                        ConsumedQty = rm.ConsumedQty,
                        ReturnedQty = 0m,
                    });
                }
            }
        }

        return new SubcontractOrderSummaryReportDto
        {
            Rows = rows,
            TotalOrders = rows.Select(r => r.SubcontractOrderId).Distinct().Count(),
            TotalOrderQty = rows.Sum(r => r.OrderQty),
            TotalReceivedQty = rows.Sum(r => r.ReceivedQty),
            TotalRequiredQty = rows.Sum(r => r.RequiredQty),
            TotalSuppliedQty = rows.Sum(r => r.SuppliedQty),
            TotalConsumedQty = rows.Sum(r => r.ConsumedQty),
        };
    }
}
