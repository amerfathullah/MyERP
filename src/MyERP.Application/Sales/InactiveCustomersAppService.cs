using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Inactive Customers report application service.
/// Implements ERPNext selling/report/inactive_customers.
/// Supports both Sales Order and Sales Invoice doc types with threshold filtering.
/// </summary>
[Authorize(MyERPPermissions.Customers.Default)]
public class InactiveCustomersAppService : ApplicationService, IInactiveCustomersAppService
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Territory, Guid> _territoryRepository;
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepository;

    public InactiveCustomersAppService(
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Territory, Guid> territoryRepository,
        IRepository<CustomerGroup, Guid> customerGroupRepository)
    {
        _customerRepository = customerRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _territoryRepository = territoryRepository;
        _customerGroupRepository = customerGroupRepository;
    }

    public async Task<InactiveCustomersReportDto> GetReportAsync(InactiveCustomersFilterDto input)
    {
        if (input.DaysSinceLastOrder <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "'Days Since Last Order' must be greater than zero.");
        }

        var docType = string.IsNullOrWhiteSpace(input.DocType) ? "Sales Order" : input.DocType.Trim();
        var isSalesOrder = docType.Equals("Sales Order", StringComparison.OrdinalIgnoreCase);
        var isSalesInvoice = docType.Equals("Sales Invoice", StringComparison.OrdinalIgnoreCase);

        if (!isSalesOrder && !isSalesInvoice)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Invalid value '{input.DocType}' for 'DocType'.");
        }

        var asOfDate = (input.AsOfDate ?? Clock.Now).Date;

        // 1. Fetch Customers matching basic filters
        var customerQuery = await _customerRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
        {
            customerQuery = customerQuery.Where(c => c.CompanyId == input.CompanyId.Value);
        }
        if (input.CustomerId.HasValue)
        {
            customerQuery = customerQuery.Where(c => c.Id == input.CustomerId.Value);
        }
        if (input.CustomerGroupId.HasValue)
        {
            customerQuery = customerQuery.Where(c => c.CustomerGroupId == input.CustomerGroupId.Value);
        }
        if (input.TerritoryId.HasValue)
        {
            customerQuery = customerQuery.Where(c => c.TerritoryId == input.TerritoryId.Value);
        }

        var customers = customerQuery.ToList();
        if (customers.Count == 0)
        {
            return new InactiveCustomersReportDto
            {
                DocType = isSalesOrder ? "Sales Order" : "Sales Invoice",
                DaysSinceLastOrderThreshold = input.DaysSinceLastOrder
            };
        }

        var customerIds = customers.Select(c => c.Id).ToHashSet();

        // Resolve lookup names
        var territoryIds = customers.Where(c => c.TerritoryId.HasValue).Select(c => c.TerritoryId!.Value).Distinct().ToList();
        var territoryMap = new Dictionary<Guid, string>();
        if (territoryIds.Count > 0)
        {
            var territories = (await _territoryRepository.GetQueryableAsync())
                .Where(t => territoryIds.Contains(t.Id))
                .ToList();
            territoryMap = territories.ToDictionary(t => t.Id, t => t.Name);
        }

        var customerGroupIds = customers.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
        var customerGroupMap = new Dictionary<Guid, string>();
        if (customerGroupIds.Count > 0)
        {
            var groups = (await _customerGroupRepository.GetQueryableAsync())
                .Where(g => customerGroupIds.Contains(g.Id))
                .ToList();
            customerGroupMap = groups.ToDictionary(g => g.Id, g => g.Name);
        }

        var customerLookup = customers.ToDictionary(c => c.Id);
        var reportItems = new List<InactiveCustomerItemDto>();

        if (isSalesOrder)
        {
            var soQuery = await _salesOrderRepository.GetQueryableAsync();
            var orders = soQuery.Where(so => customerIds.Contains(so.CustomerId)
                    && so.Status != DocumentStatus.Draft
                    && so.Status != DocumentStatus.Cancelled
                    && (!input.CompanyId.HasValue || so.CompanyId == input.CompanyId.Value)
                    && so.OrderDate <= asOfDate)
                .ToList();

            var groupedOrders = orders.GroupBy(o => o.CustomerId);

            foreach (var group in groupedOrders)
            {
                if (!customerLookup.TryGetValue(group.Key, out var customer))
                {
                    continue;
                }

                var list = group.ToList();
                var latest = list
                    .OrderByDescending(o => o.OrderDate)
                    .ThenByDescending(o => o.CreationTime)
                    .First();

                var daysSince = (asOfDate - latest.OrderDate.Date).Days;
                if (daysSince < input.DaysSinceLastOrder)
                {
                    continue;
                }

                decimal totalOrderValue = 0m;
                decimal totalConsidered = 0m;

                foreach (var o in list)
                {
                    var rate = o.ExchangeRate > 0 ? o.ExchangeRate : 1m;
                    var baseNet = o.NetTotal * rate;
                    totalOrderValue += baseNet;

                    // Closed order is only partially fulfilled, so count pro rata by delivery
                    if (o.Status == DocumentStatus.Closed)
                    {
                        totalConsidered += Math.Round(baseNet * o.PerDelivered / 100m, 2);
                    }
                    else
                    {
                        totalConsidered += baseNet;
                    }
                }

                var latestRate = latest.ExchangeRate > 0 ? latest.ExchangeRate : 1m;
                var lastOrderAmount = latest.NetTotal * latestRate;

                reportItems.Add(new InactiveCustomerItemDto
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    CustomerCode = customer.CustomerCode,
                    Territory = customer.TerritoryId.HasValue && territoryMap.TryGetValue(customer.TerritoryId.Value, out var tName) ? tName : null,
                    CustomerGroup = customer.CustomerGroupId.HasValue && customerGroupMap.TryGetValue(customer.CustomerGroupId.Value, out var cgName) ? cgName : null,
                    NumOfOrder = list.Count,
                    TotalOrderValue = Math.Round(totalOrderValue, 2),
                    TotalOrderConsidered = Math.Round(totalConsidered, 2),
                    LastOrderAmount = Math.Round(lastOrderAmount, 2),
                    LastOrderDate = latest.OrderDate,
                    DaysSinceLastOrder = daysSince
                });
            }
        }
        else
        {
            var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
            var invoices = siQuery.Where(si => customerIds.Contains(si.CustomerId)
                    && si.Status != DocumentStatus.Draft
                    && si.Status != DocumentStatus.Cancelled
                    && !si.IsReturn
                    && (!input.CompanyId.HasValue || si.CompanyId == input.CompanyId.Value)
                    && si.IssueDate <= asOfDate)
                .ToList();

            var groupedInvoices = invoices.GroupBy(i => i.CustomerId);

            foreach (var group in groupedInvoices)
            {
                if (!customerLookup.TryGetValue(group.Key, out var customer))
                {
                    continue;
                }

                var list = group.ToList();
                var latest = list
                    .OrderByDescending(i => i.IssueDate)
                    .ThenByDescending(i => i.CreationTime)
                    .First();

                var daysSince = (asOfDate - latest.IssueDate.Date).Days;
                if (daysSince < input.DaysSinceLastOrder)
                {
                    continue;
                }

                var totalOrderValue = list.Sum(i => i.BaseNetTotal);
                var totalConsidered = totalOrderValue;
                var lastOrderAmount = latest.BaseNetTotal;

                reportItems.Add(new InactiveCustomerItemDto
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    CustomerCode = customer.CustomerCode,
                    Territory = customer.TerritoryId.HasValue && territoryMap.TryGetValue(customer.TerritoryId.Value, out var tName) ? tName : null,
                    CustomerGroup = customer.CustomerGroupId.HasValue && customerGroupMap.TryGetValue(customer.CustomerGroupId.Value, out var cgName) ? cgName : null,
                    NumOfOrder = list.Count,
                    TotalOrderValue = Math.Round(totalOrderValue, 2),
                    TotalOrderConsidered = Math.Round(totalConsidered, 2),
                    LastOrderAmount = Math.Round(lastOrderAmount, 2),
                    LastOrderDate = latest.IssueDate,
                    DaysSinceLastOrder = daysSince
                });
            }
        }

        var sortedItems = reportItems.OrderByDescending(r => r.DaysSinceLastOrder).ToList();

        return new InactiveCustomersReportDto
        {
            Items = sortedItems,
            TotalCustomers = sortedItems.Count,
            TotalOrders = sortedItems.Sum(r => r.NumOfOrder),
            TotalOrderValue = Math.Round(sortedItems.Sum(r => r.TotalOrderValue), 2),
            TotalOrderConsidered = Math.Round(sortedItems.Sum(r => r.TotalOrderConsidered), 2),
            DocType = isSalesOrder ? "Sales Order" : "Sales Invoice",
            DaysSinceLastOrderThreshold = input.DaysSinceLastOrder
        };
    }
}
