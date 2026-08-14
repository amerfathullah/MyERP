using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Core;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesCommissionReportAppService : ApplicationService, ISalesCommissionReportAppService
{
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo;

    public SalesCommissionReportAppService(
        IRepository<SalesTeamEntry, Guid> teamEntryRepo,
        IRepository<SalesInvoice, Guid> invoiceRepo)
    {
        _teamEntryRepo = teamEntryRepo;
        _invoiceRepo = invoiceRepo;
    }

    public async Task<SalesCommissionReportDto> GetReportAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var invoiceQuery = await _invoiceRepo.GetQueryableAsync();
        var postedInvoices = invoiceQuery
            .Where(si => si.CompanyId == companyId
                         && si.Status == DocumentStatus.Posted
                         && !si.IsReturn
                         && si.IssueDate >= fromDate
                         && si.IssueDate <= toDate)
            .ToList();

        if (postedInvoices.Count == 0)
            return new SalesCommissionReportDto();

        var invoiceIds = postedInvoices.Select(i => i.Id).ToHashSet();

        var teamQuery = await _teamEntryRepo.GetQueryableAsync();
        var entries = teamQuery
            .Where(e => e.ParentType == "SalesInvoice" && invoiceIds.Contains(e.ParentId))
            .ToList();

        if (entries.Count == 0)
            return new SalesCommissionReportDto
            {
                TotalRevenue = postedInvoices.Sum(i => i.GrandTotal),
                InvoiceCount = postedInvoices.Count,
            };

        // Resolve sales person names
        var spRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesPerson, Guid>>();
        var spQuery = await spRepo.GetQueryableAsync();
        var spIds = entries.Select(e => e.SalesPersonId).Distinct().ToList();
        var spNames = spQuery.Where(sp => spIds.Contains(sp.Id))
            .Select(sp => new { sp.Id, sp.Name }).ToList()
            .ToDictionary(sp => sp.Id, sp => sp.Name);

        var bySalesPerson = entries.GroupBy(e => e.SalesPersonId).Select(g =>
        {
            spNames.TryGetValue(g.Key, out var salesPersonName);
            var totalAllocated = g.Sum(e => e.AllocatedAmount);
            var totalCommission = g.Sum(e => e.Incentives);
            var invoiceCount = g.Select(e => e.ParentId).Distinct().Count();

            return new SalesPersonCommissionRowDto
            {
                SalesPersonId = g.Key,
                SalesPersonName = salesPersonName ?? g.Key.ToString()[..8],
                InvoiceCount = invoiceCount,
                TotalAllocatedAmount = totalAllocated,
                TotalCommission = totalCommission,
                CommissionRate = totalAllocated > 0
                    ? Math.Round(totalCommission / totalAllocated * 100, 2)
                    : 0,
            };
        }).OrderByDescending(r => r.TotalCommission).ToList();

        return new SalesCommissionReportDto
        {
            TotalRevenue = postedInvoices.Sum(i => i.GrandTotal),
            TotalCommission = bySalesPerson.Sum(r => r.TotalCommission),
            InvoiceCount = postedInvoices.Count,
            SalesPersonCount = bySalesPerson.Count,
            Rows = bySalesPerson,
        };
    }
}

