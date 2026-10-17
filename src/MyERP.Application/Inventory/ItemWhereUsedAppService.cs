using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Item Where Used application service.
/// Identifies where an Item is consumed as a component or referenced across the system
/// (BOM components & scrap, Product Bundles, Subcontracting BOMs, Item Variants, Item Alternatives).
/// Maps to ERPNext stock/report/item_where_used/item_where_used.py.
/// </summary>
[Authorize(MyERPPermissions.Items.Default)]
public class ItemWhereUsedAppService : ApplicationService, IItemWhereUsedAppService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly IRepository<ProductBundle, Guid> _productBundleRepository;
    private readonly IRepository<SubcontractingBom, Guid> _subcontractingBomRepository;
    private readonly IRepository<ItemAlternative, Guid> _itemAlternativeRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public ItemWhereUsedAppService(
        IRepository<Item, Guid> itemRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        IRepository<ProductBundle, Guid> productBundleRepository,
        IRepository<SubcontractingBom, Guid> subcontractingBomRepository,
        IRepository<ItemAlternative, Guid> itemAlternativeRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _itemRepository = itemRepository;
        _bomRepository = bomRepository;
        _productBundleRepository = productBundleRepository;
        _subcontractingBomRepository = subcontractingBomRepository;
        _itemAlternativeRepository = itemAlternativeRepository;
        _companyRepository = companyRepository;
    }

    public async Task<ItemWhereUsedReportDto> GetReportAsync(ItemWhereUsedRequestDto input)
    {
        var targetItem = await _itemRepository.FindAsync(input.ItemId);
        if (targetItem == null)
        {
            return new ItemWhereUsedReportDto
            {
                ItemId = input.ItemId,
                ItemCode = string.Empty,
                ItemName = string.Empty
            };
        }

        var itemQuery = await _itemRepository.GetQueryableAsync();
        var allItems = itemQuery.Select(i => new { i.Id, i.ItemCode, i.ItemName, i.VariantOfId, i.IsActive, i.Description }).ToList();
        var itemDict = allItems.ToDictionary(i => i.Id);

        var companyQuery = await _companyRepository.GetQueryableAsync();
        var companyDict = companyQuery.Select(c => new { c.Id, c.Name }).ToList().ToDictionary(c => c.Id, c => c.Name);

        var rows = new List<ItemWhereUsedRowDto>();

        // 1. BOM Components & BOM Secondary Items (Where Used)
        var bomQuery = await _bomRepository.WithDetailsAsync(b => b.Items, b => b.SecondaryItems);
        var boms = bomQuery.ToList();
        if (input.CompanyId.HasValue)
        {
            boms = boms.Where(b => b.CompanyId == input.CompanyId.Value).ToList();
        }

        foreach (var bom in boms)
        {
            var relatedItem = itemDict.TryGetValue(bom.ItemId, out var rItem) ? rItem : null;
            var companyName = companyDict.TryGetValue(bom.CompanyId, out var cName) ? cName : null;

            // BOM Items
            foreach (var bomItem in bom.Items.Where(bi => bi.ItemId == input.ItemId))
            {
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "Where Used",
                    ReferenceType = "BOM Component",
                    DocumentType = "BOM",
                    DocumentName = bom.BomNumber,
                    DocumentId = bom.Id,
                    RelatedItemId = bom.ItemId,
                    RelatedItemCode = relatedItem?.ItemCode,
                    RelatedItemName = relatedItem?.ItemName,
                    Quantity = bomItem.Quantity,
                    Uom = bomItem.Uom,
                    StockQuantity = bomItem.StockQty,
                    StockUom = bomItem.StockUom,
                    CompanyId = bom.CompanyId,
                    CompanyName = companyName,
                    IsDefault = bom.IsDefault,
                    IsActive = bom.IsActive,
                    Details = bomItem.SubBomId.HasValue ? "Has Sub-BOM" : null
                });
            }

            // BOM Secondary Items
            foreach (var secItem in bom.SecondaryItems.Where(si => si.ItemId == input.ItemId))
            {
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "Where Used",
                    ReferenceType = "BOM Secondary Item",
                    DocumentType = "BOM",
                    DocumentName = bom.BomNumber,
                    DocumentId = bom.Id,
                    RelatedItemId = bom.ItemId,
                    RelatedItemCode = relatedItem?.ItemCode,
                    RelatedItemName = relatedItem?.ItemName,
                    Quantity = secItem.Quantity,
                    Uom = secItem.StockUom,
                    StockQuantity = secItem.Quantity,
                    StockUom = secItem.StockUom,
                    CompanyId = bom.CompanyId,
                    CompanyName = companyName,
                    IsDefault = bom.IsDefault,
                    IsActive = bom.IsActive,
                    Details = secItem.SecondaryItemType.ToString()
                });
            }
        }

        // 2. Product Bundle Components (Where Used)
        var bundleQuery = await _productBundleRepository.WithDetailsAsync(b => b.Items);
        var bundles = bundleQuery.ToList();
        foreach (var bundle in bundles)
        {
            var parentItem = itemDict.TryGetValue(bundle.ItemId, out var pItem) ? pItem : null;
            foreach (var bundleItem in bundle.Items.Where(bi => bi.ComponentItemId == input.ItemId))
            {
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "Where Used",
                    ReferenceType = "Product Bundle Component",
                    DocumentType = "Product Bundle",
                    DocumentName = bundle.ItemName ?? parentItem?.ItemName ?? bundle.Id.ToString(),
                    DocumentId = bundle.Id,
                    RelatedItemId = bundle.ItemId,
                    RelatedItemCode = parentItem?.ItemCode,
                    RelatedItemName = parentItem?.ItemName,
                    Quantity = bundleItem.Qty,
                    Uom = bundleItem.Uom,
                    StockQuantity = bundleItem.Qty,
                    StockUom = bundleItem.Uom,
                    IsActive = bundle.IsActive && !bundle.IsDisabled,
                    Details = bundle.Description
                });
            }
        }

        // 3. Subcontracting BOMs (Where Used)
        var scQuery = await _subcontractingBomRepository.GetQueryableAsync();
        var scBoms = scQuery.ToList();
        foreach (var sc in scBoms)
        {
            if (sc.ServiceItemId == input.ItemId)
            {
                var fgItem = itemDict.TryGetValue(sc.FinishedGoodId, out var fg) ? fg : null;
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "Where Used",
                    ReferenceType = "Subcontracting Service Item",
                    DocumentType = "Subcontracting BOM",
                    DocumentName = $"SC-BOM-{sc.Id.ToString()[..8]}",
                    DocumentId = sc.Id,
                    RelatedItemId = sc.FinishedGoodId,
                    RelatedItemCode = fg?.ItemCode,
                    RelatedItemName = fg?.ItemName,
                    Quantity = sc.ServiceItemQty,
                    Uom = sc.ServiceItemUom,
                    IsActive = sc.IsActive
                });
            }

            if (sc.FinishedGoodId == input.ItemId)
            {
                var svcItem = itemDict.TryGetValue(sc.ServiceItemId, out var svc) ? svc : null;
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "Where Used",
                    ReferenceType = "Subcontracting Finished Good",
                    DocumentType = "Subcontracting BOM",
                    DocumentName = $"SC-BOM-{sc.Id.ToString()[..8]}",
                    DocumentId = sc.Id,
                    RelatedItemId = sc.ServiceItemId,
                    RelatedItemCode = svc?.ItemCode,
                    RelatedItemName = svc?.ItemName,
                    Quantity = sc.FinishedGoodQty,
                    Uom = sc.FinishedGoodUom,
                    IsActive = sc.IsActive
                });
            }
        }

        // 4. BOM Outputs (References)
        foreach (var bom in boms.Where(b => b.ItemId == input.ItemId))
        {
            var companyName = companyDict.TryGetValue(bom.CompanyId, out var cName) ? cName : null;
            rows.Add(new ItemWhereUsedRowDto
            {
                Section = "References",
                ReferenceType = "BOM Output",
                DocumentType = "BOM",
                DocumentName = bom.BomNumber,
                DocumentId = bom.Id,
                RelatedItemId = targetItem.Id,
                RelatedItemCode = targetItem.ItemCode,
                RelatedItemName = targetItem.ItemName,
                Quantity = bom.Quantity,
                Uom = bom.Uom,
                CompanyId = bom.CompanyId,
                CompanyName = companyName,
                IsDefault = bom.IsDefault,
                IsActive = bom.IsActive
            });
        }

        // 5. Product Bundle Parent (References)
        foreach (var bundle in bundles.Where(b => b.ItemId == input.ItemId))
        {
            rows.Add(new ItemWhereUsedRowDto
            {
                Section = "References",
                ReferenceType = "Product Bundle Parent",
                DocumentType = "Product Bundle",
                DocumentName = bundle.ItemName ?? targetItem.ItemName,
                DocumentId = bundle.Id,
                RelatedItemId = targetItem.Id,
                RelatedItemCode = targetItem.ItemCode,
                RelatedItemName = targetItem.ItemName,
                IsActive = bundle.IsActive && !bundle.IsDisabled,
                Details = $"{bundle.Items.Count} component items"
            });
        }

        // 6. Item Variants (References)
        foreach (var v in allItems.Where(i => i.VariantOfId == input.ItemId))
        {
            rows.Add(new ItemWhereUsedRowDto
            {
                Section = "References",
                ReferenceType = "Item Variant",
                DocumentType = "Item",
                DocumentName = v.ItemCode,
                DocumentId = v.Id,
                RelatedItemId = v.Id,
                RelatedItemCode = v.ItemCode,
                RelatedItemName = v.ItemName,
                IsActive = v.IsActive,
                Details = v.Description
            });
        }

        // 7. Item Alternatives (References)
        var altQuery = await _itemAlternativeRepository.GetQueryableAsync();
        var alts = altQuery.ToList();
        if (input.CompanyId.HasValue)
        {
            alts = alts.Where(a => a.CompanyId == input.CompanyId.Value).ToList();
        }

        foreach (var alt in alts)
        {
            if (alt.ItemId == input.ItemId)
            {
                var altItem = itemDict.TryGetValue(alt.AlternativeItemId, out var ai) ? ai : null;
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "References",
                    ReferenceType = "Item Alternative",
                    DocumentType = "Item Alternative",
                    DocumentName = $"ALT-{alt.Id.ToString()[..8]}",
                    DocumentId = alt.Id,
                    RelatedItemId = alt.AlternativeItemId,
                    RelatedItemCode = altItem?.ItemCode,
                    RelatedItemName = altItem?.ItemName,
                    Details = alt.TwoWay ? "Two-Way Alternative" : "One-Way Alternative"
                });
            }

            if (alt.AlternativeItemId == input.ItemId)
            {
                var baseItem = itemDict.TryGetValue(alt.ItemId, out var bi) ? bi : null;
                rows.Add(new ItemWhereUsedRowDto
                {
                    Section = "References",
                    ReferenceType = "Alternative For Item",
                    DocumentType = "Item Alternative",
                    DocumentName = $"ALT-{alt.Id.ToString()[..8]}",
                    DocumentId = alt.Id,
                    RelatedItemId = alt.ItemId,
                    RelatedItemCode = baseItem?.ItemCode,
                    RelatedItemName = baseItem?.ItemName,
                    Details = alt.TwoWay ? "Two-Way Alternative" : "One-Way Alternative"
                });
            }
        }

        var totalWhereUsed = rows.Count(r => r.Section == "Where Used");
        var totalReferences = rows.Count(r => r.Section == "References");

        if (!string.IsNullOrWhiteSpace(input.Section))
        {
            rows = rows.Where(r => r.Section.Equals(input.Section.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new ItemWhereUsedReportDto
        {
            ItemId = targetItem.Id,
            ItemCode = targetItem.ItemCode,
            ItemName = targetItem.ItemName,
            Rows = rows,
            TotalWhereUsedCount = totalWhereUsed,
            TotalReferencesCount = totalReferences
        };
    }
}
