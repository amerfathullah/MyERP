using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.Quotations.Default)]
public class QuotationTrendsAppService : ApplicationService, IQuotationTrendsAppService
{
    private readonly IRepository<Quotation, Guid> _quotationRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public QuotationTrendsAppService(
        IRepository<Quotation, Guid> quotationRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _quotationRepo = quotationRepo;
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }

    public async Task<QuotationTrendsReportDto> GetReportAsync(QuotationTrendsRequestDto input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";

        var (yearStart, yearEnd) = await ResolveDateRangeAsync(input);
        var periods = GeneratePeriods(input.Period, yearStart, yearEnd);

        var quotationQuery = await _quotationRepo.WithDetailsAsync(q => q.Items);
        var quotations = quotationQuery
            .Where(q => q.CompanyId == input.CompanyId
                        && q.Status != DocumentStatus.Draft
                        && q.Status != DocumentStatus.Cancelled
                        && q.IssueDate >= yearStart
                        && q.IssueDate <= yearEnd)
            .ToList();

        // Master records lookup
        var customerIds = quotations.Select(q => q.CustomerId).Distinct().ToList();
        var itemIds = quotations.SelectMany(q => q.Items.Select(i => i.ItemId)).Distinct().ToList();

        var customerQuery = await _customerRepo.GetQueryableAsync();
        var customers = customerQuery
            .Where(c => customerIds.Contains(c.Id))
            .ToList()
            .ToDictionary(c => c.Id);

        var territoryIds = customers.Values
            .Where(c => c.TerritoryId.HasValue)
            .Select(c => c.TerritoryId!.Value)
            .Distinct()
            .ToList();

        var territoryDict = new Dictionary<Guid, string>();
        if (territoryIds.Count > 0)
        {
            var territoryRepo = LazyServiceProvider.LazyGetService<IRepository<Territory, Guid>>();
            if (territoryRepo != null)
            {
                var tQuery = await territoryRepo.GetQueryableAsync();
                territoryDict = tQuery
                    .Where(t => territoryIds.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name })
                    .ToList()
                    .ToDictionary(t => t.Id, t => t.Name);
            }
        }

        var itemQuery = await _itemRepo.GetQueryableAsync();
        var items = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToList()
            .ToDictionary(i => i.Id);

        var rows = BuildReportRows(input, quotations, customers, items, territoryDict, periods);

        // Grand Total Row
        var totalRow = new QuotationTrendsRowDto
        {
            EntityId = "Total",
            EntityCode = "Total",
            EntityName = "Total",
            SecondaryInfo = string.Empty,
        };

        foreach (var p in periods)
        {
            var pQty = rows.Where(r => string.IsNullOrEmpty(r.GroupByValue)).Sum(r => r.PeriodQuantities.GetValueOrDefault(p.Key, 0m));
            var pAmt = rows.Where(r => string.IsNullOrEmpty(r.GroupByValue)).Sum(r => r.PeriodAmounts.GetValueOrDefault(p.Key, 0m));

            if (!rows.Any(r => string.IsNullOrEmpty(r.GroupByValue)))
            {
                pQty = rows.Sum(r => r.PeriodQuantities.GetValueOrDefault(p.Key, 0m));
                pAmt = rows.Sum(r => r.PeriodAmounts.GetValueOrDefault(p.Key, 0m));
            }

            totalRow.PeriodQuantities[p.Key] = pQty;
            totalRow.PeriodAmounts[p.Key] = pAmt;
        }

        totalRow.TotalQty = totalRow.PeriodQuantities.Values.Sum();
        totalRow.TotalAmount = totalRow.PeriodAmounts.Values.Sum();

        // Chart Datapoints
        var chartLabels = periods.Select(p => p.Label).ToList();
        var chartDatapoints = periods.Select(p => totalRow.PeriodAmounts.GetValueOrDefault(p.Key, 0m)).ToList();

