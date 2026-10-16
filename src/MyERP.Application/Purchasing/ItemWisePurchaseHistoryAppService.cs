using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

/// <summary>
/// Item-wise Purchase History report application service.
/// Implements ERPNext buying/report/item_wise_purchase_history.
/// Reports transaction-level line item purchase order details, received quantities, and billed amounts.
/// </summary>
[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class ItemWisePurchaseHistoryAppService : ApplicationService, IItemWisePurchaseHistoryAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<SupplierGroup, Guid> _supplierGroupRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;

    public ItemWisePurchaseHistoryAppService(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<SupplierGroup, Guid> supplierGroupRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
        _supplierGroupRepository = supplierGroupRepository;
        _itemGroupRepository = itemGroupRepository;
    }

    public async Task<ItemWisePurchaseHistoryReportDto> GetHistoryAsync(GetItemWisePurchaseHistoryDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be greater than To Date.");
        }

        var fromDate = input.FromDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var toDate = input.ToDate ?? DateTime.UtcNow.Date;

        var poQuery = await _purchaseOrderRepository.WithDetailsAsync(po => po.Items);
        var orders = poQuery
            .Where(po => po.Status != DocumentStatus.Draft
                && po.Status != DocumentStatus.Cancelled
                && po.OrderDate >= fromDate
                && po.OrderDate <= toDate)
            .WhereIf(input.CompanyId.HasValue, po => po.CompanyId == input.CompanyId!.Value)
            .WhereIf(input.SupplierId.HasValue, po => po.SupplierId == input.SupplierId!.Value)
            .ToList();

        if (orders.Count == 0)
        {
            return new ItemWisePurchaseHistoryReportDto();
        }

        var supplierIds = orders.Select(po => po.SupplierId).Distinct().ToList();
        var allItemIds = orders.SelectMany(po => po.Items).Select(i => i.ItemId).Distinct().ToList();
        var companyIds = orders.Select(po => po.CompanyId).Distinct().ToList();

        // Load lookups
        var supplierMap = (await _supplierRepository.GetQueryableAsync())
            .Where(s => supplierIds.Contains(s.Id))
            .ToList()
            .ToDictionary(s => s.Id);

        var itemMap = (await _itemRepository.GetQueryableAsync())
            .Where(i => allItemIds.Contains(i.Id))
            .ToList()
            .ToDictionary(i => i.Id);

        var companyMap = (await _companyRepository.GetQueryableAsync())
            .Where(c => companyIds.Contains(c.Id))
            .ToList()
            .ToDictionary(c => c.Id);

        var supplierGroupIds = supplierMap.Values.Where(s => s.SupplierGroupId.HasValue).Select(s => s.SupplierGroupId!.Value).Distinct().ToList();
        var supplierGroupMap = new Dictionary<Guid, string>();
        if (supplierGroupIds.Count > 0)
        {
            supplierGroupMap = (await _supplierGroupRepository.GetQueryableAsync())
                .Where(g => supplierGroupIds.Contains(g.Id))
                .ToList()
                .ToDictionary(g => g.Id, g => g.Name);
        }

        var itemGroupIds = itemMap.Values.Where(i => i.ItemGroupId.HasValue).Select(i => i.ItemGroupId!.Value).Distinct().ToList();
        var itemGroupMap = new Dictionary<Guid, string>();
        if (itemGroupIds.Count > 0)
        {
            itemGroupMap = (await _itemGroupRepository.GetQueryableAsync())
                .Where(g => itemGroupIds.Contains(g.Id))
                .ToList()
                .ToDictionary(g => g.Id, g => g.Name);
        }

        var rows = new List<ItemWisePurchaseHistoryRowDto>();

        foreach (var order in orders)
        {
            supplierMap.TryGetValue(order.SupplierId, out var supplier);
            companyMap.TryGetValue(order.CompanyId, out var company);

            var rateMultiplier = order.ExchangeRate > 0 ? order.ExchangeRate : 1m;
            var supplierName = supplier?.Name ?? order.SupplierId.ToString().Substring(0, 8);
            var supplierGroupName = supplier?.SupplierGroupId.HasValue == true && supplierGroupMap.TryGetValue(supplier.SupplierGroupId.Value, out var sgName)
                ? sgName
                : null;
            var companyName = company?.Name ?? string.Empty;
            var currency = !string.IsNullOrWhiteSpace(company?.CurrencyCode) ? company.CurrencyCode : order.CurrencyCode;

            foreach (var itemRow in order.Items)
            {
                if (input.ItemId.HasValue && itemRow.ItemId != input.ItemId.Value)
                {
                    continue;
                }

                itemMap.TryGetValue(itemRow.ItemId, out var item);

                if (!string.IsNullOrWhiteSpace(input.ItemCode)
                    && !string.Equals(item?.ItemCode, input.ItemCode, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (input.ItemGroupId.HasValue && item?.ItemGroupId != input.ItemGroupId.Value)
                {
                    continue;
                }

                var itemGroupName = item?.ItemGroupId.HasValue == true && itemGroupMap.TryGetValue(item.ItemGroupId.Value, out var igName)
                    ? igName
                    : item?.ItemGroup;

                var baseRate = Math.Round(itemRow.UnitPrice * rateMultiplier, 4);
                var baseAmount = Math.Round(itemRow.LineTotal * rateMultiplier, 2);
                var billedAmount = Math.Round(itemRow.BilledQty * itemRow.UnitPrice * rateMultiplier, 2);

                rows.Add(new ItemWisePurchaseHistoryRowDto
                {
                    PurchaseOrderId = order.Id,
                    PurchaseOrderNumber = order.OrderNumber,
                    TransactionDate = order.OrderDate,
                    SupplierId = order.SupplierId,
                    SupplierName = supplierName,
                    SupplierGroup = supplierGroupName,
                    Project = order.ProjectId?.ToString(),
                    ItemId = itemRow.ItemId,
                    ItemCode = item?.ItemCode ?? string.Empty,
                    ItemName = item?.ItemName ?? itemRow.Description,
                    ItemGroup = itemGroupName,
                    Description = itemRow.Description,
                    Quantity = itemRow.Quantity,
                    Uom = itemRow.Uom,
                    Rate = baseRate,
                    Amount = baseAmount,
                    ReceivedQuantity = itemRow.ReceivedQty,
                    BilledAmount = billedAmount,
                    CompanyId = order.CompanyId,
                    CompanyName = companyName,
                    Currency = currency
                });
            }
        }

        // Order rows by TransactionDate desc, then PurchaseOrderNumber desc
        var sortedRows = rows
            .OrderByDescending(r => r.TransactionDate)
            .ThenByDescending(r => r.PurchaseOrderNumber)
            .ToList();

        // Top 30 items chart data
        var chartData = sortedRows
            .GroupBy(r => new { r.ItemCode, r.ItemName })
            .Select(g => new ItemWisePurchaseChartPointDto
            {
                ItemCode = g.Key.ItemCode,
                ItemName = g.Key.ItemName,
                Amount = Math.Round(g.Sum(r => r.Amount), 2)
            })
            .OrderByDescending(c => c.Amount)
            .Take(30)
            .ToList();

        return new ItemWisePurchaseHistoryReportDto
        {
            Rows = sortedRows,
            TotalQuantity = Math.Round(sortedRows.Sum(r => r.Quantity), 4),
            TotalAmount = Math.Round(sortedRows.Sum(r => r.Amount), 2),
            TotalReceivedQuantity = Math.Round(sortedRows.Sum(r => r.ReceivedQuantity), 4),
            TotalBilledAmount = Math.Round(sortedRows.Sum(r => r.BilledAmount), 2),
            TotalOrders = sortedRows.Select(r => r.PurchaseOrderId).Distinct().Count(),
            ChartData = chartData
        };
    }
}
