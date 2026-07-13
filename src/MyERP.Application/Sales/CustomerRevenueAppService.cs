using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class CustomerRevenueAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;

    public CustomerRevenueAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Customer, Guid> customerRepository)
    {
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
    }

    /// <summary>
    /// Returns revenue summary grouped by customer for the given date range.
    /// Only includes posted non-return invoices.
    /// </summary>
    public async Task<CustomerRevenueReportDto> GetReportAsync(RegisterFilterDto input)
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

        // Load customer names
        var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
        var customerQuery = await _customerRepository.GetQueryableAsync();
        var customerNames = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        var customerGroups = invoices
            .GroupBy(si => si.CustomerId)
            .Select(g => new CustomerRevenueLineDto
            {
                CustomerId = g.Key,
                CustomerName = customerNames.GetValueOrDefault(g.Key, "—"),
                InvoiceCount = g.Count(),
                TotalRevenue = g.Sum(si => si.GrandTotal),
                TotalPaid = g.Sum(si => si.AmountPaid),
                TotalOutstanding = g.Sum(si => si.OutstandingAmount),
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        return new CustomerRevenueReportDto
        {
            Items = customerGroups,
            TotalRevenue = customerGroups.Sum(x => x.TotalRevenue),
            TotalOutstanding = customerGroups.Sum(x => x.TotalOutstanding),
            CustomerCount = customerGroups.Count,
            FromDate = from,
            ToDate = to,
        };
    }
}

public class CustomerRevenueReportDto
{
    public List<CustomerRevenueLineDto> Items { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int CustomerCount { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class CustomerRevenueLineDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public int InvoiceCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
}
