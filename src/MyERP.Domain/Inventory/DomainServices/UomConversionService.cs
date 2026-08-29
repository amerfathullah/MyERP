using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Resolves UOM conversion factors for transactions.
/// Priority: item-specific conversion → global conversion → 1.0 (same UOM).
/// Per ERPNext: conversion_factor auto-resets when UOM matches stock_UOM.
/// </summary>
public class UomConversionService : DomainService
{
    private readonly IRepository<UomConversion, Guid> _repository;

    public UomConversionService(IRepository<UomConversion, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets the conversion factor from transactionUom to stockUom for an item.
    /// Returns the factor to multiply transaction qty by to get stock qty.
    /// Priority: item-specific → global → 1.0 (if same UOM or no conversion found).
    /// </summary>
    public async Task<decimal> GetConversionFactorAsync(
        Guid? itemId, string transactionUom, string stockUom, Guid? variantOfItemId = null)
    {
        if (string.Equals(transactionUom, stockUom, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var query = await _repository.GetQueryableAsync();

        // Priority 1: item-specific conversion
        if (itemId.HasValue)
        {
            var itemConversion = query
                .FirstOrDefault(c => c.ItemId == itemId.Value
                    && c.FromUom == transactionUom && c.ToUom == stockUom);

            if (itemConversion != null)
                return itemConversion.ConversionFactor;

            // Check reverse direction
            var reverseItemConversion = query
                .FirstOrDefault(c => c.ItemId == itemId.Value
                    && c.FromUom == stockUom && c.ToUom == transactionUom);

            if (reverseItemConversion != null)
                return 1m / reverseItemConversion.ConversionFactor;
        }

        // Priority 1b: variant template conversion (per PR #57553)
        if (variantOfItemId.HasValue)
        {
            var templateConversion = query
                .FirstOrDefault(c => c.ItemId == variantOfItemId.Value
                    && c.FromUom == transactionUom && c.ToUom == stockUom);

            if (templateConversion != null)
                return templateConversion.ConversionFactor;

            var reverseTemplateConversion = query
                .FirstOrDefault(c => c.ItemId == variantOfItemId.Value
                    && c.FromUom == stockUom && c.ToUom == transactionUom);

            if (reverseTemplateConversion != null)
                return 1m / reverseTemplateConversion.ConversionFactor;
        }

        // Priority 2: global conversion (ItemId = null)
        var globalConversion = query
            .FirstOrDefault(c => c.ItemId == null
                && c.FromUom == transactionUom && c.ToUom == stockUom);

        if (globalConversion != null)
            return globalConversion.ConversionFactor;

        // Check reverse global
        var reverseGlobal = query
            .FirstOrDefault(c => c.ItemId == null
                && c.FromUom == stockUom && c.ToUom == transactionUom);

        if (reverseGlobal != null && reverseGlobal.ConversionFactor != 0)
            return 1m / reverseGlobal.ConversionFactor;

        // Priority 3: Intermediate conversion via shared source UOM (per PR #58305)
        // e.g. Kg -> mg (1,000,000) and Kg -> g (1,000) => g -> mg = 1,000,000 / 1,000 = 1,000
        var sharedSource = (
            from first in query.Where(c => c.ItemId == null && c.ToUom == stockUom)
            join second in query.Where(c => c.ItemId == null && c.ToUom == transactionUom && c.ConversionFactor != 0)
                on first.FromUom equals second.FromUom
            select first.ConversionFactor / second.ConversionFactor
        ).FirstOrDefault();

        if (sharedSource != 0)
            return sharedSource;

        // Priority 4: Intermediate conversion via shared target UOM (per PR #58305)
        // e.g. 3 Kg Bag -> Kg (3) and 25 Kg Bag -> Kg (25) => 3 Kg Bag -> 25 Kg Bag = 3 / 25 = 0.12
        var sharedTarget = (
            from first in query.Where(c => c.ItemId == null && c.FromUom == transactionUom)
            join second in query.Where(c => c.ItemId == null && c.FromUom == stockUom && c.ConversionFactor != 0)
                on first.ToUom equals second.ToUom
            select first.ConversionFactor / second.ConversionFactor
        ).FirstOrDefault();

        if (sharedTarget != 0)
            return sharedTarget;

        // Default: no conversion found (assume same UOM or factor = 1)
        return 1m;
    }

    /// <summary>
    /// Converts a quantity from transaction UOM to stock UOM.
    /// </summary>
    public async Task<decimal> ConvertToStockQtyAsync(
        Guid? itemId, string transactionUom, string stockUom, decimal transactionQty)
    {
        var factor = await GetConversionFactorAsync(itemId, transactionUom, stockUom);
        return transactionQty * factor;
    }

    /// <summary>
    /// Converts stock quantity to purchase UOM quantity.
    /// Per ERPNext PR #57873 / commit ee8eb18daf:
    /// When ConsiderMinimumOrderQty is active and standard nearest rounding dips below
    /// min_order_qty, takes the grid-ceiling to ensure stock equivalent meets minimum order qty.
    /// </summary>
    public static decimal CalculatePurchaseUomQty(
        decimal plannedStockQty,
        decimal conversionFactor,
        decimal minOrderQty = 0m,
        bool considerMinOrderQty = false,
        int precision = 4)
    {
        if (conversionFactor <= 0) return plannedStockQty;

        var qty = Math.Round(plannedStockQty / conversionFactor, precision);
        if (considerMinOrderQty && minOrderQty > 0 && (qty * conversionFactor) < minOrderQty && minOrderQty <= plannedStockQty)
        {
            var factor = (decimal)Math.Pow(10, precision);
            qty = Math.Ceiling((minOrderQty / conversionFactor) * factor) / factor;
        }

        return qty;
    }
}
