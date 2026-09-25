using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for Putaway Rule allocation.
/// Distributes incoming stock across warehouses based on priority and capacity.
/// Per DO-NOT: "Skip putaway rule capacity check on incoming stock (leads to over-capacity warehouses)"
/// Per DO-NOT: "Skip FLOOR rounding for UOMs with must_be_whole_number in putaway/pick allocation"
/// </summary>
public class PutawayService : DomainService
{
    private readonly IRepository<PutawayRule, Guid> _ruleRepository;
    private readonly IRepository<Bin, Guid> _binRepository;

    public PutawayService(
        IRepository<PutawayRule, Guid> ruleRepository,
        IRepository<Bin, Guid> binRepository)
    {
        _ruleRepository = ruleRepository;
        _binRepository = binRepository;
    }

    /// <summary>
    /// Allocates incoming qty across warehouses based on putaway rules.
    /// Priority order (ascending). Capacity-limited per warehouse.
    /// Uses FLOOR for whole-number UOMs (partial units invalid).
    /// Returns list of (warehouseId, qty) allocations.
    /// </summary>
    public async Task<List<PutawayAllocation>> AllocateAsync(
        Guid companyId, Guid itemId, decimal totalQty,
        Guid? itemGroupId = null, bool mustBeWholeNumber = false)
    {
        return await AllocateBatchAsync(companyId, new[]
        {
            new PutawayBatchItem
            {
                ItemId = itemId,
                Qty = totalQty,
                ItemGroupId = itemGroupId,
                MustBeWholeNumber = mustBeWholeNumber
            }
        });
    }

    /// <summary>
    /// Allocates multiple incoming items across warehouses in a single batch/voucher.
    /// Per ERPNext PR #59380: caches and reuses ordered putaway rules for repeated items.
    /// Preserves capacity tracking across items in the batch so warehouses are not over-allocated.
    /// </summary>
    public async Task<List<PutawayAllocation>> AllocateBatchAsync(
        Guid companyId,
        IReadOnlyList<PutawayBatchItem> items)
    {
        var allocations = new List<PutawayAllocation>();
        if (items == null || items.Count == 0) return allocations;

        var ruleCache = new Dictionary<(Guid ItemId, Guid? ItemGroupId), List<PutawayRule>>();
        var allocatedPerWarehouse = new Dictionary<Guid, decimal>();
        var binBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        var binQueryable = await _binRepository.GetQueryableAsync();

        foreach (var item in items)
        {
            var remaining = item.Qty;
            var key = (item.ItemId, item.ItemGroupId);

            if (!ruleCache.TryGetValue(key, out var rawRules))
            {
                rawRules = await GetMatchingRulesAsync(companyId, item.ItemId, item.ItemGroupId);
                ruleCache[key] = rawRules;

                var whIds = rawRules.Select(r => r.WarehouseId).Distinct().ToList();
                var loadedBins = binQueryable
                    .Where(b => b.ItemId == item.ItemId && whIds.Contains(b.WarehouseId))
                    .ToDictionary(b => b.WarehouseId, b => b.ActualQty);

                foreach (var whId in whIds)
                {
                    var balanceKey = (item.ItemId, whId);
                    if (!binBalances.ContainsKey(balanceKey))
                    {
                        binBalances[balanceKey] = loadedBins.TryGetValue(whId, out var bal) ? bal : 0m;
                    }
                }
            }

            if (!rawRules.Any())
            {
                if (remaining > 0)
                {
                    allocations.Add(new PutawayAllocation
                    {
                        ItemId = item.ItemId,
                        WarehouseId = Guid.Empty,
                        Qty = remaining,
                        IsUnallocated = true
                    });
                }
                continue;
            }

            // Sort candidate rules by priority ASC, then free_space DESC (gotcha #2718)
            var ruleWithCapacities = rawRules
                .Select(r =>
                {
                    var balanceKey = (item.ItemId, r.WarehouseId);
                    var currentBalance = binBalances.TryGetValue(balanceKey, out var bal) ? bal : 0m;
                    var freeSpace = r.GetAvailableCapacity(currentBalance);
                    return new { Rule = r, FreeSpace = freeSpace };
                })
                .Where(x => x.FreeSpace > 0)
                .OrderBy(x => x.Rule.ItemId.HasValue ? 0 : 1)
                .ThenBy(x => x.Rule.Priority)
                .ThenByDescending(x => x.FreeSpace)
                .ToList();

            // Free space is a property of the WAREHOUSE, not of the rule. More than one matching rule
            // can target the same warehouse (an item-specific rule and an item-group rule, or two
            // duplicate rules), and each one computes its free space from the same Bin balance — so
            // allocating each rule's full free space in turn would fill that warehouse several times
            // over. Track what has already been promised per warehouse and net it off, mirroring
            // ERPNext apply_putaway_rule's own `rule["free_space"] -= stock_qty_to_allocate` decrement.
            foreach (var candidate in ruleWithCapacities)
            {
                if (remaining <= 0) break;

                var rule = candidate.Rule;
                var alreadyAllocated = allocatedPerWarehouse.TryGetValue(rule.WarehouseId, out var used) ? used : 0m;
                var available = candidate.FreeSpace - alreadyAllocated;
                if (available <= 0) continue;

                var allocateQty = Math.Min(remaining, available);

                // FLOOR for whole-number UOMs (gotcha #2719)
                if (item.MustBeWholeNumber)
                    allocateQty = Math.Floor(allocateQty);

                if (allocateQty <= 0) continue;

                allocations.Add(new PutawayAllocation
                {
                    ItemId = item.ItemId,
                    WarehouseId = rule.WarehouseId,
                    Qty = allocateQty,
                    PutawayRuleId = rule.Id
                });

                allocatedPerWarehouse[rule.WarehouseId] = alreadyAllocated + allocateQty;
                remaining -= allocateQty;
            }

            // Any remaining qty unallocated (no suitable warehouse or all full)
            if (remaining > 0)
            {
                allocations.Add(new PutawayAllocation
                {
                    ItemId = item.ItemId,
                    WarehouseId = Guid.Empty, // Signal: needs manual assignment
                    Qty = remaining,
                    IsUnallocated = true
                });
            }
        }

        return allocations;
    }

    /// <summary>
    /// Gets putaway rules matching an item, sorted by priority.
    /// Checks item-specific rules first, then item-group rules.
    /// </summary>
    private async Task<List<PutawayRule>> GetMatchingRulesAsync(
        Guid companyId, Guid itemId, Guid? itemGroupId)
    {
        var queryable = await _ruleRepository.GetQueryableAsync();

        // Item-specific rules first
        var rules = queryable
            .Where(r => r.CompanyId == companyId
                && r.IsEnabled
                && (r.ItemId == itemId || (r.ItemGroupId == itemGroupId && itemGroupId.HasValue)))
            .OrderBy(r => r.ItemId.HasValue ? 0 : 1) // Item-specific before group
            .ThenBy(r => r.Priority)
            .ToList();

        return rules;
    }
}

public class PutawayAllocation
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public Guid? PutawayRuleId { get; set; }
    public bool IsUnallocated { get; set; }
}

public class PutawayBatchItem
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? ItemGroupId { get; set; }
    public bool MustBeWholeNumber { get; set; }
}
