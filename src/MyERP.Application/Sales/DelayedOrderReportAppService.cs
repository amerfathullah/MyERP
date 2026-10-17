using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class DelayedOrderReportAppService : ApplicationService, IDelayedOrderReportAppService
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public DelayedOrderReportAppService(
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
    }

    public async Task<DelayedOrderReportDto> GetReportAsync(DelayedOrderFilterDto input)
    {
        if (input.FromDate > input.ToDate)
        {
            throw new UserFriendlyException("From date cannot be after To date.");
        }

        var company = await _companyRepository.FindAsync(input.CompanyId);
        if (company == null)
        {
            throw new UserFriendlyException("Company not found.");
        }

        var dnQueryable = await _deliveryNoteRepository.WithDetailsAsync(x => x.Items);
        dnQueryable = dnQueryable.Where(d =>
            d.CompanyId == input.CompanyId &&
            d.Status == DocumentStatus.Submitted &&
            !d.IsReturn &&
            d.SalesOrderId.HasValue &&
            d.PostingDate >= input.FromDate.Date &&
            d.PostingDate <= input.ToDate.Date);

        if (input.CustomerId.HasValue && input.CustomerId != Guid.Empty)
        {
            dnQueryable = dnQueryable.Where(d => d.CustomerId == input.CustomerId.Value);
        }

        var deliveries = dnQueryable.ToList();
        if (deliveries.Count == 0)
        {
            return new DelayedOrderReportDto();
        }

        var soIds = deliveries.Select(d => d.SalesOrderId!.Value).Distinct().ToList();
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        var salesOrders = soQuery
            .Where(s => soIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.OrderNumber,
                s.OrderDate,
                s.DeliveryDate,
                s.CustomerPoNumber
            })
            .ToList();
        var soDict = salesOrders.ToDictionary(s => s.Id);

        var customerIds = deliveries.Select(d => d.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customers = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToList();
        var customerDict = customers.ToDictionary(c => c.Id, c => c.Name);

        var itemIds = deliveries.SelectMany(d => d.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemDict = new Dictionary<Guid, (string Code, string Name, string? Desc)>();
        if (itemIds.Count > 0)
        {
            var itemQuery = await _itemRepository.GetQueryableAsync();
            var itemList = itemQuery
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.Description })
                .ToList();
            itemDict = itemList.ToDictionary(i => i.Id, i => (i.ItemCode, i.ItemName, i.Description));
        }

        var rows = new List<DelayedOrderRowDto>();
        var search = input.SearchText?.Trim().ToLowerInvariant();

        int totalDeliveriesCount = 0;
        int delayedDeliveriesCount = 0;
        double totalDelayDaysSum = 0;
        decimal totalDelayedAmountSum = 0m;

        foreach (var dn in deliveries.OrderBy(d => d.PostingDate))
        {
            if (!soDict.TryGetValue(dn.SalesOrderId!.Value, out var so))
            {
                continue;
            }

            totalDeliveriesCount++;

            var expectedDate = so.DeliveryDate ?? so.OrderDate;
            var actualDate = dn.PostingDate;
            var delayedDays = Math.Max(0, (actualDate.Date - expectedDate.Date).Days);
            var isDelayed = actualDate.Date > expectedDate.Date;

            if (isDelayed)
            {
                delayedDeliveriesCount++;
                totalDelayDaysSum += delayedDays;
                totalDelayedAmountSum += dn.GrandTotal;
            }

            if (input.DelayedOnly && !isDelayed)
            {
                continue;
            }

            var customerName = customerDict.TryGetValue(dn.CustomerId, out var cName) ? cName : "Unknown";

            var items = dn.Items.Select(item =>
            {
                var itemInfo = itemDict.TryGetValue(item.ItemId, out var info)
                    ? info
                    : (Code: string.Empty, Name: item.Description, Desc: (string?)null);

                return new DelayedOrderItemDto
                {
                    ItemId = item.ItemId,
                    ItemCode = itemInfo.Code,
                    ItemName = itemInfo.Name,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    Uom = item.Uom,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                };
            }).ToList();

            if (!string.IsNullOrEmpty(search))
            {
                var matches =
                    customerName.ToLowerInvariant().Contains(search) ||
                    dn.DeliveryNumber.ToLowerInvariant().Contains(search) ||
                    so.OrderNumber.ToLowerInvariant().Contains(search) ||
                    (so.CustomerPoNumber?.ToLowerInvariant().Contains(search) ?? false) ||
                    items.Any(i => i.ItemCode.ToLowerInvariant().Contains(search) || i.ItemName.ToLowerInvariant().Contains(search));

                if (!matches)
                {
                    continue;
                }
            }

            rows.Add(new DelayedOrderRowDto
            {
                DeliveryNoteId = dn.Id,
                DeliveryNumber = dn.DeliveryNumber,
                SalesOrderId = so.Id,
                OrderNumber = so.OrderNumber,
                CustomerId = dn.CustomerId,
                CustomerName = customerName,
                CustomerPoNumber = so.CustomerPoNumber,
                ShippingAddress = dn.ShippingAddress,
                ExpectedDeliveryDate = expectedDate,
                ActualDeliveryDate = actualDate,
                DelayedDays = delayedDays,
                GrandTotal = dn.GrandTotal,
                Items = items
            });
        }

        var onTimeRate = totalDeliveriesCount > 0
            ? Math.Round((decimal)(totalDeliveriesCount - delayedDeliveriesCount) / totalDeliveriesCount * 100, 2)
            : 100m;

        var avgDelay = delayedDeliveriesCount > 0
            ? Math.Round(totalDelayDaysSum / delayedDeliveriesCount, 1)
            : 0;

        return new DelayedOrderReportDto
        {
            Rows = rows.OrderByDescending(r => r.DelayedDays).ToList(),
            TotalDeliveries = totalDeliveriesCount,
            DelayedDeliveries = delayedDeliveriesCount,
            OnTimeDeliveryRate = onTimeRate,
            AverageDelayDays = avgDelay,
            TotalDelayedAmount = Math.Round(totalDelayedAmountSum, 2)
        };
    }
}
