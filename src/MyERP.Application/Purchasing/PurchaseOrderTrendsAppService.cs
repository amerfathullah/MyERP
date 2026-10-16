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
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.PurchaseOrders.Default)]
public class PurchaseOrderTrendsAppService : ApplicationService, IPurchaseOrderTrendsAppService
{
    private readonly IRepository<PurchaseOrder, Guid> _purchaseOrderRepo;
    private readonly IRepository<Supplier, Guid> _supplierRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public PurchaseOrderTrendsAppService(
        IRepository<PurchaseOrder, Guid> purchaseOrderRepo,
        IRepository<Supplier, Guid> supplierRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _purchaseOrderRepo = purchaseOrderRepo;
        _supplierRepo = supplierRepo;
        _itemRepo = itemRepo;
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }

    public async Task<PurchaseOrderTrendsReportDto> GetReportAsync(PurchaseOrderTrendsRequestDto input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";

        var (yearStart, yearEnd) = await ResolveDateRangeAsync(input);
        var periods = GeneratePeriods(input.Period, yearStart, yearEnd);

        var poQuery = await _purchaseOrderRepo.WithDetailsAsync(po => po.Items);
        var orders = poQuery
            .Where(po => po.CompanyId == input.CompanyId
                         && po.Status != DocumentStatus.Draft
                         && po.Status != DocumentStatus.Cancelled
                         && po.OrderDate >= yearStart
                         && po.OrderDate <= yearEnd)
            .ToList();

        if (!input.IncludeClosedOrders)
        {
            orders = orders.Where(po => po.Status != DocumentStatus.Closed).ToList();
        }

        if (input.SupplierId.HasValue)
        {
            orders = orders.Where(po => po.SupplierId == input.SupplierId.Value).ToList();
        }

        var supplierIds = orders.Select(po => po.SupplierId).Distinct().ToList();
        var itemIds = orders.SelectMany(po => po.Items.Select(i => i.ItemId)).Distinct().ToList();

        var supplierQuery = await _supplierRepo.GetQueryableAsync();
        var suppliers = supplierQuery
            .Where(s => supplierIds.Contains(s.Id))
            .ToList()
            .ToDictionary(s => s.Id);

        var supplierGroupIds = suppliers.Values
            .Where(s => s.SupplierGroupId.HasValue)
            .Select(s => s.SupplierGroupId!.Value)
            .Distinct()
            .ToList();

        var supplierGroupDict = new Dictionary<Guid, string>();
        if (supplierGroupIds.Count > 0)
        {
            var groupRepo = LazyServiceProvider.LazyGetService<IRepository<SupplierGroup, Guid>>();
            if (groupRepo != null)
            {
                var gQuery = await groupRepo.GetQueryableAsync();
                supplierGroupDict = gQuery
                    .Where(g => supplierGroupIds.Contains(g.Id))
                    .Select(g => new { g.Id, g.Name })
                    .ToList()
                    .ToDictionary(g => g.Id, g => g.Name);
            }
        }

        var itemQuery = await _itemRepo.GetQueryableAsync();
        var items = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .ToList()
            .ToDictionary(i => i.Id);

        var rows = BuildReportRows(input, orders, suppliers, items, supplierGroupDict, periods);

        // Grand Total Row
        var totalRow = new PurchaseOrderTrendsRowDto
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

        return new PurchaseOrderTrendsReportDto
        {
            CompanyCurrency = companyCurrency,
            Period = input.Period,
            BasedOn = input.BasedOn,
            GroupBy = input.GroupBy,
            PeriodColumns = periods,
            Rows = rows,
            TotalRow = totalRow,
            Chart = new PurchaseOrderTrendsChartDto
            {
                Labels = chartLabels,
                Datapoints = chartDatapoints,
                Currency = companyCurrency,
            },
        };
    }

    private async Task<(DateTime StartDate, DateTime EndDate)> ResolveDateRangeAsync(PurchaseOrderTrendsRequestDto input)
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

