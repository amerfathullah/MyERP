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
/// Validates BOM integrity (cycle detection) and provides phantom item explosion.
/// </summary>
public class BomValidationService : DomainService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;

    public BomValidationService(IRepository<BillOfMaterials, Guid> bomRepository)
    {
        _bomRepository = bomRepository;
    }

    /// <summary>
    /// Detects circular references in BOM hierarchy.
    /// Throws if adding a sub-BOM would create a cycle (Item A → Item B → Item A).
    /// </summary>
    public async Task ValidateNoCycleAsync(Guid bomId, Guid subBomItemId)
    {
        var visited = new HashSet<Guid> { bomId };
        await DetectCycleRecursiveAsync(subBomItemId, visited);
    }

    private async Task DetectCycleRecursiveAsync(Guid itemId, HashSet<Guid> visited)
    {
        // Find all active BOMs that produce this item
        var queryable = await _bomRepository.GetQueryableAsync();
        var childBoms = queryable
            .Where(b => b.ItemId == itemId && b.IsActive)
            .ToList();

        foreach (var childBom in childBoms)
        {
            if (!visited.Add(childBom.Id))
            {
                throw new BusinessException(MyERPDomainErrorCodes.BomCycleDetected)
                    .WithData("itemId", itemId);
            }

            // Recursively check sub-assemblies
            foreach (var item in childBom.Items.Where(i => i.SubBomId.HasValue))
            {
                await DetectCycleRecursiveAsync(item.ItemId, visited);
            }

            visited.Remove(childBom.Id);
        }
    }

    /// <summary>
    /// Explodes a BOM recursively, replacing phantom items with their components.
    /// Returns a flat list of real materials needed.
    /// </summary>
    public async Task<List<ExplodedBomItem>> ExplodeBomAsync(Guid bomId, decimal multiplier = 1m)
    {
        var result = new List<ExplodedBomItem>();
        var bom = await _bomRepository.GetAsync(bomId);

        // Validate routing sequence before explosion (catches invalid BOMs early)
        ValidateOperationsSequence(bom);

        foreach (var item in bom.Items)
        {
            var bomOutputQty = bom.Quantity > 0 ? bom.Quantity : 1m;
            var qty = (item.StockQty / bomOutputQty) * multiplier;

            if (item.IsPhantom && item.SubBomId.HasValue)
            {
                // Phantom: explode sub-BOM and bubble up components
                var subItems = await ExplodeBomAsync(item.SubBomId.Value, qty);
                result.AddRange(subItems);
            }
            else if (item.SubBomId.HasValue && !item.IsPhantom)
            {
                // Sub-assembly (non-phantom): keep as-is (produced independently)
                result.Add(new ExplodedBomItem(item.ItemId, item.ItemName, qty, item.Rate, item.Uom, item.SubBomId));
            }
            else
            {
                // Raw material: add directly
                result.Add(new ExplodedBomItem(item.ItemId, item.ItemName, qty, item.Rate, item.Uom, null));
            }
        }

        // Aggregate same items (per DO-NOT: use Min(IsPhantom) in GROUP BY; per PR #57708: weighted rate)
        return result
            .GroupBy(x => x.ItemId)
            .Select(g =>
            {
                var totalQty = g.Sum(x => x.Quantity);
                var totalCost = g.Sum(x => x.Quantity * x.Rate);
                var rate = totalQty > 0 ? totalCost / totalQty : g.First().Rate;
                return new ExplodedBomItem(
                    g.Key,
                    g.First().ItemName,
                    totalQty,
                    rate,
                    g.First().Uom,
                    g.First().SubBomId);
            })
            .ToList();
    }
    /// <summary>
    /// Validates that BOM operations have monotonically increasing sequence IDs.
    /// Per DO-NOT: "Allow routing sequence_id to decrease between rows (must be monotonically increasing)"
    /// Also validates no duplicate sequence IDs exist (parallel ops share same sequence_id,
    /// but sequential must always increase).
    /// </summary>
    public static void ValidateOperationsSequence(BillOfMaterials bom)
    {
        if (bom.Operations.Count <= 1) return;

        var sortedOps = bom.Operations.OrderBy(o => o.SequenceId).ToList();
        for (int i = 1; i < sortedOps.Count; i++)
        {
            // Per ERPNext: sequence_id must be >= previous (not strictly >; same = parallel)
            if (sortedOps[i].SequenceId < sortedOps[i - 1].SequenceId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.BomOperationSequenceOutOfOrder)
                    .WithData("operation", sortedOps[i].OperationId)
                    .WithData("sequenceId", sortedOps[i].SequenceId)
                    .WithData("previousSequenceId", sortedOps[i - 1].SequenceId);
            }
        }
    }
}

/// <summary>
/// Result of BOM explosion — a flat material requirement.
/// </summary>
public record ExplodedBomItem(
    Guid ItemId,
    string ItemName,
    decimal Quantity,
    decimal Rate,
    string? Uom,
    Guid? SubBomId);
