using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public class GetItemListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CompanyId { get; set; }
    public string? ItemType { get; set; }

    /// <summary>When set, excludes items restricted from this Customer via PartySpecificItem rules.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>When set, excludes items restricted from this Supplier via PartySpecificItem rules.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>When set, filters by template status (e.g. HasVariants=true for Variant Of filter).</summary>
    public bool? HasVariants { get; set; }

    /// <summary>When set, filters by batch tracking status (e.g. HasBatchNo=true for Batch Item filter).</summary>
    public bool? HasBatchNo { get; set; }

    /// <summary>When set, filters by serial number tracking status.</summary>
    public bool? HasSerialNo { get; set; }

    /// <summary>When set, filters by stock maintenance status (is_stock_item).</summary>
    public bool? MaintainStock { get; set; }
}

/// <summary>
/// DTO for creating an item variant from a template item.
/// Per ERPNext: variant naming uses template code + attribute abbreviations.
/// </summary>
public class CreateItemVariantDto
{
    public List<VariantAttributeDto> Attributes { get; set; } = new();
}

public class VariantAttributeDto
{
    public Guid AttributeId { get; set; }
    public string Value { get; set; } = null!;
}

public interface IItemAppService :
    ICrudAppService<
        ItemDto,
        Guid,
        GetItemListDto,
        CreateUpdateItemDto>
{
    Task<ItemDto> CreateVariantAsync(Guid templateItemId, CreateItemVariantDto input);
    Task<List<ItemPriceHistoryDto>> GetPriceHistoryAsync(Guid itemId);
    Task<List<ItemStockMovementDto>> GetRecentMovementsAsync(Guid itemId, int maxCount = 20);
    Task<List<ItemWhereUsedDto>> GetWhereUsedAsync(Guid itemId);
    Task<List<ItemVariantDto>> GetVariantsAsync(Guid templateItemId);
    Task<ItemTransactionSummaryDto> GetTransactionSummaryAsync(Guid itemId, Guid? companyId = null);
    Task<List<ReorderSuggestionDto>> GetReorderSuggestionsAsync(Guid companyId, int lookbackDays = 90);
}

/// <summary>
/// Aggregate purchase/sales metrics for an item — per ERPNext Item dashboard.
/// Shows procurement vs sales activity for inventory planning decisions.
/// </summary>
public class ItemTransactionSummaryDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";

    // Purchase metrics (last 12 months)
    public int PurchaseOrderCount { get; set; }
    public decimal TotalPurchasedQty { get; set; }
    public decimal TotalPurchasedValue { get; set; }
    public decimal? LastPurchaseRate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }

    // Sales metrics (last 12 months)
    public int SalesOrderCount { get; set; }
    public decimal TotalSoldQty { get; set; }
    public decimal TotalSoldValue { get; set; }
    public decimal? AverageSellingRate { get; set; }
    public DateTime? LastSaleDate { get; set; }

    // Stock metrics
    public decimal CurrentStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
    public int DaysOfStockRemaining { get; set; }
}

/// <summary>Item price history entry for the price timeline chart.</summary>
public class ItemPriceHistoryDto
{
    public Guid Id { get; set; }
    public string? PriceListName { get; set; }
    public decimal Rate { get; set; }
    public string Currency { get; set; } = "MYR";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public bool IsSelling { get; set; }
    public bool IsBuying { get; set; }
    public string? PartyName { get; set; }
    public string? Uom { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Recent stock movement entry for the item stock ledger panel.</summary>
public class ItemStockMovementDto
{
    public DateTime PostingDate { get; set; }
    public string WarehouseName { get; set; } = "";
    public decimal QuantityChange { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal BalanceQty { get; set; }
    public decimal BalanceValue { get; set; }
    public string VoucherType { get; set; } = "";
    public Guid? VoucherId { get; set; }
}

/// <summary>BOM reference showing where this item is used as raw material.</summary>
public class ItemWhereUsedDto
{
    public Guid BomId { get; set; }
    public string BomNumber { get; set; } = "";
    public string FgItemCode { get; set; } = "";
    public string FgItemName { get; set; } = "";
    public decimal QuantityPerUnit { get; set; }
    public decimal BomQuantity { get; set; }
}

/// <summary>Variant item summary for the template item's variant list.</summary>
public class ItemVariantDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public bool IsActive { get; set; }
    public decimal? StandardSellingPrice { get; set; }
    public decimal? StandardBuyingPrice { get; set; }
}

/// <summary>
/// Suggested reorder level calculated from actual consumption patterns.
/// Per ERPNext Recommended Reorder Level: avg_daily_consumption × lead_time_days.
/// </summary>
public class ReorderSuggestionDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public decimal CurrentReorderLevel { get; set; }
    public decimal SuggestedReorderLevel { get; set; }
    public decimal SuggestedReorderQty { get; set; }
    public decimal SuggestedSafetyStock { get; set; }
    public decimal AvgDailyConsumption { get; set; }
    public decimal CurrentStock { get; set; }
    public int DaysOfStockRemaining { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsUnderstocked { get; set; }
    public bool IsOverstocked { get; set; }
}
