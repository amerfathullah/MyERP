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
/// Converts a BOM Creator staging tree into real Bills of Materials in one pass.
/// Walks the tree top-down but resolves children first (post-order) so that a sub-assembly's
/// BOM exists before its parent's BomItem.SubBomId is set.
///
/// Per ERPNext (bom_creator.py create_bom / _create_bom): one BOM is created per item that is
/// either the top-level finished good or marked is_expandable, using only the rows whose
/// FgItemId equals that item.
///
/// Source: erpnext/manufacturing/doctype/bom_creator/bom_creator.py
/// </summary>
public class BomCreatorService : DomainService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;

    public BomCreatorService(IRepository<BillOfMaterials, Guid> bomRepository)
    {
        _bomRepository = bomRepository;
    }

    /// <summary>Generates the full BOM tree from a draft BOM Creator. Marks it Completed or Failed.</summary>
    public async Task CreateBomsAsync(BomCreator creator, Func<Guid, Task<string>> generateBomNumberAsync)
    {
        creator.Validate();

        var createdBomIds = new Dictionary<Guid, Guid>();
        try
        {
            await CreateBomForItemAsync(creator, creator.FinishedGoodItemId, creator.Qty, creator.Uom,
                creator.RoutingId, createdBomIds, generateBomNumberAsync, new HashSet<Guid> { creator.FinishedGoodItemId });
            creator.RecalculateCost();
            creator.MarkCompleted();
        }
        catch (Exception ex)
        {
            creator.MarkFailed(ex.Message);
            throw;
        }
    }

    private async Task<Guid> CreateBomForItemAsync(BomCreator creator, Guid fgItemId, decimal qty, string? uom,
        Guid? routingId, Dictionary<Guid, Guid> createdBomIds, Func<Guid, Task<string>> generateBomNumberAsync,
        HashSet<Guid> ancestorItemIds)
    {
        if (createdBomIds.TryGetValue(fgItemId, out var existingBomId))
            return existingBomId;

        var children = creator.Items.Where(i => i.FgItemId == fgItemId).ToList();

        var bomNumber = await generateBomNumberAsync(fgItemId);
        var bom = new BillOfMaterials(Guid.NewGuid(), creator.CompanyId, bomNumber, fgItemId, creator.TenantId)
        {
            Quantity = qty,
            Uom = uom,
            RoutingId = routingId,
            IsDefault = true,
        };

        foreach (var child in children)
        {
            Guid? subBomId = null;
            if (child.IsExpandable)
            {
                // Guard against a cyclic staging tree (item A expandable into B expandable back
                // into A) — without this, recursion never terminates and crashes the process with
                // a StackOverflowException instead of a catchable BusinessException.
                if (!ancestorItemIds.Add(child.ItemId))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.BomCycleDetected)
                        .WithData("itemId", child.ItemId);
                }

                // Sub-assemblies are recipes for 1 unit; the parent BomItem.Qty carries the consumed quantity.
                subBomId = await CreateBomForItemAsync(creator, child.ItemId, 1m, child.Uom,
                    null, createdBomIds, generateBomNumberAsync, ancestorItemIds);

                ancestorItemIds.Remove(child.ItemId);
            }

            var bomItem = new BomItem(Guid.NewGuid(), bom.Id, child.ItemId, child.ItemName,
                child.Qty, child.Rate, child.Uom, child.ConversionFactor, child.StockUom)
            {
                SubBomId = subBomId,
                IsPhantom = child.IsPhantomItem,
            };
            bom.Items.Add(bomItem);
            child.BomCreated = true;
        }

        bom.RecalculateCost();
        await _bomRepository.InsertAsync(bom);
        createdBomIds[fgItemId] = bom.Id;
        return bom.Id;
    }
}
