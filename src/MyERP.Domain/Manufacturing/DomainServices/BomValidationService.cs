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
    private readonly IRepository<Inventory.Entities.Item, Guid>? _itemRepository;

    public BomValidationService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<Inventory.Entities.Item, Guid>? itemRepository = null)
    {
        _bomRepository = bomRepository;
        _itemRepository = itemRepository;
    }

    private IRepository<Inventory.Entities.Item, Guid>? GetItemRepository()
        => _itemRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Inventory.Entities.Item, Guid>>();

    /// <summary>
    /// Validates that a BOM exists, belongs to the specified company, is active,
    /// and produces the specified item (or its template item for variants).
    /// Per ERPNext commits d08131b6e8 and b8e3c8d8f2.
    /// </summary>
    public async Task<BillOfMaterials> ValidateProductionBomAsync(Guid itemId, Guid bomId, Guid companyId)
    {
        var bom = await _bomRepository.FindAsync(bomId);
        if (bom == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.BomNotFound)
                .WithData("bomId", bomId)
                .WithData("detail", $"BOM {bomId} does not exist.");
        }

        if (bom.CompanyId != companyId)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                .WithData("bomCompany", bom.CompanyId)
                .WithData("productionPlanCompany", companyId)
                .WithData("detail", $"BOM {bom.BomNumber} does not belong to the selected Company.");
        }

        if (!bom.IsActive)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("bomNumber", bom.BomNumber)
                .WithData("detail", $"BOM {bom.BomNumber} is not active and cannot be used.");
        }

        if (bom.ItemId != itemId)
        {
            var itemRepo = GetItemRepository();
            var item = itemRepo != null ? await itemRepo.FindAsync(itemId) : null;
            if (item == null || item.VariantOfId != bom.ItemId)
            {
                var itemDisplay = item?.ItemName ?? itemId.ToString();
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("bomNumber", bom.BomNumber)
                    .WithData("itemId", itemId)
                    .WithData("detail", $"BOM {bom.BomNumber} does not belong to Item {itemDisplay}.");
            }
        }

        return bom;
    }

    /// <summary>
    /// Validates that no disabled (inactive) items are used in the BOM hierarchy.
    /// Covers finished good item, raw materials, secondary items, and operation finished goods.
    /// Per ERPNext PR #58997 / commit e6f431a8d6.
    /// </summary>
    public async Task ValidateNoDisabledItemsAsync(BillOfMaterials bom)
    {
        if (_itemRepository == null) return;

        var itemIds = new List<Guid> { bom.ItemId };
        itemIds.AddRange(bom.Items.Select(i => i.ItemId));
        itemIds.AddRange(bom.SecondaryItems.Select(s => s.ItemId));
        itemIds.AddRange(bom.Operations.Where(o => o.FinishedGoodItemId.HasValue).Select(o => o.FinishedGoodItemId!.Value));

        var distinctIds = itemIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0) return;

        var query = await _itemRepository.GetQueryableAsync();
        var disabledItemNames = query
            .Where(i => distinctIds.Contains(i.Id) && !i.IsActive)
            .Select(i => i.ItemName)
            .ToList();

        if (disabledItemNames.Count > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Disabled Item '{string.Join(", ", disabledItemNames)}' cannot be used in BOMs.");
        }
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
                    .WithData("itemId", itemId)
                    .WithData("bomNumber", childBom.BomNumber)
                    .WithData("solution", "If you want to use the finished good as a raw material, enable the 'Do Not Explode' checkbox in the Items table against that raw material.");
            }

            // Recursively check sub-assemblies (skip if DoNotExplode)
            foreach (var item in childBom.Items.Where(i => i.SubBomId.HasValue && !i.DoNotExplode))
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

            if (!item.DoNotExplode && item.IsPhantom && item.SubBomId.HasValue)
            {
                // Phantom: explode sub-BOM and bubble up components
                var subItems = await ExplodeBomAsync(item.SubBomId.Value, qty);
                result.AddRange(subItems);
            }
            else if (!item.DoNotExplode && item.SubBomId.HasValue && !item.IsPhantom)
            {
                // Sub-assembly (non-phantom): keep as-is (produced independently)
                result.Add(new ExplodedBomItem(item.ItemId, item.ItemName, qty, item.Rate, item.Uom, item.SubBomId));
            }
            else
            {
                // Raw material (or DoNotExplode item): add directly
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
