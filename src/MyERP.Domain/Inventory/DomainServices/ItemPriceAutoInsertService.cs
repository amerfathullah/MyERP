using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Auto-inserts Item Price records when transactions are saved with rates not yet in the price list.
/// Per ERPNext get_item_details.py → insert_item_price():
/// - Only fires when Stock Settings.auto_insert_price_list_rate_if_missing = true
/// - Creates new Item Price with valid_from = transaction_date (date-segmented history)
/// - Skips when Item Price already exists for same composite key + date range
/// - Sets IsAutoInserted = true for audit trail
/// </summary>
public class ItemPriceAutoInsertService : DomainService
{
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepository;
    private readonly IGuidGenerator _guidGenerator;

    public ItemPriceAutoInsertService(
        IRepository<ItemPrice, Guid> itemPriceRepository,
        IGuidGenerator guidGenerator)
    {
        _itemPriceRepository = itemPriceRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Auto-inserts or updates Item Prices from a transaction's items.
    /// Per ERPNext commit 3ebde4526a:
    /// - Supports update_price_list_based_on ("Rate" vs "Price List Rate")
    /// - Supports update_existing_price_list_rate
    /// - Converts transaction rate to stock UOM rate via conversion factor
    /// </summary>
    public async Task AutoInsertFromTransactionAsync(
        AutoInsertPriceContext context)
    {
        if (!context.IsEnabled || context.PriceListId == Guid.Empty) return;

        foreach (var item in context.Items)
        {
            var updateBasedOnPriceListRate = string.Equals(context.UpdatePriceListBasedOn, "Price List Rate", StringComparison.OrdinalIgnoreCase);
            var rateToConsider = updateBasedOnPriceListRate
                ? (item.PriceListRate.HasValue && item.PriceListRate.Value > 0 ? item.PriceListRate.Value : item.Rate)
                : item.Rate;

            if (rateToConsider <= 0 || item.ItemId == Guid.Empty) continue;

            var conversion = item.ConversionFactor > 0 ? item.ConversionFactor : 1m;
            var effectivePriceListRate = Math.Round(rateToConsider / conversion, 4);

            // Check if price already exists for this item+priceList+UOM covering the transaction date
            var existingQuery = await _itemPriceRepository.GetQueryableAsync();
            var existingPrice = existingQuery.FirstOrDefault(p =>
                p.ItemId == item.ItemId &&
                p.PriceListId == context.PriceListId &&
                p.Uom == (item.Uom ?? "Unit") &&
                p.CustomerId == (context.IsSelling ? context.PartyId : null) &&
                p.SupplierId == (context.IsSelling ? null : context.PartyId) &&
                (p.ValidFrom == null || p.ValidFrom <= context.TransactionDate) &&
                (p.ValidUpto == null || p.ValidUpto >= context.TransactionDate));

            if (existingPrice != null)
            {
                if (context.UpdateExistingRate && Math.Abs(existingPrice.PriceListRate - effectivePriceListRate) >= 0.01m)
                {
                    existingPrice.PriceListRate = effectivePriceListRate;
                    await _itemPriceRepository.UpdateAsync(existingPrice, autoSave: true);
                }
                continue;
            }

            // Create new Item Price with valid_from = transaction date
            var itemPrice = new ItemPrice(
                _guidGenerator.Create(), item.ItemId, context.PriceListId,
                effectivePriceListRate, item.Uom ?? "Unit", context.CurrencyCode ?? "MYR", context.TenantId)
            {
                ValidFrom = context.TransactionDate,
                CustomerId = context.IsSelling ? context.PartyId : null,
                SupplierId = context.IsSelling ? null : context.PartyId,
                IsAutoInserted = true,
            };

            await _itemPriceRepository.InsertAsync(itemPrice, autoSave: true);
        }
    }
}

/// <summary>Context for auto-inserting item prices from a transaction.</summary>
public record AutoInsertPriceContext
{
    public bool IsEnabled { get; init; }
    public Guid PriceListId { get; init; }
    public Guid? PartyId { get; init; }
    public bool IsSelling { get; init; }
    public DateTime TransactionDate { get; init; }
    public string? CurrencyCode { get; init; }
    public Guid? TenantId { get; init; }
    public bool UpdateExistingRate { get; init; }
    public string UpdatePriceListBasedOn { get; init; } = "Rate";
    public AutoInsertPriceItem[] Items { get; init; } = [];
}

/// <summary>Item data for price auto-insertion.</summary>
public record AutoInsertPriceItem
{
    public Guid ItemId { get; init; }
    public decimal Rate { get; init; }
    public decimal? PriceListRate { get; init; }
    public decimal ConversionFactor { get; init; } = 1m;
    public string? Uom { get; init; }
}
