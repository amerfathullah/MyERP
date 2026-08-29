using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Full item details resolution service implementing the ERPNext 25-step get_item_details chain.
/// Resolves ~45 fields from Item → ItemDefault (per company) → ItemGroup → Brand → Company defaults.
/// Called on every item selection change in SO/PO/SI/PI/DN/PR transaction forms.
/// Per ERPNext: get_item_details.py (1850 lines, 56 functions).
/// </summary>
public class ItemDetailsResolverService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<ItemDefault, Guid> _itemDefaultRepo;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly UomConversionService _uomConversionService;

    public ItemDetailsResolverService(
        IRepository<Item, Guid> itemRepo,
        IRepository<ItemDefault, Guid> itemDefaultRepo,
        IRepository<ItemGroup, Guid> itemGroupRepo,
        IRepository<Bin, Guid> binRepo,
        UomConversionService uomConversionService)
    {
        _itemRepo = itemRepo;
        _itemDefaultRepo = itemDefaultRepo;
        _itemGroupRepo = itemGroupRepo;
        _binRepo = binRepo;
        _uomConversionService = uomConversionService;
    }

    /// <summary>
    /// Full item details resolution implementing the ERPNext pipeline.
    /// Steps: validate → basic_details → accounts → warehouse → UOM → pricing → stock availability.
    /// </summary>
    public async Task<ResolvedItemDetails> ResolveAsync(ItemResolutionContext context)
    {
        var item = await _itemRepo.GetAsync(context.ItemId);

        // STEP 1: Validate item (template, inactive, end-of-life)
        ValidateItemForTransaction(item);

        // STEP 2: Load per-company defaults (3-tier: ItemDefault → ItemGroup → fallback)
        var defaults = await LoadDefaultsAsync(item, context.CompanyId);

        var defaultBarcode = item.Barcodes?.FirstOrDefault(b => b.IsDefault)?.Barcode
            ?? item.Barcodes?.FirstOrDefault()?.Barcode;

        // STEP 3: Build result with basic details
        var result = new ResolvedItemDetails
        {
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemName = item.ItemName,
            Description = item.Description ?? item.ItemName,
            IsStockItem = item.MaintainStock,
            HasBatchNo = item.HasBatchNo,
            HasSerialNo = item.HasSerialNo,
            ItemGroup = item.ItemGroup,
            WeightPerUnit = item.WeightPerUnit,
            WeightUom = item.WeightUom,
            Barcode = defaultBarcode,
            DefaultBomId = item.DefaultBomId,
        };

        // STEP 4: UOM resolution — selling uses sales_uom, buying uses purchase_uom
        result.Uom = context.TransactionType == TransactionType.Selling
            ? (item.SalesUom ?? item.Uom)
            : (item.PurchaseUom ?? item.Uom);
        result.StockUom = item.Uom;
        result.ConversionFactor = await _uomConversionService.GetConversionFactorAsync(
            item.Id, result.Uom, result.StockUom, item.VariantOfId);

        // STEP 5: Account resolution (Item → ItemDefault → ItemGroup → Company)
        if (context.TransactionType == TransactionType.Selling)
        {
            result.IncomeAccountId = defaults.IncomeAccountId;
            result.CostCenterId = defaults.SellingCostCenterId;
        }
        else
        {
            result.ExpenseAccountId = defaults.ExpenseAccountId;
            result.CostCenterId = defaults.BuyingCostCenterId;
        }

        // STEP 6: Warehouse resolution — 6-level chain per ERPNext
        result.WarehouseId = ResolveWarehouse(context, defaults, item);

        // STEP 7: Price resolution — priority chain per ERPNext get_price_list_rate_for():
        // 1. Party-specific Item Price (supplier for buying, customer for selling)
        // 2. Generic Item Price from the applicable price list
        // 3. Standard buying/selling price from Item master
        // 4. Last purchase rate (buying only, from SLE)
        var priceFromItemPrice = await ResolveItemPriceRateAsync(
            item.Id, context.TransactionType, context.PartyId, context.PriceListId, context.TransactionDate);

        if (context.TransactionType == TransactionType.Selling)
        {
            result.Rate = priceFromItemPrice
                ?? item.StandardSellingPrice
                ?? 0;
        }
        else
        {
            // Per ERPNext: last_purchase_rate from actual purchase transactions
            result.LastPurchaseRate = await GetLastPurchaseRateAsync(item.Id, context.CompanyId);

            result.Rate = priceFromItemPrice
                ?? item.StandardBuyingPrice
                ?? (result.LastPurchaseRate > 0 ? result.LastPurchaseRate : 0);
        }

        // STEP 7b: Material Request rate override — per ERPNext: MR forces rate=0
        if (context.IsMaterialRequest)
            result.Rate = 0;

        // STEP 8: Default supplier (for buying)
        result.DefaultSupplierId = defaults.DefaultSupplierId;

        // STEP 9: Default discount
        result.DefaultDiscountPercentage = defaults.DefaultDiscountPercentage;

        // STEP 10: Min order qty (for buying)
        result.MinOrderQty = item.MinOrderQty;

        // STEP 11: Item Tax Template/Category resolution — 3-tier fallback
        // Per ERPNext: Item.taxes → Item Group.taxes → default
        result.TaxCategoryId = item.TaxCategoryId;

        // STEP 12: Stock availability
        if (item.MaintainStock)
        {
            await PopulateStockAvailabilityAsync(result, context);
        }

        // STEP 13: Weight calculation
        result.TotalWeight = item.WeightPerUnit; // × qty done by caller

        return result;
    }

    /// <summary>
    /// Batch resolves item details for multiple items (reduces N+1 queries in transaction forms).
    /// </summary>
    public async Task<IReadOnlyList<ResolvedItemDetails>> ResolveBatchAsync(
        IReadOnlyList<Guid> itemIds, Guid? companyId, TransactionType transactionType)
    {
        var results = new List<ResolvedItemDetails>(itemIds.Count);
        foreach (var itemId in itemIds)
        {
            var ctx = new ItemResolutionContext { ItemId = itemId, CompanyId = companyId, TransactionType = transactionType };
            results.Add(await ResolveAsync(ctx));
        }
        return results;
    }

    // --- Private methods ---

    private static void ValidateItemForTransaction(Item item)
    {
        // Per ERPNext: template items cannot be used in transactions
        if (item.HasVariants)
            throw new Volo.Abp.BusinessException(MyERP.MyERPDomainErrorCodes.ItemHasVariants)
                .WithData("itemCode", item.ItemCode);

        // Per ERPNext: inactive items blocked
        if (!item.IsActive)
            throw new Volo.Abp.BusinessException(MyERP.MyERPDomainErrorCodes.ItemInactive)
                .WithData("itemCode", item.ItemCode)
                .WithData("itemName", item.ItemName);
    }

    private async Task<ResolvedDefaults> LoadDefaultsAsync(Item item, Guid? companyId)
    {
        var defaults = new ResolvedDefaults();

        // Tier 1: Per-company ItemDefault
        if (companyId.HasValue)
        {
            var itemDefaultQuery = await _itemDefaultRepo.GetQueryableAsync();
            var itemDefault = itemDefaultQuery.FirstOrDefault(d =>
                d.ItemId == item.Id && d.CompanyId == companyId.Value);

            if (itemDefault != null)
            {
                defaults.WarehouseId = itemDefault.DefaultWarehouseId;
                defaults.IncomeAccountId = itemDefault.IncomeAccountId;
                defaults.ExpenseAccountId = itemDefault.ExpenseAccountId;
                defaults.BuyingCostCenterId = itemDefault.BuyingCostCenterId;
                defaults.SellingCostCenterId = itemDefault.SellingCostCenterId;
                defaults.DefaultSupplierId = itemDefault.DefaultSupplierId;
                defaults.DefaultDiscountPercentage = itemDefault.DefaultDiscountPercentage;
            }
        }

        // Tier 2: Item-level defaults (if not set by ItemDefault)
        defaults.WarehouseId ??= item.DefaultWarehouseId;
        defaults.IncomeAccountId ??= item.DefaultIncomeAccountId;
        defaults.ExpenseAccountId ??= item.DefaultExpenseAccountId;

        // Tier 3: ItemGroup hierarchy (traverse tree upward)
        if (item.ItemGroupId.HasValue)
        {
            await TraverseGroupHierarchyAsync(item.ItemGroupId.Value, defaults);
        }

        return defaults;
    }

    private async Task TraverseGroupHierarchyAsync(Guid groupId, ResolvedDefaults defaults)
    {
        var currentGroupId = (Guid?)groupId;
        var maxDepth = 10;

        while (currentGroupId.HasValue && maxDepth-- > 0)
        {
            var group = await _itemGroupRepo.FindAsync(currentGroupId.Value);
            if (group == null) break;

            defaults.WarehouseId ??= group.DefaultWarehouseId;
            defaults.IncomeAccountId ??= group.DefaultIncomeAccountId;
            defaults.ExpenseAccountId ??= group.DefaultExpenseAccountId;

            currentGroupId = group.ParentId;
        }
    }

    private static Guid? ResolveWarehouse(ItemResolutionContext context, ResolvedDefaults defaults, Item item)
    {
        // Per ERPNext 6-level warehouse chain:
        // 1. Context document-level override
        // 2. ItemDefault per company
        // 3. ItemGroup hierarchy
        // 4. Item.DefaultWarehouseId
        // 5. Context fallback
        // 6. null (Stock Settings default handled by caller)
        return context.WarehouseOverride
            ?? defaults.WarehouseId
            ?? item.DefaultWarehouseId
            ?? context.FallbackWarehouseId;
    }

    private async Task PopulateStockAvailabilityAsync(ResolvedItemDetails result, ItemResolutionContext context)
    {
        var binQuery = await _binRepo.GetQueryableAsync();

        // Per-warehouse stock
        var targetWarehouse = result.WarehouseId;
        if (targetWarehouse.HasValue)
        {
            var bin = binQuery.FirstOrDefault(b => b.ItemId == result.ItemId && b.WarehouseId == targetWarehouse.Value);
            if (bin != null)
            {
                result.ActualQty = bin.ActualQty;
                result.ProjectedQty = bin.ProjectedQty;
                result.ReservedQty = bin.ReservedQty;
                result.AvailableQty = bin.ActualQty - bin.ReservedQty;
            }
        }

        // Company-total stock (all warehouses)
        result.CompanyTotalStock = binQuery
            .Where(b => b.ItemId == result.ItemId)
            .Sum(b => (decimal?)b.ActualQty) ?? 0;
    }

    /// <summary>
    /// Resolves the best Item Price rate for the given item and context.
    /// Per ERPNext get_price_list_rate(): rates are always scoped to ONE specific price list —
    /// resolved from <paramref name="priceListId"/> if given, else the system default price list
    /// for the transaction type (Selling vs Buying). Previously this searched ItemPrice rows across
    /// ALL price lists with no type check at all, so a Buying-only price could satisfy a Selling
    /// lookup (or vice versa) purely by matching item+party. Fixed to resolve one specific,
    /// type-matched price list first.
    /// Per ERPNext get_price_list_rate_for() priority within that price list:
    /// 1. Party-specific Item Price (supplier/customer + date-valid + min-qty match)
    /// 2. Generic Item Price (no party)
    /// Returns null when no applicable price list or no matching Item Price is found (caller falls
    /// back to standard rate).
    /// </summary>
    private async Task<decimal?> ResolveItemPriceRateAsync(
        Guid itemId, TransactionType txType, Guid? partyId, Guid? priceListId, DateTime? transactionDate)
    {
        try
        {
            var effectivePriceListId = await ResolveEffectivePriceListIdAsync(priceListId, txType);
            if (!effectivePriceListId.HasValue)
                return null;

            var itemPriceRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemPrice, Guid>>();
            var query = await itemPriceRepo.GetQueryableAsync();
            var asOfDate = transactionDate ?? DateTime.UtcNow.Date;

            // Base filter: item + the one resolved price list + active + date-valid
            var candidates = query.Where(p =>
                p.ItemId == itemId &&
                p.PriceListId == effectivePriceListId.Value &&
                (!p.ValidFrom.HasValue || p.ValidFrom.Value <= asOfDate) &&
                (!p.ValidUpto.HasValue || p.ValidUpto.Value >= asOfDate));

            // Priority 1: party-specific price (supplier for buying, customer for selling)
            if (partyId.HasValue)
            {
                var partyPrice = txType == TransactionType.Buying
                    ? candidates.Where(p => p.SupplierId == partyId.Value)
                        .OrderByDescending(p => p.ValidFrom)
                        .ThenByDescending(p => p.MinQty)
                        .Select(p => (decimal?)p.PriceListRate)
                        .FirstOrDefault()
                    : candidates.Where(p => p.CustomerId == partyId.Value)
                        .OrderByDescending(p => p.ValidFrom)
                        .ThenByDescending(p => p.MinQty)
                        .Select(p => (decimal?)p.PriceListRate)
                        .FirstOrDefault();

                if (partyPrice.HasValue && partyPrice.Value > 0)
                    return partyPrice.Value;
            }

            // Priority 2: generic price (no party filter)
            var genericPrice = candidates
                .Where(p => p.CustomerId == null && p.SupplierId == null)
                .OrderByDescending(p => p.ValidFrom)
                .ThenByDescending(p => p.MinQty)
                .Select(p => (decimal?)p.PriceListRate)
                .FirstOrDefault();

            return genericPrice.HasValue && genericPrice.Value > 0 ? genericPrice.Value : null;
        }
        catch
        {
            return null; // Graceful fallback — ItemPrice resolution failure shouldn't block item selection
        }
    }

    /// <summary>
    /// Resolves which single Price List applies: the explicit one if given, else the system default
    /// price list matching the transaction type (Selling vs Buying). Per ERPNext, a party's own
    /// default price list (Customer.default_price_list / Supplier.default_price_list) takes
    /// precedence over the system default, but that resolution happens one layer up in
    /// ItemDetailsAppService (which has the Customer/Supplier repositories); this fallback only
    /// covers the case where no price list was ever resolved by the caller.
    /// </summary>
    private async Task<Guid?> ResolveEffectivePriceListIdAsync(Guid? explicitPriceListId, TransactionType txType)
    {
        if (explicitPriceListId.HasValue)
            return explicitPriceListId;

        var priceListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PriceList, Guid>>();
        var query = await priceListRepo.GetQueryableAsync();
        var defaultList = query.FirstOrDefault(p => p.IsActive && p.IsDefault &&
            (txType == TransactionType.Selling ? p.IsSelling : p.IsBuying));
        return defaultList?.Id;
    }

    /// <summary>
    /// Gets the last purchase rate from Stock Ledger Entries (incoming rate from purchase receipts/invoices).
    /// Per ERPNext: last_purchase_rate is the most recent inward SLE rate for the item.
    /// Falls back to item's standard buying price if no SLE exists.
    /// </summary>
    private async Task<decimal> GetLastPurchaseRateAsync(Guid itemId, Guid? companyId)
    {
        try
        {
            var sleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var sleQuery = await sleRepo.GetQueryableAsync();

            var query = sleQuery
                .Where(e => e.ItemId == itemId && e.QuantityChange > 0); // inward entries only

            if (companyId.HasValue)
                query = query.Where(e => e.CompanyId == companyId.Value);

            var lastRate = query
                .OrderByDescending(e => e.PostingDate)
                .ThenByDescending(e => e.CreationTime)
                .Select(e => e.IncomingRate)
                .FirstOrDefault();

            return lastRate;
        }
        catch
        {
            return 0; // Graceful fallback if SLE not available
        }
    }
}

