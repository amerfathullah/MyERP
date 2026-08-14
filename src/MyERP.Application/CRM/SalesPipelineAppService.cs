using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Opportunities.Default)]
public class SalesPipelineAppService : ApplicationService, ISalesPipelineAppService
{
    private readonly IRepository<Lead, Guid> _leadRepository;
    private readonly IRepository<Opportunity, Guid> _opportunityRepository;
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;

    public SalesPipelineAppService(
        IRepository<Lead, Guid> leadRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository)
    {
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
    }

    /// <summary>
    /// Returns a complete sales pipeline funnel with counts and amounts per stage.
    /// Per ERPNext crm/report/sales_pipeline_analytics: shows conversion funnel.
    /// </summary>
    public async Task<SalesPipelineDashboardDto> GetPipelineDataAsync(Guid? companyId = null)
    {
        var result = new SalesPipelineDashboardDto();

        // Leads funnel stage
        var leadQuery = await _leadRepository.GetQueryableAsync();
        if (companyId.HasValue) leadQuery = leadQuery.Where(l => l.CompanyId == companyId.Value);

        result.TotalLeads = leadQuery.Count();
        result.ActiveLeads = leadQuery.Count(l => l.Status == LeadStatus.Open || l.Status == LeadStatus.Replied || l.Status == LeadStatus.Interested);
        result.QualifiedLeads = leadQuery.Count(l => l.Status == LeadStatus.Qualified || l.Status == LeadStatus.Converted);
        result.LostLeads = leadQuery.Count(l => l.Status == LeadStatus.Lost);

        // Opportunities funnel stage
        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        if (companyId.HasValue) oppQuery = oppQuery.Where(o => o.CompanyId == companyId.Value);

        result.TotalOpportunities = oppQuery.Count();
        result.OpenOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied);
        result.OpenOpportunitiesAmount = oppQuery
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied)
            .Sum(o => o.OpportunityAmount);
        result.WeightedPipelineValue = oppQuery
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied)
            .Sum(o => o.OpportunityAmount * o.Probability / 100);
        result.WonOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Converted);
        result.WonAmount = oppQuery
            .Where(o => o.Status == OpportunityStatus.Converted)
            .Sum(o => o.OpportunityAmount);
        result.LostOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Lost);

        // Opportunities by stage
        var activeOpps = oppQuery
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied)
            .ToList();

        result.StageBreakdown = activeOpps
            .GroupBy(o => o.SalesStage ?? "Unclassified")
            .Select(g => new PipelineStageDto
            {
                StageName = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(o => o.OpportunityAmount),
                WeightedAmount = g.Sum(o => o.OpportunityAmount * o.Probability / 100),
                AvgProbability = g.Count() > 0 ? (int)g.Average(o => o.Probability) : 0,
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();

        // Quotations funnel stage
        var qtnQuery = await _quotationRepository.GetQueryableAsync();
        if (companyId.HasValue) qtnQuery = qtnQuery.Where(q => q.CompanyId == companyId.Value);

        result.TotalQuotations = qtnQuery.Count(q => q.Status != DocumentStatus.Cancelled);
        result.OpenQuotations = qtnQuery.Count(q => q.Status == DocumentStatus.Submitted);
        result.OpenQuotationsAmount = qtnQuery
            .Where(q => q.Status == DocumentStatus.Submitted)
            .Sum(q => q.GrandTotal);
        result.ConvertedQuotations = qtnQuery.Count(q => q.Status == DocumentStatus.Completed);

        // Sales Orders funnel stage (completed conversions)
        var soQuery = await _salesOrderRepository.GetQueryableAsync();
        if (companyId.HasValue) soQuery = soQuery.Where(s => s.CompanyId == companyId.Value);

        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        result.OrdersThisMonth = soQuery.Count(s => s.OrderDate >= thisMonth && s.Status != DocumentStatus.Draft && s.Status != DocumentStatus.Cancelled);
        result.OrdersThisMonthAmount = soQuery
            .Where(s => s.OrderDate >= thisMonth && s.Status != DocumentStatus.Draft && s.Status != DocumentStatus.Cancelled)
            .Sum(s => s.GrandTotal);

        // Conversion rates
        result.LeadToOpportunityRate = result.TotalLeads > 0
            ? Math.Round((decimal)result.QualifiedLeads / result.TotalLeads * 100, 1)
            : 0;
        result.OpportunityToQuotationRate = result.TotalOpportunities > 0
            ? Math.Round((decimal)(result.WonOpportunities + oppQuery.Count(o => o.Status == OpportunityStatus.Quotation)) / result.TotalOpportunities * 100, 1)
            : 0;
        result.QuotationToOrderRate = result.TotalQuotations > 0
            ? Math.Round((decimal)result.ConvertedQuotations / result.TotalQuotations * 100, 1)
            : 0;

        return result;
    }

    /// <summary>
    /// Returns top opportunities ordered by weighted value for pipeline management.
    /// </summary>
    public async Task<List<PipelineOpportunityDto>> GetTopOpportunitiesAsync(Guid? companyId = null, int maxCount = 10)
    {
        var query = await _opportunityRepository.GetQueryableAsync();
        if (companyId.HasValue) query = query.Where(o => o.CompanyId == companyId.Value);

        return query
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied)
            .OrderByDescending(o => o.OpportunityAmount * o.Probability / 100)
            .Take(maxCount)
            .Select(o => new PipelineOpportunityDto
            {
                Id = o.Id,
                Title = o.Title,
                SalesStage = o.SalesStage ?? "Unclassified",
                Amount = o.OpportunityAmount,
                Probability = o.Probability,
                WeightedAmount = o.OpportunityAmount * o.Probability / 100,
                ExpectedClosingDate = o.ExpectedClosingDate,
                ContactName = o.ContactName,
                DaysOpen = (int)(DateTime.UtcNow - o.CreationTime).TotalDays,
            })
            .ToList();
    }
}

