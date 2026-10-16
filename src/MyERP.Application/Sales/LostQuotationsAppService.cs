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

[Authorize(MyERPPermissions.Quotations.Default)]
public class LostQuotationsAppService : ApplicationService, ILostQuotationsAppService
{
    private readonly IRepository<Quotation, Guid> _quotationRepository;
    private readonly IRepository<QuotationLostReasonDetail, Guid> _lostReasonDetailRepository;
    private readonly IRepository<QuotationLostReason, Guid> _lostReasonRepository;
    private readonly IRepository<CompetitorDetail, Guid> _competitorDetailRepository;
    private readonly IRepository<Competitor, Guid> _competitorRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public LostQuotationsAppService(
        IRepository<Quotation, Guid> quotationRepository,
        IRepository<QuotationLostReasonDetail, Guid> lostReasonDetailRepository,
        IRepository<QuotationLostReason, Guid> lostReasonRepository,
        IRepository<CompetitorDetail, Guid> competitorDetailRepository,
        IRepository<Competitor, Guid> competitorRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _quotationRepository = quotationRepository;
        _lostReasonDetailRepository = lostReasonDetailRepository;
        _lostReasonRepository = lostReasonRepository;
        _competitorDetailRepository = competitorDetailRepository;
        _competitorRepository = competitorRepository;
        _companyRepository = companyRepository;
    }

