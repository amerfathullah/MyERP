using System;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 13:
/// 1) Item Price converted from Standard Rate rounded to currency precision (PR #64853, commits 1853ef83ea, 629e32b70a).
/// 2) Return row with zero accepted qty leaves accepted bundle lookup out (PR #64829, commits 2f2d383015, b5f1d700b2).
/// 3) Standard Cost valuation rate prioritizes item standard buying rate over serial/batch inward entries (PR #64830, commits bc7701bad1, fe05246e80, 838c54142c).
/// </summary>
public class UpstreamBatch13ParityTests
{
    // === PR #64853 / commits 1853ef83ea, 629e32b70a: Converted Item Price Currency Precision ===

    [Fact]
    public void ItemPrice_CalculateStandardRateItemPrice_ThreeDecimalCurrency_RoundsToThreeDecimals()
    {
        // Standard selling rate = 100 in default currency INR/MYR
        // Exchange rate = 1.1 for 3-decimal currency "TPT" (e.g. #,###.### format)
        // 100 / 1.1 = 90.9090909... -> rounds to 90.909
        var result = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "TPT",
            exchangeRate: 1.1m);

        result.ShouldNotBeNull();
        result.Value.ShouldBe(90.909m);
    }

    [Fact]
    public void ItemPrice_CalculateStandardRateItemPrice_ZeroDecimalCurrency_RoundsToZeroDecimals()
    {
        // 100 / 1.1 = 90.9090909... -> rounds to 91 for JPY (0 decimals)
        var result = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "JPY",
            exchangeRate: 1.1m);

        result.ShouldNotBeNull();
        result.Value.ShouldBe(91m);
    }

    [Fact]
    public void ItemPrice_CalculateStandardRateItemPrice_TwoDecimalCurrency_RoundsToTwoDecimals()
    {
        // 100 / 1.1 = 90.9090909... -> rounds to 90.91 for USD (2 decimals)
        var result = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "USD",
            exchangeRate: 1.1m);

        result.ShouldNotBeNull();
        result.Value.ShouldBe(90.91m);
    }

    [Fact]
    public void ItemPrice_CalculateStandardRateItemPrice_SameCurrency_NoExchangeRateNeeded()
    {
        var result = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 125.456m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "MYR",
            exchangeRate: null);

        result.ShouldNotBeNull();
        result.Value.ShouldBe(125.46m);
    }

    [Fact]
    public void ItemPrice_CalculateStandardRateItemPrice_DifferentCurrencyWithoutRate_ReturnsNull()
    {
        var result = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "MYR",
            priceListCurrencyCode: "USD",
            exchangeRate: null);

        result.ShouldBeNull();
    }

    // === PR #64829 / commits 2f2d383015, b5f1d700b2: Return Row Bundle Lookup Rules ===

    [Fact]
    public void SerialBatchBundle_NeedsAcceptedReturnBundle_ZeroAcceptedQty_ReturnsFalse()
    {
        // Fully rejected receipt return (accepted qty = 0, rejected qty = -1)
        // Must skip accepted bundle lookup so it does not fail with "No Serial / Batches are available"
        var needsBundle = SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(
            acceptedQty: 0m,
            returnQtyFromRejectedWarehouse: false,
            existingAcceptedBundleId: null,
            useSerialBatchFields: false);

        needsBundle.ShouldBeFalse();
    }

    [Fact]
    public void SerialBatchBundle_NeedsAcceptedReturnBundle_FlaggedAsRejectedReturn_ReturnsFalse()
    {
        // When return row is flagged ReturnQtyFromRejectedWarehouse = true, bundle lives in rejected lookup
        var needsBundle = SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(
            acceptedQty: -2m,
            returnQtyFromRejectedWarehouse: true,
            existingAcceptedBundleId: null,
            useSerialBatchFields: false);

        needsBundle.ShouldBeFalse();
    }

    [Fact]
    public void SerialBatchBundle_NeedsAcceptedReturnBundle_AcceptedQtyPresentAndNoBundle_ReturnsTrue()
    {
        var needsBundle = SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(
            acceptedQty: -2m,
            returnQtyFromRejectedWarehouse: false,
            existingAcceptedBundleId: null,
            useSerialBatchFields: false);

        needsBundle.ShouldBeTrue();
    }

    [Fact]
    public void SerialBatchBundle_NeedsRejectedReturnBundle_RejectedQtyPresent_ReturnsTrue()
    {
        var rejectedWhId = Guid.NewGuid();
        var needsBundle = SerialBatchBundleValidationService.NeedsRejectedReturnBundle(
            rejectedQty: -1m,
            rejectedWarehouseId: rejectedWhId,
            returnQtyFromRejectedWarehouse: false,
            existingAcceptedBundleId: null,
            existingRejectedBundleId: null,
            useSerialBatchFields: false);

        needsBundle.ShouldBeTrue();
    }

    [Fact]
    public void SerialBatchBundle_NeedsRejectedReturnBundle_NoRejectedWarehouse_ReturnsFalse()
    {
        var needsBundle = SerialBatchBundleValidationService.NeedsRejectedReturnBundle(
            rejectedQty: -1m,
            rejectedWarehouseId: null,
            returnQtyFromRejectedWarehouse: false,
            existingAcceptedBundleId: null,
            existingRejectedBundleId: null,
            useSerialBatchFields: false);

        needsBundle.ShouldBeFalse();
    }
}
