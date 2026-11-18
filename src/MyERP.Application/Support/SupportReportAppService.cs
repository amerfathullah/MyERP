using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Support.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support;

/// <summary>
/// Support module reports migrating ERPNext support/report/*:
/// - Issue Summary
/// - Issue Analytics
/// - First Response Time for Issues
/// - Support Hour Distribution
/// </summary>
[Authorize(MyERPPermissions.Issues.Default)]
public class SupportReportAppService : ApplicationService, ISupportReportAppService
{
    private static readonly string[] TimeSlots =
    {
        "12AM - 3AM",
        "3AM - 6AM",
        "6AM - 9AM",
        "9AM - 12PM",
        "12PM - 3PM",
        "3PM - 6PM",
        "6PM - 9PM",
        "9PM - 12AM",
    };

    private readonly IRepository<Issue, Guid> _issueRepository;
    private readonly IRepository<Customer, Guid>? _customerRepository;
    private readonly IRepository<Employee, Guid>? _employeeRepository;

    public SupportReportAppService(
        IRepository<Issue, Guid> issueRepository,
        IRepository<Customer, Guid>? customerRepository = null,
        IRepository<Employee, Guid>? employeeRepository = null)
    {
        _issueRepository = issueRepository;
        _customerRepository = customerRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<IssueSummaryReportDto> GetIssueSummaryAsync(SupportReportFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var basedOn = NormalizeBasedOn(input.BasedOn);
        var issues = await QueryFilteredIssuesAsync(input, from, to);

        var (customerMap, employeeMap) = await LoadDimensionMapsAsync(issues, basedOn);

        var groups = issues.GroupBy(i => ResolveDimension(i, basedOn, customerMap, employeeMap));
        var rows = new List<IssueSummaryRowDto>();

        foreach (var g in groups)
        {
            var list = g.ToList();
            var slaIssues = list.Where(i => i.ServiceLevelAgreementId.HasValue).ToList();
            var frtIssues = list.Where(i => i.FirstRespondedOn.HasValue && i.FirstRespondedOn.Value > i.OpeningDate).ToList();
            var resolvedIssues = list.Where(i => i.ResolutionDate.HasValue && i.ResolutionDate.Value >= i.OpeningDate).ToList();

            rows.Add(new IssueSummaryRowDto
            {
                DimensionKey = g.Key.Key,
                DimensionLabel = g.Key.Label,
                OpenCount = list.Count(i => i.Status == IssueStatus.Open),
                RepliedCount = list.Count(i => i.Status == IssueStatus.Replied),
                OnHoldCount = list.Count(i => i.Status == IssueStatus.OnHold),
                ClosedCount = list.Count(i => i.Status == IssueStatus.Closed),
                CancelledCount = list.Count(i => i.Status == IssueStatus.Cancelled),
                TotalIssues = list.Count,
                SlaFailedCount = slaIssues.Count(i => i.AgreementStatus == AgreementStatus.Failed),
                SlaFulfilledCount = slaIssues.Count(i => i.AgreementStatus == AgreementStatus.Fulfilled),
                FirstResponseDueCount = slaIssues.Count(i => i.AgreementStatus == AgreementStatus.FirstResponseDue),
                ResolutionDueCount = slaIssues.Count(i => i.AgreementStatus == AgreementStatus.ResolutionDue),
                PausedCount = slaIssues.Count(i => i.AgreementStatus == AgreementStatus.Paused),
                AvgFirstResponseTimeHours = frtIssues.Count > 0
                    ? Math.Round(frtIssues.Average(i => i.ActualFirstResponseTimeHours), 2)
                    : 0m,
                AvgHoldTimeHours = list.Count > 0
                    ? Math.Round(list.Average(i => i.TotalHoldTime), 2)
                    : 0m,
                AvgResolutionTimeHours = resolvedIssues.Count > 0
                    ? Math.Round(resolvedIssues.Average(i => i.ActualResolutionTimeHours), 2)
                    : 0m,
            });
        }

        rows = rows.OrderByDescending(r => r.TotalIssues).ThenBy(r => r.DimensionLabel).ToList();

        return new IssueSummaryReportDto
        {
            BasedOn = basedOn,
            Rows = rows,
            TotalIssues = rows.Sum(r => r.TotalIssues),
            TotalOpen = rows.Sum(r => r.OpenCount),
            TotalClosed = rows.Sum(r => r.ClosedCount),
            TotalSlaFulfilled = rows.Sum(r => r.SlaFulfilledCount),
            TotalSlaFailed = rows.Sum(r => r.SlaFailedCount),
        };
    }

    public async Task<IssueAnalyticsReportDto> GetIssueAnalyticsAsync(SupportReportFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var basedOn = NormalizeBasedOn(input.BasedOn);
        var range = NormalizeRange(input.Range);
        var issues = await QueryFilteredIssuesAsync(input, from, to);

        var periods = BuildPeriodRanges(from, to, range);
        var periodLabels = periods.Select(p => p.Label).ToArray();
        var (customerMap, employeeMap) = await LoadDimensionMapsAsync(issues, basedOn);

        var rows = new List<IssueAnalyticsRowDto>();
        var periodTotals = new int[periods.Count];

        foreach (var g in issues.GroupBy(i => ResolveDimension(i, basedOn, customerMap, employeeMap)))
        {
            var counts = new int[periods.Count];
            foreach (var issue in g)
            {
                var date = issue.OpeningDate.Date;
                for (int idx = 0; idx < periods.Count; idx++)
                {
                    if (date >= periods[idx].Start && date <= periods[idx].End)
                    {
                        counts[idx]++;
                        periodTotals[idx]++;
                        break;
                    }
                }
            }

            rows.Add(new IssueAnalyticsRowDto
            {
                DimensionKey = g.Key.Key,
                DimensionLabel = g.Key.Label,
                PeriodCounts = counts,
                Total = counts.Sum(),
            });
        }

        rows = rows.OrderByDescending(r => r.Total).ThenBy(r => r.DimensionLabel).ToList();

        return new IssueAnalyticsReportDto
        {
            BasedOn = basedOn,
            Range = range,
            PeriodLabels = periodLabels,
            Rows = rows,
            PeriodTotals = periodTotals,
            GrandTotal = periodTotals.Sum(),
        };
    }

    public async Task<FirstResponseTimeReportDto> GetFirstResponseTimeAsync(SupportReportFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var issues = await QueryFilteredIssuesAsync(input, from, to);

        var responded = issues
            .Where(i => i.FirstRespondedOn.HasValue && i.FirstRespondedOn.Value > i.OpeningDate)
            .ToList();

        var rows = responded
            .GroupBy(i => i.OpeningDate.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new FirstResponseTimeRowDto
            {
                Date = g.Key,
                IssueCount = g.Count(),
                AvgFirstResponseTimeHours = Math.Round(g.Average(i => i.ActualFirstResponseTimeHours), 2),
            })
            .ToList();

        var overallAvg = responded.Count > 0
            ? Math.Round(responded.Average(i => i.ActualFirstResponseTimeHours), 2)
            : 0m;

        return new FirstResponseTimeReportDto
        {
            Rows = rows,
            TotalIssues = responded.Count,
            OverallAverageHours = overallAvg,
        };
    }

    public async Task<SupportHourDistributionReportDto> GetSupportHourDistributionAsync(SupportReportFilterDto input)
    {
        var (from, to) = NormalizeAndValidateDateRange(input.FromDate, input.ToDate);
        var issues = await QueryFilteredIssuesAsync(input, from, to);

        var byDate = issues
            .GroupBy(i => i.OpeningDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<SupportHourDistributionRowDto>();
        var slotTotals = new int[TimeSlots.Length];

        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var counts = new int[TimeSlots.Length];
            if (byDate.TryGetValue(day, out var dayIssues))
            {
                foreach (var issue in dayIssues)
                {
                    int slotIdx = Math.Clamp(issue.OpeningDate.Hour / 3, 0, TimeSlots.Length - 1);
                    counts[slotIdx]++;
                    slotTotals[slotIdx]++;
                }
            }

            rows.Add(new SupportHourDistributionRowDto
            {
                Date = day,
                SlotCounts = counts,
                Total = counts.Sum(),
            });
        }

        return new SupportHourDistributionReportDto
        {
            TimeSlotLabels = TimeSlots,
            Rows = rows,
            SlotTotals = slotTotals,
            GrandTotal = slotTotals.Sum(),
        };
    }

    private async Task<List<Issue>> QueryFilteredIssuesAsync(SupportReportFilterDto input, DateTime from, DateTime to)
    {
        var toExclusive = to.Date.AddDays(1);
        var query = await _issueRepository.GetQueryableAsync();
        query = query.Where(i =>
            i.CompanyId == input.CompanyId &&
            i.OpeningDate >= from &&
            i.OpeningDate < toExclusive);

        if (input.Status.HasValue)
            query = query.Where(i => i.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Priority))
            query = query.Where(i => i.Priority == input.Priority);
        if (input.CustomerId.HasValue)
            query = query.Where(i => i.CustomerId == input.CustomerId.Value);
        if (input.AssignedToId.HasValue)
            query = query.Where(i => i.AssignedToId == input.AssignedToId.Value);

        return query.ToList();
    }

