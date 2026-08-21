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
        var allocations = new List<PutawayAllocation>();
        var remaining = totalQty;

        // Get matching rules
        var rawRules = await GetMatchingRulesAsync(companyId, itemId, itemGroupId);
        if (!rawRules.Any())
        {
            if (totalQty > 0)
            {
                allocations.Add(new PutawayAllocation
                {
                    WarehouseId = Guid.Empty,
                    Qty = totalQty,
                    IsUnallocated = true
                });
            }
            return allocations;
        }

        var binQueryable = await _binRepository.GetQueryableAsync();
        var warehouseIds = rawRules.Select(r => r.WarehouseId).Distinct().ToList();
        var binBalances = binQueryable
            .Where(b => b.ItemId == itemId && warehouseIds.Contains(b.WarehouseId))
            .ToDictionary(b => b.WarehouseId, b => b.ActualQty);

        // Sort candidate rules by priority ASC, then free_space DESC (gotcha #2718)
        var ruleWithCapacities = rawRules
            .Select(r =>
            {
                var currentBalance = binBalances.TryGetValue(r.WarehouseId, out var bal) ? bal : 0m;
                var freeSpace = r.GetAvailableCapacity(currentBalance);
                return new { Rule = r, FreeSpace = freeSpace };
            })
            .Where(x => x.FreeSpace > 0)
            .OrderBy(x => x.Rule.ItemId.HasValue ? 0 : 1)
            .ThenBy(x => x.Rule.Priority)
            .ThenByDescending(x => x.FreeSpace)
            .ToList();

        foreach (var candidate in ruleWithCapacities)
        {
            if (remaining <= 0) break;

            var rule = candidate.Rule;
            var available = candidate.FreeSpace;

            var allocateQty = Math.Min(remaining, available);

            // FLOOR for whole-number UOMs (gotcha #2719)
            if (mustBeWholeNumber)
                allocateQty = Math.Floor(allocateQty);

            if (allocateQty <= 0) continue;

            allocations.Add(new PutawayAllocation
            {
                WarehouseId = rule.WarehouseId,
                Qty = allocateQty,
                PutawayRuleId = rule.Id
            });

            remaining -= allocateQty;
        }

        // Any remaining qty unallocated (no suitable warehouse or all full)
        if (remaining > 0)
        {
            allocations.Add(new PutawayAllocation
            {
                WarehouseId = Guid.Empty, // Signal: needs manual assignment
                Qty = remaining,
                IsUnallocated = true
            });
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
    public Guid WarehouseId { get; set; }
    public decimal Qty { get; set; }
    public Guid? PutawayRuleId { get; set; }
    public bool IsUnallocated { get; set; }
}