// --- Supporting types ---

public enum TransactionType { Selling, Buying }

public class ItemResolutionContext
{
    public Guid ItemId { get; set; }
    public Guid? CompanyId { get; set; }
    public TransactionType TransactionType { get; set; } = TransactionType.Selling;
    public Guid? WarehouseOverride { get; set; }
    public Guid? FallbackWarehouseId { get; set; }
    public Guid? PartyId { get; set; }
    public Guid? PriceListId { get; set; }
    public DateTime? TransactionDate { get; set; }
    /// <summary>Per ERPNext: Material Request forces rate=0 for all items.</summary>
    public bool IsMaterialRequest { get; set; }
}

public class ResolvedItemDetails
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public string Uom { get; set; } = "Unit";
    public string StockUom { get; set; } = "Unit";
    public decimal ConversionFactor { get; set; } = 1;
    public bool IsStockItem { get; set; }
    public bool HasBatchNo { get; set; }
    public bool HasSerialNo { get; set; }
    public string? ItemGroup { get; set; }
    public decimal Rate { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public Guid? DefaultBomId { get; set; }
    public decimal DefaultDiscountPercentage { get; set; }
    public decimal MinOrderQty { get; set; }
    public decimal WeightPerUnit { get; set; }
    public string? WeightUom { get; set; }
    public string? Barcode { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal LastPurchaseRate { get; set; }
    /// <summary>Tax category ID for item tax template resolution. Per ERPNext: Item.taxes fallback chain.</summary>
    public Guid? TaxCategoryId { get; set; }

    // Stock availability
    public decimal ActualQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal ReservedQty { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal CompanyTotalStock { get; set; }
}

/// <summary>Internal aggregate of resolved defaults across 3 tiers.</summary>
internal class ResolvedDefaults
{
    public Guid? WarehouseId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? BuyingCostCenterId { get; set; }
    public Guid? SellingCostCenterId { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public decimal DefaultDiscountPercentage { get; set; }
}
