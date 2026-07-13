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

        foreach (var item in bom.Items)
        {
            var qty = item.Quantity * multiplier;

            if (item.IsPhantom && item.SubBomId.HasValue)
            {
                // Phantom: explode sub-BOM and bubble up components
                var subItems = await ExplodeBomAsync(item.SubBomId.Value, qty / bom.Quantity);
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

        // Aggregate same items (per DO-NOT: use Min(IsPhantom) in GROUP BY)
        return result
            .GroupBy(x => x.ItemId)
            .Select(g => new ExplodedBomItem(
                g.Key,
                g.First().ItemName,
                g.Sum(x => x.Quantity),
                g.Max(x => x.Rate),
                g.First().Uom,
                g.First().SubBomId))
            .ToList();
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
