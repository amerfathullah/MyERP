using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize]
public class PendingSoItemsForPurchaseRequestAppService : ApplicationService, IPendingSoItemsForPurchaseRequestAppService
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<MaterialRequest, Guid> _materialRequestRepo;
    private readonly ProductBundleDecompositionService _bundleDecompositionService;

    public PendingSoItemsForPurchaseRequestAppService(
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<MaterialRequest, Guid> materialRequestRepo,
        ProductBundleDecompositionService bundleDecompositionService)
    {
        _salesOrderRepo = salesOrderRepo;
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
        _materialRequestRepo = materialRequestRepo;
        _bundleDecompositionService = bundleDecompositionService;
    }

    public async Task<PendingSoItemsForPurchaseRequestReportDto> GetReportAsync(PendingSoItemsForPurchaseRequestRequestDto input)
    {
        var soQuery = await _salesOrderRepo.WithDetailsAsync(x => x.Items);
        var orders = soQuery
            .Where(x => x.CompanyId == input.CompanyId && x.Status != DocumentStatus.Draft && x.Status != DocumentStatus.Cancelled)
            .ToList();

        if (input.CustomerId.HasValue)
        {
            orders = orders.Where(x => x.CustomerId == input.CustomerId.Value).ToList();
        }

        if (input.SalesOrderId.HasValue)
        {
            orders = orders.Where(x => x.Id == input.SalesOrderId.Value).ToList();
        }

        if (input.FromDate.HasValue)
        {
            orders = orders.Where(x => x.OrderDate.Date >= input.FromDate.Value.Date).ToList();
        }

        if (input.ToDate.HasValue)
        {
            orders = orders.Where(x => x.OrderDate.Date <= input.ToDate.Value.Date).ToList();
        }

        if (orders.Count == 0)
        {
            return new PendingSoItemsForPurchaseRequestReportDto
            {
                CompanyId = input.CompanyId,
                Rows = new List<PendingSoItemsForPurchaseRequestRowDto>()
            };
        }

        var customerIds = orders.Select(x => x.CustomerId).Distinct().ToList();
        var customers = (await _customerRepo.GetListAsync(c => customerIds.Contains(c.Id)))
            .ToDictionary(c => c.Id);

        var soItemIds = orders.SelectMany(x => x.Items).Select(i => i.ItemId).Distinct().ToList();
        var bundleItemIds = await _bundleDecompositionService.GetBundleItemIdsAsync(soItemIds);

        // Preload MaterialRequests for these orders
        var soIds = orders.Select(o => o.Id).ToHashSet();
        var mrQuery = await _materialRequestRepo.WithDetailsAsync(mr => mr.Items);
        var materialRequests = mrQuery
            .Where(mr => mr.CompanyId == input.CompanyId && mr.Status != DocumentStatus.Draft && mr.Status != DocumentStatus.Cancelled)
            .ToList();

        var mrLinesBySoAndItem = materialRequests
            .SelectMany(mr => mr.Items.Select(item => new { RequestNumber = mr.RequestNumber, Item = item }))
            .Where(x => x.Item.SalesOrderId.HasValue && soIds.Contains(x.Item.SalesOrderId.Value))
            .GroupBy(x => (SalesOrderId: x.Item.SalesOrderId!.Value, x.Item.ItemId))
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TotalQty = g.Sum(x => x.Item.Quantity),
                    RequestNumbers = g.Select(x => x.RequestNumber).Distinct().ToList()
                });

        // Also track by SalesOrderItemId if available
        var mrLinesBySoItem = materialRequests
            .SelectMany(mr => mr.Items.Select(item => new { RequestNumber = mr.RequestNumber, Item = item }))
            .Where(x => x.Item.SalesOrderItemId.HasValue)
            .GroupBy(x => x.Item.SalesOrderItemId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    TotalQty = g.Sum(x => x.Item.Quantity),
                    RequestNumbers = g.Select(x => x.RequestNumber).Distinct().ToList()
                });

        // Preload items dictionary
        var allItemIdsToLoad = new HashSet<Guid>(soItemIds);
        var rows = new List<PendingSoItemsForPurchaseRequestRowDto>();

        foreach (var order in orders)
        {
            customers.TryGetValue(order.CustomerId, out var customer);
            var customerName = customer?.Name ?? "Unknown";

            foreach (var soItem in order.Items)
            {
                if (bundleItemIds.Contains(soItem.ItemId))
                {
                    var components = await _bundleDecompositionService.DecomposeAsync(soItem.ItemId, soItem.Quantity, soItem.UnitPrice);
                    foreach (var component in components)
                    {
                        allItemIdsToLoad.Add(component.ComponentItemId);
                        var mrKey = (order.Id, component.ComponentItemId);
                        var requestedQty = 0m;
                        var mrNumbers = new List<string>();

                        if (mrLinesBySoAndItem.TryGetValue(mrKey, out var mrInfo))
                        {
                            requestedQty = mrInfo.TotalQty;
                            mrNumbers = mrInfo.RequestNumbers;
                        }

                        if (component.Qty > requestedQty)
                        {
                            rows.Add(new PendingSoItemsForPurchaseRequestRowDto
                            {
                                SalesOrderId = order.Id,
                                SalesOrderNumber = order.OrderNumber,
                                OrderDate = order.OrderDate,
                                CustomerId = order.CustomerId,
                                CustomerName = customerName,
                                ItemId = component.ComponentItemId,
                                ItemCode = string.Empty, // resolved below
                                ItemName = component.ComponentItemName ?? string.Empty,
                                Description = $"Bundle Component of {soItem.Description}",
                                MaterialRequests = string.Join(", ", mrNumbers),
                                SoQuantity = component.Qty,
                                RequestedQuantity = requestedQty,
                                PendingQuantity = component.Qty - requestedQty,
                                IsProductBundleComponent = true
                            });
                        }
                    }
                }
                else
                {
                    var requestedQty = 0m;
                    var mrNumbers = new List<string>();

                    if (mrLinesBySoItem.TryGetValue(soItem.Id, out var soItemMrInfo))
                    {
                        requestedQty = soItemMrInfo.TotalQty;
                        mrNumbers = soItemMrInfo.RequestNumbers;
                    }
                    else if (mrLinesBySoAndItem.TryGetValue((order.Id, soItem.ItemId), out var mrInfo))
                    {
                        requestedQty = mrInfo.TotalQty;
                        mrNumbers = mrInfo.RequestNumbers;
                    }

                    if (soItem.Quantity > requestedQty)
                    {
                        rows.Add(new PendingSoItemsForPurchaseRequestRowDto
                        {
                            SalesOrderId = order.Id,
                            SalesOrderNumber = order.OrderNumber,
                            OrderDate = order.OrderDate,
                            CustomerId = order.CustomerId,
                            CustomerName = customerName,
                            ItemId = soItem.ItemId,
                            ItemCode = string.Empty, // resolved below
                            ItemName = soItem.Description,
                            Description = soItem.Description,
                            MaterialRequests = string.Join(", ", mrNumbers),
                            SoQuantity = soItem.Quantity,
                            RequestedQuantity = requestedQty,
                            PendingQuantity = soItem.Quantity - requestedQty,
                            IsProductBundleComponent = false
                        });
                    }
                }
            }
        }

        // Resolve item master codes and names
        var itemsDict = (await _itemRepo.GetListAsync(i => allItemIdsToLoad.Contains(i.Id)))
            .ToDictionary(i => i.Id);

        foreach (var row in rows)
        {
            if (itemsDict.TryGetValue(row.ItemId, out var item))
            {
                row.ItemCode = item.ItemCode;
                if (string.IsNullOrWhiteSpace(row.ItemName) || row.ItemName == row.Description)
                {
                    row.ItemName = item.ItemName;
                }
            }
            else
            {
                row.ItemCode = "ITEM";
            }
        }

        if (input.ItemId.HasValue)
        {
            rows = rows.Where(r => r.ItemId == input.ItemId.Value).ToList();
        }

        rows = rows
            .OrderBy(r => r.SalesOrderNumber)
            .ThenBy(r => r.ItemCode)
            .ToList();

        return new PendingSoItemsForPurchaseRequestReportDto
        {
            CompanyId = input.CompanyId,
            Rows = rows,
            TotalSalesOrders = rows.Select(r => r.SalesOrderId).Distinct().Count(),
            TotalItems = rows.Select(r => r.ItemId).Distinct().Count(),
            TotalSoQuantity = rows.Sum(r => r.SoQuantity),
            TotalRequestedQuantity = rows.Sum(r => r.RequestedQuantity),
            TotalPendingQuantity = rows.Sum(r => r.PendingQuantity)
        };
    }
}
