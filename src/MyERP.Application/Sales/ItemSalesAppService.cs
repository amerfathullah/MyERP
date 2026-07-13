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
public class ItemSalesAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;

    public ItemSalesAppService(IRepository<SalesInvoice, Guid> invoiceRepository)
        => _invoiceRepository = invoiceRepository;

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
}

public class ItemSalesReportDto
{
    public List<ItemSalesLineDto> Items { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalQty { get; set; }
    public int UniqueItems { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class ItemSalesLineDto
{
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal TotalQty { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageRate { get; set; }
    public int InvoiceCount { get; set; }
}