        return new QuotationTrendsReportDto
        {
            CompanyCurrency = companyCurrency,
            Period = input.Period,
            BasedOn = input.BasedOn,
            GroupBy = input.GroupBy,
            PeriodColumns = periods,
            Rows = rows,
            TotalRow = totalRow,
            Chart = new QuotationTrendsChartDto
            {
                Labels = chartLabels,
                Datapoints = chartDatapoints,
                Currency = companyCurrency,
            },
        };
    }

    private async Task<(DateTime StartDate, DateTime EndDate)> ResolveDateRangeAsync(QuotationTrendsRequestDto input)
    {
        if (input.FiscalYearId.HasValue)
        {
            var fy = await _fiscalYearRepo.FindAsync(input.FiscalYearId.Value);
            if (fy != null)
            {
                return (fy.StartDate, fy.EndDate);
            }
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue)
        {
            return (input.FromDate.Value.Date, input.ToDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var today = DateTime.Today;
        var fyQuery = await _fiscalYearRepo.GetQueryableAsync();
        var currentFy = fyQuery.FirstOrDefault(f => f.CompanyId == input.CompanyId && f.StartDate <= today && f.EndDate >= today);
        if (currentFy != null)
        {
            return (currentFy.StartDate, currentFy.EndDate);
        }

        return (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31, 23, 59, 59));
    }

    private static List<QuotationTrendsPeriodColumnDto> GeneratePeriods(string period, DateTime start, DateTime end)
    {
        var result = new List<QuotationTrendsPeriodColumnDto>();
        var normPeriod = period?.Trim().ToLowerInvariant() switch
        {
            "quarterly" => "Quarterly",
            "halfyearly" or "half-yearly" => "Half-Yearly",
            "yearly" => "Yearly",
            _ => "Monthly",
        };

        var cur = start.Date;
        int idx = 1;

        while (cur <= end.Date)
        {
            DateTime nextEnd;
            string label;
            string key = $"P{idx}";

            if (normPeriod == "Monthly")
            {
                var nextMonth = new DateTime(cur.Year, cur.Month, 1).AddMonths(1);
                nextEnd = nextMonth.AddDays(-1);
                if (nextEnd > end.Date) nextEnd = end.Date;
                label = cur.ToString("MMM yyyy", CultureInfo.InvariantCulture);
            }
            else if (normPeriod == "Quarterly")
            {
                var quarterMonth = ((cur.Month - 1) / 3) * 3 + 1;
                var quarterStart = new DateTime(cur.Year, quarterMonth, 1);
                nextEnd = quarterStart.AddMonths(3).AddDays(-1);
                if (nextEnd > end.Date) nextEnd = end.Date;
                int qNum = (cur.Month - 1) / 3 + 1;
                label = $"Q{qNum} {cur.Year} ({cur:MMM}-{nextEnd:MMM})";
            }
            else if (normPeriod == "Half-Yearly")
            {
                var halfMonth = cur.Month <= 6 ? 1 : 7;
                var halfStart = new DateTime(cur.Year, halfMonth, 1);
                nextEnd = halfStart.AddMonths(6).AddDays(-1);
                if (nextEnd > end.Date) nextEnd = end.Date;
                int hNum = cur.Month <= 6 ? 1 : 2;
                label = $"H{hNum} {cur.Year} ({cur:MMM}-{nextEnd:MMM})";
            }
            else // Yearly
            {
                nextEnd = end.Date;
                label = $"FY ({cur:yyyy})";
            }

            result.Add(new QuotationTrendsPeriodColumnDto
            {
                Key = key,
                Label = label,
                StartDate = cur,
                EndDate = nextEnd.Date.AddDays(1).AddTicks(-1),
            });

            cur = nextEnd.AddDays(1);
            idx++;
        }

        return result;
    }

    private sealed record TrendLineRecord(
        DateTime IssueDate,
        Guid CustomerId,
        Guid ItemId,
        decimal Quantity,
        decimal Amount);

    private static List<QuotationTrendsRowDto> BuildReportRows(
        QuotationTrendsRequestDto input,
        List<Quotation> quotations,
        Dictionary<Guid, Customer> customers,
        Dictionary<Guid, Item> items,
        Dictionary<Guid, string> territories,
        List<QuotationTrendsPeriodColumnDto> periods)
    {
        var basedOn = input.BasedOn?.Trim().ToLowerInvariant() switch
        {
            "item" => "Item",
            "itemgroup" or "item group" => "ItemGroup",
            _ => "Customer",
        };

        var groupBy = string.IsNullOrWhiteSpace(input.GroupBy) || input.GroupBy.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? null
            : (input.GroupBy.Trim().Equals("Item", StringComparison.OrdinalIgnoreCase) ? "Item" : "Customer");

        // Flatten quotation items
        var flatLines = quotations.SelectMany(q => q.Items.Select(i => new TrendLineRecord(
            q.IssueDate,
            q.CustomerId,
            i.ItemId,
            i.Quantity,
            i.LineTotal
        ))).ToList();

        var result = new List<QuotationTrendsRowDto>();

        if (basedOn == "Item")
        {
            var byItem = flatLines.GroupBy(l => l.ItemId);
            foreach (var g in byItem)
            {
                items.TryGetValue(g.Key, out var itemMaster);
                var itemCode = itemMaster?.ItemCode ?? g.Key.ToString()[..8];
                var itemName = itemMaster?.ItemName ?? itemCode;
                var itemGroup = itemMaster?.ItemGroup ?? "—";

                var headerRow = CreateRow(g.Key.ToString(), itemCode, itemName, itemGroup, null, periods, g);
                result.Add(headerRow);

                if (groupBy == "Customer")
                {
                    var byCust = g.GroupBy(l => l.CustomerId);
                    foreach (var cg in byCust)
                    {
                        customers.TryGetValue(cg.Key, out var custMaster);
                        var custName = custMaster?.Name ?? cg.Key.ToString()[..8];
                        var detailRow = CreateRow(g.Key.ToString(), itemCode, itemName, itemGroup, custName, periods, cg);
                        result.Add(detailRow);
                    }
                }
            }
        }
        else if (basedOn == "ItemGroup")
        {
            var byGroup = flatLines.GroupBy(l =>
            {
                items.TryGetValue(l.ItemId, out var itemMaster);
                return itemMaster?.ItemGroup ?? "Not Specified";
            });

            foreach (var g in byGroup)
            {
                var groupName = g.Key;
                var headerRow = CreateRow(groupName, groupName, groupName, null, null, periods, g);
                result.Add(headerRow);

                if (groupBy == "Customer")
                {
                    var byCust = g.GroupBy(l => l.CustomerId);
                    foreach (var cg in byCust)
                    {
                        customers.TryGetValue(cg.Key, out var custMaster);
                        var custName = custMaster?.Name ?? cg.Key.ToString()[..8];
                        var detailRow = CreateRow(groupName, groupName, groupName, null, custName, periods, cg);
                        result.Add(detailRow);
                    }
                }
            }
        }
        else // Customer
        {
            var byCustomer = flatLines.GroupBy(l => l.CustomerId);
            foreach (var g in byCustomer)
            {
                customers.TryGetValue(g.Key, out var custMaster);
                var custCode = custMaster?.CustomerCode ?? g.Key.ToString()[..8];
                var custName = custMaster?.Name ?? custCode;
                var territory = custMaster?.TerritoryId.HasValue == true
                    ? territories.GetValueOrDefault(custMaster.TerritoryId.Value, "—")
                    : "—";

                var headerRow = CreateRow(g.Key.ToString(), custCode, custName, territory, null, periods, g);
                result.Add(headerRow);

                if (groupBy == "Item")
                {
                    var byItm = g.GroupBy(l => l.ItemId);
                    foreach (var ig in byItm)
                    {
                        items.TryGetValue(ig.Key, out var itemMaster);
                        var itemName = itemMaster?.ItemName ?? itemMaster?.ItemCode ?? ig.Key.ToString()[..8];
                        var detailRow = CreateRow(g.Key.ToString(), custCode, custName, territory, itemName, periods, ig);
                        result.Add(detailRow);
                    }
                }
            }
        }

        return result.OrderBy(r => r.EntityName).ThenBy(r => r.GroupByValue ?? string.Empty).ToList();
    }

    private static QuotationTrendsRowDto CreateRow(
        string id,
        string code,
        string name,
        string? secondary,
        string? groupByVal,
        List<QuotationTrendsPeriodColumnDto> periods,
        IEnumerable<TrendLineRecord> lines)
    {
        var row = new QuotationTrendsRowDto
        {
            EntityId = id,
            EntityCode = code,
            EntityName = name,
            SecondaryInfo = secondary,
            GroupByValue = groupByVal,
        };

        var lineList = lines.ToList();

        foreach (var p in periods)
        {
            var periodLines = lineList.Where(l => l.IssueDate >= p.StartDate && l.IssueDate <= p.EndDate).ToList();
            var q = periodLines.Sum(l => l.Quantity);
            var a = periodLines.Sum(l => l.Amount);

            row.PeriodQuantities[p.Key] = q;
            row.PeriodAmounts[p.Key] = a;
        }

        row.TotalQty = row.PeriodQuantities.Values.Sum();
        row.TotalAmount = row.PeriodAmounts.Values.Sum();

        return row;
    }
}
