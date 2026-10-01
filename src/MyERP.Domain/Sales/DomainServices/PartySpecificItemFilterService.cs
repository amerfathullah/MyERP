using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Resolves which items are excluded from item search for a Customer/Supplier based on
/// PartySpecificItem rules. A direct party rule overrides a same-category group rule.
/// Per ERPNext controllers/queries.py get_customer_supplier_details (party_specific_item logic).
/// </summary>
public class PartySpecificItemFilterService : DomainService
{
    private readonly IRepository<PartySpecificItem, Guid> _repository;
    private readonly IRepository<Brand, Guid> _brandRepository;
    private readonly IRepository<Item, Guid>? _itemRepository;
    private readonly IRepository<ItemGroup, Guid>? _itemGroupRepository;

    public PartySpecificItemFilterService(
        IRepository<PartySpecificItem, Guid> repository,
        IRepository<Brand, Guid> brandRepository,
        IRepository<Item, Guid>? itemRepository = null,
        IRepository<ItemGroup, Guid>? itemGroupRepository = null)
    {
        _repository = repository;
        _brandRepository = brandRepository;
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
    }

    /// <summary>
    /// Validates that items in a transaction are not restricted for the specified customer/supplier.
    /// Per ERPNext PR #59636 (commit 5a8a0f9f34) and PR #59710 (commit af30285fea):
    /// An item is restricted if it matches other parties' rules AND does not match this party's own rules.
    /// Rules on different bases (Item, Item Group, Brand) are evaluated together rather than independently per field.
    /// </summary>
    public async Task ValidatePartySpecificItemsAsync(
        PartySpecificItemPartyType directPartyType,
        Guid partyId,
        PartySpecificItemPartyType groupPartyType,
        Guid? groupId,
        IReadOnlyCollection<Guid> itemIds,
        string partyDisplayName = "Party")
    {
        if (itemIds == null || itemIds.Count == 0)
            return;

        var queryable = await _repository.GetQueryableAsync();
        var directRules = queryable.Where(r => r.PartyType == directPartyType).ToList();
        var groupRules = queryable.Where(r => r.PartyType == groupPartyType).ToList();

        var ownRules = new List<PartySpecificItem>();
        var otherRules = new List<PartySpecificItem>();

        foreach (var rule in directRules)
        {
            if (rule.PartyId == partyId)
                ownRules.Add(rule);
            else
                otherRules.Add(rule);
        }

        foreach (var rule in groupRules)
        {
            if (groupId.HasValue && rule.PartyId == groupId.Value)
                ownRules.Add(rule);
            else
                otherRules.Add(rule);
        }

        if (otherRules.Count == 0)
            return;

        var otherItemIds = otherRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Item).Select(r => r.BasedOnValueId).ToHashSet();
        var otherGroupIds = await ExpandItemGroupSubtreeAsync(otherRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.ItemGroup).Select(r => r.BasedOnValueId));
        var otherBrandNames = await ResolveBrandNamesAsync(otherRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Brand).Select(r => r.BasedOnValueId));

        var ownItemIds = ownRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Item).Select(r => r.BasedOnValueId).ToHashSet();
        var ownGroupIds = await ExpandItemGroupSubtreeAsync(ownRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.ItemGroup).Select(r => r.BasedOnValueId));
        var ownBrandNames = await ResolveBrandNamesAsync(ownRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Brand).Select(r => r.BasedOnValueId));

        if (_itemRepository == null)
            return;

        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var referencedItems = itemQueryable
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemGroupId, i.Brand })
            .ToList();

        foreach (var item in referencedItems)
        {
            bool matchesOther = MatchesRuleSet(item.Id, item.ItemGroupId, item.Brand, otherItemIds, otherGroupIds, otherBrandNames);
            if (matchesOther)
            {
                bool matchesOwn = MatchesRuleSet(item.Id, item.ItemGroupId, item.Brand, ownItemIds, ownGroupIds, ownBrandNames);
                if (!matchesOwn)
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ItemRestrictedForParty)
                        .WithData("itemCode", item.ItemCode)
                        .WithData("partyType", directPartyType.ToString())
                        .WithData("partyName", partyDisplayName);
                }
            }
        }
    }

    /// <summary>
    /// Computes the item visibility exclusions for a direct party (e.g. a specific Customer)
    /// and its group (e.g. that Customer's CustomerGroup).
    /// </summary>
    public async Task<PartySpecificItemFilter> GetItemFilterAsync(
        PartySpecificItemPartyType directPartyType, Guid partyId,
        PartySpecificItemPartyType groupPartyType, Guid? groupId)
    {
        var queryable = await _repository.GetQueryableAsync();
        var directRules = queryable.Where(r => r.PartyType == directPartyType).ToList();
        var groupRules = queryable.Where(r => r.PartyType == groupPartyType).ToList();

        var ownRules = new List<PartySpecificItem>();
        var otherRules = new List<PartySpecificItem>();

        foreach (var rule in directRules)
        {
            if (rule.PartyId == partyId)
                ownRules.Add(rule);
            else
                otherRules.Add(rule);
        }

        foreach (var rule in groupRules)
        {
            if (groupId.HasValue && rule.PartyId == groupId.Value)
                ownRules.Add(rule);
            else
                otherRules.Add(rule);
        }

        if (otherRules.Count == 0)
        {
            return new PartySpecificItemFilter(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<string>());
        }

        var otherItemIds = otherRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Item).Select(r => r.BasedOnValueId).ToHashSet();
        var otherGroupIds = await ExpandItemGroupSubtreeAsync(otherRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.ItemGroup).Select(r => r.BasedOnValueId));
        var otherBrandNames = await ResolveBrandNamesAsync(otherRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Brand).Select(r => r.BasedOnValueId));

        var ownItemIds = ownRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Item).Select(r => r.BasedOnValueId).ToHashSet();
        var ownGroupIds = await ExpandItemGroupSubtreeAsync(ownRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.ItemGroup).Select(r => r.BasedOnValueId));
        var ownBrandNames = await ResolveBrandNamesAsync(ownRules.Where(r => r.RestrictBasedOn == PartySpecificItemRestrictBasedOn.Brand).Select(r => r.BasedOnValueId));

        if (_itemRepository != null)
        {
            var itemQueryable = await _itemRepository.GetQueryableAsync();
            var allCandidateItems = itemQueryable
                .Where(i => otherItemIds.Contains(i.Id)
                    || (i.ItemGroupId.HasValue && otherGroupIds.Contains(i.ItemGroupId.Value))
                    || (!string.IsNullOrEmpty(i.Brand) && otherBrandNames.Contains(i.Brand)))
                .Select(i => new { i.Id, i.ItemGroupId, i.Brand })
                .ToList();

            var excludedItemIds = new HashSet<Guid>();
            foreach (var item in allCandidateItems)
            {
                bool matchesOther = MatchesRuleSet(item.Id, item.ItemGroupId, item.Brand, otherItemIds, otherGroupIds, otherBrandNames);
                if (matchesOther && !MatchesRuleSet(item.Id, item.ItemGroupId, item.Brand, ownItemIds, ownGroupIds, ownBrandNames))
                {
                    excludedItemIds.Add(item.Id);
                }
            }

            return new PartySpecificItemFilter(excludedItemIds, new HashSet<Guid>(), new HashSet<string>());
        }

        // Fallback when itemRepository is not available (e.g. lightweight unit test mock setups)
        var excludedItems = otherItemIds.Except(ownItemIds).ToHashSet();
        var excludedGroups = otherGroupIds.Except(ownGroupIds).ToHashSet();
        var excludedBrands = otherBrandNames.Except(ownBrandNames, StringComparer.OrdinalIgnoreCase).ToHashSet();

        return new PartySpecificItemFilter(excludedItems, excludedGroups, excludedBrands);
    }

    private async Task<HashSet<Guid>> ExpandItemGroupSubtreeAsync(IEnumerable<Guid> itemGroupIds)
    {
        var inputIds = itemGroupIds.ToHashSet();
        if (inputIds.Count == 0)
            return inputIds;

        var repo = _itemGroupRepository ?? LazyServiceProvider.LazyGetService<IRepository<ItemGroup, Guid>>();
        if (repo == null)
            return inputIds;

        var allGroups = (await repo.GetQueryableAsync()).Select(g => new { g.Id, g.ParentId }).ToList();
        var childrenLookup = allGroups
            .Where(g => g.ParentId.HasValue)
            .GroupBy(g => g.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<Guid>(inputIds);
        var queue = new Queue<Guid>(inputIds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (childrenLookup.TryGetValue(current, out var children))
            {
                foreach (var child in children)
                {
                    if (result.Add(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        return result;
    }

    private async Task<HashSet<string>> ResolveBrandNamesAsync(IEnumerable<Guid> brandIds)
    {
        var ids = brandIds.ToHashSet();
        if (ids.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var brandQueryable = await _brandRepository.GetQueryableAsync();
        return brandQueryable
            .Where(b => ids.Contains(b.Id))
            .Select(b => b.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesRuleSet(
        Guid itemId,
        Guid? itemGroupId,
        string? brand,
        HashSet<Guid> ruleItemIds,
        HashSet<Guid> ruleGroupIds,
        HashSet<string> ruleBrandNames)
    {
        if (ruleItemIds.Contains(itemId))
            return true;

        if (itemGroupId.HasValue && ruleGroupIds.Contains(itemGroupId.Value))
            return true;

        if (!string.IsNullOrEmpty(brand) && ruleBrandNames.Contains(brand))
            return true;

        return false;
    }
}

/// <summary>Item ids/item-group ids/brand names excluded from search for a given party.</summary>
public record PartySpecificItemFilter(
    HashSet<Guid> ExcludedItemIds,
    HashSet<Guid> ExcludedItemGroupIds,
    HashSet<string> ExcludedBrandNames)
{
    public bool IsEmpty => ExcludedItemIds.Count == 0 && ExcludedItemGroupIds.Count == 0 && ExcludedBrandNames.Count == 0;
}
