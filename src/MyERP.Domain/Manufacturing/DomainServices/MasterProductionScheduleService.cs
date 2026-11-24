using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Computes cumulative lead time for Master Production Schedule demand aggregation.
/// Per ERPNext MasterProductionSchedule.get_cumulative_lead_time(): sums the item's own
/// lead time with the lead times of every raw material down its BOM tree (recursively,
/// through sub-assemblies), so the order/production release date can be backed off from
/// the required delivery date.
/// </summary>
public class MasterProductionScheduleService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<ItemLeadTime, Guid>? _itemLeadTimeRepository;

    public MasterProductionScheduleService(
        IRepository<Item, Guid> itemRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<ItemLeadTime, Guid>? itemLeadTimeRepository = null)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
        _itemLeadTimeRepository = itemLeadTimeRepository;
    }

    /// <summary>
    /// Resolves the item's default BOM, if any, then delegates to the recursive calculation.
    /// Supports required quantity scaling for manufacturing duration per ERPNext PR #59007 / commit 2ad4a8c4a4.
    /// </summary>
    public async Task<int> GetCumulativeLeadTimeDaysAsync(Guid itemId, Guid? bomId = null, decimal qty = 1)
    {
        return await GetCumulativeLeadTimeDaysAsync(itemId, bomId, qty, supplierId: null);
    }

    /// <summary>
    /// Resolves cumulative lead time in days, taking the ordering supplier into account for purchased items
    /// per ERPNext commits e03a0ac44b and 1cae8b6982.
    /// </summary>
    public async Task<int> GetCumulativeLeadTimeDaysAsync(Guid itemId, Guid? bomId, decimal qty, Guid? supplierId)
    {
        var visited = new HashSet<Guid>();
        return await GetCumulativeLeadTimeRecursiveAsync(itemId, bomId, qty, visited, supplierId);
    }

    private async Task<int> GetCumulativeLeadTimeRecursiveAsync(Guid itemId, Guid? bomId, decimal qty, HashSet<Guid> visitedBoms, Guid? supplierId = null)
    {
        var item = await _itemRepository.FindAsync(itemId);
        var resolvedBomId = bomId ?? item?.DefaultBomId;
        var isManufacture = resolvedBomId.HasValue;

        var leadTime = await ResolveItemLeadTimeAsync(itemId, isManufacture, qty, item?.LeadTimeDays ?? 0, isManufacture ? null : supplierId);

        if (resolvedBomId is not { } id || !visitedBoms.Add(id))
            return leadTime;

        var bom = await _bomRepository.FindAsync(id);
        if (bom is null)
            return leadTime;

        var bomBaseQty = bom.Quantity > 0 ? bom.Quantity : 1m;
        var maxComponentTime = 0;
        foreach (var bomItem in bom.Items)
        {
            var componentQty = (bomItem.Quantity / bomBaseQty) * (qty > 0 ? qty : 1m);
            var componentTime = await GetCumulativeLeadTimeRecursiveAsync(bomItem.ItemId, bomItem.SubBomId, componentQty, visitedBoms);
            if (componentTime > maxComponentTime)
            {
                maxComponentTime = componentTime;
            }
        }

        return leadTime + maxComponentTime;
    }

    private async Task<int> ResolveItemLeadTimeAsync(Guid itemId, bool isManufacture, decimal qty, int fallbackDays, Guid? supplierId = null)
    {
        var leadTimeRepo = _itemLeadTimeRepository ?? LazyServiceProvider?.LazyGetService<IRepository<ItemLeadTime, Guid>>();
        if (leadTimeRepo != null)
        {
            var itemLeadTime = await FindItemLeadTimeAsync(leadTimeRepo, itemId, includeSuppliers: !isManufacture);
            if (itemLeadTime != null)
            {
                return isManufacture
                    ? itemLeadTime.CalculateLeadTimeDays(qty, isManufacture: true)
                    : itemLeadTime.ResolvePurchaseLeadTimeDays(supplierId);
            }
        }

        return fallbackDays;
    }

    private static async Task<ItemLeadTime?> FindItemLeadTimeAsync(
        IRepository<ItemLeadTime, Guid> leadTimeRepo,
        Guid itemId,
        bool includeSuppliers)
    {
        if (includeSuppliers)
        {
            var withDetails = await leadTimeRepo.WithDetailsAsync(lt => lt.Suppliers);
            var matched = withDetails?.FirstOrDefault(lt => lt.ItemId == itemId);
            if (matched != null)
            {
                return matched;
            }
        }

        var queryable = await leadTimeRepo.GetQueryableAsync();
        var itemLeadTime = queryable?.FirstOrDefault(lt => lt.ItemId == itemId);
        if (itemLeadTime != null)
        {
            return itemLeadTime;
        }

        if (!includeSuppliers)
        {
            var withDetails = await leadTimeRepo.WithDetailsAsync(lt => lt.Suppliers);
            return withDetails?.FirstOrDefault(lt => lt.ItemId == itemId);
        }

        return null;
    }

    /// <summary>
    /// Filters Master Production Schedule demand sources (<c>Sales Orders</c> and <c>Material Requests</c>) by the
    /// schedule's <paramref name="mpsCompanyId"/>, excludes closed or stopped Sales Orders and closed Sales Order rows,
    /// and returns an explicit empty list (so the caller clears the target table even when the fetch returns nothing)
    /// per ERPNext commits f702edfa24, fa79a5ef6d, 49aa85356a, and 33988eeeee (<c>master_production_schedule.py</c>).
    /// </summary>
    public static List<Guid> FilterMpsDemandSourcesByCompanyAndClearOnEmptyFetch(
        Guid mpsCompanyId,
        IEnumerable<(Guid SourceDocumentId, Guid CompanyId, string DocumentStatus, bool HasAnyOpenPendingRow)>? candidateDemandSources)
    {
        if (candidateDemandSources == null)
        {
            return new List<Guid>();
        }

        return candidateDemandSources
            .Where(s => s.CompanyId == mpsCompanyId
                && s.HasAnyOpenPendingRow
                && !string.Equals(s.DocumentStatus, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.DocumentStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.DocumentStatus, "Stopped", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.DocumentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.SourceDocumentId)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Resolves supplier-specific purchase lead times for MRP by joining <c>ItemLeadTimeSupplier</c> rows through
    /// the parent <c>ItemLeadTime.ItemCode</c> (or <c>ItemId</c>) rather than the parent document name so supplier
    /// lead times continue to resolve after an item rename per ERPNext commit f2ca231b01 (<c>mrp_controller.py</c>).
    /// </summary>
    public static Dictionary<(string ItemCode, Guid SupplierId), int> ResolveSupplierLeadTimesByItemCode(
        IEnumerable<(Guid ItemLeadTimeId, string CurrentItemCode)>? itemLeadTimes,
        IEnumerable<(Guid ParentItemLeadTimeId, Guid SupplierId, int PurchaseTimeDays)>? supplierRows)
    {
        var result = new Dictionary<(string ItemCode, Guid SupplierId), int>();
        if (itemLeadTimes == null || supplierRows == null)
        {
            return result;
        }

        var itemCodeByLeadTimeId = itemLeadTimes
            .Where(lt => lt.ItemLeadTimeId != Guid.Empty && !string.IsNullOrWhiteSpace(lt.CurrentItemCode))
            .ToDictionary(lt => lt.ItemLeadTimeId, lt => lt.CurrentItemCode);

        foreach (var row in supplierRows)
        {
            if (itemCodeByLeadTimeId.TryGetValue(row.ParentItemLeadTimeId, out var currentItemCode))
            {
                result[(currentItemCode, row.SupplierId)] = row.PurchaseTimeDays;
            }
        }

        return result;
    }

    /// <summary>
    /// Bulk-resolves MRP report item details (including Item Group fallback for <c>DefaultSupplierId</c>),
    /// manufacture/purchase lead times, release dates, and bucket capacities from preloaded dictionaries
    /// per ERPNext commits e17b4540ee and d305faeddb (<c>material_requirements_planning_report.py</c>).
    /// </summary>
    public static IReadOnlyList<(string ItemCode, Guid? DefaultWarehouseId, Guid? DefaultSupplierId, int LeadTimeDays, DateTime ReleaseDate, int Capacity)>
        ResolveBulkMrpRowsWithPreloadedLeadTimes(
            IEnumerable<(string ItemCode, string? ItemGroup, string TypeOfMaterial, decimal RequiredQty, DateTime DeliveryDate, bool HasRawMaterials)>? rows,
            IReadOnlyDictionary<(string ParentType, string ParentName), (Guid? DefaultWarehouseId, Guid? DefaultSupplierId)>? itemAndGroupDefaults,
            IReadOnlyDictionary<string, (int CapacityPerDay, int ManufacturingTimeInMins, int PurchaseTimeDays, int BufferTimeDays)>? preloadedLeadTimes,
            IReadOnlyDictionary<(string ItemCode, Guid SupplierId), int>? supplierPurchaseLeadDays = null,
            string bucketSize = "Daily")
    {
        if (rows == null)
        {
            return Array.Empty<(string, Guid?, Guid?, int, DateTime, int)>();
        }

        var bucketDays = string.Equals(bucketSize, "Daily", StringComparison.OrdinalIgnoreCase)
            ? 1
            : string.Equals(bucketSize, "Weekly", StringComparison.OrdinalIgnoreCase)
                ? 7
                : 30;

        var results = new List<(string ItemCode, Guid? DefaultWarehouseId, Guid? DefaultSupplierId, int LeadTimeDays, DateTime ReleaseDate, int Capacity)>();

        foreach (var row in rows)
        {
            Guid? defaultWarehouseId = null;
            Guid? defaultSupplierId = null;

            if (itemAndGroupDefaults != null)
            {
                if (itemAndGroupDefaults.TryGetValue(("Item", row.ItemCode), out var itemDef))
                {
                    defaultWarehouseId = itemDef.DefaultWarehouseId;
                    defaultSupplierId = itemDef.DefaultSupplierId;
                }

                if (!defaultSupplierId.HasValue
                    && !string.IsNullOrWhiteSpace(row.ItemGroup)
                    && itemAndGroupDefaults.TryGetValue(("Item Group", row.ItemGroup), out var groupDef))
                {
                    defaultSupplierId = groupDef.DefaultSupplierId;
                }
            }

            (int CapacityPerDay, int ManufacturingTimeInMins, int PurchaseTimeDays, int BufferTimeDays) lt = default;
            var hasLeadTime = preloadedLeadTimes != null && preloadedLeadTimes.TryGetValue(row.ItemCode, out lt);
            var capacity = 0;
            if (row.HasRawMaterials && hasLeadTime && lt.CapacityPerDay > 0)
            {
                capacity = lt.CapacityPerDay * bucketDays;
            }

            int leadTimeDays;
            if (string.Equals(row.TypeOfMaterial, "Manufacture", StringComparison.OrdinalIgnoreCase))
            {
                if (!hasLeadTime || row.RequiredQty <= 0m)
                {
                    leadTimeDays = 0;
                }
                else
                {
                    var mfgDays = (Math.Max(lt.ManufacturingTimeInMins, 0) * (double)row.RequiredQty / 1440.0) + lt.BufferTimeDays;
                    leadTimeDays = (int)Math.Ceiling(Math.Round((decimal)mfgDays, 4));
                }
            }
            else
            {
                if (defaultSupplierId.HasValue
                    && supplierPurchaseLeadDays != null
                    && supplierPurchaseLeadDays.TryGetValue((row.ItemCode, defaultSupplierId.Value), out var suppDays))
                {
                    leadTimeDays = suppDays + (hasLeadTime ? lt.BufferTimeDays : 0);
                }
                else
                {
                    leadTimeDays = hasLeadTime ? (lt.PurchaseTimeDays + lt.BufferTimeDays) : 0;
                }
            }

            var releaseDate = row.DeliveryDate.AddDays(-leadTimeDays);
            results.Add((row.ItemCode, defaultWarehouseId, defaultSupplierId, leadTimeDays, releaseDate, capacity));
        }

        return results;
    }

    /// <summary>
    /// Lets MPS demand across each forecast period <c>[PeriodStart, PeriodEnd)</c> consume that period's
    /// sales forecast (even on days outside the report window), ordering periods that end first so overlapping
    /// periods consume as much demand as possible, and dropping fully consumed forecast rows.
    /// Per ERPNext PR #67498 (<c>consume_forecast</c> in <c>material_requirements_planning_report.py</c>).
    /// </summary>
    public static List<(string ItemCode, DateTime PeriodStart, DateTime PeriodEnd, decimal RemainingForecastQty)>
        ConsumeForecastFromMpsDemand(
            IEnumerable<(string ItemCode, DateTime PeriodStart, DateTime PeriodEnd, decimal ForecastQty)>? forecastRows,
            IEnumerable<(string ItemCode, DateTime DeliveryDate, decimal PlannedQty)>? mpsDemandRows,
            int precision = 4)
    {
        if (forecastRows == null)
        {
            return new List<(string, DateTime, DateTime, decimal)>();
        }

        var demandByItem = new Dictionary<string, List<(DateTime Date, decimal Qty)>>(StringComparer.OrdinalIgnoreCase);
        if (mpsDemandRows != null)
        {
            foreach (var row in mpsDemandRows)
            {
                if (!demandByItem.TryGetValue(row.ItemCode, out var list))
                {
                    list = new List<(DateTime, decimal)>();
                    demandByItem[row.ItemCode] = list;
                }
                list.Add((row.DeliveryDate.Date, row.PlannedQty));
            }
        }

        var orderedForecasts = forecastRows
            .Select((r, idx) => (Row: r, Index: idx))
            .OrderBy(x => x.Row.PeriodEnd)
            .ThenBy(x => x.Row.PeriodStart)
            .ThenBy(x => x.Index)
            .ToList();

        var remaining = new List<(string ItemCode, DateTime PeriodStart, DateTime PeriodEnd, decimal RemainingForecastQty)>();
        foreach (var entry in orderedForecasts)
        {
            var r = entry.Row;
            var remForecast = r.ForecastQty;
            if (demandByItem.TryGetValue(r.ItemCode, out var demands))
            {
                for (var i = 0; i < demands.Count && remForecast > 0m; i++)
                {
                    var d = demands[i];
                    if (d.Date >= r.PeriodStart.Date && d.Date < r.PeriodEnd.Date && d.Qty > 0m)
                    {
                        var consumed = Math.Min(remForecast, d.Qty);
                        remForecast -= consumed;
                        demands[i] = (d.Date, d.Qty - consumed);
                    }
                }
            }

            var rounded = Math.Round(remForecast, precision, MidpointRounding.AwayFromZero);
            if (rounded > 0m)
            {
                remaining.Add((r.ItemCode, r.PeriodStart, r.PeriodEnd, rounded));
            }
        }

        return remaining;
    }

    /// <summary>
    /// Combines MPS planned demand with remaining unconsumed forecast (and optional ad-hoc qty)
    /// per ERPNext PR #67498: uses addition (<c>planned + remainingForecast</c>) rather than <c>max</c>
    /// because overlapping forecast has already been consumed by <see cref="ConsumeForecastFromMpsDemand"/>.
    /// </summary>
    public static decimal CalculateCombinedMrpDemand(decimal mpsPlannedQty, decimal remainingForecastQty, decimal adhocQty = 0m)
        => mpsPlannedQty + remainingForecastQty + adhocQty;

    /// <summary>
    /// Resolves the half-open forecast period <c>[StartDate, EndDate)</c> starting from the forecast row's own
    /// <paramref name="deliveryDate"/> (so monthly forecasts spread from their own date rather than snapping to the 1st of the month).
    /// Per ERPNext PR #67489 (<c>get_forecast_period</c>).
    /// </summary>
    public static (DateTime StartDate, DateTime EndDate) GetForecastPeriod(DateTime deliveryDate, string? frequency)
    {
        var start = deliveryDate.Date;
        if (string.Equals(frequency, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            return (start, start.AddMonths(1));
        }

        if (string.Equals(frequency, "Weekly", StringComparison.OrdinalIgnoreCase))
        {
            return (start, start.AddDays(7));
        }

        return (start, start.AddDays(1));
    }

    /// <summary>
    /// Spreads forecast rows evenly across daily buckets in each row's own <see cref="GetForecastPeriod"/>
    /// and filters the resulting daily buckets to <c>[fromDate, toDate]</c> when supplied.
    /// Per ERPNext PR #67489 (<c>convert_to_daily_bucket_data</c>).
    /// </summary>
    public static List<(string ItemCode, DateTime DeliveryDate, decimal DailyQty)> SpreadForecastToDailyBuckets(
        IEnumerable<(string ItemCode, DateTime DeliveryDate, string? Frequency, decimal Qty)>? forecastRows,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var buckets = new List<(string ItemCode, DateTime DeliveryDate, decimal DailyQty)>();
        if (forecastRows == null)
        {
            return buckets;
        }

        foreach (var row in forecastRows)
        {
            var (start, end) = GetForecastPeriod(row.DeliveryDate, row.Frequency);
            var days = Math.Max(1, (int)(end - start).TotalDays);
            var dailyQty = row.Qty / days;

            for (var i = 0; i < days; i++)
            {
                var date = start.AddDays(i);
                if (fromDate.HasValue && date < fromDate.Value.Date)
                    continue;
                if (toDate.HasValue && date > toDate.Value.Date)
                    continue;

                buckets.Add((row.ItemCode, date, dailyQty));
            }
        }

        return buckets;
    }

    /// <summary>
    /// Computes cumulative lead time (in days) for an item in a Master Production Schedule:
    /// - Purchased items (<paramref name="hasBom"/> = false) use <paramref name="purchaseLeadDays"/>
    ///   (resolved from supplier lead times / item lead time days) directly.
    /// - Manufactured items (<paramref name="hasBom"/> = true) add <paramref name="manufacturingLeadDays"/>
    ///   plus <paramref name="bufferTimeDays"/> to the longest child component cumulative lead time.
    /// Per ERPNext commits <c>3d452749fa</c> and <c>4fe3d19743</c> (<c>master_production_schedule.py</c>).
    /// </summary>
    public static decimal CalculateMpsCumulativeLeadTimeDays(
        bool hasBom,
        decimal purchaseLeadDays,
        decimal manufacturingLeadDays,
        decimal bufferTimeDays,
        IEnumerable<decimal>? componentCumulativeLeadDays = null)
    {
        if (!hasBom)
        {
            return purchaseLeadDays > 0m ? purchaseLeadDays : 0m;
        }

        var ownTime = manufacturingLeadDays + bufferTimeDays;
        var longestComponent = componentCumulativeLeadDays != null && componentCumulativeLeadDays.Any()
            ? componentCumulativeLeadDays.Max()
            : 0m;

        return ownTime + longestComponent;
    }

    /// <summary>
    /// Consumes MRP forecast rows against Master Production Schedule demand across each forecast's
    /// whole <c>[PeriodStart, PeriodEnd)</c> window per ERPNext PR #67498
    /// (<c>MaterialRequirementsPlanningReport.consume_forecast</c>).
    /// Sorts forecast periods by <c>(PeriodEnd, PeriodStart)</c> ascending so overlapping periods
    /// ending earlier consume matching MPS demand deterministically regardless of input row order.
    /// Returns remaining unconsumed forecast quantities in the original input order.
    /// </summary>
    public static decimal[] ConsumeForecastAgainstMpsDemand(
        IReadOnlyList<(Guid ItemId, DateTime PeriodStart, DateTime PeriodEnd, decimal ForecastQty)> forecasts,
        IReadOnlyList<(Guid ItemId, DateTime DemandDate, decimal PlannedQty)> mpsDemands)
    {
        if (forecasts == null || forecasts.Count == 0)
            return Array.Empty<decimal>();

        var remainingForecasts = forecasts.Select(f => f.ForecastQty).ToArray();
        if (mpsDemands == null || mpsDemands.Count == 0)
            return remainingForecasts;

        // Track remaining consumable demand per item sorted by demand date
        var demandByItem = mpsDemands
            .GroupBy(d => d.ItemId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => new DemandBucket(d.DemandDate.Date, d.PlannedQty)).ToList());

        var orderedIndices = Enumerable.Range(0, forecasts.Count)
            .OrderBy(i => forecasts[i].PeriodEnd.Date)
            .ThenBy(i => forecasts[i].PeriodStart.Date)
            .ToList();

        foreach (var idx in orderedIndices)
        {
            var f = forecasts[idx];
            if (!demandByItem.TryGetValue(f.ItemId, out var itemDemands))
                continue;

            var start = f.PeriodStart.Date;
            var end = f.PeriodEnd.Date;

            foreach (var demand in itemDemands)
            {
                if (remainingForecasts[idx] <= 0m)
                    break;

                if (demand.RemainingQty <= 0m)
                    continue;

                if (demand.Date >= start && demand.Date < end)
                {
                    var consumed = Math.Min(remainingForecasts[idx], demand.RemainingQty);
                    remainingForecasts[idx] -= consumed;
                    demand.RemainingQty -= consumed;
                }
            }
        }

        return remainingForecasts;
    }

    /// <summary>
    /// Computes the half-open period window <c>[StartDate, ExclusiveEndDate)</c> for a Sales Forecast row
    /// starting from its own <paramref name="deliveryDate"/> per ERPNext PR #67489
    /// (<c>sales_forecast.py: get_forecast_period</c>):
    /// 1 calendar month for <c>"Monthly"</c>, 7 days otherwise.
    /// </summary>
    public static (DateTime StartDate, DateTime ExclusiveEndDate) GetForecastPeriodWindow(
        DateTime deliveryDate,
        string? frequency)
    {
        var start = deliveryDate.Date;
        var exclusiveEnd = string.Equals(frequency, "Monthly", StringComparison.OrdinalIgnoreCase)
            ? start.AddMonths(1)
            : start.AddDays(7);
        return (start, exclusiveEnd);
    }

    /// <summary>
    /// Computes the inclusive MPS <c>ToDate</c> that covers the full last forecast period
    /// per ERPNext PR #67489 (<c>master_production_schedule.py: set_to_date</c>):
    /// <c>ExclusiveEndDate.AddDays(-1)</c>.
    /// </summary>
    public static DateTime CalculateMpsToDateCoveringLastForecast(
        DateTime lastForecastDeliveryDate,
        string? frequency)
    {
        var (_, exclusiveEnd) = GetForecastPeriodWindow(lastForecastDeliveryDate, frequency);
        return exclusiveEnd.AddDays(-1);
    }

    private sealed class DemandBucket
    {
        public DateTime Date { get; }
        public decimal RemainingQty { get; set; }

        public DemandBucket(DateTime date, decimal qty)
        {
            Date = date;
            RemainingQty = qty;
        }
    }
}
