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
        // Per ERPNext commits 784d9cbe09, 9f33bb012e, 6bfba876ac, f303513d06:
        // Rejected warehouses must never be picked from in Pick Lists.
        var whRepo = _warehouseRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Warehouse, Guid>>();
        if (whRepo != null)
        {
            var wh = await whRepo.FindAsync(warehouseId);
            if (wh != null && (wh.IsRejectedWarehouse || wh.IsGroup || !wh.IsActive))
            {
                return 0m;
            }
        }

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
            .Sum(pi => Math.Max(0m, pi.Qty - pi.DeliveredQty - pi.TransferredQty)), 4);

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
                    .Select(pi => new { pl.WorkOrderId, pi.WarehouseId, Qty = Math.Max(0m, pi.Qty - pi.DeliveredQty - pi.TransferredQty) }))
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
            .Sum(pi => Math.Max(0m, pi.Qty - pi.DeliveredQty - pi.TransferredQty)), 4);

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
                    var holdingQty = Math.Max(0m, pi.Qty - pi.DeliveredQty - pi.TransferredQty);
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
                        .Select(pi => new { pl.WorkOrderId, pi.WarehouseId, Qty = Math.Max(0m, pi.Qty - pi.DeliveredQty - pi.TransferredQty) }))
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
    /// Per ERPNext PR #62149 / commit 05bc9ce0fe and PR #65051 (commits 740575ad27, 3750ba894b, fe42da0c74):
    /// - Counts picked product bundles against the bundle version recorded on the order row (not active version).
    /// - Component bundle quantities must be expressed in component stock UOM (with conversion factor applied).
    /// - Groups by component ItemId (not ProductBundleItemId) and aggregates picked quantities across repeated rows,
    ///   so bundles with repeated component items are picked and delivered accurately.
    /// </summary>
    public static int ComputePickedQtyForBundle(
        IEnumerable<PickListItem> pickItems,
        Guid salesOrderItemId,
        IReadOnlyDictionary<Guid, decimal> componentRequiredStockQuantities,
        bool pendingOnly = false)
    {
        var possibleBundles = new Dictionary<Guid, decimal>();

        foreach (var item in pickItems)
        {
            if (item.SourceDocumentItemId != salesOrderItemId)
                continue;

            if (componentRequiredStockQuantities.TryGetValue(item.ItemId, out var requiredQty) && requiredQty > 0)
            {
                var effectiveQty = pendingOnly ? Math.Max(0m, item.Qty - item.DeliveredQty) : item.Qty;
                // PR #65051: Group by ItemId (component item), not ProductBundleItemId,
                // so multiple packed rows with the same component accumulate their picked qty towards the item total.
                var bundleKey = item.ItemId;
                if (!possibleBundles.ContainsKey(bundleKey))
                {
                    possibleBundles[bundleKey] = 0;
                }
                possibleBundles[bundleKey] += effectiveQty / requiredQty;
            }
        }

        if (possibleBundles.Count == 0 || possibleBundles.Count < componentRequiredStockQuantities.Count)
            return 0;

        return (int)Math.Floor(Math.Round(possibleBundles.Values.Min(), 6));
    }

    /// <summary>
    /// Resolves UOM, conversion factor, and stock quantity for a Pick List item row.
    /// Per ERPNext commits d0796ff33b and 0de4680ea9:
    /// - Defaults row UOM to the item's stock UOM.
    /// - If row UOM == stock UOM, conversion factor = 1.
    /// - Else if the row is NOT a product bundle component (!productBundleItemId.HasValue) and references an ordered row
    ///   (Sales Order Item or Material Request Item) whose (SourceDocumentItemId, ItemId, Uom) matches,
    ///   preserves the ordered row's conversion factor even if the item master UOM conversion changed.
    /// - Else uses the item's current UOM conversion factor (or fallback 1).
    /// - Computes StockQty = Round(Qty * ConversionFactor, 4).
    /// </summary>
    public static (string Uom, decimal ConversionFactor, decimal StockQty) ResolveRowUomAndStockQty(
        Guid itemId,
        string stockUom,
        string? rowUom,
        decimal qty,
        Guid? sourceDocumentItemId,
        Guid? productBundleItemId,
        IReadOnlyDictionary<(Guid RowId, Guid ItemId, string Uom), decimal>? orderedConversionFactors,
        decimal itemMasterConversionFactor = 1m,
        decimal? savedConversionFactor = null)
    {
        var effectiveUom = !string.IsNullOrWhiteSpace(rowUom) ? rowUom.Trim() : (stockUom ?? "Unit");
        decimal factor;

        if (string.Equals(effectiveUom, stockUom, StringComparison.OrdinalIgnoreCase))
        {
            factor = 1m;
        }
        else if (!productBundleItemId.HasValue
            && sourceDocumentItemId.HasValue
            && orderedConversionFactors != null
            && orderedConversionFactors.TryGetValue((sourceDocumentItemId.Value, itemId, effectiveUom.ToUpperInvariant()), out var orderedFactor)
            && orderedFactor > 0)
        {
            factor = orderedFactor;
        }
        else if (savedConversionFactor.HasValue && savedConversionFactor.Value > 0)
        {
            // Per ERPNext PR #64938 / commit 4305cb27f5: keep saved conversion factor when loading/updating draft pick list
            factor = savedConversionFactor.Value;
        }
        else
        {
            factor = itemMasterConversionFactor > 0 ? itemMasterConversionFactor : 1m;
        }

        var stockQty = Math.Round(qty * factor, 4);
        return (effectiveUom, factor, stockQty);
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

    /// <summary>
    /// Allocates locations and serial numbers for a pick list item row.
    /// Per ERPNext PR #65146 (commits 9d105dfa3b, cf4ff918e3):
    /// When a location has more quantity than requested for the row, the remaining serial numbers
    /// on the location are sliced starting from stock_qty offset (next serial numbers), NOT from the tail
    /// (newest serial numbers). This ensures later pick list rows receive the next serial numbers in FIFO order.
    /// </summary>
    public static List<AllocatedLocationItem> AllocateItemLocations(
        decimal itemQty,
        decimal itemStockQty,
        decimal conversionFactor,
        List<PickLocationAllocation> availableLocations,
        bool uomMustBeWholeNumber = false,
        int precision = 4)
    {
        var locations = new List<AllocatedLocationItem>();
        var remainingStockQty = itemStockQty > 0 ? itemStockQty : itemQty * conversionFactor;
        var factor = conversionFactor > 0 ? conversionFactor : 1m;

        while (Math.Round(remainingStockQty, precision) > 0 && availableLocations.Count > 0)
        {
            var itemLoc = availableLocations[0];
            availableLocations.RemoveAt(0);

            var stockQty = itemLoc.Qty >= remainingStockQty ? remainingStockQty : itemLoc.Qty;
            var qty = stockQty / factor;

            if (uomMustBeWholeNumber)
            {
                qty = Math.Floor(qty);
                stockQty = qty * factor;
                if (stockQty <= 0)
                {
                    continue;
                }
            }

            var allocatedSerials = new List<string>();
            if (itemLoc.SerialNumbers != null && itemLoc.SerialNumbers.Count > 0)
            {
                var takeCount = (int)Math.Min((long)itemLoc.SerialNumbers.Count, (long)Math.Ceiling(stockQty));
                allocatedSerials = itemLoc.SerialNumbers.Take(takeCount).ToList();
            }

            locations.Add(new AllocatedLocationItem
            {
                Qty = qty,
                StockQty = stockQty,
                WarehouseId = itemLoc.WarehouseId,
                BatchId = itemLoc.BatchId,
                SerialNumbers = allocatedSerials,
            });

            remainingStockQty -= stockQty;
            var qtyDiff = itemLoc.Qty - stockQty;

            if (qtyDiff > 0)
            {
                itemLoc.Qty = qtyDiff;
                if (itemLoc.SerialNumbers != null && itemLoc.SerialNumbers.Count > 0)
                {
                    // PR #65146 / commit 9d105dfa3b: give later rows the NEXT serial numbers, not the newest
                    itemLoc.SerialNumbers = itemLoc.SerialNumbers.Skip((int)stockQty).ToList();
                }
                availableLocations.Insert(0, itemLoc);
            }
        }

        return locations;
    }

    private static readonly HashSet<string> WorkOrderAlternativeItemPurposes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Material Transfer for Manufacture",
        "Manufacture",
        "Material Consumption for Manufacture",
        "MaterialTransferForManufacture",
        "MaterialConsumptionForManufacture"
    };

    /// <summary>
    /// Validates Delivery Note, Sales Invoice, or Stock Entry rows against their referenced Pick List rows
    /// per ERPNext commits ca335b6613, f303513d06, 6bfba876ac, 628480dcb1, 9f33bb012e, and e5ff908919:
    /// - Refuses any row whose <c>ItemId</c> differs from the picked row's <c>ItemId</c>, except on a Stock Entry
    ///   with a <paramref name="workOrderId"/> whose purpose validates alternative items and where
    ///   <c>OriginalItemId == PickedItemId</c>.
    /// - On a Stock Entry (<paramref name="isStockEntry"/> == true), validates <c>SourceWarehouseId == PickedWarehouseId</c>
    ///   and rounds cumulative <c>PickedTransferredQty + RowTransferQty</c> to <paramref name="transferredQtyPrecision"/>
    ///   before comparing against <c>PickedQty</c>.
    /// </summary>
    public static void ValidateRowsAgainstPickListItems(
        string pickListNumber,
        bool isStockEntry,
        Guid? workOrderId,
        string? stockEntryPurpose,
        IReadOnlyDictionary<Guid, (Guid ItemId, string ItemCode, Guid WarehouseId, string WarehouseName, decimal PickedQty, decimal TransferredQty)> pickedRowsById,
        IEnumerable<(int RowNumber, Guid? PickListItemId, Guid ItemId, string ItemCode, Guid? OriginalItemId, Guid? SourceWarehouseId, string? SourceWarehouseName, decimal TransferQty)> documentRows,
        int transferredQtyPrecision = 4)
    {
        if (pickedRowsById == null || documentRows == null)
        {
            return;
        }

        var cumulativeTransferByPickRow = new Dictionary<Guid, decimal>();
        foreach (var row in documentRows)
        {
            if (!row.PickListItemId.HasValue || !pickedRowsById.TryGetValue(row.PickListItemId.Value, out var pickedRow))
            {
                continue;
            }

            var isWorkOrderAlternative = isStockEntry
                && workOrderId.HasValue
                && workOrderId.Value != Guid.Empty
                && !string.IsNullOrWhiteSpace(stockEntryPurpose)
                && WorkOrderAlternativeItemPurposes.Contains(stockEntryPurpose)
                && row.OriginalItemId.HasValue
                && row.OriginalItemId.Value == pickedRow.ItemId;

            if (row.ItemId != pickedRow.ItemId && !isWorkOrderAlternative)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{row.RowNumber}: Item {row.ItemCode} is not the item {pickedRow.ItemCode} picked in {pickListNumber}.");
            }

            if (isStockEntry)
            {
                if (row.SourceWarehouseId.HasValue &&
                    row.SourceWarehouseId.Value != Guid.Empty &&
                    row.SourceWarehouseId.Value != pickedRow.WarehouseId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{row.RowNumber}: Item {row.ItemCode} was picked from {pickedRow.WarehouseName}, not {row.SourceWarehouseName}.");
                }

                var pickRowId = row.PickListItemId.Value;
                cumulativeTransferByPickRow[pickRowId] = cumulativeTransferByPickRow.GetValueOrDefault(pickRowId, 0m) + row.TransferQty;
                var totalTransferred = Math.Round(
                    pickedRow.TransferredQty + cumulativeTransferByPickRow[pickRowId],
                    transferredQtyPrecision,
                    MidpointRounding.AwayFromZero);

                if (totalTransferred > pickedRow.PickedQty)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Row #{row.RowNumber}: Item {row.ItemCode} is transferred more than the {pickedRow.PickedQty} picked in {pickListNumber}.");
                }
            }
        }
    }

    /// <summary>
    /// Resolves the transaction currency when creating a Delivery Note from a Pick List row without a Sales Order:
    /// uses the customer's default billing currency if set, otherwise the Pick List company's currency (never a global default)
    /// per ERPNext commit 8fbc02f81b (<c>pick_list.py</c>).
    /// </summary>
    public static string ResolveCurrencyForDocumentFromPickListWithoutSalesOrder(
        string? customerDefaultCurrency,
        string companyCurrency)
    {
        return !string.IsNullOrWhiteSpace(customerDefaultCurrency)
            ? customerDefaultCurrency.Trim()
            : (companyCurrency ?? string.Empty).Trim();
    }

    /// <summary>
    /// Filters open Sales Orders eligible for Pick List creation so closed or stopped orders and rows with no remaining
    /// pickable quantity are excluded, and matches batches / serial bundles across split pick locations
    /// per ERPNext commits e4e0d9c707, 072fe92f40, 784d9cbe09, 9f33bb012e, 6bfba876ac, and f303513d06.
    /// </summary>
    public static List<Guid> FilterEligibleSalesOrderItemsForPickList(
        IEnumerable<(Guid SalesOrderItemId, bool IsRowClosed, string OrderStatus, decimal StockQty, decimal DeliveredStockQty, decimal AlreadyPickedStockQty)> candidateRows)
    {
        if (candidateRows == null)
        {
            return new List<Guid>();
        }

        return candidateRows
            .Where(r => !r.IsRowClosed
                && !string.Equals(r.OrderStatus, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(r.OrderStatus, "Stopped", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(r.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(r.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && (r.StockQty - r.DeliveredStockQty - r.AlreadyPickedStockQty) > 0m)
            .Select(r => r.SalesOrderItemId)
            .ToList();
    }

    /// <summary>
    /// Projects the packed component rows that a Pick List re-pack produces for a bundle row's current quantity
    /// when auto-setting batch quantities or serial numbers per ERPNext commits 101c9ed031, a214b67c86, and 263c81276d
    /// (<c>get_kept_component_rows</c> / <c>get_current_packed_items</c> in <c>packed_item.py</c> &amp; <c>get_item_details.py</c>):
    /// 1. Allocates the required component quantity across Pick List locations in order.
    /// 2. Picks the kept stale row or creates a new packed row in a single <c>if / else</c> branch when a re-pack adds
    ///    back a previously dropped location.
    /// 3. Trims or tops up the packed row's serial numbers to the location's <c>packQty</c> so lowered bundle quantities
    ///    immediately release trimmed serial numbers and raised bundle quantities include newly added location batches.
    /// </summary>
    public static IReadOnlyList<(Guid ComponentItemId, Guid WarehouseId, Guid? BatchId, decimal Qty, IReadOnlyList<string> SerialNos)>
        ProjectPackedRowsForRepackAutoBatchAndSerialSelection(
            Guid componentItemId,
            decimal componentQtyPerBundleUnit,
            decimal bundleRowStockQty,
            IEnumerable<(Guid WarehouseId, Guid? BatchId, decimal Qty, IReadOnlyList<string>? SerialNos)>? stalePackedRows,
            IEnumerable<(Guid WarehouseId, Guid? BatchId, decimal LocationQty, IReadOnlyList<string>? LocationSerialNos)>? pickListLocations,
            int precision = 4)
    {
        var staleList = (stalePackedRows ?? Enumerable.Empty<(Guid WarehouseId, Guid? BatchId, decimal Qty, IReadOnlyList<string>? SerialNos)>())
            .ToList();
        if (staleList.Count == 0 || bundleRowStockQty <= 0m || componentQtyPerBundleUnit <= 0m)
        {
            return Array.Empty<(Guid, Guid, Guid?, decimal, IReadOnlyList<string>)>();
        }

        var requiredQty = Math.Round(componentQtyPerBundleUnit * bundleRowStockQty, precision, MidpointRounding.AwayFromZero);
        var locations = (pickListLocations ?? Enumerable.Empty<(Guid WarehouseId, Guid? BatchId, decimal LocationQty, IReadOnlyList<string>? LocationSerialNos)>())
            .ToList();

        var projected = new List<(Guid ComponentItemId, Guid WarehouseId, Guid? BatchId, decimal Qty, IReadOnlyList<string> SerialNos)>();

        if (locations.Count == 0)
        {
            var firstStale = staleList[0];
            var targetSerialCount = Math.Max(0, (int)Math.Round(requiredQty, 0, MidpointRounding.AwayFromZero));
            var serials = (firstStale.SerialNos ?? Array.Empty<string>()).Take(targetSerialCount).ToList();
            projected.Add((componentItemId, firstStale.WarehouseId, firstStale.BatchId, requiredQty, serials));
            return projected;
        }

        var remaining = requiredQty;
        foreach (var loc in locations)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var packQty = Math.Round(Math.Min(remaining, loc.LocationQty), precision, MidpointRounding.AwayFromZero);
            if (packQty <= 0m)
            {
                continue;
            }
            remaining = Math.Round(remaining - packQty, precision, MidpointRounding.AwayFromZero);

            (Guid WarehouseId, Guid? BatchId, decimal Qty, IReadOnlyList<string>? SerialNos) baseRow;
            if (staleList.Count > 0)
            {
                var matchIdx = staleList.FindIndex(s => s.WarehouseId == loc.WarehouseId && s.BatchId == loc.BatchId);
                if (matchIdx < 0)
                {
                    matchIdx = staleList.FindIndex(s => s.WarehouseId == loc.WarehouseId);
                }
                if (matchIdx < 0)
                {
                    matchIdx = 0;
                }
                baseRow = staleList[matchIdx];
                staleList.RemoveAt(matchIdx);
            }
            else
            {
                baseRow = (loc.WarehouseId, loc.BatchId, 0m, Array.Empty<string>());
            }

            var targetSerialCount = Math.Max(0, (int)Math.Round(packQty, 0, MidpointRounding.AwayFromZero));
            var mergedSerials = new List<string>();
            if (baseRow.SerialNos != null)
            {
                foreach (var sn in baseRow.SerialNos)
                {
                    if (!string.IsNullOrWhiteSpace(sn) && !mergedSerials.Contains(sn, StringComparer.OrdinalIgnoreCase))
                    {
                        mergedSerials.Add(sn);
                    }
                }
            }
            if (loc.LocationSerialNos != null && mergedSerials.Count < targetSerialCount)
            {
                foreach (var sn in loc.LocationSerialNos)
                {
                    if (mergedSerials.Count >= targetSerialCount)
                    {
                        break;
                    }
                    if (!string.IsNullOrWhiteSpace(sn) && !mergedSerials.Contains(sn, StringComparer.OrdinalIgnoreCase))
                    {
                        mergedSerials.Add(sn);
                    }
                }
            }

            var finalSerials = mergedSerials.Take(targetSerialCount).ToList();
            projected.Add((componentItemId, loc.WarehouseId, loc.BatchId ?? baseRow.BatchId, packQty, finalSerials));
        }

        return projected;
    }

    /// <summary>
    /// Filters candidate pick locations against materials already held by other active Pick Lists
    /// per ERPNext PR #67381 (commit 3d24a5b8ea) and PR #67382 (commit cb24f32fa8:
    /// <c>filter_locations_by_picked_materials</c>):
    /// - Deducts held quantity (<c>picked - delivered - transferred</c>).
    /// - Removes already-picked serial numbers from serialized locations AND caps <c>Qty</c> at
    ///   <c>Math.Min(Qty, freeSerialNos.Count)</c> so a location never offers more qty than its free serial numbers.
    /// </summary>
    public static List<PickLocationAllocation> FilterLocationsByPickedMaterials(
        IEnumerable<PickLocationAllocation> locations,
        IReadOnlyDictionary<(Guid WarehouseId, Guid? BatchId), (decimal HeldQty, IReadOnlySet<string>? PickedSerialNos)> pickedByLocation,
        int precision = 4)
    {
        var filtered = new List<PickLocationAllocation>();
        if (locations == null)
        {
            return filtered;
        }

        foreach (var loc in locations)
        {
            var qty = loc.Qty;
            var serials = loc.SerialNumbers != null ? new List<string>(loc.SerialNumbers) : new List<string>();
            var hasSerials = serials.Count > 0;

            if (pickedByLocation != null
                && pickedByLocation.TryGetValue((loc.WarehouseId, loc.BatchId), out var picked))
            {
                qty = Math.Max(0m, qty - picked.HeldQty);
                if (hasSerials && picked.PickedSerialNos != null && picked.PickedSerialNos.Count > 0)
                {
                    serials = serials
                        .Where(sn => !picked.PickedSerialNos.Contains(sn))
                        .ToList();
                    // A partly moved pick list row can still list more serial nos than it holds (PR #67382)
                    qty = Math.Min(qty, serials.Count);
                }
            }

            if (Math.Round(qty, precision, MidpointRounding.AwayFromZero) > 0m)
            {
                filtered.Add(new PickLocationAllocation
                {
                    WarehouseId = loc.WarehouseId,
                    BatchId = loc.BatchId,
                    Qty = qty,
                    SerialNumbers = serials
                });
            }
        }

        return filtered;
    }

    /// <summary>
    /// Distributes delivered quantity for a Product Bundle across Pick List location rows
    /// per ERPNext PR #65492 (<c>PickList._get_bundle_location_delivered_qty</c>):
    /// first each row takes the count of its own picked serial numbers that were delivered (case-insensitive),
    /// then any remaining delivered quantity (including unpicked/substituted serial numbers) is allocated
    /// sequentially by remaining picked capacity.
    /// </summary>
    public static Dictionary<Guid, decimal> DistributeBundleLocationDeliveredQty(
        IReadOnlyList<(Guid LocationId, decimal PickedQty, IReadOnlyList<string>? PickedSerialNos)>? locations,
        decimal totalDeliveredQty,
        IReadOnlyCollection<string>? deliveredSerialNos,
        int precision = 4)
    {
        var qtyByLocation = new Dictionary<Guid, decimal>();
        if (locations == null || locations.Count == 0)
        {
            return qtyByLocation;
        }

        var deliveredSet = new HashSet<string>(
            (deliveredSerialNos ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        decimal matchedTotal = 0m;
        foreach (var loc in locations)
        {
            var matched = 0m;
            if (loc.PickedSerialNos != null && deliveredSet.Count > 0)
            {
                var uniquePicked = new HashSet<string>(
                    loc.PickedSerialNos.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
                    StringComparer.OrdinalIgnoreCase);
                matched = uniquePicked.Count(sn => deliveredSet.Contains(sn));
            }

            qtyByLocation[loc.LocationId] = matched;
            matchedTotal += matched;
        }

        var remaining = Math.Max(totalDeliveredQty - matchedTotal, 0m);
        foreach (var loc in locations)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var capacity = Math.Max(0m, loc.PickedQty - qtyByLocation[loc.LocationId]);
            var add = Math.Min(capacity, remaining);
            qtyByLocation[loc.LocationId] = Math.Round(qtyByLocation[loc.LocationId] + add, precision, MidpointRounding.AwayFromZero);
            remaining -= add;
        }

        foreach (var loc in locations)
        {
            qtyByLocation[loc.LocationId] = Math.Round(qtyByLocation[loc.LocationId], precision, MidpointRounding.AwayFromZero);
        }

        return qtyByLocation;
    }
}

public class AllocatedLocationItem
{
    public decimal Qty { get; set; }
    public decimal StockQty { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
    public List<string> SerialNumbers { get; set; } = new();
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

