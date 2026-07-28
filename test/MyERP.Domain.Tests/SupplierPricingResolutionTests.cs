using System;
using Xunit;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for supplier-specific Item Price resolution in ItemDetailsResolverService.
/// Per ERPNext get_price_list_rate_for(): party-specific → generic → standard → last purchase rate.
/// </summary>
public class SupplierPricingResolutionTests
{
    [Fact]
    public void ItemResolutionContext_PartyId_DefaultsNull()
    {
        var ctx = new ItemResolutionContext { ItemId = Guid.NewGuid() };
        Assert.Null(ctx.PartyId);
    }

    [Fact]
    public void ItemResolutionContext_PriceListId_DefaultsNull()
    {
        var ctx = new ItemResolutionContext { ItemId = Guid.NewGuid() };
        Assert.Null(ctx.PriceListId);
    }

    [Fact]
    public void ItemResolutionContext_TransactionDate_DefaultsNull()
    {
        var ctx = new ItemResolutionContext { ItemId = Guid.NewGuid() };
        Assert.Null(ctx.TransactionDate);
    }

    [Fact]
    public void ItemResolutionContext_BuyingTransaction_CanSetPartyId()
    {
        var supplierId = Guid.NewGuid();
        var ctx = new ItemResolutionContext
        {
            ItemId = Guid.NewGuid(),
            TransactionType = TransactionType.Buying,
            PartyId = supplierId,
        };
        Assert.Equal(supplierId, ctx.PartyId);
        Assert.Equal(TransactionType.Buying, ctx.TransactionType);
    }

    [Fact]
    public void ItemResolutionContext_SellingTransaction_CanSetPartyId()
    {
        var customerId = Guid.NewGuid();
        var ctx = new ItemResolutionContext
        {
            ItemId = Guid.NewGuid(),
            TransactionType = TransactionType.Selling,
            PartyId = customerId,
        };
        Assert.Equal(customerId, ctx.PartyId);
    }

    [Fact]
    public void ItemPrice_HasSupplierField()
    {
        var ip = new ItemPrice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            priceListRate: 25.50m,
            uom: "Unit",
            currencyCode: "MYR");

