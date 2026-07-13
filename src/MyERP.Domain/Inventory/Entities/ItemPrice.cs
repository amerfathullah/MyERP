using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Item Price — a specific price for an item in a price list.
/// Composite uniqueness: (item + price_list + UOM + valid_from + customer/supplier + batch).
/// Used for rate lookup during transaction item entry.
/// </summary>
public class ItemPrice : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ItemId { get; set; }
    public Guid PriceListId { get; set; }

    /// <summary>The price rate in the price list's currency.</summary>
    public decimal PriceListRate { get; set; }

    /// <summary>UOM for which this price applies (default = item's stock UOM).</summary>
    public string Uom { get; set; } = "Unit";

    /// <summary>Currency (inherited from price list but stored for lookup).</summary>
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Minimum quantity for this price to apply (qty-based pricing).</summary>
    public decimal MinQty { get; set; }

    /// <summary>Date from which this price is valid.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Date until which this price is valid.</summary>
    public DateTime? ValidUpto { get; set; }

    /// <summary>Customer-specific price (null = applies to all customers).</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Supplier-specific price (null = applies to all suppliers).</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Batch-specific price (null = applies to all batches).</summary>
    public string? BatchNo { get; set; }

    /// <summary>Whether this price was auto-inserted from a transaction.</summary>
    public bool IsAutoInserted { get; set; }

    protected ItemPrice() { }

    public ItemPrice(Guid id, Guid itemId, Guid priceListId, decimal priceListRate, string uom, string currencyCode, Guid? tenantId = null)
        : base(id)
    {
        ItemId = itemId;
        PriceListId = priceListId;
        PriceListRate = priceListRate;
        Uom = uom;
        CurrencyCode = currencyCode;
        TenantId = tenantId;
    }

    /// <summary>Check if this price is valid for the given date.</summary>
    public bool IsValidOnDate(DateTime date)
    {
        if (ValidFrom.HasValue && date < ValidFrom.Value) return false;
        if (ValidUpto.HasValue && date > ValidUpto.Value) return false;
        return true;
    }
}
