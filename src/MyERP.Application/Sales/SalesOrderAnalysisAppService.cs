using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Sales Order Analysis report application service.
/// Implements ERPNext selling/report/sales_order_analysis with grouping by SO or by Item.
/// Per ERPNext PR #59236: group_by_item groups by (company, item_code, uom).
/// </summary>
[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesOrderAnalysisAppService : ApplicationService, ISalesOrderAnalysisAppService
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public SalesOrderAnalysisAppService(
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _companyRepository = companyRepository;
    }

    public async Task<SalesOrderAnalysisReportDto> GetAnalysisAsync(GetSalesOrderAnalysisDto input)
    {
        if (input.GroupBySo && input.GroupByItem)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot combine Group By SO and Group By Item.");
        }

        var fromDate = input.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var toDate = input.ToDate ?? DateTime.UtcNow.Date;

        var soQuery = await _salesOrderRepository.WithDetailsAsync(so => so.Items);
        var orders = soQuery
            .Where(so => so.CompanyId == input.CompanyId
                         && so.Status != DocumentStatus.Draft
                         && so.Status != DocumentStatus.Cancelled
                         && so.OrderDate >= fromDate
                         && so.OrderDate <= toDate)
            .WhereIf(input.CustomerId.HasValue, so => so.CustomerId == input.CustomerId!.Value)
            .WhereIf(input.SalesOrderId.HasValue, so => so.Id == input.SalesOrderId!.Value)
            .ToList();

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customerMap = (await _customerRepository.GetQueryableAsync())
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        var allItemIds = orders.SelectMany(o => o.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemMap = (await _itemRepository.GetQueryableAsync())
            .Where(i => allItemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });

        var warehouseIds = orders.SelectMany(o => o.Items)
            .Where(i => i.WarehouseId.HasValue)
            .Select(i => i.WarehouseId!.Value)
            .Distinct()
            .ToList();
        var warehouseMap = (await _warehouseRepository.GetQueryableAsync())
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var companyName = company?.Name ?? string.Empty;

        var detailedRows = new List<SalesOrderAnalysisRowDto>();

        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value)
                    continue;

                var itemInfo = itemMap.GetValueOrDefault(item.ItemId);
                var itemCode = itemInfo?.ItemCode ?? item.ItemId.ToString();
                var customerName = customerMap.GetValueOrDefault(order.CustomerId, string.Empty);
                var warehouseName = item.WarehouseId.HasValue
                    ? warehouseMap.GetValueOrDefault(item.WarehouseId.Value, string.Empty)
                    : string.Empty;

                var pendingQty = Math.Max(0m, item.Quantity - item.DeliveredQty);
                var qtyToBill = Math.Max(0m, item.Quantity - item.BilledQty);
                var amount = Math.Round(item.Quantity * item.UnitPrice * order.ExchangeRate, 2);
                var deliveredQtyAmount = Math.Round(item.DeliveredQty * item.UnitPrice * order.ExchangeRate, 2);
                var billedAmount = Math.Round(item.BilledQty * item.UnitPrice * order.ExchangeRate, 2);
                var pendingAmount = Math.Round(pendingQty * item.UnitPrice * order.ExchangeRate, 2);

                var deliveryDate = item.DeliveryDate ?? order.DeliveryDate;
                var delayDays = 0;
                if (deliveryDate.HasValue && deliveryDate.Value.Date < DateTime.UtcNow.Date && pendingQty > 0)
                {
                    delayDays = (int)(DateTime.UtcNow.Date - deliveryDate.Value.Date).TotalDays;
                }

                detailedRows.Add(new SalesOrderAnalysisRowDto
                {
                    SalesOrderNumber = order.OrderNumber,
                    SalesOrderId = order.Id,
                    Date = order.OrderDate,
                    CustomerId = order.CustomerId,
                    CustomerName = customerName,
                    ItemId = item.ItemId,
                    ItemCode = itemCode,
                    Description = item.Description,
                    Uom = item.Uom,
                    Qty = item.Quantity,
                    DeliveredQty = item.DeliveredQty,
                    PendingQty = pendingQty,
                    BilledQty = item.BilledQty,
                    QtyToBill = qtyToBill,
                    Amount = amount,
                    DeliveredQtyAmount = deliveredQtyAmount,
                    BilledAmount = billedAmount,
                    PendingAmount = pendingAmount,
                    DeliveryDate = deliveryDate,
                    DelayDays = delayDays,
                    WarehouseId = item.WarehouseId,
                    WarehouseName = warehouseName,
                    CompanyId = order.CompanyId,
                    CompanyName = companyName,
                });
            }
        }

        List<SalesOrderAnalysisRowDto> resultRows;

        if (input.GroupBySo)
        {
            resultRows = detailedRows
                .GroupBy(r => r.SalesOrderId)
                .Select(g =>
                {
                    var first = g.First();
                    return new SalesOrderAnalysisRowDto
                    {
                        SalesOrderNumber = first.SalesOrderNumber,
                        SalesOrderId = first.SalesOrderId,
                        Date = first.Date,
                        CustomerId = first.CustomerId,
                        CustomerName = first.CustomerName,
                        Qty = g.Sum(r => r.Qty),
                        DeliveredQty = g.Sum(r => r.DeliveredQty),
                        PendingQty = g.Sum(r => r.PendingQty),
                        BilledQty = g.Sum(r => r.BilledQty),
                        QtyToBill = g.Sum(r => r.QtyToBill),
                        Amount = g.Sum(r => r.Amount),
                        DeliveredQtyAmount = g.Sum(r => r.DeliveredQtyAmount),
                        BilledAmount = g.Sum(r => r.BilledAmount),
                        PendingAmount = g.Sum(r => r.PendingAmount),
                        DeliveryDate = g.Min(r => r.DeliveryDate),
                        DelayDays = g.Max(r => r.DelayDays),
                        CompanyId = first.CompanyId,
                        CompanyName = first.CompanyName,
                    };
                })
                .OrderByDescending(r => r.Date)
                .ThenBy(r => r.SalesOrderNumber)
                .ToList();
        }
        else if (input.GroupByItem)
        {
            // Per ERPNext PR #59236: group by (company, item_code, uom)
            resultRows = detailedRows
                .GroupBy(r => (r.CompanyId, r.ItemCode, r.Uom))
                .Select(g =>
                {
                    var first = g.First();
                    return new SalesOrderAnalysisRowDto
                    {
                        CompanyId = first.CompanyId,
                        CompanyName = first.CompanyName,
                        ItemId = first.ItemId,
                        ItemCode = first.ItemCode,
                        Description = first.Description,
                        Uom = first.Uom,
                        Qty = g.Sum(r => r.Qty),
                        DeliveredQty = g.Sum(r => r.DeliveredQty),
                        PendingQty = g.Sum(r => r.PendingQty),
                        BilledQty = g.Sum(r => r.BilledQty),
                        QtyToBill = g.Sum(r => r.QtyToBill),
                        Amount = g.Sum(r => r.Amount),
                        DeliveredQtyAmount = g.Sum(r => r.DeliveredQtyAmount),
                        BilledAmount = g.Sum(r => r.BilledAmount),
                        PendingAmount = g.Sum(r => r.PendingAmount),
                    };
                })
                .OrderBy(r => r.CompanyName)
                .ThenBy(r => r.ItemCode)
                .ThenBy(r => r.Uom)
                .ToList();
        }
        else
        {
            resultRows = detailedRows
                .OrderByDescending(r => r.Date)
                .ThenBy(r => r.SalesOrderNumber)
                .ToList();
        }

        return new SalesOrderAnalysisReportDto
        {
            Rows = resultRows,
            TotalAmount = detailedRows.Sum(r => r.Amount),
            TotalBilledAmount = detailedRows.Sum(r => r.BilledAmount),
            TotalAmountToBill = detailedRows.Sum(r => r.PendingAmount),
            TotalQty = detailedRows.Sum(r => r.Qty),
            TotalDeliveredQty = detailedRows.Sum(r => r.DeliveredQty),
        };
    }
}
