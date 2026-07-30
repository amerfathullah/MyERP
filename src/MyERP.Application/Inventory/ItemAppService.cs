using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace MyERP.Inventory;

public class ItemAppService :
    CrudAppService<
        Item,
        ItemDto,
        Guid,
        GetItemListDto,
        CreateUpdateItemDto>,
    IItemAppService
{
    public ItemAppService(IRepository<Item, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Items.Default;
        GetListPolicyName = MyERPPermissions.Items.Default;
        CreatePolicyName = MyERPPermissions.Items.Create;
        UpdatePolicyName = MyERPPermissions.Items.Edit;
        DeletePolicyName = MyERPPermissions.Items.Delete;
    }

    /// <summary>
    /// Override UpdateAsync to prevent deactivation of items in active orders.
    /// Per DO-NOT: "Show disabled/variant-template/expired items in link field queries"
    /// </summary>
    public override async Task<ItemDto> UpdateAsync(Guid id, CreateUpdateItemDto input)
    {
        var existing = await Repository.GetAsync(id);

        // If deactivating (was active → now inactive), validate no active orders use this item
        if (existing.IsActive && !input.IsActive)
        {
            await ValidateCanDeactivateAsync(id);
        }

        return await base.UpdateAsync(id, input);
    }

    private async Task ValidateCanDeactivateAsync(Guid itemId)
    {
        // Check SO items in active status (not Draft/Cancelled/Completed)
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();
        var hasActiveSO = soQuery.Any(so =>
            so.Items.Any(i => i.ItemId == itemId)
            && so.Status != DocumentStatus.Draft
            && so.Status != DocumentStatus.Cancelled
            && so.Status != DocumentStatus.Completed);

        if (hasActiveSO)
        {
            throw new BusinessException("MyERP:05017")
                .WithData("itemId", itemId)
                .WithData("reason", "Item is used in active Sales Orders.");
        }

        // Check PO items in active status
        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var poQuery = await poRepo.GetQueryableAsync();
        var hasActivePO = poQuery.Any(po =>
            po.Items.Any(i => i.ItemId == itemId)
            && po.Status != DocumentStatus.Draft
            && po.Status != DocumentStatus.Cancelled
            && po.Status != DocumentStatus.Completed);

        if (hasActivePO)
        {
            throw new BusinessException("MyERP:05017")
                .WithData("itemId", itemId)
                .WithData("reason", "Item is used in active Purchase Orders.");
        }
    }

    /// <summary>
    /// Override DeleteAsync to prevent deletion of items with stock ledger entries.
    /// Items with any stock history cannot be deleted (only deactivated).
    /// </summary>
    public override async Task DeleteAsync(Guid id)
    {
        // Check for stock ledger entries
        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQuery = await sleRepo.GetQueryableAsync();
        var hasStockHistory = sleQuery.Any(s => s.ItemId == id);

        if (hasStockHistory)
        {
            throw new BusinessException("MyERP:05018")
                .WithData("itemId", id)
                .WithData("reason", "Item has stock ledger history. Deactivate instead of deleting.");
        }

        // Check for active orders (same as deactivation)
        await ValidateCanDeactivateAsync(id);

        await base.DeleteAsync(id);
    }

    public override async Task<PagedResultDto<ItemDto>> GetListAsync(GetItemListDto input)
    {
        var filter = input.Filter;
        var companyId = input.CompanyId;

        var queryable = await Repository.GetQueryableAsync();

        // Filter out restricted items that don't allow the specified company
        if (companyId.HasValue)
        {
            var restrictionRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.CompanyRestrictionEntry, Guid>>();
            var restrictionQuery = await restrictionRepo.GetQueryableAsync();
            var allowedItemIds = restrictionQuery
                .Where(r => r.ParentType == "Item" && r.CompanyId == companyId.Value)
                .Select(r => r.ParentId)
                .ToList();

            queryable = queryable.Where(i =>
                !i.RestrictToCompanies || allowedItemIds.Contains(i.Id));
        }

        if (companyId.HasValue)
        {
            queryable = queryable.Where(i => i.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queryable = queryable.Where(i =>
                i.ItemCode.Contains(filter)
                || i.ItemName.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.ItemType) && Enum.TryParse<ItemType>(input.ItemType, true, out var itemType))
        {
            queryable = queryable.Where(i => i.ItemType == itemType);
        }

        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(i => i.ItemName)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = items.Select(ObjectMapper.Map<Item, ItemDto>).ToList();

        // Resolve stock levels from Bin
        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
        var binQuery = await binRepo.GetQueryableAsync();
        var itemIds = items.Select(i => i.Id).ToList();
        var stockByItem = binQuery
            .Where(b => itemIds.Contains(b.ItemId))
            .GroupBy(b => b.ItemId)
            .Select(g => new { ItemId = g.Key, TotalQty = g.Sum(b => b.ActualQty) })
            .ToDictionary(x => x.ItemId, x => x.TotalQty);

        foreach (var dto in dtos)
        {
            if (stockByItem.TryGetValue(dto.Id, out var qty))
                dto.TotalStockQty = qty;
            dto.IsLowStock = dto.MaintainStock && dto.ReorderLevel > 0 && dto.TotalStockQty <= dto.ReorderLevel;
        }

        return new PagedResultDto<ItemDto>(totalCount, dtos);
    }

    protected override Item MapToEntity(CreateUpdateItemDto input)
    {
        var item = new Item(
            GuidGenerator.Create(), input.CompanyId,
            input.ItemCode, input.ItemName, input.ItemType, CurrentTenant.Id);
        MapToEntity(input, item);
        return item;
    }

    protected override void MapToEntity(CreateUpdateItemDto input, Item entity)
    {
        entity.SetItemCode(input.ItemCode);
        entity.SetItemName(input.ItemName);
        entity.Barcode = input.Barcode;
        entity.Description = input.Description;
        entity.ItemType = input.ItemType;
        entity.ItemGroup = input.ItemGroup;
        entity.Brand = input.Brand;
        entity.Uom = input.Uom;
        entity.ValuationMethod = input.ValuationMethod;
        entity.StandardSellingPrice = input.StandardSellingPrice;
        entity.StandardBuyingPrice = input.StandardBuyingPrice;
        entity.TaxCategoryId = input.TaxCategoryId;
        entity.MaintainStock = input.MaintainStock;
        entity.DefaultIncomeAccountId = input.DefaultIncomeAccountId;
        entity.DefaultExpenseAccountId = input.DefaultExpenseAccountId;
        entity.IsActive = input.IsActive;
        entity.ReorderLevel = input.ReorderLevel;
        entity.ReorderQty = input.ReorderQty;
        entity.SafetyStock = input.SafetyStock;
        entity.DefaultWarehouseId = input.DefaultWarehouseId;
        entity.MinOrderQty = input.MinOrderQty;
        entity.InspectionRequiredBeforePurchase = input.InspectionRequiredBeforePurchase;
        entity.InspectionRequiredBeforeDelivery = input.InspectionRequiredBeforeDelivery;
    }

    /// <summary>
    /// Create a variant item from a template item with specified attribute values.
    /// Per ERPNext: template items (HasVariants=true) define attributes,
    /// variants are concrete items with specific attribute values.
    /// </summary>
    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<ItemDto> CreateVariantAsync(Guid templateItemId, CreateItemVariantDto input)
    {
        var variantService = LazyServiceProvider.LazyGetRequiredService<DomainServices.ItemVariantService>();

        // Validate attribute values against definitions (numeric range/increment, text value-set)
        var attributes = input.Attributes.Select(a => new DomainServices.VariantAttributeInput
        {
            AttributeId = a.AttributeId,
            Value = a.Value,
        }).ToList();
        await variantService.ValidateAttributeValuesAsync(attributes);

        // Create the variant (generates code, checks duplicates, copies template properties)
        var variant = await variantService.CreateVariantAsync(templateItemId, attributes);

        return ObjectMapper.Map<Item, ItemDto>(variant);
    }

    /// <summary>
    /// Get price history for an item — shows how buying/selling rates changed over time.
    /// Per ERPNext: Item Price records with valid_from dates create a pricing timeline.
    /// </summary>
    [Authorize(MyERPPermissions.Items.Default)]
    public async Task<List<ItemPriceHistoryDto>> GetPriceHistoryAsync(Guid itemId)
    {
        var priceRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemPrice, Guid>>();
        var priceQuery = await priceRepo.GetQueryableAsync();
        var priceListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PriceList, Guid>>();
        var plQuery = await priceListRepo.GetQueryableAsync();

        var prices = priceQuery
            .Where(p => p.ItemId == itemId)
            .OrderByDescending(p => p.ValidFrom ?? p.CreationTime)
            .Take(50)
            .ToList();

        // Resolve price list names and selling/buying flags
        var plIds = prices.Select(p => p.PriceListId).Distinct().ToArray();
        var priceLists = plQuery
            .Where(pl => plIds.Contains(pl.Id))
            .ToDictionary(pl => pl.Id, pl => new { pl.Name, pl.IsSelling, pl.IsBuying });

        return prices.Select(p =>
        {
            var pl = priceLists.GetValueOrDefault(p.PriceListId);
            return new ItemPriceHistoryDto
            {
                Id = p.Id,
                PriceListName = pl?.Name,
                Rate = p.PriceListRate,
                Currency = p.CurrencyCode,
                ValidFrom = p.ValidFrom,
                ValidUpto = p.ValidUpto,
                IsSelling = pl?.IsSelling ?? false,
                IsBuying = pl?.IsBuying ?? false,
                PartyName = null, // Party resolution would need join to Customer/Supplier
                Uom = p.Uom,
                CreatedAt = p.CreationTime,
            };
        }).ToList();
    }

    /// <summary>
    /// Get recent stock movements for an item (last N SLE entries).
    /// Provides quick visibility into recent stock activity on the item detail page.
    /// </summary>
    [Authorize(MyERPPermissions.Items.Default)]
    public async Task<List<ItemStockMovementDto>> GetRecentMovementsAsync(Guid itemId, int maxCount = 20)
    {
        var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
        var sleQuery = await sleRepo.GetQueryableAsync();

        var entries = sleQuery
            .Where(s => s.ItemId == itemId)
            .OrderByDescending(s => s.PostingDate)
            .ThenByDescending(s => s.CreationTime)
            .Take(maxCount)
            .ToList();

        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var warehouseQuery = await warehouseRepo.GetQueryableAsync();
        var warehouseIds = entries.Select(e => e.WarehouseId).Distinct().ToArray();
        var warehouses = warehouseQuery
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        return entries.Select(e => new ItemStockMovementDto
        {
            PostingDate = e.PostingDate,
            WarehouseName = warehouses.GetValueOrDefault(e.WarehouseId, ""),
            QuantityChange = e.QuantityChange,
            ValuationRate = e.ValuationRate,
            BalanceQty = e.BalanceQuantity,
            BalanceValue = e.BalanceValue,
            VoucherType = e.VoucherType ?? "",
            VoucherId = e.VoucherId,
        }).ToList();
    }

    /// <summary>
    /// Get BOM references that use this item as a raw material (Where Used).
    /// Per ERPNext: item_where_used report (gotcha #5934) — 8-query 2-section architecture.
    /// Returns BOMs where this item appears as component.
    /// </summary>
    [Authorize(MyERPPermissions.Items.Default)]
    public async Task<List<ItemWhereUsedDto>> GetWhereUsedAsync(Guid itemId)
    {
        var bomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
        var bomQuery = await bomRepo.GetQueryableAsync();

        // Find all active BOMs that contain this item as a raw material
        var boms = bomQuery
            .Where(b => b.IsActive && b.Items.Any(i => i.ItemId == itemId))
            .OrderBy(b => b.BomNumber)
            .Take(50)
            .ToList();

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var fgItemIds = boms.Select(b => b.ItemId).Distinct().ToArray();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var fgItems = itemQuery
            .Where(i => fgItemIds.Contains(i.Id))
            .ToDictionary(i => i.Id, i => new { i.ItemCode, i.ItemName });

        return boms.Select(b =>
        {
            var bomItem = b.Items.First(i => i.ItemId == itemId);
            var fgItem = fgItems.GetValueOrDefault(b.ItemId);
            return new ItemWhereUsedDto
            {
                BomId = b.Id,
                BomNumber = b.BomNumber ?? "",
                FgItemCode = fgItem?.ItemCode ?? "",
                FgItemName = fgItem?.ItemName ?? "",
                QuantityPerUnit = b.Quantity > 0 ? bomItem.Quantity / b.Quantity : bomItem.Quantity,
                BomQuantity = b.Quantity,
            };
        }).ToList();
    }

    /// <summary>
    /// Get item variants for a template item.
    /// </summary>
    [Authorize(MyERPPermissions.Items.Default)]
    public async Task<List<ItemVariantDto>> GetVariantsAsync(Guid templateItemId)
    {
        var queryable = await Repository.GetQueryableAsync();
        var variants = queryable
            .Where(i => i.VariantOfId == templateItemId)
            .OrderBy(i => i.ItemCode)
            .ToList();

        return variants.Select(v => new ItemVariantDto
        {
            Id = v.Id,
            ItemCode = v.ItemCode,
            ItemName = v.ItemName,
            IsActive = v.IsActive,
            StandardSellingPrice = v.StandardSellingPrice,
            StandardBuyingPrice = v.StandardBuyingPrice,
        }).ToList();
    }
}

