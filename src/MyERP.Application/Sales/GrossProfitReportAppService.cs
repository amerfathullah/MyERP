using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class GrossProfitReportAppService : ApplicationService, IGrossProfitReportAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly GrossProfitService _grossProfitService;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public GrossProfitReportAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        GrossProfitService grossProfitService,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _invoiceRepository = invoiceRepository;
        _grossProfitService = grossProfitService;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<GrossProfitReportDto> GetReportAsync(GrossProfitRequestDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;
        var groupBy = (input.GroupBy ?? "Invoice").Trim();

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoicesQ = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == Core.DocumentStatus.Posted
                      && si.IssueDate >= from
                      && si.IssueDate <= to);

        if (input.CustomerId.HasValue)
        {
            invoicesQ = invoicesQ.Where(si => si.CustomerId == input.CustomerId.Value);
        }

        var invoices = invoicesQ
            .OrderByDescending(si => si.IssueDate)
            .ToList();

        var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
        var itemIds = invoices.SelectMany(si => si.Items).Select(i => i.ItemId).Distinct().ToList();
        var warehouseIds = invoices.Where(si => si.WarehouseId.HasValue).Select(si => si.WarehouseId!.Value).Distinct().ToList();

        var customers = (await _customerRepository.GetListAsync(c => customerIds.Contains(c.Id)))
            .ToDictionary(c => c.Id, c => c.Name);
        var items = (await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id)))
            .ToDictionary(i => i.Id);
        var warehouses = (await _warehouseRepository.GetListAsync(w => warehouseIds.Contains(w.Id)))
            .ToDictionary(w => w.Id, w => w.Name);

        var lineData = new List<GrossProfitLineDto>();

        foreach (var invoice in invoices)
        {
            var customerName = customers.GetValueOrDefault(invoice.CustomerId) ?? string.Empty;
            var warehouseName = invoice.WarehouseId.HasValue ? warehouses.GetValueOrDefault(invoice.WarehouseId.Value) : null;
            var gp = _grossProfitService.CalculateForInvoice(invoice);

            foreach (var itemDetail in gp.ItemDetails)
            {
                if (input.ItemId.HasValue && itemDetail.ItemId != input.ItemId.Value)
                    continue;

                var itemEntity = items.GetValueOrDefault(itemDetail.ItemId);
                var revenue = Math.Round(itemDetail.Quantity * itemDetail.SellingRate, 2);
                var cost = Math.Round(itemDetail.Quantity * itemDetail.ValuationRate, 2);
                var profit = revenue - cost;
                var profitPercent = revenue > 0 ? Math.Round((profit / revenue) * 100m, 2) : 0m;

                lineData.Add(new GrossProfitLineDto
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber ?? string.Empty,
                    IssueDate = invoice.IssueDate,
                    CustomerId = invoice.CustomerId,
                    CustomerName = customerName,
                    ItemId = itemDetail.ItemId,
                    ItemCode = itemEntity?.ItemCode ?? string.Empty,
                    ItemName = itemEntity?.ItemName ?? itemDetail.Description,
                    ItemGroup = itemEntity?.ItemGroup ?? string.Empty,
                    WarehouseId = invoice.WarehouseId,
                    WarehouseName = warehouseName,
                    Quantity = itemDetail.Quantity,
                    SellingRate = itemDetail.SellingRate,
                    ValuationRate = itemDetail.ValuationRate,
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = profit,
                    GrossProfitPercentage = profitPercent
                });
            }
        }

        List<GrossProfitLineDto> resultRows;

        if (groupBy.Equals("Item", StringComparison.OrdinalIgnoreCase))
        {
            resultRows = lineData
                .GroupBy(l => l.ItemId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;
                    var avgSelling = totalQty > 0 ? Math.Round(totalRev / totalQty, 4) : 0m;
                    var avgValuation = totalQty > 0 ? Math.Round(totalCost / totalQty, 4) : 0m;

                    return new GrossProfitLineDto
                    {
                        ItemId = first.ItemId,
                        ItemCode = first.ItemCode,
                        ItemName = first.ItemName,
                        ItemGroup = first.ItemGroup,
                        Quantity = totalQty,
                        SellingRate = avgSelling,
                        ValuationRate = avgValuation,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else if (groupBy.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            resultRows = lineData
                .GroupBy(l => l.CustomerId)
                .Select(g =>
                {
                    var first = g.First();
                    var totalQty = g.Sum(x => x.Quantity);
                    var totalRev = g.Sum(x => x.Revenue);
                    var totalCost = g.Sum(x => x.Cost);
                    var profit = totalRev - totalCost;
                    var profitPct = totalRev > 0 ? Math.Round((profit / totalRev) * 100m, 2) : 0m;

                    return new GrossProfitLineDto
                    {
                        CustomerId = first.CustomerId,
                        CustomerName = first.CustomerName,
                        Quantity = totalQty,
                        Revenue = totalRev,
                        Cost = totalCost,
                        GrossProfit = profit,
                        GrossProfitPercentage = profitPct
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
        }
        else
        {
            // Default: GroupBy == "Invoice"
            resultRows = lineData
                .OrderByDescending(l => l.IssueDate)
                .ThenBy(l => l.InvoiceNumber)
                .ThenBy(l => l.ItemCode)
                .ToList();
        }

        var totalRevenue = lineData.Sum(l => l.Revenue);
        var totalCost = lineData.Sum(l => l.Cost);
        var grossProfit = totalRevenue - totalCost;
        var grossProfitPercentage = totalRevenue > 0
            ? Math.Round((grossProfit / totalRevenue) * 100m, 2)
            : 0m;

        return new GrossProfitReportDto
        {
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            GrossProfit = grossProfit,
            GrossProfitPercentage = grossProfitPercentage,
            Items = resultRows
        };
    }
}
