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
    private readonly IRepository<SalesStage, Guid> _salesStageRepository;

    public SalesPipelineAppService(
        IRepository<Lead, Guid> leadRepository,
        IRepository<Opportunity, Guid> opportunityRepository,
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<SalesStage, Guid> salesStageRepository)
    {
        _leadRepository = leadRepository;
        _opportunityRepository = opportunityRepository;
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesStageRepository = salesStageRepository;
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
        // Per ERPNext PR #59894 / commit 31a1504c31: active pipeline includes Open, Replied, and Quotation (excludes Lost and Closed)
        result.OpenOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied || o.Status == OpportunityStatus.Quotation);
        result.OpenOpportunitiesAmount = oppQuery
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied || o.Status == OpportunityStatus.Quotation)
            .Sum(o => o.OpportunityAmount);
        result.WeightedPipelineValue = oppQuery
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied || o.Status == OpportunityStatus.Quotation)
            .Sum(o => o.OpportunityAmount * o.Probability / 100);
        result.WonOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Converted);
        result.WonAmount = oppQuery
            .Where(o => o.Status == OpportunityStatus.Converted)
            .Sum(o => o.OpportunityAmount);
        result.LostOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Lost);
        result.ClosedOpportunities = oppQuery.Count(o => o.Status == OpportunityStatus.Closed);

        // Opportunities by stage (active pipeline: Open, Replied, Quotation)
        var activeOpps = oppQuery
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied || o.Status == OpportunityStatus.Quotation)
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
            .Where(o => o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied || o.Status == OpportunityStatus.Quotation)
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

    /// <summary>
    /// Returns opportunity summary matrix grouped by owner, source, or opportunity type across sales stages.
    /// Per ERPNext PR #59908 / commit f4feb24f70:
    /// - Opportunities without a sales stage use internal key __no_sales_stage to keep them separate from a stage named "Not Set".
    /// - Label is shown as "Not Set" in column headers and charts.
    /// - Company is mandatory when Data Based On is Amount.
    /// - Currency conversion uses conversion_rate or company base currency.
    /// </summary>
    public async Task<OpportunitySummaryBySalesStageDto> GetOpportunitySummaryBySalesStageAsync(GetOpportunitySummaryBySalesStageRequestDto input)
    {
        var isAmount = string.Equals(input.DataBasedOn, "Amount", StringComparison.OrdinalIgnoreCase);
        if (isAmount && !input.CompanyId.HasValue)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.CompanyMandatoryWhenDataBasedOnAmount);
        }

        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        if (input.CompanyId.HasValue) oppQuery = oppQuery.Where(o => o.CompanyId == input.CompanyId.Value);
        if (input.OpportunityType.HasValue) oppQuery = oppQuery.Where(o => o.OpportunityType == input.OpportunityType.Value);
        if (!string.IsNullOrWhiteSpace(input.OpportunitySource)) oppQuery = oppQuery.Where(o => o.UtmSource == input.OpportunitySource);
        if (input.Statuses != null && input.Statuses.Count > 0) oppQuery = oppQuery.Where(o => input.Statuses.Contains(o.Status));
        if (input.FromDate.HasValue) oppQuery = oppQuery.Where(o => o.CreationTime >= input.FromDate.Value);
        if (input.ToDate.HasValue) oppQuery = oppQuery.Where(o => o.CreationTime <= input.ToDate.Value);

        var opps = oppQuery.ToList();

        var stages = (await _salesStageRepository.GetListAsync())
            .OrderBy(s => s.SortOrder)
            .Select(s => s.StageName)
            .Distinct()
            .ToList();

        foreach (var stage in opps.Where(o => !string.IsNullOrWhiteSpace(o.SalesStage)).Select(o => o.SalesStage!).Distinct())
        {
            if (!stages.Contains(stage))
                stages.Add(stage);
        }

        var hasEmptyStage = opps.Any(o => string.IsNullOrWhiteSpace(o.SalesStage));
        if (hasEmptyStage && !stages.Contains(OpportunitySummaryConsts.NoSalesStageKey))
        {
            stages.Add(OpportunitySummaryConsts.NoSalesStageKey);
        }

        var basedOnField = input.BasedOn switch
        {
            "Opportunity Owner" => ("Opportunity Owner", "opportunity_owner"),
            "Source" => ("Source", "utm_source"),
            _ => ("Opportunity Type", "opportunity_type")
        };

        var columns = new List<OpportunitySummaryColumnDto>
        {
            new OpportunitySummaryColumnDto
            {
                Label = basedOnField.Item1,
                Fieldname = basedOnField.Item2,
                Fieldtype = "Data",
                Width = 200
            }
        };

        foreach (var stage in stages)
        {
            var label = stage == OpportunitySummaryConsts.NoSalesStageKey
                ? OpportunitySummaryConsts.NotSetLabel
                : stage;

            columns.Add(new OpportunitySummaryColumnDto
            {
                Label = label,
                Fieldname = stage,
                Fieldtype = isAmount ? "Currency" : "Int",
                Width = 150
            });
        }

        var grouped = opps.GroupBy(o => input.BasedOn switch
        {
            "Opportunity Owner" => o.AssignedUserId?.ToString() ?? "Not Assigned",
            "Source" => o.UtmSource ?? "Not Assigned",
            _ => o.OpportunityType.ToString()
        }).ToList();

        var rows = new List<OpportunitySummaryRowDto>();
        foreach (var group in grouped)
        {
            var row = new OpportunitySummaryRowDto { Key = group.Key };
            foreach (var stage in stages)
            {
                var matchingOpps = group.Where(o =>
                    (stage == OpportunitySummaryConsts.NoSalesStageKey && string.IsNullOrWhiteSpace(o.SalesStage))
                    || (o.SalesStage == stage));

                var val = isAmount
                    ? matchingOpps.Sum(o => o.OpportunityAmount * (o.ConversionRate > 0 ? o.ConversionRate : 1.0m))
                    : matchingOpps.Count();

                row.StageValues[stage] = val;
            }
            rows.Add(row);
        }

        var chartLabels = stages.Select(s => s == OpportunitySummaryConsts.NoSalesStageKey ? OpportunitySummaryConsts.NotSetLabel : s).ToList();
        var chartValues = stages.Select(s => rows.Sum(r => r.StageValues.GetValueOrDefault(s, 0m))).ToList();

        var chart = new OpportunitySummaryChartDto
        {
            Labels = chartLabels,
            Datasets = new List<OpportunitySummaryChartDatasetDto>
            {
                new OpportunitySummaryChartDatasetDto
                {
                    Name = isAmount ? "amount" : "count",
                    Values = chartValues
                }
            },
            Type = "line"
        };

        return new OpportunitySummaryBySalesStageDto
        {
            Columns = columns,
            Data = rows,
            Chart = chart
        };
    }

    /// <summary>
    /// Computes daily average first response time in seconds for opportunities with responses.
    /// Per ERPNext PR #59922 / commit ab8a279282: filters by company and user permissions so companies aren't blended.
    /// </summary>
    public async Task<FirstResponseTimeReportDto> GetFirstResponseTimeReportAsync(GetFirstResponseTimeReportRequestDto input)
    {
        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        oppQuery = oppQuery.Where(o => o.FirstResponseTime.HasValue && o.FirstResponseTime.Value > 0);
        if (input.CompanyId.HasValue) oppQuery = oppQuery.Where(o => o.CompanyId == input.CompanyId.Value);
        if (input.FromDate.HasValue) oppQuery = oppQuery.Where(o => o.CreationTime.Date >= input.FromDate.Value.Date);
        if (input.ToDate.HasValue) oppQuery = oppQuery.Where(o => o.CreationTime.Date <= input.ToDate.Value.Date);

        var opps = oppQuery.ToList();
        var grouped = opps
            .GroupBy(o => o.CreationTime.Date)
            .OrderByDescending(g => g.Key)
            .ToList();

        var data = grouped.Select(g =>
        {
            var avgSec = g.Average(o => o.FirstResponseTime!.Value);
            return new FirstResponseTimeRowDto
            {
                CreationDate = g.Key,
                AvgResponseTimeSeconds = Math.Round(avgSec, 2),
                FormattedResponseTime = FormatDuration(avgSec)
            };
        }).ToList();

        var chart = new OpportunitySummaryChartDto
        {
            Labels = data.OrderBy(d => d.CreationDate).Select(d => d.CreationDate.ToString("yyyy-MM-dd")).ToList(),
            Datasets = new List<OpportunitySummaryChartDatasetDto>
            {
                new OpportunitySummaryChartDatasetDto
                {
                    Name = "First Response Time (s)",
                    Values = data.OrderBy(d => d.CreationDate).Select(d => (decimal)d.AvgResponseTimeSeconds).ToList()
                }
            },
            Type = "line"
        };

        return new FirstResponseTimeReportDto
        {
            Data = data,
            Chart = chart
        };
    }

    /// <summary>
    /// Returns lost opportunities report.
    /// Per ERPNext PR #59918 / commit c541ac3f98: filtering by a specific lost reason keeps all
    /// reasons visible on each returned row.
    /// </summary>
    public async Task<LostOpportunityReportDto> GetLostOpportunityReportAsync(GetLostOpportunityReportRequestDto input)
    {
        var oppQuery = await _opportunityRepository.GetQueryableAsync();
        oppQuery = oppQuery.Where(o => o.Status == OpportunityStatus.Lost);
        if (input.CompanyId.HasValue) oppQuery = oppQuery.Where(o => o.CompanyId == input.CompanyId.Value);
        if (input.FromDate.HasValue) oppQuery = oppQuery.Where(o => o.CreationTime.Date >= input.FromDate.Value.Date);
        if (input.ToDate.HasValue) oppQuery = oppQuery.Where(o => o.CreationTime.Date <= input.ToDate.Value.Date);
        if (!string.IsNullOrWhiteSpace(input.Territory)) oppQuery = oppQuery.Where(o => o.Territory == input.Territory);
        if (input.PartyId.HasValue) oppQuery = oppQuery.Where(o => o.LeadId == input.PartyId.Value || o.CustomerId == input.PartyId.Value);
        if (string.Equals(input.OpportunityFrom, "Lead", StringComparison.OrdinalIgnoreCase)) oppQuery = oppQuery.Where(o => o.LeadId != null);
        else if (string.Equals(input.OpportunityFrom, "Customer", StringComparison.OrdinalIgnoreCase)) oppQuery = oppQuery.Where(o => o.CustomerId != null);

        var opps = oppQuery.ToList();
        if (!string.IsNullOrWhiteSpace(input.LostReason))
        {
            opps = opps.Where(o => o.LostReason != null && o.LostReason.Contains(input.LostReason, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var data = opps.OrderByDescending(o => o.CreationTime).Select(o => new LostOpportunityRowDto
        {
            Id = o.Id,
            OpportunityNumber = o.OpportunityNumber,
            Title = o.Title,
            OpportunityFrom = o.LeadId.HasValue ? "Lead" : (o.CustomerId.HasValue ? "Customer" : "None"),
            PartyId = o.LeadId ?? o.CustomerId,
            PartyName = o.ContactName,
            CustomerName = o.ContactName,
            OpportunityType = o.OpportunityType.ToString(),
            // Keep all reasons intact when filtering by one reason (PR #59918)
            LostReasons = o.LostReason ?? string.Empty,
            SalesStage = o.SalesStage,
            Territory = o.Territory,
            OpportunityAmount = o.OpportunityAmount,
            CreationTime = o.CreationTime
        }).ToList();

        return new LostOpportunityReportDto { Data = data };
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
}

