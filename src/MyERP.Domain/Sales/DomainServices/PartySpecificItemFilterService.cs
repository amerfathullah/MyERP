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

    public PartySpecificItemFilterService(
        IRepository<PartySpecificItem, Guid> repository,
        IRepository<Brand, Guid> brandRepository)
    {
        _repository = repository;
        _brandRepository = brandRepository;
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

        var allowed = new Dictionary<PartySpecificItemRestrictBasedOn, HashSet<Guid>>();
        var restricted = new Dictionary<PartySpecificItemRestrictBasedOn, HashSet<Guid>>();

        void Bucket(Dictionary<PartySpecificItemRestrictBasedOn, HashSet<Guid>> bucket, PartySpecificItem rule)
        {
            if (!bucket.TryGetValue(rule.RestrictBasedOn, out var set))
            {
                set = new HashSet<Guid>();
                bucket[rule.RestrictBasedOn] = set;
            }
            set.Add(rule.BasedOnValueId);
        }

        foreach (var rule in directRules)
        {
            Bucket(rule.PartyId == partyId ? allowed : restricted, rule);
        }

        foreach (var rule in groupRules)
        {
            Bucket(groupId.HasValue && rule.PartyId == groupId.Value ? allowed : restricted, rule);
        }

        var excludedItemIds = ExcludedValues(PartySpecificItemRestrictBasedOn.Item, allowed, restricted);
        var excludedItemGroupIds = ExcludedValues(PartySpecificItemRestrictBasedOn.ItemGroup, allowed, restricted);
        var excludedBrandIds = ExcludedValues(PartySpecificItemRestrictBasedOn.Brand, allowed, restricted);

        var excludedBrandNames = new HashSet<string>();
        if (excludedBrandIds.Count > 0)
        {
            var brandQueryable = await _brandRepository.GetQueryableAsync();
            excludedBrandNames = brandQueryable
                .Where(b => excludedBrandIds.Contains(b.Id))
                .Select(b => b.Name)
                .ToHashSet();
        }

        return new PartySpecificItemFilter(excludedItemIds, excludedItemGroupIds, excludedBrandNames);
    }

    private static HashSet<Guid> ExcludedValues(
        PartySpecificItemRestrictBasedOn category,
        Dictionary<PartySpecificItemRestrictBasedOn, HashSet<Guid>> allowed,
        Dictionary<PartySpecificItemRestrictBasedOn, HashSet<Guid>> restricted)
    {
        if (!restricted.TryGetValue(category, out var restrictedSet))
        {
            return new HashSet<Guid>();
        }

        var allowedSet = allowed.TryGetValue(category, out var a) ? a : new HashSet<Guid>();
        return restrictedSet.Except(allowedSet).ToHashSet();
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
