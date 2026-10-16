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

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class ItemSalesAppService : ApplicationService, IItemSalesAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<SalesOrder, Guid>? _salesOrderRepository;
    private readonly IRepository<Customer, Guid>? _customerRepository;
    private readonly IRepository<CustomerGroup, Guid>? _customerGroupRepository;
    private readonly IRepository<Item, Guid>? _itemRepository;
    private readonly IRepository<ItemGroup, Guid>? _itemGroupRepository;
    private readonly IRepository<Company, Guid>? _companyRepository;

    public ItemSalesAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<SalesOrder, Guid>? salesOrderRepository = null,
        IRepository<Customer, Guid>? customerRepository = null,
        IRepository<CustomerGroup, Guid>? customerGroupRepository = null,
        IRepository<Item, Guid>? itemRepository = null,
        IRepository<ItemGroup, Guid>? itemGroupRepository = null,
        IRepository<Company, Guid>? companyRepository = null)
    {
        _invoiceRepository = invoiceRepository;
        _salesOrderRepository = salesOrderRepository;
        _customerRepository = customerRepository;
        _customerGroupRepository = customerGroupRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Returns item-wise sales summary: total qty sold, revenue, and avg rate per item
    /// for posted invoices in the given date range.
    /// </summary>
    public async Task<ItemSalesReportDto> GetReportAsync(RegisterFilterDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-3).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == DocumentStatus.Posted
                      && !si.IsReturn
                      && si.IssueDate >= from
                      && si.IssueDate <= to)
            .ToList();

        var itemGroups = invoices
            .SelectMany(si => si.Items.Select(item => new
            {
                item.ItemId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                Amount = item.Quantity * item.UnitPrice,
            }))
            .GroupBy(x => x.ItemId)
            .Select(g => new ItemSalesLineDto
            {
                ItemId = g.Key,
                ItemName = g.First().Description ?? "—",
                TotalQty = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Amount),
                AverageRate = g.Sum(x => x.Quantity) > 0
                    ? g.Sum(x => x.Amount) / g.Sum(x => x.Quantity) : 0,
                InvoiceCount = g.Count(),
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        return new ItemSalesReportDto
        {
            Items = itemGroups,
            TotalRevenue = itemGroups.Sum(x => x.TotalRevenue),
            TotalQty = itemGroups.Sum(x => x.TotalQty),
            UniqueItems = itemGroups.Count,
            FromDate = from,
            ToDate = to,
        };
    }

    /// <summary>
    /// Item-wise Sales History report.
    /// Maps to ERPNext selling/report/item_wise_sales_history.
    /// Per ERPNext PR #60350:
    /// - Guards date comparison (commit 326663e9a0)
    /// - Customer group read from order / transaction time (commit 98ef9605b1)
    /// - Child item groups expanded recursively (commit e6cc82f855)
    /// </summary>
    public async Task<ItemWiseSalesHistoryReportDto> GetHistoryReportAsync(GetItemWiseSalesHistoryDto input)
    {
        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value > input.ToDate.Value)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be greater than To Date.");
        }

        var soRepo = _salesOrderRepository ?? LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.WithDetailsAsync(so => so.Items);
        var ordersQuery = soQuery
            .Where(so => so.CompanyId == input.CompanyId
                      && so.Status != DocumentStatus.Draft
                      && so.Status != DocumentStatus.Cancelled);

        if (input.FromDate.HasValue)
        {
            ordersQuery = ordersQuery.Where(so => so.OrderDate >= input.FromDate.Value);
        }
        if (input.ToDate.HasValue)
        {
            ordersQuery = ordersQuery.Where(so => so.OrderDate <= input.ToDate.Value);
        }
        if (input.CustomerId.HasValue)
        {
            ordersQuery = ordersQuery.Where(so => so.CustomerId == input.CustomerId.Value);
        }

        var orders = ordersQuery.ToList();

        HashSet<Guid>? validGroupIds = null;
        if (input.ItemGroupId.HasValue)
        {
            validGroupIds = await GetItemGroupWithDescendantIdsAsync(input.ItemGroupId.Value);
        }

        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customerRepo = _customerRepository ?? LazyServiceProvider.LazyGetService<IRepository<Customer, Guid>>();
        var custGroupRepo = _customerGroupRepository ?? LazyServiceProvider.LazyGetService<IRepository<CustomerGroup, Guid>>();
        Dictionary<Guid, Customer> customerMap = new();
        Dictionary<Guid, string> customerGroupMap = new();

        if (customerRepo != null && customerIds.Count > 0)
        {
            var custQuery = await customerRepo.GetQueryableAsync();
            customerMap = custQuery.Where(c => customerIds.Contains(c.Id)).ToDictionary(c => c.Id);
            if (custGroupRepo != null)
            {
                var allCustGroups = await custGroupRepo.GetListAsync();
                customerGroupMap = allCustGroups.ToDictionary(g => g.Id, g => g.Name);
            }
        }

        var allItemIds = orders.SelectMany(o => o.Items).Select(i => i.ItemId).Distinct().ToList();
        var itemRepo = _itemRepository ?? LazyServiceProvider.LazyGetService<IRepository<Item, Guid>>();
        Dictionary<Guid, Item> itemMap = new();
        if (itemRepo != null && allItemIds.Count > 0)
        {
            var iQuery = await itemRepo.GetQueryableAsync();
            itemMap = iQuery.Where(i => allItemIds.Contains(i.Id)).ToDictionary(i => i.Id);
        }

        var compRepo = _companyRepository ?? LazyServiceProvider.LazyGetService<IRepository<Company, Guid>>();
        var companyName = (compRepo != null ? (await compRepo.FindAsync(input.CompanyId))?.Name : null) ?? string.Empty;

        var rows = new List<ItemWiseSalesHistoryRowDto>();
        foreach (var order in orders)
        {
            customerMap.TryGetValue(order.CustomerId, out var cust);
            var custName = cust?.Name ?? string.Empty;
            var custGroupName = string.Empty;
            if (cust != null && cust.CustomerGroupId.HasValue)
            {
                customerGroupMap.TryGetValue(cust.CustomerGroupId.Value, out custGroupName);
            }

            foreach (var item in order.Items)
            {
                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value) continue;
                itemMap.TryGetValue(item.ItemId, out var itemMaster);
                if (!string.IsNullOrWhiteSpace(input.ItemCode) && itemMaster != null && !string.Equals(itemMaster.ItemCode, input.ItemCode, StringComparison.OrdinalIgnoreCase)) continue;
                if (validGroupIds != null)
                {
                    if (itemMaster == null || !itemMaster.ItemGroupId.HasValue || !validGroupIds.Contains(itemMaster.ItemGroupId.Value)) continue;
                }

                var itemCode = itemMaster?.ItemCode ?? item.ItemId.ToString();
                var itemName = itemMaster?.ItemName ?? item.Description ?? string.Empty;
                var itemGroup = itemMaster?.ItemGroup ?? string.Empty;
                var rate = Math.Round(item.UnitPrice * order.ExchangeRate, 2);
                var amount = Math.Round(item.Quantity * rate, 2);
                var billedAmt = Math.Round(item.BilledQty * rate, 2);

                rows.Add(new ItemWiseSalesHistoryRowDto
                {
                    ItemId = item.ItemId,
                    ItemCode = itemCode,
                    ItemName = itemName,
                    ItemGroup = itemGroup,
                    Description = item.Description ?? string.Empty,
                    Quantity = item.Quantity,
                    Uom = item.Uom,
                    Rate = rate,
                    Amount = amount,
                    SalesOrderId = order.Id,
                    SalesOrderNumber = order.OrderNumber,
                    TransactionDate = order.OrderDate,
                    CustomerId = order.CustomerId,
                    CustomerName = custName,
                    CustomerGroup = custGroupName ?? string.Empty,
                    DeliveredQuantity = item.DeliveredQty,
                    BilledAmount = billedAmt,
                    CompanyId = order.CompanyId,
                    CompanyName = companyName,
                });
            }
        }

        return new ItemWiseSalesHistoryReportDto
        {
            Rows = rows.OrderBy(r => r.TransactionDate).ThenBy(r => r.SalesOrderNumber).ToList(),
            TotalQty = rows.Sum(r => r.Quantity),
            TotalAmount = rows.Sum(r => r.Amount),
            TotalDeliveredQty = rows.Sum(r => r.DeliveredQuantity),
            TotalBilledAmount = rows.Sum(r => r.BilledAmount),
        };
    }

    private async Task<HashSet<Guid>> GetItemGroupWithDescendantIdsAsync(Guid rootGroupId)
    {
        var result = new HashSet<Guid> { rootGroupId };
        var groupRepo = _itemGroupRepository ?? LazyServiceProvider.LazyGetService<IRepository<ItemGroup, Guid>>();
        if (groupRepo == null) return result;

        try
        {
            var allGroups = await groupRepo.GetListAsync();
            if (allGroups != null)
            {
                var queue = new Queue<Guid>();
                queue.Enqueue(rootGroupId);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var children = allGroups.Where(g => g.ParentId == current).Select(g => g.Id);
                    foreach (var childId in children)
                    {
                        if (result.Add(childId)) queue.Enqueue(childId);
                    }
                }
            }
        }
        catch
        {
        }
        return result;
    }
}
