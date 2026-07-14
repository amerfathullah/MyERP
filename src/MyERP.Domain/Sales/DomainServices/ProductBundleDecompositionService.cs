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
/// - Scales component qty by parent item qty
/// - Rate rollup: parent_rate × (component_amount / total_bundle_amount)
/// - Product Bundle items are excluded from packed qty validation
/// - Internal transfers hard-block packed items
/// </summary>
public class ProductBundleDecompositionService : DomainService
{
    private readonly IRepository<ProductBundle, Guid> _bundleRepository;

    public ProductBundleDecompositionService(IRepository<ProductBundle, Guid> bundleRepository)
    {
        _bundleRepository = bundleRepository;
    }

    /// <summary>
    /// Checks if an item is an active Product Bundle.
    /// </summary>
    public async Task<bool> IsBundleItemAsync(Guid itemId)
    {
        var queryable = await _bundleRepository.GetQueryableAsync();
        return queryable.Any(b => b.ItemId == itemId && b.IsActive);
    }

    /// <summary>
    /// Gets all active bundle item IDs from a list of item IDs (batch check).
    /// </summary>
    public async Task<HashSet<Guid>> GetBundleItemIdsAsync(IEnumerable<Guid> itemIds)
    {
        var itemIdList = itemIds.ToList();
        if (!itemIdList.Any()) return new HashSet<Guid>();

        var queryable = await _bundleRepository.GetQueryableAsync();
        var bundleItemIds = queryable
            .Where(b => itemIdList.Contains(b.ItemId) && b.IsActive)
            .Select(b => b.ItemId)
            .ToHashSet();

        return bundleItemIds;
    }

    /// <summary>
    /// Decomposes a single bundle item into its components, scaling by the transaction qty.
    /// Returns the list of component items with their scaled quantities and proportional rates.
    /// </summary>
    /// <param name="itemId">The bundle parent item ID</param>
    /// <param name="transactionQty">Quantity of the parent in the transaction</param>
    /// <param name="parentRate">Rate/unit price of the parent bundle item</param>
    /// <returns>List of decomposed component items with qty and proportional rate</returns>
    public async Task<List<DecomposedItem>> DecomposeAsync(Guid itemId, decimal transactionQty, decimal parentRate)
    {
        var queryable = await _bundleRepository.GetQueryableAsync();
        var bundle = queryable.FirstOrDefault(b => b.ItemId == itemId && b.IsActive);

        if (bundle == null)
            return new List<DecomposedItem>();

        var totalBundleAmount = bundle.Items.Sum(i => i.Qty);
        if (totalBundleAmount == 0)
            return new List<DecomposedItem>();

        var result = new List<DecomposedItem>();
        foreach (var component in bundle.Items)
        {
            // Scale qty: component_qty × parent_transaction_qty
            var scaledQty = component.Qty * transactionQty;

            // Proportional rate: parent_rate × (component_qty / total_bundle_qty)
            // This distributes the selling price across components proportionally
            var proportionalRate = parentRate * (component.Qty / totalBundleAmount);

            result.Add(new DecomposedItem(
                ComponentItemId: component.ComponentItemId,
                ComponentItemName: component.ItemName,
                Qty: scaledQty,
                Rate: proportionalRate,
                Uom: component.Uom ?? "Unit",
                ParentBundleItemId: itemId,
                ParentBundleId: bundle.Id
            ));
        }

        return result;
    }

    /// <summary>
    /// Decomposes all bundle items in a transaction. Non-bundle items are returned as-is.
    /// Returns a combined list: non-bundle items unchanged + bundle items exploded into components.
    /// </summary>
    public async Task<DecompositionResult> DecomposeTransactionItemsAsync(
        IEnumerable<BundleTransactionItem> items)
    {
        var itemList = items.ToList();
        var allItemIds = itemList.Select(i => i.ItemId).Distinct().ToList();
        var bundleItemIds = await GetBundleItemIdsAsync(allItemIds);

        var regularItems = new List<BundleTransactionItem>();
        var packedItems = new List<DecomposedItem>();

        foreach (var item in itemList)
        {
            if (bundleItemIds.Contains(item.ItemId))
            {
                var components = await DecomposeAsync(item.ItemId, item.Qty, item.Rate);
                packedItems.AddRange(components);
            }
            else
            {
                regularItems.Add(item);
            }
        }

        return new DecompositionResult(regularItems, packedItems);
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
    Guid ParentBundleId
);

/// <summary>
/// Input item for bundle decomposition check.
/// </summary>
public record BundleTransactionItem(
    Guid ItemId,
    decimal Qty,
    decimal Rate
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
