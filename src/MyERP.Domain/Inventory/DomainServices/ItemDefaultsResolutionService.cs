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
    private readonly IRepository<ItemDefault, Guid>? _itemDefaultRepository;

    public ItemDefaultsResolutionService(
        IRepository<Item, Guid> itemRepository,
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<ItemDefault, Guid>? itemDefaultRepository = null)
    {
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
        _itemDefaultRepository = itemDefaultRepository;
    }

    private IRepository<ItemDefault, Guid>? GetItemDefaultRepository()
        => _itemDefaultRepository ?? LazyServiceProvider.LazyGetService<IRepository<ItemDefault, Guid>>();

    /// <summary>
    /// Resolves the income account for an item (for sales GL posting).
    /// Chain: ItemDefault (for company) → Item.DefaultIncomeAccountId → ItemGroup hierarchy (traverse parents up) → null.
    /// </summary>
    public async Task<Guid?> ResolveIncomeAccountAsync(Guid itemId, Guid? companyId = null)
    {
        if (companyId.HasValue)
        {
            var defRepo = GetItemDefaultRepository();
            if (defRepo != null)
            {
                var defQ = await defRepo.GetQueryableAsync();
                var defIncome = defQ
                    .Where(d => d.ItemId == itemId && d.CompanyId == companyId.Value && d.IncomeAccountId != null)
                    .Select(d => d.IncomeAccountId)
                    .FirstOrDefault();
                if (defIncome.HasValue)
                    return defIncome;
            }
        }

        var item = await _itemRepository.GetAsync(itemId);
        if (item.DefaultIncomeAccountId.HasValue)
            return item.DefaultIncomeAccountId;

        // Traverse item group hierarchy upward
        return await TraverseGroupHierarchyAsync(item.ItemGroupId, g => g.DefaultIncomeAccountId);
    }

    /// <summary>
    /// Resolves the expense/COGS account for an item (for purchasing/COGS GL posting).
    /// Chain: ItemDefault (for company) → Item.DefaultExpenseAccountId → ItemGroup hierarchy (traverse parents up) → Company.ServiceExpenseAccountId (for non-stock/service items) → null.
    /// </summary>
    public async Task<Guid?> ResolveExpenseAccountAsync(Guid itemId, Guid? companyId = null)
    {
        if (companyId.HasValue)
        {
            var defRepo = GetItemDefaultRepository();
            if (defRepo != null)
            {
                var defQ = await defRepo.GetQueryableAsync();
                var defExpense = defQ
                    .Where(d => d.ItemId == itemId && d.CompanyId == companyId.Value && d.ExpenseAccountId != null)
                    .Select(d => d.ExpenseAccountId)
                    .FirstOrDefault();
                if (defExpense.HasValue)
                    return defExpense;
            }
        }

        var item = await _itemRepository.GetAsync(itemId);
        if (item.DefaultExpenseAccountId.HasValue)
            return item.DefaultExpenseAccountId;

        var groupAccount = await TraverseGroupHierarchyAsync(item.ItemGroupId, g => g.DefaultExpenseAccountId);
        if (groupAccount.HasValue)
            return groupAccount;

        if (!item.MaintainStock && companyId.HasValue)
        {
            var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var company = await companyRepo.FindAsync(companyId.Value);
            if (company?.ServiceExpenseAccountId.HasValue == true)
                return company.ServiceExpenseAccountId;
        }

        return null;
    }

    /// <summary>
    /// Resolves the default warehouse for an item.
    /// Chain: ItemDefault (for company) → Item.DefaultWarehouseId → ItemGroup hierarchy (traverse parents up) → null.
    /// Per ERPNext PR #58663 / get_item_defaults chain.
    /// </summary>
    public async Task<Guid?> ResolveWarehouseAsync(Guid itemId, Guid? companyId = null)
    {
        if (companyId.HasValue)
        {
            var defRepo = GetItemDefaultRepository();
            if (defRepo != null)
            {
                var defQ = await defRepo.GetQueryableAsync();
                var defWh = defQ
                    .Where(d => d.ItemId == itemId && d.CompanyId == companyId.Value && d.DefaultWarehouseId != null)
                    .Select(d => d.DefaultWarehouseId)
                    .FirstOrDefault();
                if (defWh.HasValue)
                    return defWh;
            }
        }

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
