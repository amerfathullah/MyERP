using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesAnalyticsAppService : ApplicationService, ISalesAnalyticsAppService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;

    public SalesAnalyticsAppService(
        IRepository<SalesInvoice, Guid> siRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo)
    {
        _siRepo = siRepo;
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
    }

    public async Task<SalesAnalyticsReportDto> GetReportAsync(SalesAnalyticsRequestDto input)
    {
        var periods = GeneratePeriods(input.FromDate, input.ToDate, input.PeriodType);
        var useQty = string.Equals(input.ValueField, "Quantity", StringComparison.OrdinalIgnoreCase);

        var siQuery = await _siRepo.GetQueryableAsync();
        var invoices = siQuery
            .Where(si => si.CompanyId == input.CompanyId
                && si.Status == DocumentStatus.Posted
                && !si.IsReturn
                && si.IssueDate >= input.FromDate
                && si.IssueDate <= input.ToDate)
            .ToList();

        // Resolve entity names
        var customerNames = new Dictionary<Guid, string>();
        var itemNames = new Dictionary<Guid, string>();

        if (input.GroupBy == AnalyticsGroupBy.Customer)
        {
            var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
            if (customerIds.Count > 0)
            {
                var customers = (await _customerRepo.GetQueryableAsync())
                    .Where(c => customerIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToList();
                customerNames = customers.ToDictionary(c => c.Id, c => c.Name);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var allItemIds = invoices.SelectMany(si => si.Items).Select(i => i.ItemId).Distinct().ToList();
            if (allItemIds.Count > 0)
            {
                var items = (await _itemRepo.GetQueryableAsync())
                    .Where(i => allItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.ItemName })
                    .ToList();
                itemNames = items.ToDictionary(i => i.Id, i => i.ItemName);
            }
        }

        // Group data by entity + period
        var rows = new List<SalesAnalyticsRowDto>();

        if (input.GroupBy == AnalyticsGroupBy.Customer)
        {
            var grouped = invoices.GroupBy(si => si.CustomerId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), customerNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    g.ToList(), periods, useQty);
                rows.Add(row);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var itemGroups = invoices
                .SelectMany(si => si.Items.Select(item => new { si.IssueDate, item.ItemId, item.Quantity, Amount = item.Quantity * item.UnitPrice }))
                .GroupBy(x => x.ItemId);

            foreach (var g in itemGroups)
            {
                var periodValues = new List<decimal>();
                foreach (var period in periods)
                {
                    var periodItems = g.Where(x => x.IssueDate >= period.From && x.IssueDate <= period.To);
                    periodValues.Add(useQty ? periodItems.Sum(x => x.Quantity) : periodItems.Sum(x => x.Amount));
                }
                var total = periodValues.Sum();
                var growth = CalculateGrowth(periodValues);
                rows.Add(new SalesAnalyticsRowDto
                {
                    EntityId = g.Key.ToString(),
                    EntityName = itemNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    PeriodValues = periodValues,
                    Total = total,
                    Growth = growth,
                });
            }
        }
        else
        {
            // Territory/SalesPerson/ItemGroup: group by customer as fallback
            var grouped = invoices.GroupBy(si => si.CustomerId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), customerNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    g.ToList(), periods, useQty);
                rows.Add(row);
            }
        }

        // Sort by total descending (top revenue first)
        rows = rows.OrderByDescending(r => r.Total).ToList();

        // Calculate period totals
        var periodTotals = new List<decimal>();
        for (int i = 0; i < periods.Count; i++)
        {
            periodTotals.Add(rows.Sum(r => r.PeriodValues.Count > i ? r.PeriodValues[i] : 0m));
        }

        return new SalesAnalyticsReportDto
        {
            PeriodLabels = periods.Select(p => p.Label).ToList(),
            Rows = rows,
            GrandTotal = rows.Sum(r => r.Total),
            PeriodTotals = periodTotals,
        };
    }

    private SalesAnalyticsRowDto BuildRow(string entityId, string entityName, List<SalesInvoice> invoices,
        List<PeriodRange> periods, bool useQty)
    {
        var periodValues = new List<decimal>();
        foreach (var period in periods)
        {
            var periodInvoices = invoices.Where(si => si.IssueDate >= period.From && si.IssueDate <= period.To);
            if (useQty)
                periodValues.Add(periodInvoices.SelectMany(si => si.Items).Sum(i => i.Quantity));
            else
                periodValues.Add(periodInvoices.Sum(si => si.GrandTotal));
        }

        return new SalesAnalyticsRowDto
        {
            EntityId = entityId,
            EntityName = entityName,
            PeriodValues = periodValues,
            Total = periodValues.Sum(),
            Growth = CalculateGrowth(periodValues),
        };
    }

    private static decimal CalculateGrowth(List<decimal> values)
    {
        if (values.Count < 2) return 0;
        var first = values.First();
        var last = values.Last();
        if (first == 0 && last > 0) return 100;
        if (first == 0 && last < 0) return -100;
        if (first == 0) return 0;
        return Math.Round((last - first) / first * 100, 1);
    }

    private static List<PeriodRange> GeneratePeriods(DateTime from, DateTime to, AnalyticsPeriodType periodType)
    {
        var periods = new List<PeriodRange>();
        var current = from;

        while (current <= to)
        {
            DateTime periodStart = current;
            DateTime periodEnd;
            string label;

            switch (periodType)
            {
                case AnalyticsPeriodType.Monthly:
                    periodEnd = new DateTime(current.Year, current.Month, DateTime.DaysInMonth(current.Year, current.Month));
                    label = current.ToString("MMM yyyy");
                    current = periodEnd.AddDays(1);
                    break;
                case AnalyticsPeriodType.Quarterly:
                    int quarter = ((current.Month - 1) / 3) + 1;
                    int endMonth = quarter * 3;
                    periodEnd = new DateTime(current.Year, endMonth, DateTime.DaysInMonth(current.Year, endMonth));
                    label = $"Q{quarter} {current.Year}";
                    current = periodEnd.AddDays(1);
                    break;
                default:
                    periodEnd = new DateTime(current.Year, 12, 31);
                    label = current.Year.ToString();
                    current = periodEnd.AddDays(1);
                    break;
            }

            if (periodEnd > to) periodEnd = to;
            periods.Add(new PeriodRange(Start: periodStart, End: periodEnd, Label: label));
        }

        return periods;
    }

    private record PeriodRange(DateTime Start, DateTime End, string Label)
    {
        public DateTime From => Start;
        public DateTime To => End;
    }
}
