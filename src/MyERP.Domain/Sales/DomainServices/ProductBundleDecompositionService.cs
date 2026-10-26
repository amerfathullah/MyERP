using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Decomposes Product Bundle items into their component items for stock operations.
/// When a transaction contains a bundle item, stock is reserved/delivered/consumed
/// at the COMPONENT level — the bundle parent item is virtual (non-stock).
/// 
/// Per ERPNext: "make_packing_list" orchestrator:
/// - Selects active bundle version (priority: highest version active, with version deactivation)
/// - Scales component qty by parent item qty, converting component UOM to stock UOM
/// - Rate rollup: parent_rate × (component_amount / total_bundle_amount)
/// - Product Bundle items are excluded from packed qty validation
/// - Internal transfers hard-block packed items
/// - Returns allow previously active, now-disabled bundles (PR #62149 / commit ffcf97c223)
/// </summary>
public class ProductBundleDecompositionService : DomainService
{
    private readonly IRepository<ProductBundle, Guid> _bundleRepository;
    private readonly IRepository<Inventory.Entities.Item, Guid>? _itemRepository;
    private readonly Inventory.DomainServices.UomConversionService? _uomConversionService;

    public ProductBundleDecompositionService(IRepository<ProductBundle, Guid> bundleRepository)
        : this(bundleRepository, null, null)
    {
    }

    public ProductBundleDecompositionService(
        IRepository<ProductBundle, Guid> bundleRepository,
        IRepository<Inventory.Entities.Item, Guid>? itemRepository,
        Inventory.DomainServices.UomConversionService? uomConversionService)
    {
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _uomConversionService = uomConversionService;
    }

    /// <summary>
    /// Checks if an item is an active Product Bundle.
    /// </summary>
    public async Task<bool> IsBundleItemAsync(Guid itemId, bool isReturn = false)
    {
        var queryable = await _bundleRepository.GetQueryableAsync();
        return queryable.Any(b => b.ItemId == itemId && b.IsActive && (!b.IsDisabled || isReturn));
    }

    /// <summary>
    /// Gets all active bundle item IDs from a list of item IDs (batch check).
    /// </summary>
    public async Task<HashSet<Guid>> GetBundleItemIdsAsync(IEnumerable<Guid> itemIds, bool isReturn = false)
    {
        var itemIdList = itemIds.ToList();
        if (!itemIdList.Any()) return new HashSet<Guid>();

        var queryable = await _bundleRepository.GetQueryableAsync();
        var bundleItemIds = queryable
            .Where(b => itemIdList.Contains(b.ItemId) && b.IsActive && (!b.IsDisabled || isReturn))
            .Select(b => b.ItemId)
            .ToHashSet();

        return bundleItemIds;
    }

    /// <summary>
    /// Decomposes a single bundle item into its components, scaling by the transaction qty.
    /// Returns the list of component items with their scaled quantities and proportional rates.
    /// Converts component UOM to stock UOM per ERPNext PR #62149 / commit 80590d5e40.
    /// Allows now-disabled bundles for return transactions per PR #62149 / commit ffcf97c223.
    /// </summary>
    /// <param name="itemId">The bundle parent item ID</param>
    /// <param name="transactionQty">Quantity of the parent in the transaction</param>
    /// <param name="parentRate">Rate/unit price of the parent bundle item</param>
    /// <param name="warehouseId">Optional warehouse from parent item row</param>
    /// <param name="parentDetailId">Optional row ID of the parent bundle transaction item (for exact packed item matching per PR #60745)</param>
    /// <param name="parentIndex">Optional row index of the parent bundle transaction item (for exact packed item matching per PR #60745)</param>
    /// <param name="isReturn">Whether this transaction is a return document (allows disabled bundles)</param>
    /// <returns>List of decomposed component items with qty and proportional rate</returns>
    public async Task<List<DecomposedItem>> DecomposeAsync(
        Guid itemId, decimal transactionQty, decimal parentRate,
        Guid? warehouseId = null, Guid? parentDetailId = null, int? parentIndex = null,
        bool isReturn = false,
        Guid? productBundleId = null)
    {
        var queryable = await _bundleRepository.GetQueryableAsync();
        ProductBundle? bundle = null;
        if (productBundleId.HasValue)
        {
            bundle = queryable.FirstOrDefault(b => b.Id == productBundleId.Value);
        }
        bundle ??= queryable.FirstOrDefault(b => b.ItemId == itemId && b.IsActive && (!b.IsDisabled || isReturn));

        if (bundle == null)
            return new List<DecomposedItem>();

        var totalBundleAmount = bundle.Items.Sum(i => i.Qty);
        if (totalBundleAmount == 0)
            return new List<DecomposedItem>();

        var componentItemIds = bundle.Items.Select(i => i.ComponentItemId).Distinct().ToList();
        var itemMap = new Dictionary<Guid, Inventory.Entities.Item>();
        if (_itemRepository != null)
        {
            var itemQueryable = await _itemRepository.GetQueryableAsync();
            itemMap = itemQueryable.Where(i => componentItemIds.Contains(i.Id)).ToDictionary(i => i.Id);
        }

        var result = new List<DecomposedItem>();
        foreach (var component in bundle.Items)
        {
            var componentFactor = 1m;
            var targetUom = component.Uom ?? "Unit";

            if (itemMap.TryGetValue(component.ComponentItemId, out var compItem))
            {
                targetUom = compItem.Uom;
                if (!string.IsNullOrWhiteSpace(component.Uom) && _uomConversionService != null)
                {
                    componentFactor = await _uomConversionService.GetConversionFactorAsync(
                        compItem.Id, component.Uom, compItem.Uom, compItem.VariantOfId);
                }
            }

            // Scale qty: component_qty × componentFactor × parent_transaction_qty
            var scaledQty = component.Qty * componentFactor * transactionQty;

            // Proportional rate: parent_rate × (component_qty / total_bundle_qty)
            // This distributes the selling price across components proportionally
            var proportionalRate = parentRate * (component.Qty / totalBundleAmount);

            result.Add(new DecomposedItem(
                ComponentItemId: component.ComponentItemId,
                ComponentItemName: component.ItemName,
                Qty: scaledQty,
                Rate: proportionalRate,
                Uom: targetUom,
                ParentBundleItemId: itemId,
                ParentBundleId: bundle.Id,
                WarehouseId: warehouseId,
                ParentDetailId: parentDetailId,
                ParentIndex: parentIndex
            ));
        }

        return result;
    }

