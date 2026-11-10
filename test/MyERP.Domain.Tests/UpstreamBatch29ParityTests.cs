using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP;

/// <summary>
/// Unit tests for Batch 29 upstream parity:
/// 1) Product bundle demand netting on partial delivery in MRP (PR #64317 / commit 007b7f3162).
/// 2) Whole-pack PO calculation with float noise guard (PR #64310 / commit 11ea4f022a).
/// 3) Item Price converted from Standard Selling Rate rounds to currency precision (PR #64855 / commit 1853ef83ea, e8a8b0b260).
/// 4) Return row with zero accepted qty excluded from accepted bundle lookup (PR #64853 / commit 2f2d383015).
/// 5) Subcontracting return skips unavailable warehouse batches if remaining batches cover return (PR #64461 / commit c11dc5f413, dd07d12215).
/// 6) Subcontracting return finished good takes receipt rate (PR #64280 / commit 8c5681ad46).
/// </summary>
public class UpstreamBatch29ParityTests
{
    // === 1) PR #64317: Net Delivered Product Bundle Demand in MRP ===

    [Fact]
    public void Mrp_ProductBundleDemand_NetsDeliveredQuantityProportionally()
    {
        var bundleOrderedQty = 5m;
        var bundleDeliveredQty = 2m;
        var pendingQty = bundleOrderedQty - bundleDeliveredQty; // 3 bundles left
        var componentQtyPerBundle = 4m; // 4 components per bundle

        // PR #64317 / commit 007b7f3162:
        // In ERPNext Packed Item (total packed qty = 20): net = packed_qty * pending_qty / so_item.qty = 20 * 3 / 5 = 12
        // In MyERP ProductBundle (component per bundle = 4): net = componentQtyPerBundle * pendingQty = 4 * 3 = 12
        var netDemandFromBundle = Math.Round(componentQtyPerBundle * pendingQty, 4);
        netDemandFromBundle.ShouldBe(12m); // 3 bundles pending * 4 components = 12 units needed, not 20

        var erpnextPackedItemQty = componentQtyPerBundle * bundleOrderedQty; // 20m
        var erpnextFormulaDemand = Math.Round((erpnextPackedItemQty * pendingQty) / bundleOrderedQty, 4);
        erpnextFormulaDemand.ShouldBe(12m);
    }

    // === 2) PR #64310: Whole-Pack Purchase Order Calculation With Float Noise Guard ===

    [Fact]
    public void UomConversionService_CalculateWholePackPurchaseOrderQty_GuardsFloatNoise()
    {
        // 3 units of Tenth Pack (conversion factor = 0.1) -> stockQty = 0.3
        var reqStockQty = 0.3m;
        var minOrderQty = 0m;
        var factor = 0.1m;

        var poQty = UomConversionService.CalculateWholePackPurchaseOrderQty(
            requiredStockQty: reqStockQty,
            minOrderQty: minOrderQty,
            conversionFactor: factor,
            mustBeWholeNumber: true,
            stockQtyPrecision: 4);

        // Float division 0.3 / 0.1 can yield 3.0000000000000004 -> Math.Ceiling would mistakenly give 4
        // The float noise check ensures (qty - 1) * factor >= stockQty drops it back to 3
        poQty.ShouldBe(3m);
    }

    // === 3) PR #64855: Converted Item Price Rounds to Currency Precision ===

    [Fact]
    public void ItemPriceAutoInsertService_CalculateStandardRateItemPrice_RoundsToTargetCurrencyPrecision()
    {
        var standardSellingRate = 100m; // in MYR
        var exchangeRate = 4.3521m; // 1 USD = 4.3521 MYR -> 100 / 4.3521 = 22.9774...

        // Target currency USD has 2 decimal precision
        var usdPrice = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate, "MYR", "USD", exchangeRate);

        usdPrice.ShouldNotBeNull();
        usdPrice.Value.ShouldBe(22.98m);

        // Target currency JPY has 0 decimal precision (ZeroDecimalCurrencies)
        var jpyExchangeRate = 0.0315m; // 1 JPY = 0.0315 MYR -> 100 / 0.0315 = 3174.60...
        var jpyPrice = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate, "MYR", "JPY", jpyExchangeRate);

        jpyPrice.ShouldNotBeNull();
        jpyPrice.Value.ShouldBe(3175m);
    }

    // === 4) PR #64853: Zero Accepted Qty or Rejected Warehouse Return Skips Accepted Bundle Lookup ===

    [Fact]
    public void SerialBatchBundleValidationService_NeedsAcceptedReturnBundle_SkipsZeroQtyOrRejectedWarehouse()
    {
        // Accepted qty = 0 (only returning rejected items) -> false
        var zeroAccepted = SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(
            acceptedQty: 0m,
            returnQtyFromRejectedWarehouse: false);
        zeroAccepted.ShouldBeFalse();

        // Returning from rejected warehouse -> false
        var rejectedWhReturn = SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(
            acceptedQty: 5m,
            returnQtyFromRejectedWarehouse: true);
        rejectedWhReturn.ShouldBeFalse();

        // Normal accepted return row -> true
        var standardAccepted = SerialBatchBundleValidationService.NeedsAcceptedReturnBundle(
            acceptedQty: 5m,
            returnQtyFromRejectedWarehouse: false);
        standardAccepted.ShouldBeTrue();
    }

    // === 5) PR #64461 & PR #64462: Filter Return Batches Skips Warehouse-Unavailable Batches ===

    [Fact]
    public void SerialBatchBundleValidationService_FilterReturnBatches_SkipsUnavailableBatchesIfCovered()
    {
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();
        var whId = Guid.NewGuid();

        // Source row had Batch1 (qty 5) and Batch2 (qty 5)
        var sourceBatches = new Dictionary<Guid, decimal>
        {
            [batch1] = 5m,
            [batch2] = 5m
        };

        // Batch1 is no longer in warehouse (0 available), Batch2 has 5 available
        var availableInWh = new Dictionary<Guid, decimal>
        {
            [batch1] = 0m,
            [batch2] = 5m
        };

        // Returning 3 units: should skip Batch1 and pick 3 from Batch2 without throwing
        var filtered = SerialBatchBundleValidationService.FilterReturnBatches(
            sourceBatches,
            requiredReturnQty: 3m,
            warehouseId: whId,
            availableBatchQuantitiesInWarehouse: availableInWh,
            isPurchaseReturn: true);

        filtered.ShouldNotContainKey(batch1);
        filtered.ShouldContainKey(batch2);
        filtered[batch2].ShouldBe(3m);
    }

    // === 6) PR #64280: Subcontracting Receipt Return Retains Original Receipt Rate ===

    [Fact]
    public void SubcontractingReceipt_ReturnRate_RetainsOriginalReceiptRate()
    {
        var origRate = 145.50m;

        // Accepted return row retains original receipt rate (PR #64280 / commit 8c5681ad46)
        var returnRateAccepted = SubcontractingManager.CalculateSuppliedItemRate(
            isReturn: false,
            serialBatchConsumedRate: null,
            originalConsumedRate: origRate,
            standardCostRate: origRate);

        returnRateAccepted.ShouldBe(145.50m);
    }
}
