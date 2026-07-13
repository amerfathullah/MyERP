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
        Guid? itemId, string transactionUom, string stockUom)
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

        if (reverseGlobal != null)
            return 1m / reverseGlobal.ConversionFactor;

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
}
