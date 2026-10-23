using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
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
            // Update the sub-assembly item rate in the parent BOM (PR #59178: respects SetRateOfSubAssemblyItemBasedOnBom per item)
            foreach (var item in parentBom.Items.Where(i => i.SubBomId == bomId))
            {
                if (item.SetRateOfSubAssemblyItemBasedOnBom || item.IsPhantom)
                {
                    var unitCost = bom.UnitCost;
                    item.Rate = unitCost * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                    item.Recalculate();
                }
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
    /// Replaces every reference to <paramref name="currentBomId"/> as a sub-assembly (BomItem.SubBomId)
    /// across all parent BOMs with <paramref name="newBomId"/>, then recosts and propagates upward.
    /// Per ERPNext "BOM Update Tool" (Replace BOM): both BOMs must produce the same item.
    /// </summary>
    public async Task<(int UpdatedBomItemCount, int RecostedBomCount)> ReplaceBomAsync(Guid currentBomId, Guid newBomId)
    {
        if (currentBomId == newBomId)
            throw new BusinessException(MyERPDomainErrorCodes.BomReplaceSameBom);

        var currentBom = await _bomRepository.GetAsync(currentBomId);
        var newBom = await _bomRepository.GetAsync(newBomId);
        if (currentBom.ItemId != newBom.ItemId)
            throw new BusinessException(MyERPDomainErrorCodes.BomReplaceItemMismatch);

        // ERPNext PR #60549: replace BOM only with a submitted active BOM of same company
        if (!newBom.IsActive)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"New BOM {newBom.BomNumber} must be submitted and active.");
        }

        if (currentBom.CompanyId != newBom.CompanyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Current BOM and New BOM must have the same company.");
        }

        var parentBoms = await FindParentBomsAsync(currentBomId);

        // ERPNext PR #60549: refuse replacing BOM of parents that track semi-finished goods
        var semiFgParents = parentBoms.Where(p => p.TrackSemiFinishedGoods).ToList();
        if (semiFgParents.Count > 0)
        {
            var parentNames = string.Join(", ", semiFgParents.Select(p => p.BomNumber));
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"BOM {currentBom.BomNumber} is used in {parentNames}, which track semi-finished goods. Amend those BOMs to use the new BOM so their operation materials are fetched again.");
        }

        var newUnitCost = newBom.UnitCost;
        var updatedItemCount = 0;
        foreach (var parentBom in parentBoms)
        {
            foreach (var item in parentBom.Items.Where(i => i.SubBomId == currentBomId))
            {
                item.SubBomId = newBomId;
                if (item.SetRateOfSubAssemblyItemBasedOnBom || item.IsPhantom)
                {
                    item.Rate = newUnitCost * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                    item.Recalculate();
                }
                updatedItemCount++;
            }

            parentBom.RecalculateCost();
            await _bomRepository.UpdateAsync(parentBom);
        }

        // Propagate upward from the (now newly-linked) new BOM to recost every ancestor.
        var recostedCount = await UpdateCostAndPropagateAsync(newBomId);

        return (updatedItemCount, recostedCount);
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

        if (allBoms.Count == 0) return 0;

        var activeBomIds = allBoms.Select(b => b.Id).ToHashSet();

        // Process leaf BOMs first (no active sub-BOMs), then work upward
        var processed = new HashSet<Guid>();
        var updatedCount = 0;

        // Pass 1: BOMs with no active sub-assemblies (leaf level - ERPNext PR #60549)
        var leafBoms = allBoms.Where(b => !b.Items.Any(i => i.SubBomId.HasValue && activeBomIds.Contains(i.SubBomId.Value))).ToList();
        if (leafBoms.Count == 0)
        {
            // Complete update cost log when there are no leaf BOMs
            return 0;
        }

        foreach (var bom in leafBoms)
        {
            bom.RecalculateCost();
            await _bomRepository.UpdateAsync(bom);
            processed.Add(bom.Id);
            updatedCount++;
        }

        // Pass 2+: BOMs whose active sub-BOMs are all already processed
        var remaining = allBoms.Where(b => !processed.Contains(b.Id)).ToList();
        var maxIterations = remaining.Count + 1; // prevent infinite loop
        while (remaining.Count > 0 && maxIterations-- > 0)
        {
            var readyBoms = remaining
                .Where(b => b.Items
                    .Where(i => i.SubBomId.HasValue && activeBomIds.Contains(i.SubBomId.Value))
                    .All(i => processed.Contains(i.SubBomId!.Value)))
                .ToList();

            if (readyBoms.Count == 0) break; // Remaining have unresolvable deps (cycle)

            foreach (var bom in readyBoms)
            {
                // Update sub-assembly rates from processed child BOMs (PR #59178: respects SetRateOfSubAssemblyItemBasedOnBom per item)
                foreach (var item in bom.Items.Where(i => i.SubBomId.HasValue && activeBomIds.Contains(i.SubBomId.Value)))
                {
                    if (item.SetRateOfSubAssemblyItemBasedOnBom || item.IsPhantom)
                    {
                        var childBom = allBoms.FirstOrDefault(b => b.Id == item.SubBomId!.Value);
                        if (childBom != null)
                        {
                            var unitCost = childBom.UnitCost;
                            item.Rate = unitCost * (item.ConversionFactor > 0 ? item.ConversionFactor : 1m);
                            item.Recalculate();
                        }
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