    private static List<PurchaseOrderTrendsPeriodColumnDto> GeneratePeriods(string period, DateTime start, DateTime end)
    {
        var result = new List<PurchaseOrderTrendsPeriodColumnDto>();
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

            result.Add(new PurchaseOrderTrendsPeriodColumnDto
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
        DateTime OrderDate,
        Guid SupplierId,
        Guid ItemId,
        decimal Quantity,
        decimal Amount);

    private static List<PurchaseOrderTrendsRowDto> BuildReportRows(
        PurchaseOrderTrendsRequestDto input,
        List<PurchaseOrder> orders,
        Dictionary<Guid, Supplier> suppliers,
        Dictionary<Guid, Item> items,
        Dictionary<Guid, string> supplierGroups,
        List<PurchaseOrderTrendsPeriodColumnDto> periods)
    {
        var basedOn = input.BasedOn?.Trim().ToLowerInvariant() switch
        {
            "item" => "Item",
            "itemgroup" or "item group" => "ItemGroup",
            _ => "Supplier",
        };

        var groupBy = string.IsNullOrWhiteSpace(input.GroupBy) || input.GroupBy.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? null
            : (input.GroupBy.Trim().Equals("Item", StringComparison.OrdinalIgnoreCase) ? "Item" : "Supplier");

        // Flatten purchase order items
        var flatLines = new List<TrendLineRecord>();
        foreach (var po in orders)
        {
            var rate = po.ExchangeRate > 0 ? po.ExchangeRate : 1m;
            foreach (var item in po.Items)
            {
                if (input.ItemId.HasValue && item.ItemId != input.ItemId.Value)
                    continue;

                items.TryGetValue(item.ItemId, out var itemMaster);
                if (!string.IsNullOrWhiteSpace(input.ItemGroup) && !string.Equals(itemMaster?.ItemGroup, input.ItemGroup, StringComparison.OrdinalIgnoreCase))
                    continue;

                var stockQty = item.StockQty;
                var baseAmount = item.LineTotal * rate;

                flatLines.Add(new TrendLineRecord(
                    po.OrderDate,
                    po.SupplierId,
                    item.ItemId,
                    stockQty,
                    baseAmount
                ));
            }
        }

        var result = new List<PurchaseOrderTrendsRowDto>();

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

                if (groupBy == "Supplier")
                {
                    var bySupp = g.GroupBy(l => l.SupplierId);
                    foreach (var sg in bySupp)
                    {
                        suppliers.TryGetValue(sg.Key, out var suppMaster);
                        var suppName = suppMaster?.Name ?? sg.Key.ToString()[..8];
                        var detailRow = CreateRow(g.Key.ToString(), itemCode, itemName, itemGroup, suppName, periods, sg);
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

                if (groupBy == "Supplier")
                {
                    var bySupp = g.GroupBy(l => l.SupplierId);
                    foreach (var sg in bySupp)
                    {
                        suppliers.TryGetValue(sg.Key, out var suppMaster);
                        var suppName = suppMaster?.Name ?? sg.Key.ToString()[..8];
                        var detailRow = CreateRow(groupName, groupName, groupName, null, suppName, periods, sg);
                        result.Add(detailRow);
                    }
                }
            }
        }
        else // Supplier
        {
            var bySupplier = flatLines.GroupBy(l => l.SupplierId);
            foreach (var g in bySupplier)
            {
                suppliers.TryGetValue(g.Key, out var suppMaster);
                var suppCode = suppMaster?.SupplierCode ?? suppMaster?.Name ?? g.Key.ToString()[..8];
                var suppName = suppMaster?.Name ?? suppCode;
                var suppGroup = suppMaster?.SupplierGroupId.HasValue == true
                    ? supplierGroups.GetValueOrDefault(suppMaster.SupplierGroupId.Value, "—")
                    : "—";

                var headerRow = CreateRow(g.Key.ToString(), suppCode, suppName, suppGroup, null, periods, g);
                result.Add(headerRow);

                if (groupBy == "Item")
                {
                    var byItm = g.GroupBy(l => l.ItemId);
                    foreach (var ig in byItm)
                    {
                        items.TryGetValue(ig.Key, out var itemMaster);
                        var itemName = itemMaster?.ItemName ?? itemMaster?.ItemCode ?? ig.Key.ToString()[..8];
                        var detailRow = CreateRow(g.Key.ToString(), suppCode, suppName, suppGroup, itemName, periods, ig);
                        result.Add(detailRow);
                    }
                }
            }
        }

        return result;
    }

    private static PurchaseOrderTrendsRowDto CreateRow(
        string entityId,
        string code,
        string name,
        string? secondary,
        string? groupByValue,
        List<PurchaseOrderTrendsPeriodColumnDto> periods,
        IEnumerable<TrendLineRecord> lines)
    {
        var lineList = lines.ToList();
        var row = new PurchaseOrderTrendsRowDto
        {
            EntityId = entityId,
            EntityCode = code,
            EntityName = name,
            SecondaryInfo = secondary,
            GroupByValue = groupByValue,
        };

        foreach (var p in periods)
        {
            var pLines = lineList.Where(l => l.OrderDate >= p.StartDate && l.OrderDate <= p.EndDate).ToList();
            var q = pLines.Sum(l => l.Quantity);
            var a = pLines.Sum(l => l.Amount);

            row.PeriodQuantities[p.Key] = q;
            row.PeriodAmounts[p.Key] = a;
        }

        row.TotalQty = row.PeriodQuantities.Values.Sum();
        row.TotalAmount = row.PeriodAmounts.Values.Sum();

        return row;
    }
}