    public async Task<LostQuotationsReportDto> GetReportAsync(GetLostQuotationsReportInput input)
    {
        var (fromDate, toDate) = ResolveTimespan(input.Timespan, input.FromDate, input.ToDate);

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var quotationQuery = await _quotationRepository.GetQueryableAsync();
        var lostQuotations = quotationQuery
            .Where(q => q.CompanyId == input.CompanyId
                && q.Status == DocumentStatus.Rejected
                && q.IsActive
                && q.IssueDate >= fromDate
                && q.IssueDate <= toDate)
            .Select(q => new
            {
                q.Id,
                q.NetTotal,
                q.QuotationNumber
            })
            .ToList();

        var totalQuotations = lostQuotations.Count;
        var totalValue = lostQuotations.Sum(q => q.NetTotal);

        var result = new LostQuotationsReportDto
        {
            Summary = new LostQuotationsSummaryDto
            {
                TotalLostQuotations = totalQuotations,
                TotalLostValue = totalValue,
                Currency = currency
            }
        };

        if (totalQuotations == 0)
        {
            return result;
        }

        var isCompetitorGroup = string.Equals(input.GroupBy?.Trim(), "Competitor", StringComparison.OrdinalIgnoreCase);
        var lostQuotationIds = lostQuotations.Select(q => q.Id).ToList();

        List<(Guid QuotationId, decimal NetTotal, string Dimension)> pairs;

        if (isCompetitorGroup)
        {
            var competitorDetailQuery = await _competitorDetailRepository.GetQueryableAsync();
            var competitorDetails = competitorDetailQuery
                .Where(c => c.ParentType == "Quotation" && lostQuotationIds.Contains(c.ParentId))
                .ToList();

            var compIds = competitorDetails.Select(c => c.CompetitorId).Distinct().ToList();
            var compQuery = await _competitorRepository.GetQueryableAsync();
            var competitorNames = compQuery
                .Where(c => compIds.Contains(c.Id))
                .ToDictionary(c => c.Id, c => c.Name);

            pairs = new List<(Guid QuotationId, decimal NetTotal, string Dimension)>();
            foreach (var q in lostQuotations)
            {
                var names = competitorDetails
                    .Where(c => c.ParentId == q.Id)
                    .Select(c => competitorNames.GetValueOrDefault(c.CompetitorId) ?? "Not Specified")
                    .Distinct()
                    .ToList();

                if (!names.Any())
                {
                    pairs.Add((q.Id, q.NetTotal, "Not Specified"));
                }
                else
                {
                    foreach (var name in names)
                    {
                        pairs.Add((q.Id, q.NetTotal, name));
                    }
                }
            }
        }
        else
        {
            var reasonDetailQuery = await _lostReasonDetailRepository.GetQueryableAsync();
            var reasonDetails = reasonDetailQuery
                .Where(d => lostQuotationIds.Contains(d.QuotationId))
                .ToList();

            var reasonIds = reasonDetails.Select(d => d.QuotationLostReasonId).Distinct().ToList();
            var reasonQuery = await _lostReasonRepository.GetQueryableAsync();
            var reasonNames = reasonQuery
                .Where(r => reasonIds.Contains(r.Id))
                .ToDictionary(r => r.Id, r => r.Reason);

            pairs = new List<(Guid QuotationId, decimal NetTotal, string Dimension)>();
            foreach (var q in lostQuotations)
            {
                var names = reasonDetails
                    .Where(d => d.QuotationId == q.Id)
                    .Select(d => reasonNames.GetValueOrDefault(d.QuotationLostReasonId) ?? "Not Specified")
                    .Distinct()
                    .ToList();

                if (!names.Any())
                {
                    pairs.Add((q.Id, q.NetTotal, "Not Specified"));
                }
                else
                {
                    foreach (var name in names)
                    {
                        pairs.Add((q.Id, q.NetTotal, name));
                    }
                }
            }
        }

        // Distinct reasons count per quotation
        var distinctDimensionsPerQuotation = pairs
            .GroupBy(p => p.QuotationId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Split each quotation's value evenly across its distinct reasons
        var splitList = pairs
            .Select(p => new
            {
                p.Dimension,
                p.QuotationId,
                SplitValue = distinctDimensionsPerQuotation[p.QuotationId] > 0
                    ? p.NetTotal / distinctDimensionsPerQuotation[p.QuotationId]
                    : p.NetTotal
            })
            .ToList();

        // Group by dimension
        var rows = splitList
            .GroupBy(s => s.Dimension)
            .Select(g =>
            {
                var distinctQuotationCount = g.Select(s => s.QuotationId).Distinct().Count();
                var dimensionLostValue = g.Sum(s => s.SplitValue);

                return new LostQuotationReportRowDto
                {
                    Dimension = g.Key,
                    LostQuotations = distinctQuotationCount,
                    LostQuotationsPct = totalQuotations > 0
                        ? Math.Round((decimal)distinctQuotationCount * 100m / totalQuotations, 2)
                        : 0m,
                    LostValue = Math.Round(dimensionLostValue, 2),
                    LostValuePct = totalValue > 0
                        ? Math.Round(dimensionLostValue * 100m / totalValue, 2)
                        : 0m,
                    Currency = currency
                };
            })
            .OrderByDescending(r => r.LostValue)
            .ThenByDescending(r => r.LostQuotations)
            .ToList();

        result.Rows = rows;

        // Populate top chart data
        var topRows = rows.Take(10).ToList();
        result.Chart = new LostQuotationsChartDto
        {
            Labels = topRows.Select(r => r.Dimension).ToList(),
            Values = topRows.Select(r => r.LostValue).ToList(),
            Counts = topRows.Select(r => r.LostQuotations).ToList()
        };

        return result;
    }

    private static (DateTime FromDate, DateTime ToDate) ResolveTimespan(string? timespan, DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue)
        {
            return (fromDate.Value.Date, toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var today = DateTime.UtcNow.Date;
        timespan = timespan?.Trim();

        return timespan?.ToLowerInvariant() switch
        {
            "this week" => (
                today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday),
                today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday + 6).AddDays(1).AddTicks(-1)
            ),
            "last week" => (
                today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday - 7),
                today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday - 1).AddDays(1).AddTicks(-1)
            ),
            "this month" => (
                new DateTime(today.Year, today.Month, 1),
                new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)).AddDays(1).AddTicks(-1)
            ),
            "last month" => (
                new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                new DateTime(today.Year, today.Month, 1).AddTicks(-1)
            ),
            "this quarter" => (
                new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1),
                new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1).AddMonths(3).AddTicks(-1)
            ),
            "last quarter" => (
                new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1).AddMonths(-3),
                new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1).AddTicks(-1)
            ),
            "last 6 months" => (
                today.AddMonths(-6),
                today.AddDays(1).AddTicks(-1)
            ),
            "last year" => (
                new DateTime(today.Year - 1, 1, 1),
                new DateTime(today.Year - 1, 12, 31).AddDays(1).AddTicks(-1)
            ),
            _ => (
                new DateTime(today.Year, 1, 1),
                new DateTime(today.Year, 12, 31).AddDays(1).AddTicks(-1)
            )
        };
    }
}