    private async Task<(Dictionary<Guid, string> Customers, Dictionary<Guid, string> Employees)> LoadDimensionMapsAsync(
        List<Issue> issues,
        string basedOn)
    {
        var customerMap = new Dictionary<Guid, string>();
        var employeeMap = new Dictionary<Guid, string>();

        if (basedOn == "Customer")
        {
            var custRepo = _customerRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Customer, Guid>>();
            var custIds = issues.Where(i => i.CustomerId.HasValue).Select(i => i.CustomerId!.Value).Distinct().ToList();
            if (custRepo != null && custIds.Count > 0)
            {
                var q = await custRepo.GetQueryableAsync();
                customerMap = q.Where(c => custIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Name);
            }
        }
        else if (basedOn == "Assigned To")
        {
            var empRepo = _employeeRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Employee, Guid>>();
            var empIds = issues.Where(i => i.AssignedToId.HasValue).Select(i => i.AssignedToId!.Value).Distinct().ToList();
            if (empRepo != null && empIds.Count > 0)
            {
                var q = await empRepo.GetQueryableAsync();
                employeeMap = q.Where(e => empIds.Contains(e.Id)).ToDictionary(e => e.Id, e => e.FullName);
            }
        }

        return (customerMap, employeeMap);
    }

    private static (string Key, string Label) ResolveDimension(
        Issue issue,
        string basedOn,
        Dictionary<Guid, string> customerMap,
        Dictionary<Guid, string> employeeMap)
    {
        return basedOn switch
        {
            "Customer" => issue.CustomerId.HasValue
                ? (issue.CustomerId.Value.ToString(), customerMap.GetValueOrDefault(issue.CustomerId.Value, issue.CustomerId.Value.ToString()))
                : ("Not Specified", "Not Specified"),
            "Assigned To" => issue.AssignedToId.HasValue
                ? (issue.AssignedToId.Value.ToString(), employeeMap.GetValueOrDefault(issue.AssignedToId.Value, issue.AssignedToId.Value.ToString()))
                : ("Not Specified", "Not Specified"),
            "Issue Type" => !string.IsNullOrWhiteSpace(issue.IssueType)
                ? (issue.IssueType, issue.IssueType)
                : ("Not Specified", "Not Specified"),
            "Issue Priority" => !string.IsNullOrWhiteSpace(issue.Priority)
                ? (issue.Priority, issue.Priority)
                : ("Not Specified", "Not Specified"),
            _ => ("Not Specified", "Not Specified"),
        };
    }

