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

    private static readonly System.Collections.Generic.HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "MGA",
        "PYG", "RWF", "TWD", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    private static readonly System.Collections.Generic.HashSet<string> ThreeDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND", "TPT"
    };

    /// <summary>
    /// Returns the standard decimal precision for a given currency code.
    /// </summary>
    public static int GetCurrencyPrecision(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return 2;

        var normalized = currencyCode.Trim();
        if (ZeroDecimalCurrencies.Contains(normalized))
            return 0;
        if (ThreeDecimalCurrencies.Contains(normalized))
            return 3;
        return 2;
    }

    /// <summary>
    /// Converts an item's StandardSellingPrice (in the company's default currency) to the target
    /// price list currency and rounds to the target currency's precision.
    /// Per ERPNext commits e8a8b0b260 & 1853ef83ea (item.py::add_price):
    /// - If target currency differs from default currency and no valid exchange rate is provided, returns null.
    /// - Divides standardSellingRate by exchangeRate (1 unit of priceListCurrency = exchangeRate units of defaultCurrency)
    ///   and rounds to the price list currency's precision.
    /// </summary>
    public static decimal? CalculateStandardRateItemPrice(
        decimal standardSellingRate,
        string? defaultCurrencyCode,
        string? priceListCurrencyCode,
        decimal? exchangeRate,
        int? currencyPrecision = null)
    {
        if (standardSellingRate <= 0m)
            return null;

        var defaultCurrency = string.IsNullOrWhiteSpace(defaultCurrencyCode) ? "MYR" : defaultCurrencyCode.Trim();
        var targetCurrency = string.IsNullOrWhiteSpace(priceListCurrencyCode) ? defaultCurrency : priceListCurrencyCode.Trim();
        var precision = currencyPrecision ?? GetCurrencyPrecision(targetCurrency);

        if (!string.Equals(targetCurrency, defaultCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (!exchangeRate.HasValue || exchangeRate.Value <= 0m)
                return null;

            return Math.Round(standardSellingRate / exchangeRate.Value, precision, MidpointRounding.AwayFromZero);
        }

        return Math.Round(standardSellingRate, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Creates an ItemPrice from an Item's StandardSellingPrice on a selling PriceList if not already present,
    /// converting and rounding to the price list currency's precision (ERPNext e8a8b0b260 & 1853ef83ea).
    /// </summary>
    public async Task<ItemPrice?> EnsureStandardSellingPriceAsync(
        Item item,
        string? defaultCurrencyCode,
        PriceList priceList,
        decimal? exchangeRate = null,
        int? currencyPrecision = null)
    {
        if (item.HasVariants || (item.StandardSellingPrice ?? 0m) <= 0m || !priceList.IsSelling)
            return null;

        var uom = string.IsNullOrWhiteSpace(item.Uom) ? "Unit" : item.Uom;
        var existingQuery = await _itemPriceRepository.GetQueryableAsync();
        var alreadyExists = existingQuery.Any(p =>
            p.ItemId == item.Id &&
            p.PriceListId == priceList.Id &&
            p.Uom == uom &&
            p.CustomerId == null &&
            p.SupplierId == null);

        if (alreadyExists)
            return null;

        var convertedRate = CalculateStandardRateItemPrice(
            item.StandardSellingPrice!.Value,
            defaultCurrencyCode,
            priceList.CurrencyCode,
            exchangeRate,
            currencyPrecision);

        if (!convertedRate.HasValue || convertedRate.Value <= 0m)
            return null;

        var targetCurrency = string.IsNullOrWhiteSpace(priceList.CurrencyCode)
            ? (string.IsNullOrWhiteSpace(defaultCurrencyCode) ? "MYR" : defaultCurrencyCode.Trim())
            : priceList.CurrencyCode.Trim();

        var itemPrice = new ItemPrice(
            _guidGenerator.Create(),
            item.Id,
            priceList.Id,
            convertedRate.Value,
            uom,
            targetCurrency,
            item.TenantId)
        {
            IsAutoInserted = true,
        };

        await _itemPriceRepository.InsertAsync(itemPrice, autoSave: true);
        return itemPrice;
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
