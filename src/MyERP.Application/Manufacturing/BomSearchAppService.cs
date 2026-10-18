using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// BOM Search — find BOMs and Product Bundles containing a given set of raw materials / component items.
/// Supports deep recursive search through exploded sub-assemblies.
/// Maps to ERPNext stock/report/bom_search/bom_search.py.
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class BomSearchAppService : ApplicationService, IBomSearchAppService
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<ProductBundle, Guid> _bundleRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public BomSearchAppService(
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<ProductBundle, Guid> bundleRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _bomRepository = bomRepository;
        _bundleRepository = bundleRepository;
        _itemRepository = itemRepository;
        _companyRepository = companyRepository;
    }

    public async Task<BomSearchReportDto> SearchAsync(BomSearchFilterDto input)
    {
        // 1. Gather all required search item IDs
        var searchItemIds = new HashSet<Guid>();
        if (input.Item1Id.HasValue && input.Item1Id.Value != Guid.Empty) searchItemIds.Add(input.Item1Id.Value);
        if (input.Item2Id.HasValue && input.Item2Id.Value != Guid.Empty) searchItemIds.Add(input.Item2Id.Value);
        if (input.Item3Id.HasValue && input.Item3Id.Value != Guid.Empty) searchItemIds.Add(input.Item3Id.Value);
        if (input.Item4Id.HasValue && input.Item4Id.Value != Guid.Empty) searchItemIds.Add(input.Item4Id.Value);
        if (input.Item5Id.HasValue && input.Item5Id.Value != Guid.Empty) searchItemIds.Add(input.Item5Id.Value);
        if (input.ItemIds != null)
        {
            foreach (var id in input.ItemIds)
            {
                if (id != Guid.Empty) searchItemIds.Add(id);
            }
        }

        // If no items specified, return empty report
        if (searchItemIds.Count == 0)
        {
            return new BomSearchReportDto();
        }

        var resultRows = new List<BomSearchRowDto>();

        // Preload items for metadata
        var allItems = await _itemRepository.GetListAsync();
        var itemsById = allItems.ToDictionary(i => i.Id, i => i);

        // Preload companies
        var companies = await _companyRepository.GetListAsync();
        var companiesById = companies.ToDictionary(c => c.Id, c => c.Name);

        // 2. Search Product Bundles
        var bundleQuery = await _bundleRepository.WithDetailsAsync();
        var allBundles = bundleQuery.ToList();

        foreach (var bundle in allBundles)
        {
            if (bundle.IsDisabled) continue;

            var componentItemIds = bundle.Items.Select(i => i.ComponentItemId).ToHashSet();
            if (searchItemIds.All(id => componentItemIds.Contains(id)))
            {
                itemsById.TryGetValue(bundle.ItemId, out var parentItem);

                var componentSummary = string.Join(", ", bundle.Items
                    .Select(bi => itemsById.TryGetValue(bi.ComponentItemId, out var ci) ? ci.ItemCode : bi.ComponentItemId.ToString()[..8]));

                resultRows.Add(new BomSearchRowDto
                {
                    ParentId = bundle.Id,
                    ParentName = parentItem?.ItemCode ?? bundle.ItemName ?? "Product Bundle",
                    DocType = "Product Bundle",
                    ItemId = bundle.ItemId,
                    ItemCode = parentItem?.ItemCode ?? string.Empty,
                    ItemName = parentItem?.ItemName ?? bundle.ItemName ?? string.Empty,
                    Quantity = 1m,
                    Uom = parentItem?.Uom ?? "Unit",
                    CompanyId = null,
                    CompanyName = null,
                    IsActive = bundle.IsActive,
                    IsDefault = false,
                    TotalCost = null,
                    FoundInSubAssembly = false,
                    ComponentItemsSummary = componentSummary
                });
            }
        }

        // 3. Search Bill of Materials (BOM)
        var bomQuery = await _bomRepository.WithDetailsAsync(b => b.Items);
        var bomsList = bomQuery.ToList();

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            bomsList = bomsList.Where(b => b.CompanyId == input.CompanyId.Value).ToList();
        }

        var allBomsById = bomsList.ToDictionary(b => b.Id, b => b);
        var defaultBomsByItemId = bomsList.Where(b => b.IsDefault).ToDictionary(b => b.ItemId, b => b);

        foreach (var bom in bomsList)
        {
            if (!bom.IsActive) continue;

            var directItemIds = bom.Items.Select(i => i.ItemId).ToHashSet();
            HashSet<Guid> matchPool;
            bool foundInSubAssembly = false;

            if (input.SearchSubAssemblies)
            {
                var explodedItemIds = new HashSet<Guid>();
                var visited = new HashSet<Guid>();
                CollectExplodedItemIds(bom, allBomsById, defaultBomsByItemId, explodedItemIds, visited);
                matchPool = explodedItemIds;
                foundInSubAssembly = !directItemIds.IsSupersetOf(searchItemIds);
            }
            else
            {
                matchPool = directItemIds;
                foundInSubAssembly = false;
            }

            if (searchItemIds.All(id => matchPool.Contains(id)))
            {
                itemsById.TryGetValue(bom.ItemId, out var fgItem);
                companiesById.TryGetValue(bom.CompanyId, out var companyName);

                var componentSummary = string.Join(", ", bom.Items
                    .Select(bi => itemsById.TryGetValue(bi.ItemId, out var ci) ? ci.ItemCode : bi.ItemId.ToString()[..8]));

                resultRows.Add(new BomSearchRowDto
                {
                    ParentId = bom.Id,
                    ParentName = bom.BomNumber,
                    DocType = "BOM",
                    ItemId = bom.ItemId,
                    ItemCode = fgItem?.ItemCode ?? string.Empty,
                    ItemName = fgItem?.ItemName ?? string.Empty,
                    Quantity = bom.Quantity,
                    Uom = bom.Uom ?? fgItem?.Uom ?? "Unit",
                    CompanyId = bom.CompanyId,
                    CompanyName = companyName,
                    IsActive = bom.IsActive,
                    IsDefault = bom.IsDefault,
                    TotalCost = bom.TotalCost,
                    FoundInSubAssembly = foundInSubAssembly,
                    ComponentItemsSummary = componentSummary
                });
            }
        }

        // 4. Optional text search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim().ToLowerInvariant();
            resultRows = resultRows.Where(r =>
                r.ParentName.ToLowerInvariant().Contains(term) ||
                r.ItemCode.ToLowerInvariant().Contains(term) ||
                r.ItemName.ToLowerInvariant().Contains(term) ||
                r.ComponentItemsSummary.ToLowerInvariant().Contains(term) ||
                (r.CompanyName != null && r.CompanyName.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        return new BomSearchReportDto
        {
            TotalMatches = resultRows.Count,
            BomMatchesCount = resultRows.Count(r => r.DocType == "BOM"),
            ProductBundleMatchesCount = resultRows.Count(r => r.DocType == "Product Bundle"),
            Rows = resultRows.OrderBy(r => r.DocType).ThenBy(r => r.ParentName).ToList()
        };
    }

    private void CollectExplodedItemIds(
        BillOfMaterials bom,
        Dictionary<Guid, BillOfMaterials> allBomsById,
        Dictionary<Guid, BillOfMaterials> defaultBomsByItemId,
        HashSet<Guid> collectedItemIds,
        HashSet<Guid> visitedBomIds)
    {
        if (!visitedBomIds.Add(bom.Id)) return;

        foreach (var item in bom.Items)
        {
            collectedItemIds.Add(item.ItemId);

            if (!item.DoNotExplode)
            {
                BillOfMaterials? subBom = null;
                if (item.SubBomId.HasValue && allBomsById.TryGetValue(item.SubBomId.Value, out var sb))
                {
                    subBom = sb;
                }
                else if (defaultBomsByItemId.TryGetValue(item.ItemId, out var db))
                {
                    subBom = db;
                }

                if (subBom != null)
                {
                    CollectExplodedItemIds(subBom, allBomsById, defaultBomsByItemId, collectedItemIds, visitedBomIds);
                }
            }
        }
    }
}
