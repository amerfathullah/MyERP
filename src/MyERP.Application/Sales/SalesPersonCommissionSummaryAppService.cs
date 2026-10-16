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

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesPersonCommissionSummaryAppService : ApplicationService, ISalesPersonCommissionSummaryAppService
{
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Territory, Guid> _territoryRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public SalesPersonCommissionSummaryAppService(
        IRepository<SalesTeamEntry, Guid> teamEntryRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<SalesPerson, Guid> salesPersonRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Territory, Guid> territoryRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _teamEntryRepo = teamEntryRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _salesOrderRepo = salesOrderRepo;
        _salesPersonRepo = salesPersonRepo;
        _customerRepo = customerRepo;
        _territoryRepo = territoryRepo;
        _companyRepo = companyRepo;
    }

    public async Task<SalesPersonCommissionSummaryReportDto> GetReportAsync(GetSalesPersonCommissionSummaryInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var isSalesOrder = string.Equals(input.DocType?.Trim(), "Sales Order", StringComparison.OrdinalIgnoreCase);
        var docTypeName = isSalesOrder ? "Sales Order" : "Sales Invoice";
        var parentTypeName = isSalesOrder ? "SalesOrder" : "SalesInvoice";

        // Query customers, territories, and sales persons in memory lookup
        var customerQuery = await _customerRepo.GetQueryableAsync();
        var customers = customerQuery.Where(c => c.CompanyId == input.CompanyId).ToList();
        var customerMap = customers.ToDictionary(c => c.Id, c => c);

        var territoryQuery = await _territoryRepo.GetQueryableAsync();
        var territories = territoryQuery.ToList();
        var territoryMap = territories.ToDictionary(t => t.Id, t => t.Name);

        var spQuery = await _salesPersonRepo.GetQueryableAsync();
        var salesPersons = spQuery.ToList();
        var spMap = salesPersons.ToDictionary(sp => sp.Id, sp => sp.Name);

        var rows = new List<SalesPersonCommissionSummaryRowDto>();

        if (isSalesOrder)
        {
            var soQuery = await _salesOrderRepo.GetQueryableAsync();
            var ordersQueryable = soQuery.Where(so =>
                so.CompanyId == input.CompanyId &&
                so.Status != DocumentStatus.Cancelled);

            if (input.FromDate.HasValue)
                ordersQueryable = ordersQueryable.Where(so => so.OrderDate >= input.FromDate.Value);
            if (input.ToDate.HasValue)
                ordersQueryable = ordersQueryable.Where(so => so.OrderDate <= input.ToDate.Value);
            if (input.CustomerId.HasValue)
                ordersQueryable = ordersQueryable.Where(so => so.CustomerId == input.CustomerId.Value);

            var orders = ordersQueryable.ToList();
            if (orders.Count > 0)
            {
                var orderIds = orders.Select(so => so.Id).ToHashSet();
                var teamQuery = await _teamEntryRepo.GetQueryableAsync();
                var entries = teamQuery.Where(e =>
                    e.ParentType == parentTypeName &&
                    orderIds.Contains(e.ParentId)).ToList();

                if (input.SalesPersonId.HasValue)
                    entries = entries.Where(e => e.SalesPersonId == input.SalesPersonId.Value).ToList();

                var orderMap = orders.ToDictionary(so => so.Id, so => so);

                foreach (var entry in entries)
                {
                    if (!orderMap.TryGetValue(entry.ParentId, out var order))
                        continue;

                    customerMap.TryGetValue(order.CustomerId, out var cust);
                    string? terrName = null;
                    if (cust?.TerritoryId.HasValue == true)
                    {
                        territoryMap.TryGetValue(cust.TerritoryId.Value, out terrName);
                    }

                    if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                        continue;

                    spMap.TryGetValue(entry.SalesPersonId, out var spName);

                    rows.Add(new SalesPersonCommissionSummaryRowDto
                    {
                        TransactionId = order.Id,
                        TransactionNumber = order.OrderNumber,
                        DocType = docTypeName,
                        PostingDate = order.OrderDate,
                        CustomerId = order.CustomerId,
                        CustomerName = cust?.Name ?? order.CustomerId.ToString()[..8],
                        TerritoryName = terrName,
                        NetAmount = order.NetTotal > 0 ? order.NetTotal : order.GrandTotal,
                        SalesPersonId = entry.SalesPersonId,
                        SalesPersonName = spName ?? entry.SalesPersonId.ToString()[..8],
                        AllocatedPercentage = entry.AllocatedPercentage,
                        CommissionRate = entry.CommissionRate,
                        AllocatedAmount = entry.AllocatedAmount,
                        Incentives = entry.Incentives,
                    });
                }
            }
        }
        else
        {
            var siQuery = await _salesInvoiceRepo.GetQueryableAsync();
            var invoicesQueryable = siQuery.Where(si =>
                si.CompanyId == input.CompanyId &&
                si.Status != DocumentStatus.Cancelled);

            if (input.FromDate.HasValue)
                invoicesQueryable = invoicesQueryable.Where(si => si.IssueDate >= input.FromDate.Value);
            if (input.ToDate.HasValue)
                invoicesQueryable = invoicesQueryable.Where(si => si.IssueDate <= input.ToDate.Value);
            if (input.CustomerId.HasValue)
                invoicesQueryable = invoicesQueryable.Where(si => si.CustomerId == input.CustomerId.Value);

            var invoices = invoicesQueryable.ToList();
            if (invoices.Count > 0)
            {
                var invoiceIds = invoices.Select(si => si.Id).ToHashSet();
                var teamQuery = await _teamEntryRepo.GetQueryableAsync();
                var entries = teamQuery.Where(e =>
                    e.ParentType == parentTypeName &&
                    invoiceIds.Contains(e.ParentId)).ToList();

                if (input.SalesPersonId.HasValue)
                    entries = entries.Where(e => e.SalesPersonId == input.SalesPersonId.Value).ToList();

                var invoiceMap = invoices.ToDictionary(si => si.Id, si => si);

                foreach (var entry in entries)
                {
                    if (!invoiceMap.TryGetValue(entry.ParentId, out var invoice))
                        continue;

                    customerMap.TryGetValue(invoice.CustomerId, out var cust);
                    string? terrName = null;
                    if (cust?.TerritoryId.HasValue == true)
                    {
                        territoryMap.TryGetValue(cust.TerritoryId.Value, out terrName);
                    }

                    if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                        continue;

                    spMap.TryGetValue(entry.SalesPersonId, out var spName);

                    rows.Add(new SalesPersonCommissionSummaryRowDto
                    {
                        TransactionId = invoice.Id,
                        TransactionNumber = invoice.InvoiceNumber,
                        DocType = docTypeName,
                        PostingDate = invoice.IssueDate,
                        CustomerId = invoice.CustomerId,
                        CustomerName = cust?.Name ?? invoice.CustomerId.ToString()[..8],
                        TerritoryName = terrName,
                        NetAmount = invoice.NetTotal > 0 ? invoice.NetTotal : invoice.GrandTotal,
                        SalesPersonId = entry.SalesPersonId,
                        SalesPersonName = spName ?? entry.SalesPersonId.ToString()[..8],
                        AllocatedPercentage = entry.AllocatedPercentage,
                        CommissionRate = entry.CommissionRate,
                        AllocatedAmount = entry.AllocatedAmount,
                        Incentives = entry.Incentives,
                    });
                }
            }
        }

        var sortedRows = rows
            .OrderByDescending(r => r.PostingDate)
            .ThenBy(r => r.TransactionNumber)
            .ThenBy(r => r.SalesPersonName)
            .ToList();

        var distinctTx = sortedRows
            .Select(r => new { r.TransactionId, r.NetAmount })
            .Distinct()
            .ToList();

        return new SalesPersonCommissionSummaryReportDto
        {
            Currency = currency,
            DocType = docTypeName,
            Rows = sortedRows,
            TotalNetAmount = distinctTx.Sum(x => x.NetAmount),
            TotalAllocatedAmount = sortedRows.Sum(r => r.AllocatedAmount),
            TotalIncentives = sortedRows.Sum(r => r.Incentives),
            TransactionCount = distinctTx.Count,
        };
    }
}
