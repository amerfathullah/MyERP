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

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesInvoiceTrendsAppService : ApplicationService, ISalesInvoiceTrendsAppService
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public SalesInvoiceTrendsAppService(
        IRepository<SalesInvoice, Guid> invoiceRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _invoiceRepo = invoiceRepo;
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }

    public async Task<SalesInvoiceTrendsReportDto> GetReportAsync(SalesInvoiceTrendsRequestDto input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";

        var (yearStart, yearEnd) = await ResolveDateRangeAsync(input);
        var periods = GeneratePeriods(input.Period, yearStart, yearEnd);

        var invoiceQuery = await _invoiceRepo.WithDetailsAsync(i => i.Items);
        var invoices = invoiceQuery
            .Where(i => i.CompanyId == input.CompanyId
                        && i.Status != DocumentStatus.Draft
                        && i.Status != DocumentStatus.Cancelled
                        && i.IssueDate >= yearStart
                        && i.IssueDate <= yearEnd)
            .ToList();

        if (!input.IncludeReturns)
        {
            invoices = invoices.Where(i => !i.IsReturn).ToList();
        }

        // Master records lookup
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var itemIds = invoices.SelectMany(i => i.Items.Select(item => item.ItemId)).Distinct().ToList();

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

        var rows = BuildReportRows(input, invoices, customers, items, territoryDict, periods);

        // Calculate Grand Total Row
        var totalRow = new SalesInvoiceTrendsRowDto
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

        return new SalesInvoiceTrendsReportDto
        {
            CompanyCurrency = companyCurrency,
            Period = input.Period,
            BasedOn = input.BasedOn,
            GroupBy = input.GroupBy,
            PeriodColumns = periods,
            Rows = rows,
            TotalRow = totalRow,
            Chart = new SalesInvoiceTrendsChartDto
            {
                Labels = chartLabels,
                Datapoints = chartDatapoints,
                Currency = companyCurrency,
            },
        };
    }

    private async Task<(DateTime StartDate, DateTime EndDate)> ResolveDateRangeAsync(SalesInvoiceTrendsRequestDto input)
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

    private static List<SalesInvoiceTrendsPeriodColumnDto> GeneratePeriods(string period, DateTime start, DateTime end)
    {
        var result = new List<SalesInvoiceTrendsPeriodColumnDto>();
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

            result.Add(new SalesInvoiceTrendsPeriodColumnDto
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

    private static List<SalesInvoiceTrendsRowDto> BuildReportRows(
        SalesInvoiceTrendsRequestDto input,
        List<SalesInvoice> invoices,
        Dictionary<Guid, Customer> customers,
        Dictionary<Guid, Item> items,
        Dictionary<Guid, string> territories,
        List<SalesInvoiceTrendsPeriodColumnDto> periods)
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

        // Flatten invoice items with date and customer info
        var flatLines = invoices.SelectMany(inv => inv.Items.Select(item => new TrendLineRecord(
            inv.IssueDate,
            inv.CustomerId,
            item.ItemId,
            item.Quantity,
            item.LineTotal * (inv.ExchangeRate > 0 ? inv.ExchangeRate : 1m)
        ))).ToList();

        var result = new List<SalesInvoiceTrendsRowDto>();

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
                        var custCode = custMaster?.CustomerCode ?? cg.Key.ToString()[..8];
                        var custName = custMaster?.Name ?? custCode;
                        var subRow = CreateRow(cg.Key.ToString(), custCode, custName, null, custName, periods, cg);
                        result.Add(subRow);
                    }
                }
            }
        }
        else if (basedOn == "ItemGroup")
        {
            var withGroup = flatLines.Select(l =>
            {
                items.TryGetValue(l.ItemId, out var itemMaster);
                var group = !string.IsNullOrWhiteSpace(itemMaster?.ItemGroup) ? itemMaster.ItemGroup : "Unclassified";
                return new { Line = l, Group = group };
            });

            var byGroup = withGroup.GroupBy(x => x.Group);
            foreach (var g in byGroup)
            {
                var groupLines = g.Select(x => x.Line);
                var headerRow = CreateRow(g.Key, g.Key, g.Key, null, null, periods, groupLines);
                result.Add(headerRow);

                if (groupBy == "Item")
                {
                    var byItem = g.GroupBy(x => x.Line.ItemId);
                    foreach (var ig in byItem)
                    {
                        items.TryGetValue(ig.Key, out var itemMaster);
                        var itemCode = itemMaster?.ItemCode ?? ig.Key.ToString()[..8];
                        var itemName = itemMaster?.ItemName ?? itemCode;
                        var subRow = CreateRow(ig.Key.ToString(), itemCode, itemName, null, itemName, periods, ig.Select(x => x.Line));
                        result.Add(subRow);
                    }
                }
                else if (groupBy == "Customer")
                {
                    var byCust = g.GroupBy(x => x.Line.CustomerId);
                    foreach (var cg in byCust)
                    {
                        customers.TryGetValue(cg.Key, out var custMaster);
                        var custCode = custMaster?.CustomerCode ?? cg.Key.ToString()[..8];
                        var custName = custMaster?.Name ?? custCode;
                        var subRow = CreateRow(cg.Key.ToString(), custCode, custName, null, custName, periods, cg.Select(x => x.Line));
                        result.Add(subRow);
                    }
                }
            }
        }
        else // Customer
        {
            var byCust = flatLines.GroupBy(l => l.CustomerId);
            foreach (var g in byCust)
            {
                customers.TryGetValue(g.Key, out var custMaster);
                var custCode = custMaster?.CustomerCode ?? g.Key.ToString()[..8];
                var custName = custMaster?.Name ?? custCode;
                string? territory = null;
                if (custMaster?.TerritoryId.HasValue == true)
                {
                    territories.TryGetValue(custMaster.TerritoryId.Value, out territory);
                }

                var headerRow = CreateRow(g.Key.ToString(), custCode, custName, territory, null, periods, g);
                result.Add(headerRow);

                if (groupBy == "Item")
                {
                    var byItem = g.GroupBy(l => l.ItemId);
                    foreach (var ig in byItem)
                    {
                        items.TryGetValue(ig.Key, out var itemMaster);
                        var itemCode = itemMaster?.ItemCode ?? ig.Key.ToString()[..8];
                        var itemName = itemMaster?.ItemName ?? itemCode;
                        var subRow = CreateRow(ig.Key.ToString(), itemCode, itemName, null, itemName, periods, ig);
                        result.Add(subRow);
                    }
                }
            }
        }

        return result.OrderBy(r => r.EntityName).ToList();
    }

    private static SalesInvoiceTrendsRowDto CreateRow(
        string entityId,
        string entityCode,
        string entityName,
        string? secondaryInfo,
        string? groupByValue,
        List<SalesInvoiceTrendsPeriodColumnDto> periods,
        IEnumerable<TrendLineRecord> lines)
    {
        var lineList = lines.ToList();
        var row = new SalesInvoiceTrendsRowDto
        {
            EntityId = entityId,
            EntityCode = entityCode,
            EntityName = entityName,
            SecondaryInfo = secondaryInfo,
            GroupByValue = groupByValue,
        };

        foreach (var p in periods)
        {
            var pLines = lineList.Where(l => l.IssueDate >= p.StartDate && l.IssueDate <= p.EndDate).ToList();
            row.PeriodQuantities[p.Key] = pLines.Sum(l => l.Quantity);
            row.PeriodAmounts[p.Key] = pLines.Sum(l => l.Amount);
        }

        row.TotalQty = row.PeriodQuantities.Values.Sum();
        row.TotalAmount = row.PeriodAmounts.Values.Sum();

        return row;
    }
}
