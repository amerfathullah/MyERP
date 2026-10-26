using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for Pick List business rules.
/// Handles warehouse allocation, batch/serial selection, and partial transfer tracking.
/// Per DO-NOT: "Skip double-pick prevention in pick list allocation"
/// Per DO-NOT: "Block creation of a second Stock Entry from a Pick List (partial transfers supported)"
/// </summary>
public class PickListManager : DomainService
{
    private readonly IRepository<PickList, Guid> _pickListRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<StockReservationEntry, Guid>? _sreRepository;
    private readonly IRepository<Item, Guid>? _itemRepository;
    private readonly IRepository<Warehouse, Guid>? _warehouseRepository;

    public PickListManager(
        IRepository<PickList, Guid> pickListRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<StockReservationEntry, Guid>? sreRepository = null,
        IRepository<Item, Guid>? itemRepository = null,
        IRepository<Warehouse, Guid>? warehouseRepository = null)
    {
        _pickListRepository = pickListRepository;
        _binRepository = binRepository;
        _sreRepository = sreRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// Allocates stock to pick list items from warehouse bins.
    /// Uses priority: specific warehouse first, then checks availability.
    /// Deducts already-picked quantities from other active pick lists.
    /// </summary>
    public async Task<PickAllocationResult> AllocateStockAsync(PickList pickList)
    {
        var result = new PickAllocationResult();

        // Per ERPNext PR #59170: respect manual picking, do not auto-reallocate locations
        if (pickList.PickManually)
        {
            return result;
        }

        var ownVoucherId = pickList.SalesOrderId ?? pickList.WorkOrderId;
        foreach (var item in pickList.Items)
        {
            var requestedQty = Math.Round(item.Qty, 4);
            var availableQty = Math.Round(await GetAvailableQtyForPickAsync(
                item.ItemId, item.WarehouseId, pickList.Id, ownVoucherId), 4);

            var allocatedQty = Math.Min(requestedQty, availableQty);
            result.Allocations.Add(new PickAllocation
            {
                ItemId = item.ItemId,
                WarehouseId = item.WarehouseId,
                RequestedQty = requestedQty,
                AllocatedQty = allocatedQty,
                ShortageQty = Math.Round(requestedQty - allocatedQty, 4)
            });

            if (allocatedQty < requestedQty)
                result.HasShortage = true;
        }

        return result;
    }

    /// <summary>
    /// Gets available qty for picking, deducting quantities from other active pick lists and stock reservations.
    /// Per ERPNext PR #62256 / PR #62249 (commits cece6b913d, 6872af9162, ee41d1bfa4).
    /// </summary>
    public async Task<decimal> GetAvailableQtyForPickAsync(
        Guid itemId, Guid warehouseId, Guid excludePickListId, Guid? ownVoucherId = null)
    {
        // Get bin balance
        var binQueryable = await _binRepository.GetQueryableAsync();
        var bin = binQueryable.FirstOrDefault(b =>
            b.ItemId == itemId && b.WarehouseId == warehouseId);

        var actualQty = Math.Round(bin?.ActualQty ?? 0, 4);

        // Deduct quantities already picked by other active pick lists
        var pickQueryable = await _pickListRepository.GetQueryableAsync();
        var otherPickLists = pickQueryable
            .Where(pl => pl.Id != excludePickListId
                && (pl.Status == Core.DocumentStatus.Submitted || pl.Status == Core.DocumentStatus.Draft))
            .ToList();

        var alreadyPicked = Math.Round(otherPickLists
            .SelectMany(pl => pl.Items)
            .Where(pi => pi.ItemId == itemId && pi.WarehouseId == warehouseId)
            .Sum(pi => Math.Max(0m, pi.Qty - Math.Max(pi.TransferredQty, pi.DeliveredQty))), 4);

        decimal reservedQty = 0;
        if (_sreRepository != null)
        {
            var sreQuery = await _sreRepository.GetQueryableAsync();
            var sreList = sreQuery
                .Where(s => s.ItemId == itemId && s.WarehouseId == warehouseId
                    && s.Status == Core.DocumentStatus.Submitted
                    && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
                .ToList();

            if (ownVoucherId.HasValue)
            {
                sreList = sreList.Where(s => s.VoucherId != ownVoucherId.Value).ToList();
            }

            var woPickHolds = otherPickLists
                .Where(pl => pl.WorkOrderId.HasValue)
                .SelectMany(pl => pl.Items.Where(pi => pi.ItemId == itemId && pi.WarehouseId == warehouseId)
                    .Select(pi => new { pl.WorkOrderId, pi.WarehouseId, Qty = Math.Max(0m, pi.Qty - Math.Max(pi.TransferredQty, pi.DeliveredQty)) }))
                .GroupBy(x => x.WorkOrderId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

            foreach (var sre in sreList)
            {
                var rem = Math.Round(sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty, 4);
                if ((sre.VoucherType == "Work Order" || sre.VoucherType == "WorkOrder") &&
                    woPickHolds.TryGetValue(sre.VoucherId, out var heldQty) && heldQty > 0)
                {
                    var released = Math.Min(rem, heldQty);
                    rem = Math.Round(rem - released, 4);
                    woPickHolds[sre.VoucherId] = Math.Round(heldQty - released, 4);
                }
                reservedQty = Math.Round(reservedQty + rem, 4);
            }
        }

        return Math.Round(Math.Max(0, actualQty - alreadyPicked - reservedQty), 4);
    }

    /// <summary>
    /// Calculates pending transfer quantities for a pick list.
    /// Returns items that still have qty to transfer (picked_qty - transferred_qty > 0).
    /// </summary>
    public IReadOnlyList<PendingTransfer> GetPendingTransfers(PickList pickList)
    {
        return pickList.Items
            .Where(i => i.PendingQty > 0)
            .Select(i => new PendingTransfer
            {
                PickListItemId = i.Id,
                ItemId = i.ItemId,
                WarehouseId = i.WarehouseId,
                PendingQty = i.PendingQty,
                BatchId = i.BatchId,
                SourceDocumentItemId = i.SourceDocumentItemId
            })
            .ToList();
    }

    /// <summary>
    /// Validates that a stock entry can be created from the pick list.
    /// Per DO-NOT: partial transfers ARE supported (not blocked).
    /// Returns false if nothing pending.
    /// </summary>
    public bool HasPendingTransfers(PickList pickList)
    {
        return pickList.Items.Any(i => i.PendingQty > 0);
    }

    /// <summary>
    /// Updates delivered quantities on pick list items matching delivered transaction items / packed components.
    /// Per ERPNext commit 696f0df811: updates pick list status and delivered_qty for product bundles / standard items.
    /// </summary>
    public void UpdateDeliveredQuantities(
        PickList pickList,
        IEnumerable<DeliveredComponentItem> deliveredItems)
    {
        var deliveredMap = deliveredItems
            .GroupBy(d => new { d.ItemId, d.WarehouseId, d.SourceDocumentItemId, d.BatchId })
            .ToDictionary(g => g.Key, g => g.Sum(d => d.DeliveredQty));

        foreach (var item in pickList.Items)
        {
            var key = new
            {
                item.ItemId,
                item.WarehouseId,
                SourceDocumentItemId = item.SourceDocumentItemId,
                item.BatchId
            };

            if (deliveredMap.TryGetValue(key, out var deliveredQty))
            {
                var qtyToApply = Math.Min(item.Qty, deliveredQty);
                item.DeliveredQty = qtyToApply;
            }
        }
    }

    /// <summary>
    /// Gets detailed stock availability for items in warehouses, considering active pick lists and stock reservations.
    /// Per ERPNext PR #58243 (commit bf1ec51680):
    /// free_qty = actual_qty - picked_qty - reserved_qty.
    /// </summary>
    public async Task<PickStockAvailability> GetStockAvailabilityAsync(
        Guid itemId, Guid warehouseId, Guid? excludePickListId = null,
        IRepository<StockReservationEntry, Guid>? sreRepository = null)
    {
        var binQueryable = await _binRepository.GetQueryableAsync();
        var bin = binQueryable.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);
        var actualQty = Math.Round(bin?.ActualQty ?? 0, 4);

        var pickQueryable = await _pickListRepository.GetQueryableAsync();
        var pickListQuery = pickQueryable
            .Where(pl => pl.Status == Core.DocumentStatus.Submitted || pl.Status == Core.DocumentStatus.Draft);

        if (excludePickListId.HasValue)
        {
            pickListQuery = pickListQuery.Where(pl => pl.Id != excludePickListId.Value);
        }

        var pickedQty = Math.Round(pickListQuery
            .SelectMany(pl => pl.Items)
            .Where(pi => pi.ItemId == itemId && pi.WarehouseId == warehouseId)
            .Sum(pi => pi.Qty - pi.TransferredQty), 4);

        decimal reservedQty = 0;
        if (sreRepository != null)
        {
            var sreQuery = await sreRepository.GetQueryableAsync();
            reservedQty = Math.Round(sreQuery
                .Where(s => s.ItemId == itemId && s.WarehouseId == warehouseId
                    && s.Status == Core.DocumentStatus.Submitted
                    && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
                .Sum(s => s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty), 4);
        }

        var freeQty = Math.Round(Math.Max(0m, actualQty - pickedQty - reservedQty), 4);

        return new PickStockAvailability(
            ItemId: itemId,
            WarehouseId: warehouseId,
            ActualQty: actualQty,
            PickedQty: pickedQty,
            ReservedQty: reservedQty,
            FreeQty: freeQty
        );
    }

    /// <summary>
    /// Gets detailed stock availability and holding pick lists for all items in the pick list.
    /// Maps to ERPNext get_stock_availability / get_pick_list_holders logic.
    /// </summary>
    public async Task<List<StockAvailabilityInsightResult>> GetStockAvailabilityInsightAsync(
        PickList pickList,
        IRepository<StockReservationEntry, Guid>? sreRepository = null)
    {
        var activeSreRepo = sreRepository ?? _sreRepository;
        var results = new List<StockAvailabilityInsightResult>();

        var distinctPairs = pickList.Items
            .GroupBy(i => new { i.ItemId, i.WarehouseId })
            .Select(g => new { g.Key.ItemId, g.Key.WarehouseId, ItemName = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.ItemName))?.ItemName })
            .ToList();

        var binQueryable = await _binRepository.GetQueryableAsync();
        var pickQueryable = (await _pickListRepository.WithDetailsAsync()).AsQueryable();

        var otherPickLists = pickQueryable
            .Where(pl => pl.Id != pickList.Id &&
                         (pl.Status == Core.DocumentStatus.Submitted || pl.Status == Core.DocumentStatus.Draft))
            .ToList();

        IQueryable<StockReservationEntry>? sreQuery = null;
        if (activeSreRepo != null)
        {
            sreQuery = await activeSreRepo.GetQueryableAsync();
        }

        foreach (var pair in distinctPairs)
        {
            var bin = binQueryable.FirstOrDefault(b => b.ItemId == pair.ItemId && b.WarehouseId == pair.WarehouseId);
            var actualQty = bin?.ActualQty ?? 0;

            var holders = new List<HoldingPickListInfo>();
            foreach (var otherPl in otherPickLists)
            {
                var matchingItems = otherPl.Items
                    .Where(pi => pi.ItemId == pair.ItemId && pi.WarehouseId == pair.WarehouseId);

                foreach (var pi in matchingItems)
                {
                    var holdingQty = Math.Max(0m, pi.Qty - Math.Max(pi.TransferredQty, pi.DeliveredQty));
                    if (holdingQty > 0)
                    {
                        holders.Add(new HoldingPickListInfo(
                            otherPl.Id,
                            otherPl.PickListNumber ?? otherPl.Id.ToString()[..8],
                            otherPl.Status.ToString(),
                            pi.WarehouseId,
                            holdingQty));
                    }
                }
            }

            var pickedQty = holders.Sum(h => h.HoldingQty);

            decimal reservedQty = 0;
            if (sreQuery != null)
            {
                var reservations = sreQuery
                    .Where(s => s.ItemId == pair.ItemId && s.WarehouseId == pair.WarehouseId
                        && s.Status == Core.DocumentStatus.Submitted
                        && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
                    .ToList();

                // Per ERPNext commit 42b16975c7: stock reserved for this pick list's voucher is ours to pick
                var ownVouchers = new HashSet<Guid>();
                if (pickList.SalesOrderId.HasValue) ownVouchers.Add(pickList.SalesOrderId.Value);
                if (pickList.WorkOrderId.HasValue) ownVouchers.Add(pickList.WorkOrderId.Value);

                var othersReservations = reservations
                    .Where(s => !ownVouchers.Contains(s.VoucherId))
                    .ToList();

                // Deduct any work order pick holds from open pick lists so work order reservations are not counted twice (commit ee41d1bfa4)
                var woPickHolds = otherPickLists
                    .Where(pl => pl.WorkOrderId.HasValue)
                    .SelectMany(pl => pl.Items.Where(pi => pi.ItemId == pair.ItemId && pi.WarehouseId == pair.WarehouseId)
                        .Select(pi => new { pl.WorkOrderId, pi.WarehouseId, Qty = pi.Qty - Math.Max(pi.TransferredQty, pi.DeliveredQty) }))
                    .GroupBy(x => x.WorkOrderId!.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

                foreach (var sre in othersReservations)
                {
                    var rem = Math.Round(sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty, 4);
                    if ((sre.VoucherType == "Work Order" || sre.VoucherType == "WorkOrder") &&
                        woPickHolds.TryGetValue(sre.VoucherId, out var heldQty) && heldQty > 0)
                    {
                        var released = Math.Min(rem, heldQty);
                        rem = Math.Round(rem - released, 4);
                        woPickHolds[sre.VoucherId] -= released;
                    }
                    reservedQty = Math.Round(reservedQty + rem, 4);
                }
            }

            var freeQty = Math.Round(Math.Max(0m, actualQty - pickedQty - reservedQty), 4);

            string? itemName = pair.ItemName;
            if (string.IsNullOrEmpty(itemName) && _itemRepository != null)
            {
                var itemObj = await _itemRepository.FindAsync(pair.ItemId);
                itemName = itemObj?.ItemName;
            }

            string? warehouseName = null;
            if (_warehouseRepository != null)
            {
                var whObj = await _warehouseRepository.FindAsync(pair.WarehouseId);
                warehouseName = whObj?.Name ?? whObj?.WarehouseCode;
            }

            results.Add(new StockAvailabilityInsightResult(
                pair.ItemId,
                itemName,
                pair.WarehouseId,
                warehouseName,
                actualQty,
                pickedQty,
                reservedQty,
                freeQty,
                holders
            ));
        }

        return results;
    }

    /// <summary>
    /// Computes how many full bundles can be created from picked items.
    /// Per ERPNext PR #62149 / commit 05bc9ce0fe:
    /// - Counts picked product bundles against the bundle version recorded on the order row (not active version).
    /// - Component bundle quantities must be expressed in component stock UOM (with conversion factor applied).
    /// </summary>
    public static int ComputePickedQtyForBundle(
        IEnumerable<PickListItem> pickItems,
        Guid salesOrderItemId,
        IReadOnlyDictionary<Guid, decimal> componentRequiredStockQuantities)
    {
        var possibleBundles = new Dictionary<Guid, decimal>();

        foreach (var item in pickItems)
        {
            if (item.SourceDocumentItemId != salesOrderItemId)
                continue;

            if (componentRequiredStockQuantities.TryGetValue(item.ItemId, out var requiredQty) && requiredQty > 0)
            {
                var bundleKey = item.ProductBundleItemId ?? item.Id;
                if (!possibleBundles.ContainsKey(bundleKey))
                {
                    possibleBundles[bundleKey] = 0;
                }
                possibleBundles[bundleKey] += item.Qty / requiredQty;
            }
        }

        if (possibleBundles.Count == 0)
            return 0;

        return (int)Math.Floor(possibleBundles.Values.Min());
    }

    /// <summary>
    /// Aggregates picked batch quantities from bundles, correctly splitting across multiple batches in a bundle.
    /// Per ERPNext PR #60301 (commit ead0229375b): handles multiple batches in pick list bundles without KeyError/loss.
    /// </summary>
    public static void AggregateBundlePickedBatches(
        IDictionary<(Guid WarehouseId, Guid? BatchId), decimal> pickedQtyMap,
        Guid defaultWarehouseId,
        IEnumerable<SerialAndBatchEntry> bundleEntries)
    {
        foreach (var entry in bundleEntries)
        {
            var qty = Math.Abs(entry.Qty);
            var key = (entry.WarehouseId ?? defaultWarehouseId, entry.BatchId);
            if (!pickedQtyMap.ContainsKey(key))
            {
                pickedQtyMap[key] = 0;
            }
            pickedQtyMap[key] += qty;
        }
    }

    /// <summary>
    /// Deducts already picked quantities across warehouse/batch locations (per ERPNext PR #58613).
    /// Properly consumes picked quantity across multiple location rows and preserves serial number ordering.
    /// </summary>
    public static List<PickLocationAllocation> FilterLocationsByPickedMaterials(
        IEnumerable<PickLocationAllocation> locations,
        IDictionary<(Guid WarehouseId, Guid? BatchId), decimal> pickedQtyMap,
        IDictionary<Guid, HashSet<string>>? pickedSerialNosMap = null)
    {
        var filtered = new List<PickLocationAllocation>();

        foreach (var loc in locations)
        {
            var key = (loc.WarehouseId, loc.BatchId);
            if (!pickedQtyMap.TryGetValue(key, out var pickedQty) || pickedQty <= 0)
            {
                filtered.Add(loc);
                continue;
            }

            if (pickedQty > loc.Qty)
            {
                pickedQtyMap[key] -= loc.Qty;
                loc.Qty = 0;
            }
            else
            {
                loc.Qty = Math.Round(loc.Qty - pickedQty, 4);
                pickedQtyMap[key] = 0;

                if (loc.SerialNumbers != null && loc.SerialNumbers.Count > 0 && pickedSerialNosMap != null && pickedSerialNosMap.TryGetValue(loc.WarehouseId, out var pickedSerials))
                {
                    loc.SerialNumbers = loc.SerialNumbers.Where(sn => !pickedSerials.Contains(sn)).ToList();
                }
            }

            if (loc.Qty > 0)
            {
                filtered.Add(loc);
            }
        }

        return filtered;
    }
}

public class PickLocationAllocation
{
    public Guid WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal Qty { get; set; }
    public List<string> SerialNumbers { get; set; } = new();
}

public class PickAllocationResult
{
    public List<PickAllocation> Allocations { get; set; } = new();
    public bool HasShortage { get; set; }
}

public class PickAllocation
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal RequestedQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal ShortageQty { get; set; }
}

public class PendingTransfer
{
    public Guid PickListItemId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal PendingQty { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? SourceDocumentItemId { get; set; }
}

public record DeliveredComponentItem(
    Guid ItemId,
    Guid WarehouseId,
    decimal DeliveredQty,
    Guid? SourceDocumentItemId = null,
    Guid? BatchId = null);

public record HoldingPickListInfo(
    Guid PickListId,
    string? PickListNumber,
    string Status,
    Guid WarehouseId,
    decimal HoldingQty);

public record StockAvailabilityInsightResult(
    Guid ItemId,
    string? ItemName,
    Guid WarehouseId,
    string? WarehouseName,
    decimal ActualQty,
    decimal PickedQty,
    decimal ReservedQty,
    decimal FreeQty,
    IReadOnlyList<HoldingPickListInfo> HoldingPickLists);

public record PickStockAvailability(
    Guid ItemId,
    Guid WarehouseId,
    decimal ActualQty,
    decimal PickedQty,
    decimal ReservedQty,
    decimal FreeQty,
    IReadOnlyList<HoldingPickListInfo>? HoldingPickLists = null);