    /// <summary>
    /// Decomposes all bundle items in a transaction. Non-bundle items are returned as-is.
    /// Returns a combined list: non-bundle items unchanged + bundle items exploded into components.
    /// </summary>
    public async Task<DecompositionResult> DecomposeTransactionItemsAsync(
        IEnumerable<BundleTransactionItem> items,
        bool isReturn = false)
    {
        var itemList = items.ToList();
        var allItemIds = itemList.Select(i => i.ItemId).Distinct().ToList();
        var bundleItemIds = await GetBundleItemIdsAsync(allItemIds, isReturn);

        var regularItems = new List<BundleTransactionItem>();
        var packedItems = new List<DecomposedItem>();

        foreach (var item in itemList)
        {
            if (bundleItemIds.Contains(item.ItemId))
            {
                var components = await DecomposeAsync(
                    item.ItemId, item.Qty, item.Rate, item.WarehouseId, item.DetailId, item.Index, isReturn, item.ProductBundleId);
                packedItems.AddRange(components);
            }
            else
            {
                regularItems.Add(item);
            }
        }

        return new DecompositionResult(regularItems, packedItems);
    }

    /// <summary>
    /// Matches packed items belonging to a specific parent bundle row.
    /// Per ERPNext PR #60745 / commit de9d145b06:
    /// Match packed items to a bundle row only by exact DetailId (name/guid) or exact Index.
    /// Prevents digit-prefix false positives from reassigning other bundle rows' packed items.
    /// </summary>
    public static IEnumerable<DecomposedItem> GetPackedItemsForBundleRow(
        IEnumerable<DecomposedItem> packedItems, Guid parentDetailId, int? parentIndex = null)
    {
        return packedItems.Where(p =>
            (p.ParentDetailId.HasValue && p.ParentDetailId.Value == parentDetailId) ||
            (!p.ParentDetailId.HasValue && parentIndex.HasValue && p.ParentIndex.HasValue && p.ParentIndex.Value == parentIndex.Value));
    }
}

/// <summary>
/// A single decomposed component from a Product Bundle.
/// </summary>
public record DecomposedItem(
    Guid ComponentItemId,
    string? ComponentItemName,
    decimal Qty,
    decimal Rate,
    string Uom,
    Guid ParentBundleItemId,
    Guid ParentBundleId,
    Guid? WarehouseId = null,
    Guid? ParentDetailId = null,
    int? ParentIndex = null
);

/// <summary>
/// Input item for bundle decomposition check.
/// </summary>
public record BundleTransactionItem(
    Guid ItemId,
    decimal Qty,
    decimal Rate,
    Guid? WarehouseId = null,
    Guid? DetailId = null,
    int? Index = null,
    Guid? ProductBundleId = null
);

/// <summary>
/// Result of decomposing a transaction's items.
/// RegularItems = non-bundle items (stock as-is).
/// PackedItems = component items from bundles (stock these instead of parent).
/// </summary>
public record DecompositionResult(
    List<BundleTransactionItem> RegularItems,
    List<DecomposedItem> PackedItems
)
{
    /// <summary>
    /// Returns all items that need stock operations (regular + packed components).
    /// </summary>
    public IEnumerable<(Guid ItemId, decimal Qty)> GetStockItems()
    {
        foreach (var r in RegularItems)
            yield return (r.ItemId, r.Qty);
        foreach (var p in PackedItems)
            yield return (p.ComponentItemId, p.Qty);
    }

    public bool HasBundleItems => PackedItems.Any();
}
