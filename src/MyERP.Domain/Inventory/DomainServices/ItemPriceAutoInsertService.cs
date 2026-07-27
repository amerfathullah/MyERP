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
    /// Auto-inserts Item Prices from a transaction's items if they don't already exist.
    /// Per ERPNext: creates date-segmented price history (valid_from = transaction date).
    /// </summary>
    public async Task AutoInsertFromTransactionAsync(
        AutoInsertPriceContext context)
    {
        if (!context.IsEnabled || context.PriceListId == Guid.Empty) return;

        foreach (var item in context.Items)
        {
            if (item.Rate <= 0 || item.ItemId == Guid.Empty) continue;

            // Check if price already exists for this item+priceList+UOM covering the transaction date
            var existingQuery = await _itemPriceRepository.GetQueryableAsync();
            var exists = existingQuery.Any(p =>
                p.ItemId == item.ItemId &&
                p.PriceListId == context.PriceListId &&
                p.Uom == (item.Uom ?? "Unit") &&
                p.CustomerId == context.PartyId &&
                (p.ValidFrom == null || p.ValidFrom <= context.TransactionDate) &&
                (p.ValidUpto == null || p.ValidUpto >= context.TransactionDate) &&
                Math.Abs(p.PriceListRate - item.Rate) < 0.01m);

            if (exists) continue;

            // Create new Item Price with valid_from = transaction date
            var itemPrice = new ItemPrice(
                _guidGenerator.Create(), item.ItemId, context.PriceListId,
                item.Rate, item.Uom ?? "Unit", context.CurrencyCode ?? "MYR", context.TenantId)
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
    public AutoInsertPriceItem[] Items { get; init; } = [];
}

/// <summary>Item data for price auto-insertion.</summary>
public record AutoInsertPriceItem
{
    public Guid ItemId { get; init; }
    public decimal Rate { get; init; }
    public string? Uom { get; init; }
}
