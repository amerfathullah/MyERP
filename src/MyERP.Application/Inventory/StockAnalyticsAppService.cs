using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Stock Analytics Report application service.
/// Calculates periodic stock balances and valuation trends across dynamic time intervals.
/// Ported from ERPNext stock/report/stock_analytics.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockAnalyticsAppService : ApplicationService, IStockAnalyticsAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public StockAnalyticsAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _sleRepository = sleRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _itemGroupRepository = itemGroupRepository;
        _companyRepository = companyRepository;
    }

    public async Task<StockAnalyticsReportDto> GetReportAsync(StockAnalyticsRequestDto input)
    {
        var company = await _companyRepository.FindAsync(input.CompanyId);
        var companyCurrency = company?.CurrencyCode ?? "MYR";

        var today = DateTime.UtcNow.Date;
        var fromDate = input.FromDate?.Date ?? new DateTime(today.Year, 1, 1);
        var toDate = input.ToDate?.Date ?? today;
        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var isValue = string.Equals(input.ValueOrQuantity, "Value", StringComparison.OrdinalIgnoreCase);
        var range = string.IsNullOrWhiteSpace(input.Range) ? "Monthly" : input.Range;

        var periodColumns = GeneratePeriodColumns(fromDate, toDate, range);

        // Fetch candidate items
        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var itemQuery = itemQueryable.Where(i => i.MaintainStock && i.IsActive);
        if (input.ItemGroupId.HasValue)
        {
            itemQuery = itemQuery.Where(i => i.ItemGroupId == input.ItemGroupId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            itemQuery = itemQuery.Where(i => i.ItemCode.Contains(term) || i.ItemName.Contains(term));
        }

        var items = itemQuery.ToList();
        if (items.Count == 0)
        {
            return new StockAnalyticsReportDto
            {
                CompanyCurrency = companyCurrency,
                Range = range,
                ValueOrQuantity = input.ValueOrQuantity,
                PeriodColumns = periodColumns
            };
        }

        var itemIds = items.Select(i => i.Id).ToHashSet();

        // Fetch SLEs up to end date cutoff
        var toDateCutoff = toDate.AddDays(1).AddTicks(-1);
        var sleQueryable = await _sleRepository.GetQueryableAsync();
        var sleQuery = sleQueryable.Where(s =>
            s.CompanyId == input.CompanyId &&
            !s.IsCancelled &&
            s.PostingDateTime <= toDateCutoff &&
            itemIds.Contains(s.ItemId));

        if (input.WarehouseId.HasValue)
        {
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);
        }

        var sles = sleQuery.ToList();

        // Item groups dictionary
        var groupIds = items.Where(i => i.ItemGroupId.HasValue).Select(i => i.ItemGroupId!.Value).Distinct().ToList();
        var groupQueryable = await _itemGroupRepository.GetQueryableAsync();
        var itemGroups = groupQueryable.Where(g => groupIds.Contains(g.Id)).ToList().ToDictionary(g => g.Id, g => g.Name);

        var slesByItem = sles.GroupBy(s => s.ItemId).ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<StockAnalyticsRowDto>();

        foreach (var item in items)
        {
            slesByItem.TryGetValue(item.Id, out var itemSles);
            itemSles ??= new List<StockLedgerEntry>();

            var opening = itemSles
                .Where(s => s.PostingDateTime < fromDate)
                .Sum(s => isValue ? s.StockValueDifference : s.QuantityChange);

            var runningBalance = opening;
            var periodBalances = new Dictionary<string, decimal>();

            foreach (var col in periodColumns)
            {
                var colEndCutoff = col.EndDate.AddDays(1).AddTicks(-1);
                var periodDelta = itemSles
                    .Where(s => s.PostingDateTime >= col.StartDate && s.PostingDateTime <= colEndCutoff)
                    .Sum(s => isValue ? s.StockValueDifference : s.QuantityChange);

                runningBalance += periodDelta;
                periodBalances[col.Key] = runningBalance;
            }

            var closingBalance = runningBalance;

            // Include only items with transaction activity or non-zero balance
            if (opening == 0m && closingBalance == 0m && itemSles.Count == 0)
                continue;

            var groupName = item.ItemGroup ?? (item.ItemGroupId.HasValue && itemGroups.TryGetValue(item.ItemGroupId.Value, out var gName) ? gName : string.Empty);

            rows.Add(new StockAnalyticsRowDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                ItemGroup = groupName,
                Brand = item.Brand,
                Uom = item.Uom,
                OpeningBalance = opening,
                PeriodBalances = periodBalances,
                ClosingBalance = closingBalance
            });
        }

        // Sort rows by ItemCode ascending
        rows = rows.OrderBy(r => r.ItemCode).ToList();

        // Total row calculation
        var totalOpening = rows.Sum(r => r.OpeningBalance);
        var totalClosing = rows.Sum(r => r.ClosingBalance);
        var totalPeriodBalances = new Dictionary<string, decimal>();

        foreach (var col in periodColumns)
        {
            totalPeriodBalances[col.Key] = rows.Sum(r => r.PeriodBalances.TryGetValue(col.Key, out var b) ? b : 0m);
        }

        var totalRow = new StockAnalyticsRowDto
        {
            ItemCode = "Total",
            ItemName = "Total",
            OpeningBalance = totalOpening,
            PeriodBalances = totalPeriodBalances,
            ClosingBalance = totalClosing
        };

        // Chart data
        var chart = new StockAnalyticsChartDto
        {
            Labels = periodColumns.Select(c => c.Label).ToList(),
            Datapoints = periodColumns.Select(c => totalPeriodBalances.TryGetValue(c.Key, out var b) ? b : 0m).ToList(),
            Unit = isValue ? companyCurrency : "Qty"
        };

        // Calculate overall closing quantity and value stats
        var totalClosingQty = isValue
            ? sles.Where(s => s.PostingDateTime <= toDateCutoff).Sum(s => s.QuantityChange)
            : totalClosing;

        var totalClosingVal = isValue
            ? totalClosing
            : sles.Where(s => s.PostingDateTime <= toDateCutoff).Sum(s => s.StockValueDifference);

        return new StockAnalyticsReportDto
        {
            CompanyCurrency = companyCurrency,
            Range = range,
            ValueOrQuantity = input.ValueOrQuantity,
            PeriodColumns = periodColumns,
            Rows = rows,
            TotalRow = totalRow,
            Chart = chart,
            TotalItems = rows.Count,
            TotalClosingQuantity = totalClosingQty,
            TotalClosingValue = totalClosingVal
        };
    }

    private static List<StockAnalyticsPeriodColumnDto> GeneratePeriodColumns(DateTime fromDate, DateTime toDate, string range)
    {
        var columns = new List<StockAnalyticsPeriodColumnDto>();
        var current = fromDate;

        while (current <= toDate)
        {
            DateTime periodEnd;
            string label;

            switch (range.ToLowerInvariant())
            {
                case "weekly":
                    var weekEnd = current.AddDays(6);
                    periodEnd = weekEnd > toDate ? toDate : weekEnd;
                    var isoWeek = System.Globalization.ISOWeek.GetWeekOfYear(weekEnd);
                    var isoYear = System.Globalization.ISOWeek.GetYear(weekEnd);
                    label = $"W{isoWeek} {isoYear}";
                    current = periodEnd.AddDays(1);
                    break;

                case "quarterly":
                    var quarterNumber = (current.Month - 1) / 3 + 1;
                    var qStartMonth = (quarterNumber - 1) * 3 + 1;
                    var qStartDate = new DateTime(current.Year, qStartMonth, 1);
                    periodEnd = qStartDate.AddMonths(3).AddDays(-1);
                    if (periodEnd > toDate) periodEnd = toDate;
                    label = $"Q{quarterNumber} {current.Year}";
                    current = periodEnd.AddDays(1);
                    break;

                case "halfyearly":
                    var halfNumber = current.Month <= 6 ? 1 : 2;
                    var hStartMonth = halfNumber == 1 ? 1 : 7;
                    var hStartDate = new DateTime(current.Year, hStartMonth, 1);
                    periodEnd = hStartDate.AddMonths(6).AddDays(-1);
                    if (periodEnd > toDate) periodEnd = toDate;
                    label = $"H{halfNumber} {current.Year}";
                    current = periodEnd.AddDays(1);
                    break;

                case "yearly":
                    periodEnd = new DateTime(current.Year, 12, 31);
                    if (periodEnd > toDate) periodEnd = toDate;
                    label = current.Year.ToString();
                    current = periodEnd.AddDays(1);
                    break;

                case "monthly":
                default:
                    var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                    periodEnd = new DateTime(current.Year, current.Month, daysInMonth);
                    if (periodEnd > toDate) periodEnd = toDate;
                    label = current.ToString("MMM yyyy");
                    current = periodEnd.AddDays(1);
                    break;
            }

            var colStartDate = columns.Count == 0 ? fromDate : columns.Last().EndDate.AddDays(1);
            var key = $"p_{colStartDate:yyyyMMdd}_{periodEnd:yyyyMMdd}";

            columns.Add(new StockAnalyticsPeriodColumnDto
            {
                Key = key,
                Label = label,
                StartDate = colStartDate,
                EndDate = periodEnd
            });
        }

        return columns;
    }
}