    private static (DateTime From, DateTime To) NormalizeAndValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        var to = (toDate ?? DateTime.UtcNow).Date;
        var from = (fromDate ?? to.AddDays(-30)).Date;
        if (from > to)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "From Date cannot be after To Date.");
        }
        return (from, to);
    }

    private static string NormalizeBasedOn(string? basedOn)
    {
        return basedOn switch
        {
            "Assigned To" => "Assigned To",
            "Issue Type" => "Issue Type",
            "Issue Priority" => "Issue Priority",
            _ => "Customer",
        };
    }

    private static string NormalizeRange(string? range)
    {
        return range switch
        {
            "Weekly" => "Weekly",
            "Quarterly" => "Quarterly",
            "Yearly" => "Yearly",
            _ => "Monthly",
        };
    }

    private static List<(DateTime Start, DateTime End, string Label)> BuildPeriodRanges(DateTime from, DateTime to, string range)
    {
        var result = new List<(DateTime Start, DateTime End, string Label)>();
        bool multiYear = from.Year != to.Year;

        if (range == "Weekly")
        {
            int diff = ((int)from.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var cursor = from.AddDays(-diff);
            while (cursor <= to)
            {
                var end = cursor.AddDays(6);
                if (end > to) end = to;
                int week = ISOWeek.GetWeekOfYear(end);
                int isoYear = ISOWeek.GetYear(end);
                var label = multiYear ? $"Week {week} {isoYear}" : $"Week {week}";
                result.Add((cursor, end, label));
                cursor = end.AddDays(1);
            }
            return result;
        }

        if (range == "Quarterly")
        {
            var cursor = new DateTime(from.Year, ((from.Month - 1) / 3) * 3 + 1, 1);
            while (cursor <= to)
            {
                var end = cursor.AddMonths(3).AddDays(-1);
                if (end > to) end = to;
                int q = ((cursor.Month - 1) / 3) + 1;
                var label = multiYear ? $"Quarter {q} {cursor.Year}" : $"Quarter {q}";
                result.Add((cursor, end, label));
                cursor = end.AddDays(1);
            }
            return result;
        }

        if (range == "Yearly")
        {
            var cursor = new DateTime(from.Year, 1, 1);
            while (cursor <= to)
            {
                var end = new DateTime(cursor.Year, 12, 31);
                if (end > to) end = to;
                result.Add((cursor, end, cursor.Year.ToString(CultureInfo.InvariantCulture)));
                cursor = new DateTime(cursor.Year + 1, 1, 1);
            }
            return result;
        }

        // Monthly (default)
        {
            var cursor = new DateTime(from.Year, from.Month, 1);
            while (cursor <= to)
            {
                var end = cursor.AddMonths(1).AddDays(-1);
                if (end > to) end = to;
                var monthAbbr = cursor.ToString("MMM", CultureInfo.InvariantCulture);
                var label = multiYear ? $"{monthAbbr} {cursor.Year}" : monthAbbr;
                result.Add((cursor, end, label));
                cursor = end.AddDays(1);
            }
            return result;
        }
    }
}
