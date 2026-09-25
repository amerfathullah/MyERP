using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Implements Subcontracting Inward Reports matching ERPNext PR #59395.
/// Exposes items to be delivered and inward order summary reports.
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class SubcontractingInwardReportAppService : ApplicationService, ISubcontractingInwardReportAppService
{
    private readonly IRepository<SubcontractingInwardOrder, Guid> _orderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;

    public SubcontractingInwardReportAppService(
        IRepository<SubcontractingInwardOrder, Guid> orderRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Customer, Guid> customerRepository)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _supplierRepository = supplierRepository;
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
    }

    /// <inheritdoc />
    public async Task<SubcontractedItemsToBeDeliveredReportDto> GetItemsToBeDeliveredReportAsync(SubcontractingInwardReportFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _orderRepository.GetQueryableAsync();
        var orders = query
            .Where(o => o.CompanyId == input.CompanyId
                     && o.Status != SubcontractingInwardOrderStatus.Draft
                     && o.Status != SubcontractingInwardOrderStatus.Closed
                     && o.Status != SubcontractingInwardOrderStatus.Cancelled
                     && o.OrderDate >= from
                     && o.OrderDate <= to)
            .WhereIf(input.SubcontractingInwardOrderId.HasValue, o => o.Id == input.SubcontractingInwardOrderId!.Value)
            .WhereIf(input.SupplierId.HasValue, o => o.SupplierId == input.SupplierId!.Value)
            .ToList();

        if (orders.Count == 0)
        {
            return new SubcontractedItemsToBeDeliveredReportDto();
        }

        var allLines = orders
            .SelectMany(o => o.Items.Select(item => new { Order = o, Item = item }))
            .WhereIf(input.ItemId.HasValue, x => x.Item.ItemId == input.ItemId!.Value)
            .ToList();

        if (allLines.Count == 0)
        {
            return new SubcontractedItemsToBeDeliveredReportDto();
        }

        var itemIds = allLines.Select(x => x.Item.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemsMap = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var suppliersMap = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var soIds = orders.Where(o => o.SalesOrderId.HasValue).Select(o => o.SalesOrderId!.Value).Distinct().ToList();
        var soMap = new Dictionary<Guid, Guid>();
        if (soIds.Count > 0)
        {
            var soQuery = await _salesOrderRepository.GetQueryableAsync();
            soMap = soQuery.Where(s => soIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.CustomerId);
        }

        var customerIds = soMap.Values.Distinct().ToList();
        var customerMap = new Dictionary<Guid, string>();
        if (customerIds.Count > 0)
        {
            var custQuery = await _customerRepository.GetQueryableAsync();
            customerMap = custQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);
        }

        var rows = new List<SubcontractedItemToBeDeliveredRowDto>();
        foreach (var line in allLines)
        {
            var pendingQty = line.Item.PendingReceiptQty;
            if (pendingQty <= 0) continue;

            itemsMap.TryGetValue(line.Item.ItemId, out var itemMaster);

            string partyName = string.Empty;
            if (line.Order.SalesOrderId.HasValue &&
                soMap.TryGetValue(line.Order.SalesOrderId.Value, out var custId) &&
                customerMap.TryGetValue(custId, out var custName) &&
                !string.IsNullOrWhiteSpace(custName))
            {
                partyName = custName;
            }
            else if (suppliersMap.TryGetValue(line.Order.SupplierId, out var suppName))
            {
                partyName = suppName;
            }

            rows.Add(new SubcontractedItemToBeDeliveredRowDto
            {
                SubcontractingInwardOrderId = line.Order.Id,
                OrderNumber = line.Order.OrderNumber,
                OrderDate = line.Order.OrderDate,
                SupplierId = line.Order.SupplierId,
                PartyName = partyName,
                ItemId = line.Item.ItemId,
                ItemCode = itemMaster?.ItemCode ?? string.Empty,
                ItemName = itemMaster?.ItemName ?? string.Empty,
                Uom = itemMaster?.Uom ?? "Nos",
                OrderQty = line.Item.Quantity,
                ProducedQty = line.Item.ReceivedQty,
                DeliveredQty = line.Item.ReceivedQty,
                PendingQty = pendingQty
            });
        }

        return new SubcontractedItemsToBeDeliveredReportDto
        {
            Rows = rows,
            TotalOrderQty = rows.Sum(r => r.OrderQty),
            TotalProducedQty = rows.Sum(r => r.ProducedQty),
            TotalDeliveredQty = rows.Sum(r => r.DeliveredQty),
            TotalPendingQty = rows.Sum(r => r.PendingQty)
        };
    }

    /// <inheritdoc />
    public async Task<SubcontractingInwardOrderSummaryReportDto> GetOrderSummaryReportAsync(SubcontractingInwardReportFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _orderRepository.GetQueryableAsync();
        var ordersQuery = query
            .Where(o => o.CompanyId == input.CompanyId
                     && o.OrderDate >= from
                     && o.OrderDate <= to)
            .WhereIf(input.SubcontractingInwardOrderId.HasValue, o => o.Id == input.SubcontractingInwardOrderId!.Value)
            .WhereIf(input.SupplierId.HasValue, o => o.SupplierId == input.SupplierId!.Value);

        if (!string.IsNullOrWhiteSpace(input.Status) &&
            Enum.TryParse<SubcontractingInwardOrderStatus>(input.Status, true, out var statusFilter))
        {
            ordersQuery = ordersQuery.Where(o => o.Status == statusFilter);
        }
        else
        {
            ordersQuery = ordersQuery.Where(o => o.Status != SubcontractingInwardOrderStatus.Cancelled);
        }

        var orders = ordersQuery.ToList();
        if (orders.Count == 0)
        {
            return new SubcontractingInwardOrderSummaryReportDto();
        }

        var allLines = orders
            .SelectMany(o => o.Items.Select(item => new { Order = o, Item = item }))
            .WhereIf(input.ItemId.HasValue, x => x.Item.ItemId == input.ItemId!.Value)
            .ToList();

        if (allLines.Count == 0)
        {
            return new SubcontractingInwardOrderSummaryReportDto();
        }

        var itemIds = allLines.Select(x => x.Item.ItemId).Distinct().ToList();
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var itemsMap = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionary(i => i.Id);

        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();
        var suppliersMap = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionary(s => s.Id, s => s.Name);

        var soIds = orders.Where(o => o.SalesOrderId.HasValue).Select(o => o.SalesOrderId!.Value).Distinct().ToList();
        var soMap = new Dictionary<Guid, Guid>();
        if (soIds.Count > 0)
        {
            var soQuery = await _salesOrderRepository.GetQueryableAsync();
            soMap = soQuery.Where(s => soIds.Contains(s.Id)).ToDictionary(s => s.Id, s => s.CustomerId);
        }

        var customerIds = soMap.Values.Distinct().ToList();
        var customerMap = new Dictionary<Guid, string>();
        if (customerIds.Count > 0)
        {
            var custQuery = await _customerRepository.GetQueryableAsync();
            customerMap = custQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);
        }

        var rows = new List<SubcontractingInwardOrderSummaryRowDto>();
        foreach (var line in allLines)
        {
            itemsMap.TryGetValue(line.Item.ItemId, out var itemMaster);

            string partyName = string.Empty;
            if (line.Order.SalesOrderId.HasValue &&
                soMap.TryGetValue(line.Order.SalesOrderId.Value, out var custId) &&
                customerMap.TryGetValue(custId, out var custName) &&
                !string.IsNullOrWhiteSpace(custName))
            {
                partyName = custName;
            }
            else if (suppliersMap.TryGetValue(line.Order.SupplierId, out var suppName))
            {
                partyName = suppName;
            }

            rows.Add(new SubcontractingInwardOrderSummaryRowDto
            {
                SubcontractingInwardOrderId = line.Order.Id,
                OrderNumber = line.Order.OrderNumber,
                OrderDate = line.Order.OrderDate,
                SupplierId = line.Order.SupplierId,
                PartyName = partyName,
                Status = line.Order.Status,
                ItemId = line.Item.ItemId,
                ItemCode = itemMaster?.ItemCode ?? string.Empty,
                ItemName = itemMaster?.ItemName ?? string.Empty,
                Uom = itemMaster?.Uom ?? "Nos",
                OrderQty = line.Item.Quantity,
                ProducedQty = line.Item.ReceivedQty,
                DeliveredQty = line.Item.ReceivedQty,
                PendingQty = line.Item.PendingReceiptQty,
                Rate = line.Item.Rate,
                Amount = line.Item.Amount
            });
        }

        return new SubcontractingInwardOrderSummaryReportDto
        {
            Rows = rows,
            TotalOrderQty = rows.Sum(r => r.OrderQty),
            TotalProducedQty = rows.Sum(r => r.ProducedQty),
            TotalDeliveredQty = rows.Sum(r => r.DeliveredQty),
            TotalPendingQty = rows.Sum(r => r.PendingQty),
            TotalAmount = rows.Sum(r => r.Amount)
        };
    }
}
