using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.CRM;

/// <summary>
/// Campaign Efficiency and Lead Owner Efficiency Reporting Service.
/// Evaluates conversion metrics across CRM funnel by UTM Campaign, Campaign Name, or Lead Owner.
/// Per ERPNext crm/report/campaign_efficiency and crm/report/lead_owner_efficiency / PR #59916.
/// </summary>
[Authorize(MyERPPermissions.Leads.Default)]
public class CampaignEfficiencyAppService : ApplicationService, ICampaignEfficiencyAppService
{
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<IdentityUser, Guid>? _userRepository;

    public CampaignEfficiencyAppService(
        IRepository<Lead, Guid> leadRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<IdentityUser, Guid>? userRepository = null)
    {
        _leadRepository = leadRepository;
        _customerRepository = customerRepository;
        _opportunityRepository = opportunityRepository;
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _userRepository = userRepository;
    }

    public async Task<CampaignEfficiencyReportDto> GetReportAsync(CampaignEfficiencyFilterDto filter)
    {
        var leadQuery = await _leadRepository.GetQueryableAsync();

        if (filter.CompanyId.HasValue)
            leadQuery = leadQuery.Where(l => l.CompanyId == filter.CompanyId.Value);

        if (filter.FromDate.HasValue)
            leadQuery = leadQuery.Where(l => l.CreationTime >= filter.FromDate.Value.Date);

        if (filter.ToDate.HasValue)
        {
            var endOfDay = filter.ToDate.Value.Date.AddDays(1);
            leadQuery = leadQuery.Where(l => l.CreationTime < endOfDay);
        }

        var isLeadOwner = string.Equals(filter.BasedOn, "LeadOwner", StringComparison.OrdinalIgnoreCase);
        var isUtm = string.Equals(filter.BasedOn, "UtmCampaign", StringComparison.OrdinalIgnoreCase);

        List<Lead> leads;
        List<IGrouping<string, Lead>> leadGroups;

        if (isLeadOwner)
        {
            leads = leadQuery.ToList();
            var ownerIds = leads
                .Select(l => l.AssignedUserId ?? l.CreatorId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var ownerNames = await ResolveOwnerNamesAsync(ownerIds);
            leadGroups = leads
                .GroupBy(l =>
                {
                    var ownerId = l.AssignedUserId ?? l.CreatorId;
                    return ownerId.HasValue
                        ? ownerNames.GetValueOrDefault(ownerId.Value, ownerId.Value.ToString()[..8])
                        : "Unassigned";
                })
                .OrderBy(g => g.Key)
                .ToList();
        }
        else
        {
            leads = isUtm
                ? leadQuery.Where(l => !string.IsNullOrEmpty(l.UtmCampaign)).ToList()
                : leadQuery.Where(l => !string.IsNullOrEmpty(l.CampaignName)).ToList();

            leadGroups = leads
                .GroupBy(l => isUtm ? l.UtmCampaign! : l.CampaignName!)
                .OrderBy(g => g.Key)
                .ToList();
        }

        var allLeadIds = leads.Select(l => l.Id).ToList();

        var customerQuery = await _customerRepository.GetQueryableAsync();
        var convertedCustomers = customerQuery
            .Where(c => c.LeadId.HasValue && allLeadIds.Contains(c.LeadId.Value))
            .Select(c => new { c.Id, LeadId = c.LeadId!.Value })
            .ToList();

        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        var allOpps = oppQuery
            .Where(o => (o.LeadId.HasValue && allLeadIds.Contains(o.LeadId.Value)) ||
                        (o.CustomerId.HasValue && convertedCustomers.Select(c => c.Id).Contains(o.CustomerId.Value)))
            .ToList();

        var allOppIds = allOpps.Select(o => o.Id).ToList();
        var allCustomerIds = convertedCustomers.Select(c => c.Id).ToList();

        var quotQuery = (await _quotationRepository.WithDetailsAsync(q => q.Items)).AsQueryable();
        var allQuots = quotQuery
            .Where(q => q.Status == DocumentStatus.Submitted &&
                        ((q.OpportunityId.HasValue && allOppIds.Contains(q.OpportunityId.Value)) ||
                         allCustomerIds.Contains(q.CustomerId)))
            .ToList();

        var allQuotIds = allQuots.Select(q => q.Id).ToList();

        var soQuery = (await _salesOrderRepository.WithDetailsAsync(s => s.Items)).AsQueryable();
        // Per ERPNext PR #59906 / commit b08f1eacf3 & f5ca48542c:
        // Only count submitted sales orders in order count and order value (leave cancelled and draft documents out)
        var allSalesOrders = soQuery
            .Where(s => s.Status != DocumentStatus.Draft && s.Status != DocumentStatus.Cancelled
                     && s.QuotationId.HasValue && allQuotIds.Contains(s.QuotationId.Value))
            .ToList();

        var report = new CampaignEfficiencyReportDto();

        foreach (var group in leadGroups)
        {
            var groupLeadIds = group.Select(l => l.Id).ToHashSet();
            var groupCustomerIds = convertedCustomers
                .Where(c => groupLeadIds.Contains(c.LeadId))
                .Select(c => c.Id)
                .ToHashSet();

            var groupOpps = allOpps
                .Where(o => (o.LeadId.HasValue && groupLeadIds.Contains(o.LeadId.Value)) ||
                            (o.CustomerId.HasValue && groupCustomerIds.Contains(o.CustomerId.Value)))
                .ToList();
            var groupOppIds = groupOpps.Select(o => o.Id).ToHashSet();

            var groupQuots = allQuots
                .Where(q => (q.OpportunityId.HasValue && groupOppIds.Contains(q.OpportunityId.Value)) ||
                            groupCustomerIds.Contains(q.CustomerId))
                .ToList();
            var groupQuotIds = groupQuots.Select(q => q.Id).ToHashSet();

            // Per ERPNext PR #59916 / commit 2deb1549f9: count partly ordered quotations
            var orderCount = groupQuots.Count(q => q.OrderStatus == "Ordered" || q.OrderStatus == "Partially Ordered");

            // Order value from submitted Sales Orders in company base currency
            var groupOrders = allSalesOrders
                .Where(s => s.QuotationId.HasValue && groupQuotIds.Contains(s.QuotationId.Value))
                .ToList();
            var orderValue = groupOrders.Sum(s => (s.NetTotal > 0 ? s.NetTotal : s.GrandTotal) * (s.ExchangeRate > 0 ? s.ExchangeRate : 1m));

            var leadCount = group.Count();
            var oppCount = groupOpps.Count;
            var quotCount = groupQuots.Count;

            var row = new CampaignEfficiencyRowDto
            {
                Campaign = group.Key,
                LeadCount = leadCount,
                OppCount = oppCount,
                QuotCount = quotCount,
                OrderCount = orderCount,
                OrderValue = orderValue,
                OppLeadRate = leadCount > 0 ? Math.Round((decimal)oppCount / leadCount * 100m, 2) : 0m,
                QuotLeadRate = leadCount > 0 ? Math.Round((decimal)quotCount / leadCount * 100m, 2) : 0m,
                OrderQuotRate = quotCount > 0 ? Math.Round((decimal)orderCount / quotCount * 100m, 2) : 0m,
            };

            report.Rows.Add(row);
        }

        report.TotalLeads = report.Rows.Sum(r => r.LeadCount);
        report.TotalOpps = report.Rows.Sum(r => r.OppCount);
        report.TotalQuots = report.Rows.Sum(r => r.QuotCount);
        report.TotalOrders = report.Rows.Sum(r => r.OrderCount);
        report.TotalOrderValue = report.Rows.Sum(r => r.OrderValue);

        return report;
    }

    private async Task<Dictionary<Guid, string>> ResolveOwnerNamesAsync(List<Guid> ownerIds)
    {
        if (ownerIds.Count == 0 || _userRepository == null)
        {
            return new Dictionary<Guid, string>();
        }

        var users = await _userRepository.GetListAsync(u => ownerIds.Contains(u.Id));
        return users.ToDictionary(
            u => u.Id,
            u => !string.IsNullOrWhiteSpace(u.Name) ? u.Name : u.UserName);
    }
}
