using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Exponential Smoothing Demand Forecasting AppService.
/// Forecasts future product demand using Holt-Winters / single exponential smoothing
/// across historical Sales Orders, Delivery Notes, or Sales Invoices.
/// Maps to ERPNext manufacturing/report/exponential_smoothing_forecasting/exponential_smoothing_forecasting.py.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class ExponentialSmoothingForecastingAppService : ApplicationService, IExponentialSmoothingForecastingAppService
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public ExponentialSmoothingForecastingAppService(
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<DeliveryNote, Guid> deliveryNoteRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _salesOrderRepository = salesOrderRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<ExponentialSmoothingForecastingDto> GetForecastAsync(ExponentialSmoothingForecastingFilterDto input)
    {
        var alpha = Math.Clamp(input.SmoothingConstant, 0.01m, 1.0m);
        var noOfYears = Math.Max(1, input.NoOfYears);
        var fromDate = input.FromDate.Date;
        var toDate = input.ToDate.Date;
        if (toDate < fromDate) toDate = fromDate.AddMonths(3);

        var historyStartDate = fromDate.AddYears(-noOfYears);

        // 1. Generate historical and future periods
        var (historicalPeriods, futurePeriods) = GeneratePeriods(historyStartDate, fromDate, toDate, input.Periodicity);
        var allPeriods = historicalPeriods.Concat(futurePeriods).ToList();

        // 2. Load Item and Warehouse metadata
        var items = await _itemRepository.GetListAsync();
        var itemMap = items.ToDictionary(i => i.Id);

        var warehouses = await _warehouseRepository.GetListAsync();
        var warehouseMap = warehouses.ToDictionary(w => w.Id);

        // 3. Query historical demand records strictly before fromDate
        var demandRecords = await GetDemandRecordsAsync(input, historyStartDate, fromDate);

        // Filter by Item and Warehouse if specified
        if (input.ItemId.HasValue)
        {
            demandRecords = demandRecords.Where(r => r.ItemId == input.ItemId.Value).ToList();
        }

        if (input.WarehouseId.HasValue)
        {
            demandRecords = demandRecords.Where(r => r.WarehouseId == input.WarehouseId.Value).ToList();
        }

        // 4. Bucket demand by (ItemId, WarehouseId) and historical periods
        var groupMap = new Dictionary<(Guid ItemId, Guid? WarehouseId), Dictionary<string, decimal>>();
        foreach (var rec in demandRecords)
        {
            var key = (rec.ItemId, rec.WarehouseId);
            if (!groupMap.TryGetValue(key, out var periodDict))
            {
                periodDict = new Dictionary<string, decimal>();
                groupMap[key] = periodDict;
            }

            var matchingPeriod = historicalPeriods.FirstOrDefault(p => rec.Date >= p.FromDate && rec.Date <= p.ToDate);
            if (matchingPeriod != null)
            {
                periodDict[matchingPeriod.Key] = periodDict.GetValueOrDefault(matchingPeriod.Key) + rec.Value;
            }
        }

        // If no demand records found but item was specifically requested, initialize empty entry
        if (groupMap.Count == 0 && input.ItemId.HasValue)
        {
            groupMap[(input.ItemId.Value, input.WarehouseId)] = new Dictionary<string, decimal>();
        }

        // 5. Execute Exponential Smoothing for each (ItemId, WarehouseId) group
        var rows = new List<ExponentialSmoothingForecastingRowDto>();
        decimal grandTotalDemandPast = 0m;
        decimal grandTotalForecastPast = 0m;
        decimal grandTotalForecastFuture = 0m;

        foreach (var (key, periodDemand) in groupMap)
        {
            var item = itemMap.TryGetValue(key.ItemId, out var it) ? it : null;
            var itemCode = item?.ItemCode ?? string.Empty;
            var itemName = item?.ItemName ?? string.Empty;
            var warehouseName = key.WarehouseId.HasValue && warehouseMap.TryGetValue(key.WarehouseId.Value, out var wh)
                ? wh.Name
                : string.Empty;

            var row = new ExponentialSmoothingForecastingRowDto
            {
                ItemId = key.ItemId,
                ItemCode = itemCode,
                ItemName = itemName,
                WarehouseId = key.WarehouseId,
                WarehouseName = warehouseName
            };

            // Historical average of non-zero periods
            var nonZeroValues = historicalPeriods
                .Select(p => periodDemand.GetValueOrDefault(p.Key, 0m))
                .Where(v => v > 0m)
                .ToList();

            var avg = nonZeroValues.Count > 0
                ? nonZeroValues.Sum() / nonZeroValues.Count
                : 0m;

            row.AverageHistoricalDemand = Math.Round(avg, 4);

            var forecastData = new List<(decimal actual, decimal forecast)>();

            foreach (var period in allPeriods)
            {
                var forecastKey = "forecast_" + period.Key;
                var hasActual = periodDemand.TryGetValue(period.Key, out var actual) && actual > 0m;
                var actualVal = hasActual ? actual : 0m;

                decimal? periodForecast = null;

                if (hasActual && forecastData.Count == 0)
                {
                    periodForecast = avg > 0m ? avg : actualVal;
                }
                else if (forecastData.Count > 0)
                {
                    var (prevActual, prevForecast) = forecastData[^1];
                    if (period.IsForecast && prevActual == 0m)
                    {
                        periodForecast = prevForecast;
                    }
                    else
                    {
                        periodForecast = prevForecast + alpha * (prevActual - prevForecast);
                    }
                }

                if (periodForecast.HasValue)
                {
                    row.PeriodValues[forecastKey] = Math.Round(periodForecast.Value, 4);
                    if (period.IsForecast)
                    {
                        row.PeriodValues[period.Key] = Math.Round(periodForecast.Value, 4);
                        row.TotalForecast += periodForecast.Value;
                        grandTotalForecastFuture += periodForecast.Value;
                    }
                    else
                    {
                        grandTotalForecastPast += periodForecast.Value;
                    }

                    forecastData.Add((actualVal, periodForecast.Value));
                }

                if (!period.IsForecast)
                {
                    row.PeriodValues[period.Key] = actualVal;
                    row.TotalDemand += actualVal;
                    grandTotalDemandPast += actualVal;
                }
            }

            rows.Add(row);
        }

        // Filter by SearchTerm if provided
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            rows = rows.Where(r =>
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Sort rows by ItemCode
        rows = rows.OrderBy(r => r.ItemCode).ThenBy(r => r.WarehouseName).ToList();

        // Build Total Row
        ExponentialSmoothingForecastingRowDto? totalRow = null;
        if (rows.Count > 0)
        {
            totalRow = new ExponentialSmoothingForecastingRowDto
            {
                ItemCode = "Total",
                ItemName = "Total Quantity / Amount"
            };

            foreach (var p in allPeriods)
            {
                totalRow.PeriodValues[p.Key] = rows.Sum(r => r.PeriodValues.GetValueOrDefault(p.Key, 0m));
                var fKey = "forecast_" + p.Key;
                totalRow.PeriodValues[fKey] = rows.Sum(r => r.PeriodValues.GetValueOrDefault(fKey, 0m));
            }

            totalRow.TotalDemand = rows.Sum(r => r.TotalDemand);
            totalRow.TotalForecast = rows.Sum(r => r.TotalForecast);
        }

        return new ExponentialSmoothingForecastingDto
        {
            Periods = allPeriods,
            TotalDemandPastData = Math.Round(grandTotalDemandPast, 2),
            TotalForecastPastData = Math.Round(grandTotalForecastPast, 2),
            TotalForecastFutureData = Math.Round(grandTotalForecastFuture, 2),
            Rows = rows,
            TotalRow = totalRow
        };
    }

    private static (List<ForecastingPeriodDto> Historical, List<ForecastingPeriodDto> Future) GeneratePeriods(
        DateTime historyStartDate,
        DateTime fromDate,
        DateTime toDate,
        string periodicity)
    {
        var historical = new List<ForecastingPeriodDto>();
        var future = new List<ForecastingPeriodDto>();

        var p = string.IsNullOrWhiteSpace(periodicity) ? "Monthly" : periodicity.Trim();

        var curr = new DateTime(historyStartDate.Year, historyStartDate.Month, 1);
        while (curr < fromDate)
        {
            var (next, key, label) = GetPeriodStep(curr, p);
            var periodEnd = next.AddDays(-1);
            historical.Add(new ForecastingPeriodDto
            {
                Key = key,
                Label = label,
                FromDate = curr,
                ToDate = periodEnd,
                IsForecast = false
            });
            curr = next;
        }

        curr = new DateTime(fromDate.Year, fromDate.Month, 1);
        while (curr <= toDate)
        {
            var (next, key, label) = GetPeriodStep(curr, p);
            var periodEnd = next.AddDays(-1);
            future.Add(new ForecastingPeriodDto
            {
                Key = key,
                Label = label + " (Forecast)",
                FromDate = curr,
                ToDate = periodEnd,
                IsForecast = true
            });
            curr = next;
        }

        return (historical, future);
    }

    private static (DateTime Next, string Key, string Label) GetPeriodStep(DateTime start, string periodicity)
    {
        return periodicity.ToLowerInvariant() switch
        {
            "yearly" => (start.AddYears(1), start.ToString("yyyy"), start.ToString("yyyy")),
            "half-yearly" => (start.AddMonths(6),
                $"{start:yyyy}-H{(start.Month <= 6 ? 1 : 2)}",
                $"H{(start.Month <= 6 ? 1 : 2)} {start:yyyy}"),
            "quarterly" => (start.AddMonths(3),
                $"{start:yyyy}-Q{((start.Month - 1) / 3) + 1}",
                $"Q{((start.Month - 1) / 3) + 1} {start:yyyy}"),
            _ => (start.AddMonths(1), start.ToString("yyyy-MM"), start.ToString("MMM yyyy"))
        };
    }

    private async Task<List<DemandRecord>> GetDemandRecordsAsync(
        ExponentialSmoothingForecastingFilterDto input,
        DateTime startDate,
        DateTime endDate)
    {
        var result = new List<DemandRecord>();
        var basedOnDoc = string.IsNullOrWhiteSpace(input.BasedOnDocument) ? "Sales Order" : input.BasedOnDocument.Trim();
        var isQty = !string.Equals(input.BasedOnField, "Amount", StringComparison.OrdinalIgnoreCase);

        if (basedOnDoc.Equals("Sales Order", StringComparison.OrdinalIgnoreCase))
        {
            var soQuery = await _salesOrderRepository.WithDetailsAsync(s => s.Items);
            var orders = soQuery
                .Where(s => s.Status != DocumentStatus.Draft && s.Status != DocumentStatus.Cancelled)
                .Where(s => s.OrderDate >= startDate && s.OrderDate < endDate)
                .ToList();

            if (input.CompanyId.HasValue)
            {
                orders = orders.Where(s => s.CompanyId == input.CompanyId.Value).ToList();
            }

            foreach (var so in orders)
            {
                foreach (var item in so.Items)
                {
                    result.Add(new DemandRecord
                    {
                        ItemId = item.ItemId,
                        WarehouseId = null,
                        Date = so.OrderDate,
                        Value = isQty ? item.Quantity : item.LineTotal
                    });
                }
            }
        }
        else if (basedOnDoc.Equals("Delivery Note", StringComparison.OrdinalIgnoreCase))
        {
            var dnQuery = await _deliveryNoteRepository.WithDetailsAsync(d => d.Items);
            var notes = dnQuery
                .Where(d => d.Status != DocumentStatus.Draft && d.Status != DocumentStatus.Cancelled)
                .Where(d => d.PostingDate >= startDate && d.PostingDate < endDate)
                .ToList();

            if (input.CompanyId.HasValue)
            {
                notes = notes.Where(d => d.CompanyId == input.CompanyId.Value).ToList();
            }

            foreach (var dn in notes)
            {
                foreach (var item in dn.Items)
                {
                    result.Add(new DemandRecord
                    {
                        ItemId = item.ItemId,
                        WarehouseId = item.WarehouseId ?? (dn.WarehouseId != Guid.Empty ? dn.WarehouseId : null),
                        Date = dn.PostingDate,
                        Value = isQty ? item.Quantity : item.LineTotal
                    });
                }
            }
        }
        else if (basedOnDoc.Equals("Sales Invoice", StringComparison.OrdinalIgnoreCase))
        {
            var siQuery = await _salesInvoiceRepository.WithDetailsAsync(s => s.Items);
            var invoices = siQuery
                .Where(s => s.Status != DocumentStatus.Draft && s.Status != DocumentStatus.Cancelled)
                .Where(s => s.IssueDate >= startDate && s.IssueDate < endDate)
                .ToList();

            if (input.CompanyId.HasValue)
            {
                invoices = invoices.Where(s => s.CompanyId == input.CompanyId.Value).ToList();
            }

            foreach (var si in invoices)
            {
                foreach (var item in si.Items)
                {
                    result.Add(new DemandRecord
                    {
                        ItemId = item.ItemId,
                        WarehouseId = null,
                        Date = si.IssueDate,
                        Value = isQty ? item.Quantity : item.LineTotal
                    });
                }
            }
        }

        return result;
    }

    private class DemandRecord
    {
        public Guid ItemId { get; set; }
        public Guid? WarehouseId { get; set; }
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }
}