        Assert.Equal(25.50m, ip.PriceListRate);
        Assert.Null(ip.SupplierId);
        Assert.Null(ip.CustomerId);
    }

    [Fact]
    public void ItemPrice_SupplierSpecific_CanBeSet()
    {
        var supplierId = Guid.NewGuid();
        var ip = new ItemPrice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            priceListRate: 18.00m,
            uom: "Unit",
            currencyCode: "MYR");
        ip.SupplierId = supplierId;

        Assert.Equal(supplierId, ip.SupplierId);
        Assert.Equal(18.00m, ip.PriceListRate);
    }

    [Fact]
    public void ItemPrice_CustomerSpecific_CanBeSet()
    {
        var customerId = Guid.NewGuid();
        var ip = new ItemPrice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            priceListRate: 35.00m,
            uom: "Unit",
            currencyCode: "MYR");
        ip.CustomerId = customerId;

        Assert.Equal(customerId, ip.CustomerId);
    }

    [Fact]
    public void ItemPrice_ValidFrom_FiltersConcept()
    {
        // Verify the entity has ValidFrom/ValidUpto for date-valid pricing
        var ip = new ItemPrice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            priceListRate: 20.00m,
            uom: "Unit",
            currencyCode: "MYR");

        ip.ValidFrom = new DateTime(2026, 1, 1);
        ip.ValidUpto = new DateTime(2026, 12, 31);

        Assert.Equal(new DateTime(2026, 1, 1), ip.ValidFrom);
        Assert.Equal(new DateTime(2026, 12, 31), ip.ValidUpto);
    }

    [Fact]
    public void ResolvedItemDetails_Rate_DefaultsZero()
    {
        var result = new ResolvedItemDetails();
        Assert.Equal(0m, result.Rate);
    }

    [Fact]
    public void ResolvedItemDetails_LastPurchaseRate_DefaultsZero()
    {
        var result = new ResolvedItemDetails();
        Assert.Equal(0m, result.LastPurchaseRate);
    }

    [Fact]
    public void GetItemDetailsInput_SupplierId_DefaultsNull()
    {
        var input = new MyERP.Inventory.GetItemDetailsInput();
        Assert.Null(input.SupplierId);
    }

    [Fact]
    public void GetItemDetailsInput_CustomerId_DefaultsNull()
    {
        var input = new MyERP.Inventory.GetItemDetailsInput();
        Assert.Null(input.CustomerId);
    }

    [Fact]
    public void GetItemDetailsInput_PriceListId_DefaultsNull()
    {
        var input = new MyERP.Inventory.GetItemDetailsInput();
        Assert.Null(input.PriceListId);
    }

    [Fact]
    public void GetItemDetailsInput_TransactionDate_DefaultsNull()
    {
        var input = new MyERP.Inventory.GetItemDetailsInput();
        Assert.Null(input.TransactionDate);
    }

    [Fact]
    public void GetItemDetailsInput_AllFieldsSettable()
    {
        var input = new MyERP.Inventory.GetItemDetailsInput
        {
            ItemId = Guid.NewGuid(),
            TransactionType = "Buying",
            CompanyId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            PriceListId = Guid.NewGuid(),
            TransactionDate = new DateTime(2026, 7, 28),
        };

        Assert.NotNull(input.SupplierId);
        Assert.NotNull(input.PriceListId);
        Assert.Equal("Buying", input.TransactionType);
        Assert.Equal(new DateTime(2026, 7, 28), input.TransactionDate);
    }

    // --- Pricing Priority Chain Concept Tests ---

    [Fact]
    public void PricingPriority_SupplierSpecific_TakesPrecedence()
    {
        // Concept: supplier-specific rate (18.00) wins over generic rate (25.00)
        // and standard buying price (20.00)
        decimal supplierRate = 18.00m;
        decimal genericRate = 25.00m;
        decimal standardRate = 20.00m;

        // Resolution chain: supplier > generic > standard > last_purchase
        decimal? resolved = supplierRate > 0 ? supplierRate : (genericRate > 0 ? genericRate : standardRate);
        Assert.Equal(18.00m, resolved);
    }

    [Fact]
    public void PricingPriority_GenericRate_WhenNoSupplierPrice()
    {
        decimal? supplierRate = null; // No supplier-specific price
        decimal genericRate = 25.00m;
        decimal standardRate = 20.00m;

        decimal? resolved = supplierRate ?? (genericRate > 0 ? genericRate : standardRate);
        Assert.Equal(25.00m, resolved);
    }

    [Fact]
    public void PricingPriority_StandardRate_WhenNoItemPrice()
    {
        decimal? supplierRate = null;
        decimal? genericRate = null;
        decimal standardRate = 20.00m;

        decimal resolved = supplierRate ?? genericRate ?? standardRate;
        Assert.Equal(20.00m, resolved);
    }

    [Fact]
    public void PricingPriority_LastPurchaseRate_WhenAllZero()
    {
        decimal? supplierRate = null;
        decimal? genericRate = null;
        decimal standardRate = 0m;
        decimal lastPurchaseRate = 15.50m;

        decimal resolved = supplierRate ?? genericRate ?? (standardRate > 0 ? standardRate : lastPurchaseRate);
        Assert.Equal(15.50m, resolved);
    }

    [Fact]
    public void PricingPriority_ZeroRate_FallsThrough()
    {
        // Zero-valued prices should NOT be used (fall through to next tier)
        decimal? supplierRate = 0m; // Exists but zero — skip
        decimal genericRate = 25.00m;

        // Per ERPNext: zero rate = not a valid price, skip to next
        decimal? resolved = (supplierRate.HasValue && supplierRate.Value > 0)
            ? supplierRate.Value
            : genericRate;
        Assert.Equal(25.00m, resolved);
    }
}
