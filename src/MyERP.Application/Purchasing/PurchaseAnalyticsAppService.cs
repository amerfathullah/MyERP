using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Core.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseInvoices.Default)]
public class PurchaseAnalyticsAppService : ApplicationService, IPurchaseAnalyticsAppService
{
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;

    public PurchaseAnalyticsAppService(
        IRepository<PurchaseInvoice, Guid> piRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo)
    {
        _piRepo = piRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
    }

    public async Task<PurchaseAnalyticsReportDto> GetReportAsync(PurchaseAnalyticsRequestDto input)
    {
        var periods = GeneratePeriods(input.FromDate, input.ToDate, input.PeriodType);
        var useQty = string.Equals(input.ValueField, "Quantity", StringComparison.OrdinalIgnoreCase);

        var piQuery = await _piRepo.GetQueryableAsync();
        var invoices = piQuery
            .Where(pi => pi.CompanyId == input.CompanyId
                && pi.Status == DocumentStatus.Posted
                && !pi.IsReturn
                && !pi.IsOpening
                && pi.IssueDate >= input.FromDate
                && pi.IssueDate <= input.ToDate)
            .ToList();

        var supplierNames = new Dictionary<Guid, string>();
        var itemNames = new Dictionary<Guid, string>();

        if (input.GroupBy == AnalyticsGroupBy.Customer)
        {
            var supplierIds = invoices.Select(pi => pi.SupplierId).Distinct().ToList();
            if (supplierIds.Count > 0)
            {
                var suppliers = (await _supplierRepo.GetQueryableAsync())
                    .Where(s => supplierIds.Contains(s.Id))
                    .Select(s => new { s.Id, s.Name })
                    .ToList();
                supplierNames = suppliers.ToDictionary(s => s.Id, s => s.Name);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var allItemIds = invoices.SelectMany(pi => pi.Items).Select(i => i.ItemId).Distinct().ToList();
            if (allItemIds.Count > 0)
            {
                var items = (await _itemRepo.GetQueryableAsync())
                    .Where(i => allItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.ItemName })
                    .ToList();
                itemNames = items.ToDictionary(i => i.Id, i => i.ItemName);
            }
        }

        var rows = new List<PurchaseAnalyticsRowDto>();

        if (input.GroupBy == AnalyticsGroupBy.Customer)
        {
            var grouped = invoices.GroupBy(pi => pi.SupplierId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), supplierNames.GetValueOrDefault(g.Key, g.Key.ToString()[..8]),
                    g.ToList(), periods, useQty);
                rows.Add(row);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var itemGroups = invoices
                .SelectMany(pi => pi.Items.Select(item => new { pi.IssueDate, item.ItemId, item.Quantity, Amount = item.Quantity * item.UnitPrice }))
                .GroupBy(x => x.ItemId);

            foreach (var g in itemGroups)
            {
                var periodValues = new List<decimal>();
                foreach (var period in periods)
                {
                    var periodItems = g.Where(x => x.IssueDate >= period.From && x.IssueDate <= period.To);
                    periodValues.Add(useQty ? periodItems.Sum(x => x.Quantity) : periodItems.Sum(x => x.Amount));
                }
                rows.Add(new PurchaseAnalyticsRowDto
                {
                    EntityId = g.Key.ToString(),
                    EntityName = itemNames.GetValueOrDefault(g.Key, g.Key.ToString()[..8]),
                    PeriodValues = periodValues,
                    Total = periodValues.Sum(),
                    Growth = CalculateGrowth(periodValues),
                });
            }
        }
        else
        {
            var grouped = invoices.GroupBy(pi => pi.SupplierId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), supplierNames.GetValueOrDefault(g.Key, g.Key.ToString()[..8]),
                    g.ToList(), periods, useQty);
                rows.Add(row);
            }
        }

        // Filter by entity IDs if specified (ERPNext commit 3f29cdf8d2)
        if (input.EntityIds != null && input.EntityIds.Count > 0)
        {
            var entitySet = input.EntityIds.ToHashSet();
            rows = rows.Where(r => entitySet.Contains(r.EntityId)).ToList();
        }

        rows = rows.OrderByDescending(r => r.Total).ToList();

        var periodTotals = new List<decimal>();
        for (int i = 0; i < periods.Count; i++)
            periodTotals.Add(rows.Sum(r => r.PeriodValues.Count > i ? r.PeriodValues[i] : 0m));

        return new PurchaseAnalyticsReportDto
        {
            PeriodLabels = periods.Select(p => p.Label).ToList(),
            Rows = rows,
            GrandTotal = rows.Sum(r => r.Total),
            PeriodTotals = periodTotals,
        };
    }

    private static PurchaseAnalyticsRowDto BuildRow(string entityId, string entityName, List<PurchaseInvoice> invoices,
        List<PeriodRange> periods, bool useQty)
    {
        var periodValues = new List<decimal>();
        foreach (var period in periods)
        {
            var periodInvoices = invoices.Where(pi => pi.IssueDate >= period.From && pi.IssueDate <= period.To);
            if (useQty)
                periodValues.Add(periodInvoices.SelectMany(pi => pi.Items).Sum(i => i.Quantity));
            else
                periodValues.Add(periodInvoices.Sum(pi => pi.GrandTotal));
        }

        return new PurchaseAnalyticsRowDto
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
