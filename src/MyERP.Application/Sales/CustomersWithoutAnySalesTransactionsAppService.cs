using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Customers Without Any Sales Transactions report application service.
/// Implements ERPNext selling/report/customers_without_any_sales_transactions.
/// Identifies customers having zero submitted sales invoices, sales orders, or delivery notes.
/// </summary>
[Authorize(MyERPPermissions.Customers.Default)]
public class CustomersWithoutAnySalesTransactionsAppService : ApplicationService, ICustomersWithoutAnySalesTransactionsAppService
{
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<Territory, Guid> _territoryRepository;
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepository;

    public CustomersWithoutAnySalesTransactionsAppService(
        IRepository<Customer, Guid> customerRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<Territory, Guid> territoryRepository,
        IRepository<CustomerGroup, Guid> customerGroupRepository)
    {
        _customerRepository = customerRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _salesOrderRepository = salesOrderRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _territoryRepository = territoryRepository;
        _customerGroupRepository = customerGroupRepository;
    }

    public async Task<CustomersWithoutAnySalesTransactionsReportDto> GetReportAsync(CustomersWithoutAnySalesTransactionsFilterDto input)
    {
        var customerQuery = await _customerRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            customerQuery = customerQuery.Where(c => c.CompanyId == input.CompanyId.Value);
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
            return new CustomersWithoutAnySalesTransactionsReportDto();
        }

        var customerIds = customers.Select(c => c.Id).ToHashSet();

        // 1. Customers with submitted/posted Sales Invoices
        var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var customersWithInvoices = siQuery
            .Where(si => customerIds.Contains(si.CustomerId)
                && si.Status != DocumentStatus.Draft
                && si.Status != DocumentStatus.Cancelled
                && (!input.CompanyId.HasValue || si.CompanyId == input.CompanyId.Value))
            .Select(si => si.CustomerId)
            .Distinct()
            .ToList();

        // 2. Customers with submitted Sales Orders
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        var customersWithOrders = soQuery
            .Where(so => customerIds.Contains(so.CustomerId)
                && so.Status != DocumentStatus.Draft
                && so.Status != DocumentStatus.Cancelled
                && (!input.CompanyId.HasValue || so.CompanyId == input.CompanyId.Value))
            .Select(so => so.CustomerId)
            .Distinct()
            .ToList();

        // 3. Customers with submitted Delivery Notes
        var dnQuery = await _deliveryNoteRepository.GetQueryableAsync();
        var customersWithDeliveries = dnQuery
            .Where(dn => customerIds.Contains(dn.CustomerId)
                && dn.Status != DocumentStatus.Draft
                && dn.Status != DocumentStatus.Cancelled
                && (!input.CompanyId.HasValue || dn.CompanyId == input.CompanyId.Value))
            .Select(dn => dn.CustomerId)
            .Distinct()
            .ToList();

        var customersWithSales = new HashSet<Guid>(customersWithInvoices);
        customersWithSales.UnionWith(customersWithOrders);
        customersWithSales.UnionWith(customersWithDeliveries);

        var customersWithoutSales = customers
            .Where(c => !customersWithSales.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToList();

        if (customersWithoutSales.Count == 0)
        {
            return new CustomersWithoutAnySalesTransactionsReportDto();
        }

        // Resolve lookups
        var territoryIds = customersWithoutSales.Where(c => c.TerritoryId.HasValue).Select(c => c.TerritoryId!.Value).Distinct().ToList();
        var territoryMap = new Dictionary<Guid, string>();
        if (territoryIds.Count > 0)
        {
            var territories = (await _territoryRepository.GetQueryableAsync())
                .Where(t => territoryIds.Contains(t.Id))
                .ToList();
            territoryMap = territories.ToDictionary(t => t.Id, t => t.Name);
        }

        var customerGroupIds = customersWithoutSales.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
        var customerGroupMap = new Dictionary<Guid, string>();
        if (customerGroupIds.Count > 0)
        {
            var groups = (await _customerGroupRepository.GetQueryableAsync())
                .Where(g => customerGroupIds.Contains(g.Id))
                .ToList();
            customerGroupMap = groups.ToDictionary(g => g.Id, g => g.Name);
        }

        var items = customersWithoutSales.Select(c => new CustomerWithoutSalesItemDto
        {
            CustomerId = c.Id,
            CustomerName = c.Name,
            CustomerCode = c.CustomerCode,
            Territory = c.TerritoryId.HasValue && territoryMap.TryGetValue(c.TerritoryId.Value, out var tName) ? tName : null,
            CustomerGroup = c.CustomerGroupId.HasValue && customerGroupMap.TryGetValue(c.CustomerGroupId.Value, out var cgName) ? cgName : null,
            CreationTime = c.CreationTime
        }).ToList();

        return new CustomersWithoutAnySalesTransactionsReportDto
        {
            Items = items,
            TotalCustomers = items.Count
        };
    }
}
