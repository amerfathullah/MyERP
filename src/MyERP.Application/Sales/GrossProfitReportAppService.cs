using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

public class GrossProfitReportDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitPercentage { get; set; }
    public List<GrossProfitLineDto> Items { get; set; } = new();
}

public class GrossProfitLineDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public string? CustomerName { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitPercentage { get; set; }
}

public class GrossProfitRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class GrossProfitReportAppService : ApplicationService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly GrossProfitService _grossProfitService;

    public GrossProfitReportAppService(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        GrossProfitService grossProfitService)
    {
        _invoiceRepository = invoiceRepository;
        _grossProfitService = grossProfitService;
    }

    public async Task<GrossProfitReportDto> GetReportAsync(GrossProfitRequestDto input)
    {
        var from = input.FromDate ?? DateTime.UtcNow.AddMonths(-1).Date;
        var to = input.ToDate ?? DateTime.UtcNow.Date;

        var query = await _invoiceRepository.GetQueryableAsync();
        var invoices = query
            .Where(si => si.CompanyId == input.CompanyId
                      && si.Status == Core.DocumentStatus.Posted
                      && !si.IsReturn
                      && si.IssueDate >= from
                      && si.IssueDate <= to)
            .OrderByDescending(si => si.IssueDate)
            .ToList();

        var result = new GrossProfitReportDto();
        foreach (var invoice in invoices)
        {
            var gp = _grossProfitService.CalculateForInvoice(invoice);
            result.Items.Add(new GrossProfitLineDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber ?? "",
                IssueDate = invoice.IssueDate,
                Revenue = gp.TotalRevenue,
                Cost = gp.TotalCost,
                GrossProfit = gp.GrossProfit,
                GrossProfitPercentage = gp.GrossProfitPercentage,
            });
            result.TotalRevenue += gp.TotalRevenue;
            result.TotalCost += gp.TotalCost;
        }

        result.GrossProfit = result.TotalRevenue - result.TotalCost;
        result.GrossProfitPercentage = result.TotalRevenue > 0
            ? Math.Round((result.GrossProfit / result.TotalRevenue) * 100m, 2)
            : 0m;

        return result;
    }
}
