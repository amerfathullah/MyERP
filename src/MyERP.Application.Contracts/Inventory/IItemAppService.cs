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
