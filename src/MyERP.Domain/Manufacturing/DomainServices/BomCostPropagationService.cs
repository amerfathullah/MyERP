using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Propagates cost changes from child BOMs to all parent BOMs that reference them.
/// Implements the ERPNext "BOM Update Log" pattern.
/// Per DO-NOT: "Run more than one Update Cost BOM Update Log simultaneously (concurrency = 1)"
/// </summary>
public class BomCostPropagationService : DomainService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;

    public BomCostPropagationService(IRepository<BillOfMaterials, Guid> bomRepository)
    {
        _bomRepository = bomRepository;
    }

    /// <summary>
    /// Updates cost of a single BOM and propagates upward to all parent BOMs.
    /// Returns the count of BOMs updated.
    /// </summary>
    public async Task<int> UpdateCostAndPropagateAsync(Guid bomId)
    {
        var bom = await _bomRepository.GetAsync(bomId, includeDetails: true);
        bom.RecalculateCost();
        await _bomRepository.UpdateAsync(bom);

        // Find all parent BOMs that reference this BOM's item as a sub-assembly
        var updatedCount = 1;
        var parentBoms = await FindParentBomsAsync(bomId);

        foreach (var parentBom in parentBoms)
        {
            // Update the sub-assembly item rate in the parent BOM
            foreach (var item in parentBom.Items.Where(i => i.SubBomId == bomId))
            {
                item.Rate = bom.TotalCost / (bom.Quantity > 0 ? bom.Quantity : 1);
                item.Recalculate();
            }

            parentBom.RecalculateCost();
            await _bomRepository.UpdateAsync(parentBom);
            updatedCount++;

            // Recursively propagate upward (level-wise)
            updatedCount += await PropagateUpwardAsync(parentBom.Id);
        }

        return updatedCount;
    }

    /// <summary>
    /// Batch update: recalculates cost for all active BOMs (bottom-up, level-wise).
    /// Used by scheduled background job.
    /// </summary>
    public async Task<int> UpdateAllCostsAsync(Guid companyId)
    {
        var queryable = await _bomRepository.GetQueryableAsync();
        var allBoms = queryable
            .Where(b => b.CompanyId == companyId && b.IsActive)
            .ToList();

        // Process leaf BOMs first (no sub-BOMs), then work upward
        var processed = new HashSet<Guid>();
        var updatedCount = 0;

        // Pass 1: BOMs with no sub-assemblies (leaf level)
        var leafBoms = allBoms.Where(b => !b.Items.Any(i => i.SubBomId.HasValue)).ToList();
        foreach (var bom in leafBoms)
        {
            bom.RecalculateCost();
            await _bomRepository.UpdateAsync(bom);
            processed.Add(bom.Id);
            updatedCount++;
        }

        // Pass 2+: BOMs whose sub-BOMs are all already processed
        var remaining = allBoms.Where(b => !processed.Contains(b.Id)).ToList();
        var maxIterations = remaining.Count + 1; // prevent infinite loop
        while (remaining.Count > 0 && maxIterations-- > 0)
        {
            var readyBoms = remaining
                .Where(b => b.Items
                    .Where(i => i.SubBomId.HasValue)
                    .All(i => processed.Contains(i.SubBomId!.Value)))
                .ToList();

            if (readyBoms.Count == 0) break; // Remaining have unresolvable deps (cycle)

            foreach (var bom in readyBoms)
            {
                // Update sub-assembly rates from processed child BOMs
                foreach (var item in bom.Items.Where(i => i.SubBomId.HasValue))
                {
                    var childBom = allBoms.FirstOrDefault(b => b.Id == item.SubBomId!.Value);
                    if (childBom != null)
                    {
                        item.Rate = childBom.TotalCost / (childBom.Quantity > 0 ? childBom.Quantity : 1);
                        item.Recalculate();
                    }
                }

                bom.RecalculateCost();
                await _bomRepository.UpdateAsync(bom);
                processed.Add(bom.Id);
                updatedCount++;
            }

            remaining = remaining.Where(b => !processed.Contains(b.Id)).ToList();
        }

        return updatedCount;
    }

    private async Task<List<BillOfMaterials>> FindParentBomsAsync(Guid childBomId)
    {
        var queryable = await _bomRepository.GetQueryableAsync();
        return queryable
            .Where(b => b.IsActive && b.Items.Any(i => i.SubBomId == childBomId))
            .ToList();
    }

    private async Task<int> PropagateUpwardAsync(Guid bomId)
    {
        var parents = await FindParentBomsAsync(bomId);
        var count = 0;
        foreach (var parent in parents)
        {
            parent.RecalculateCost();
            await _bomRepository.UpdateAsync(parent);
            count++;
            count += await PropagateUpwardAsync(parent.Id);
        }
        return count;
    }
}
