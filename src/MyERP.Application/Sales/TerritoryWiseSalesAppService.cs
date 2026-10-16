using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class TerritoryWiseSalesAppService : ApplicationService, ITerritoryWiseSalesAppService
{
    private readonly IRepository<Territory, Guid> _territoryRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public TerritoryWiseSalesAppService(
        IRepository<Territory, Guid> territoryRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _territoryRepository = territoryRepository;
        _opportunityRepository = opportunityRepository;
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _companyRepository = companyRepository;
    }

    public async Task<TerritoryWiseSalesReportDto> GetReportAsync(GetTerritoryWiseSalesInput input)
    {
        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        // 1. Get territories
        var territoryQueryable = await _territoryRepository.GetQueryableAsync();
        if (input.TerritoryId.HasValue)
        {
            territoryQueryable = territoryQueryable.Where(t => t.Id == input.TerritoryId.Value);
        }
        var territories = territoryQueryable.ToList();

        // 2. Query opportunities
        var oppQueryable = await _opportunityRepository.GetQueryableAsync();
        oppQueryable = oppQueryable.Where(o => o.CompanyId == input.CompanyId);

        if (input.FromDate.HasValue)
        {
            oppQueryable = oppQueryable.Where(o => o.CreationTime >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            oppQueryable = oppQueryable.Where(o => o.CreationTime <= input.ToDate.Value);
        }

        var opportunities = oppQueryable.ToList();
        var opportunityIds = opportunities.Select(o => o.Id).ToList();

        // 3. Query quotations linked to opportunities
        var quotations = new List<Quotation>();
        if (opportunityIds.Count > 0)
        {
            var quoteQueryable = await _quotationRepository.GetQueryableAsync();
            quotations = quoteQueryable.Where(q =>
                q.CompanyId == input.CompanyId &&
                q.Status != DocumentStatus.Cancelled &&
                q.OpportunityId.HasValue &&
                opportunityIds.Contains(q.OpportunityId.Value))
                .ToList();
        }
        var quotationIds = quotations.Select(q => q.Id).ToList();

        // 4. Query sales orders linked to quotations
        var salesOrders = new List<SalesOrder>();
        if (quotationIds.Count > 0)
        {
            var soQueryable = await _salesOrderRepository.WithDetailsAsync(so => so.Items);
            salesOrders = soQueryable.Where(so =>
                so.CompanyId == input.CompanyId &&
                so.Status != DocumentStatus.Cancelled &&
                so.QuotationId.HasValue &&
                quotationIds.Contains(so.QuotationId.Value))
                .ToList();
        }
        var salesOrderIds = salesOrders.Select(so => so.Id).ToList();
        var salesOrderItemIds = salesOrders
            .SelectMany(so => so.Items.Select(i => i.Id))
            .ToList();

        // 5. Query sales invoices linked to sales orders
        var salesInvoices = new List<SalesInvoice>();
        if (salesOrderItemIds.Count > 0)
        {
            var siQueryable = await _salesInvoiceRepository.WithDetailsAsync(si => si.Items);
            salesInvoices = siQueryable.Where(si =>
                si.CompanyId == input.CompanyId &&
                si.Status != DocumentStatus.Cancelled &&
                si.Items.Any(i => i.SalesOrderItemId.HasValue && salesOrderItemIds.Contains(i.SalesOrderItemId.Value)))
                .ToList();
        }

        // 6. Aggregate by territory
        var rows = new List<TerritoryWiseSalesRowDto>();
        var matchedOppIds = new HashSet<Guid>();

        foreach (var territory in territories.OrderBy(t => t.Name))
        {
            var terrOpps = opportunities
                .Where(o => !string.IsNullOrWhiteSpace(o.Territory) &&
                            (o.Territory.Equals(territory.Name, StringComparison.OrdinalIgnoreCase) ||
                             o.Territory.Equals(territory.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var o in terrOpps)
            {
                matchedOppIds.Add(o.Id);
            }

            var terrOppIds = terrOpps.Select(o => o.Id).ToHashSet();
            var terrQuotes = quotations
                .Where(q => q.OpportunityId.HasValue && terrOppIds.Contains(q.OpportunityId.Value))
                .ToList();

            var terrQuoteIds = terrQuotes.Select(q => q.Id).ToHashSet();
            var terrOrders = salesOrders
                .Where(so => so.QuotationId.HasValue && terrQuoteIds.Contains(so.QuotationId.Value))
                .ToList();

            var terrOrderItemIds = terrOrders
                .SelectMany(so => so.Items.Select(i => i.Id))
                .ToHashSet();

            var terrInvoices = salesInvoices
                .Where(si => si.Items.Any(i => i.SalesOrderItemId.HasValue && terrOrderItemIds.Contains(i.SalesOrderItemId.Value)))
                .ToList();

            rows.Add(new TerritoryWiseSalesRowDto
            {
                TerritoryId = territory.Id,
                TerritoryName = territory.Name,
                OpportunityAmount = terrOpps.Sum(o => o.OpportunityAmount * (o.ConversionRate > 0 ? o.ConversionRate : 1m)),
                QuotationAmount = terrQuotes.Sum(q => q.GrandTotal),
                OrderAmount = terrOrders.Sum(so => so.GrandTotal),
                BillingAmount = terrInvoices.Sum(si => si.GrandTotal),
                OpportunityCount = terrOpps.Count,
                QuotationCount = terrQuotes.Count,
                OrderCount = terrOrders.Count,
                BillingCount = terrInvoices.Count,
            });
        }

        // 7. Check for unassigned or non-matching territories
        var unassignedOpps = opportunities.Where(o => !matchedOppIds.Contains(o.Id)).ToList();
        if (unassignedOpps.Count > 0)
        {
            var unassignedOppIds = unassignedOpps.Select(o => o.Id).ToHashSet();
            var unassignedQuotes = quotations
                .Where(q => q.OpportunityId.HasValue && unassignedOppIds.Contains(q.OpportunityId.Value))
                .ToList();

            var unassignedQuoteIds = unassignedQuotes.Select(q => q.Id).ToHashSet();
            var unassignedOrders = salesOrders
                .Where(so => so.QuotationId.HasValue && unassignedQuoteIds.Contains(so.QuotationId.Value))
                .ToList();

            var unassignedOrderItemIds = unassignedOrders
                .SelectMany(so => so.Items.Select(i => i.Id))
                .ToHashSet();

            var unassignedInvoices = salesInvoices
                .Where(si => si.Items.Any(i => i.SalesOrderItemId.HasValue && unassignedOrderItemIds.Contains(i.SalesOrderItemId.Value)))
                .ToList();

            rows.Add(new TerritoryWiseSalesRowDto
            {
                TerritoryId = null,
                TerritoryName = "Unassigned",
                OpportunityAmount = unassignedOpps.Sum(o => o.OpportunityAmount * (o.ConversionRate > 0 ? o.ConversionRate : 1m)),
                QuotationAmount = unassignedQuotes.Sum(q => q.GrandTotal),
                OrderAmount = unassignedOrders.Sum(so => so.GrandTotal),
                BillingAmount = unassignedInvoices.Sum(si => si.GrandTotal),
                OpportunityCount = unassignedOpps.Count,
                QuotationCount = unassignedQuotes.Count,
                OrderCount = unassignedOrders.Count,
                BillingCount = unassignedInvoices.Count,
            });
        }

        return new TerritoryWiseSalesReportDto
        {
            Currency = currency,
            Rows = rows,
            TotalOpportunityAmount = rows.Sum(r => r.OpportunityAmount),
            TotalQuotationAmount = rows.Sum(r => r.QuotationAmount),
            TotalOrderAmount = rows.Sum(r => r.OrderAmount),
            TotalBillingAmount = rows.Sum(r => r.BillingAmount),
        };
    }
}
