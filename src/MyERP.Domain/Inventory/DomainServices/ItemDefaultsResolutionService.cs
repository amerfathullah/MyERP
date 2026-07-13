using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Resolves default accounts for items using the ERPNext fallback chain:
/// Item → Item Group (traverse tree up) → Company defaults.
/// Per ERPNext: get_item_defaults and get_item_group_defaults chain.
/// </summary>
public class ItemDefaultsResolutionService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;

    public ItemDefaultsResolutionService(
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository)
    {
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
    }

    /// <summary>
    /// Resolves the income account for an item (for sales GL posting).
    /// Chain: Item.DefaultIncomeAccountId → ItemGroup hierarchy (traverse parents up) → null.
    /// </summary>
    public async Task<Guid?> ResolveIncomeAccountAsync(Guid itemId)
    {
        var item = await _itemRepository.GetAsync(itemId);
        if (item.DefaultIncomeAccountId.HasValue)
            return item.DefaultIncomeAccountId;

        // Traverse item group hierarchy upward
        return await TraverseGroupHierarchyAsync(item.ItemGroupId, g => g.DefaultIncomeAccountId);
    }

    /// <summary>
    /// Resolves the expense/COGS account for an item (for purchasing/COGS GL posting).
    /// Chain: Item.DefaultExpenseAccountId → ItemGroup hierarchy (traverse parents up) → null.
    /// </summary>
    public async Task<Guid?> ResolveExpenseAccountAsync(Guid itemId)
    {
        var item = await _itemRepository.GetAsync(itemId);
        if (item.DefaultExpenseAccountId.HasValue)
            return item.DefaultExpenseAccountId;

        return await TraverseGroupHierarchyAsync(item.ItemGroupId, g => g.DefaultExpenseAccountId);
    }

    /// <summary>
    /// Resolves the default warehouse for an item.
    /// Chain: Item.DefaultWarehouseId → ItemGroup hierarchy (traverse parents up) → null.
    /// </summary>
    public async Task<Guid?> ResolveWarehouseAsync(Guid itemId)
    {
        var item = await _itemRepository.GetAsync(itemId);
        if (item.DefaultWarehouseId.HasValue)
            return item.DefaultWarehouseId;

        return await TraverseGroupHierarchyAsync(item.ItemGroupId, g => g.DefaultWarehouseId);
    }

    /// <summary>
    /// Traverses item group hierarchy (child → parent → grandparent) looking for a non-null value.
    /// Max depth = 10 to prevent infinite loops from data corruption.
    /// </summary>
    private async Task<Guid?> TraverseGroupHierarchyAsync(Guid? groupId, Func<ItemGroup, Guid?> selector)
    {
        var currentGroupId = groupId;
        var maxDepth = 10;

        while (currentGroupId.HasValue && maxDepth-- > 0)
        {
            var group = await _itemGroupRepository.FindAsync(currentGroupId.Value);
            if (group == null) break;

            var value = selector(group);
            if (value.HasValue)
                return value;

            currentGroupId = group.ParentId;
        }

        return null;
    }
}
