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
}
