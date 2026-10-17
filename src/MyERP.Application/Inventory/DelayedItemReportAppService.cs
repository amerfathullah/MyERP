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
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Delayed Item Report application service.
/// Tracks per-item delivery delays against expected sales order delivery dates.
/// Ported from ERPNext stock/report/delayed_item_report.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class DelayedItemReportAppService : ApplicationService, IDelayedItemReportAppService
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;

    public DelayedItemReportAppService(
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
    }

    public async Task<DelayedItemReportDto> GetReportAsync(DelayedItemFilterDto input)
    {
        var basedOn = string.Equals(input.BasedOn, "Sales Invoice", StringComparison.OrdinalIgnoreCase)
            ? "Sales Invoice"
            : "Delivery Note";

        var today = DateTime.UtcNow.Date;
        var fromDate = input.FromDate?.Date ?? new DateTime(today.Year, 1, 1);
        var toDate = input.ToDate?.Date ?? today;
        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var candidateVouchers = new List<(
            string VoucherType,
            Guid VoucherId,
            string VoucherNumber,
            DateTime PostingDate,
            Guid CustomerId,
            Guid? SalesOrderId,
            string? ShippingAddress,
            List<(Guid ItemId, decimal Qty, decimal Rate, decimal Amount, string? Uom, Guid? SalesOrderItemId)> Items
        )>();

        if (basedOn == "Delivery Note")
        {
            var dnQueryable = await _deliveryNoteRepository.WithDetailsAsync(d => d.Items);
            var dnQuery = dnQueryable.Where(d =>
                d.CompanyId == input.CompanyId &&
                d.Status == DocumentStatus.Submitted &&
                !d.IsReturn &&
                d.PostingDate >= fromDate &&
                d.PostingDate <= toDate);

            if (input.CustomerId.HasValue)
                dnQuery = dnQuery.Where(d => d.CustomerId == input.CustomerId.Value);

            if (input.SalesOrderId.HasValue)
                dnQuery = dnQuery.Where(d => d.SalesOrderId == input.SalesOrderId.Value);

            var dns = dnQuery.ToList();
            foreach (var dn in dns)
            {
                var dnItems = dn.Items
                    .Where(i => i.SalesOrderItemId.HasValue || dn.SalesOrderId.HasValue)
                    .Select(i => (i.ItemId, i.Quantity, i.UnitPrice, i.LineTotal, (string?)i.Uom, i.SalesOrderItemId))
                    .ToList();

                if (dnItems.Count > 0)
                {
                    candidateVouchers.Add((
                        "Delivery Note",
                        dn.Id,
                        dn.DeliveryNumber,
                        dn.PostingDate,
                        dn.CustomerId,
                        dn.SalesOrderId,
                        dn.ShippingAddress,
                        dnItems
                    ));
                }
            }
        }
        else
        {
            var siQueryable = await _salesInvoiceRepository.WithDetailsAsync(s => s.Items);
            var siQuery = siQueryable.Where(s =>
                s.CompanyId == input.CompanyId &&
                s.Status == DocumentStatus.Submitted &&
                !s.IsReturn &&
                s.UpdateStock &&
                s.IssueDate >= fromDate &&
                s.IssueDate <= toDate);

            if (input.CustomerId.HasValue)
                siQuery = siQuery.Where(s => s.CustomerId == input.CustomerId.Value);

            var sis = siQuery.ToList();
            foreach (var si in sis)
            {
                var siItems = si.Items
                    .Where(i => i.SalesOrderItemId.HasValue)
                    .Select(i => (i.ItemId, i.Quantity, i.UnitPrice, i.LineTotal, (string?)i.Uom, i.SalesOrderItemId))
                    .ToList();

                if (siItems.Count > 0)
                {
                    candidateVouchers.Add((
                        "Sales Invoice",
                        si.Id,
                        si.InvoiceNumber,
                        si.IssueDate,
                        si.CustomerId,
                        (Guid?)null,
                        (string?)null,
                        siItems
                    ));
                }
            }
        }

        if (candidateVouchers.Count == 0)
        {
            return new DelayedItemReportDto
            {
                CompanyId = input.CompanyId,
                BasedOn = basedOn
            };
        }

        // Fetch Sales Orders
        var soItemIds = candidateVouchers
            .SelectMany(v => v.Items)
            .Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value)
            .Distinct()
            .ToHashSet();

        var soHeaderIds = candidateVouchers
            .Where(v => v.SalesOrderId.HasValue)
            .Select(v => v.SalesOrderId!.Value)
            .Distinct()
            .ToHashSet();

        var soQueryable = await _salesOrderRepository.WithDetailsAsync(so => so.Items);
        var matchingSalesOrders = soQueryable
            .Where(so => soHeaderIds.Contains(so.Id) || so.Items.Any(i => soItemIds.Contains(i.Id)))
            .ToList();

        var soMap = matchingSalesOrders.ToDictionary(so => so.Id);
        var soItemMap = new Dictionary<Guid, (SalesOrderItem Item, SalesOrder Order)>();
        foreach (var so in matchingSalesOrders)
        {
            foreach (var item in so.Items)
            {
                soItemMap[item.Id] = (item, so);
            }
        }

        // Fetch Customers
        var customerIds = candidateVouchers.Select(v => v.CustomerId).Distinct().ToList();
        var custQueryable = await _customerRepository.GetQueryableAsync();
        var customers = custQueryable
            .Where(c => customerIds.Contains(c.Id))
            .ToList()
            .ToDictionary(c => c.Id, c => c.Name);

        // Fetch Items
        var itemIds = candidateVouchers.SelectMany(v => v.Items.Select(i => i.ItemId)).Distinct().ToList();
        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var itemsQuery = itemQueryable.Where(i => itemIds.Contains(i.Id));
        if (input.ItemGroupId.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }
        var items = itemsQuery.ToList().ToDictionary(i => i.Id);

        // Fetch Item Groups
        var groupIds = items.Values.Where(i => i.ItemGroupId.HasValue).Select(i => i.ItemGroupId!.Value).Distinct().ToList();
        var groupQueryable = await _itemGroupRepository.GetQueryableAsync();
        var itemGroups = groupQueryable.Where(g => groupIds.Contains(g.Id)).ToList().ToDictionary(g => g.Id, g => g.Name);

        var rows = new List<DelayedItemRowDto>();

        foreach (var v in candidateVouchers)
        {
            var customerName = customers.TryGetValue(v.CustomerId, out var cName) ? cName : string.Empty;

            foreach (var itemRow in v.Items)
            {
                if (!items.TryGetValue(itemRow.ItemId, out var item))
                    continue;

                // Resolve Sales Order & Sales Order Item
                SalesOrder? salesOrder = null;
                SalesOrderItem? soItem = null;

                if (itemRow.SalesOrderItemId.HasValue && soItemMap.TryGetValue(itemRow.SalesOrderItemId.Value, out var tuple))
                {
                    soItem = tuple.Item;
                    salesOrder = tuple.Order;
                }
                else if (v.SalesOrderId.HasValue && soMap.TryGetValue(v.SalesOrderId.Value, out var so))
                {
                    salesOrder = so;
                    soItem = so.Items.FirstOrDefault(i => i.ItemId == itemRow.ItemId);
                }

                if (salesOrder == null)
                    continue;

                if (input.SalesOrderId.HasValue && salesOrder.Id != input.SalesOrderId.Value)
                    continue;

                var expectedDeliveryDate = soItem?.DeliveryDate
                    ?? salesOrder.DeliveryDate
                    ?? v.PostingDate;

                var delayedDays = (int)(v.PostingDate.Date - expectedDeliveryDate.Date).TotalDays;

                if (input.DelayedOnly && delayedDays <= 0)
                    continue;

                var groupName = item.ItemGroup ?? (item.ItemGroupId.HasValue && itemGroups.TryGetValue(item.ItemGroupId.Value, out var gName) ? gName : string.Empty);

                rows.Add(new DelayedItemRowDto
                {
                    VoucherType = v.VoucherType,
                    VoucherId = v.VoucherId,
                    VoucherNumber = v.VoucherNumber,
                    CustomerId = v.CustomerId,
                    CustomerName = customerName,
                    ShippingAddress = v.ShippingAddress,
                    SalesOrderId = salesOrder.Id,
                    SalesOrderNumber = salesOrder.OrderNumber,
                    CustomerPoNumber = salesOrder.CustomerPoNumber,
                    ItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.ItemName,
                    ItemGroup = groupName,
                    Quantity = itemRow.Qty,
                    Uom = itemRow.Uom ?? item.Uom,
                    Rate = itemRow.Rate,
                    Amount = itemRow.Amount,
                    ExpectedDeliveryDate = expectedDeliveryDate,
                    ActualDeliveryDate = v.PostingDate,
                    DelayedDays = Math.Max(0, delayedDays)
                });
            }
        }

        // Apply optional search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            rows = rows.Where(r =>
                r.VoucherNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.SalesOrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Order by delayed days descending (longest delays first)
        rows = rows.OrderByDescending(r => r.DelayedDays).ThenByDescending(r => r.Amount).ToList();

        var totalItems = rows.Count;
        var delayedItems = rows.Count(r => r.DelayedDays > 0);
        var onTimeRate = totalItems > 0
            ? Math.Round((decimal)(totalItems - delayedItems) / totalItems * 100m, 2)
            : 100m;
        var averageDelayDays = delayedItems > 0
            ? Math.Round(rows.Where(r => r.DelayedDays > 0).Average(r => r.DelayedDays), 1)
            : 0;
        var totalDelayedAmount = rows.Where(r => r.DelayedDays > 0).Sum(r => r.Amount);

        return new DelayedItemReportDto
        {
            CompanyId = input.CompanyId,
            BasedOn = basedOn,
            TotalItems = totalItems,
            DelayedItems = delayedItems,
            OnTimeRate = onTimeRate,
            AverageDelayDays = averageDelayDays,
            TotalDelayedAmount = totalDelayedAmount,
            Rows = rows
        };
    }
}
