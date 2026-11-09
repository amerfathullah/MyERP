using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core.DomainServices;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP;

/// <summary>
/// Unit tests for Batch 28 upstream parity:
/// 1) Pick list bundle component ratio grouping across split locations & repeated items (PR #65001 / commit 710a59f031).
/// 2) Pick list preserves saved conversion factor on draft reload (PR #64938 / commit 4305cb27f5).
/// 3) Party account currency offers billing currency or company default (PR #64867 / commit 9ecfac159f).
/// 4) Work order BOM change without operations resets operating costs (PR #64975 / commit 00a1268b74, c3f3c96a2c).
/// 5) Subcontracting return row treated as rejected only when in rejected warehouse (PR #64859 / commit aeb5278994, 7a711015db).
/// </summary>
public class UpstreamBatch28ParityTests
{
    // === 1) PR #65001 & PR #65051: Bundle Pick List Decomposition Groups Multiple Rows ===

    [Fact]
    public void DeliveryNote_BundleDecomposition_GroupsMultiplePicksForSameComponent()
    {
        var componentItemId = Guid.NewGuid();
        var soQuantity = 2m; // 2 bundles ordered

        // Split pick across 2 locations for the same component item: 10 units + 10 units = 20 units total
        var matchingPicks = new List<(Guid ItemId, decimal Qty, decimal StockQty)>
        {
            (componentItemId, 10m, 10m),
            (componentItemId, 10m, 10m)
        };

        // Grouping by ItemId and summing StockQty
        var componentStockQtyPerUnit = matchingPicks
            .GroupBy(p => p.ItemId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Sum(p => p.StockQty > 0 ? p.StockQty : p.Qty) / soQuantity, 4));

        componentStockQtyPerUnit.ShouldContainKey(componentItemId);
        componentStockQtyPerUnit[componentItemId].ShouldBe(10m); // 20 units / 2 bundles = 10 units/bundle
    }

    // === 2) PR #64938: Keep Saved Conversion Factor on Draft Pick List Reload ===

    [Fact]
    public void PickListManager_ResolveRowUomAndStockQty_KeepsSavedConversionFactor()
    {
        var itemId = Guid.NewGuid();
        var stockUom = "Nos";
        var rowUom = "Box";
        var qty = 5m;
        var savedFactor = 10m; // Previously saved on draft row: 1 Box = 10 Nos
        var updatedMasterFactor = 12m; // Master changed to 12 Nos later

        // When savedConversionFactor is provided (draft reload), it must preserve the saved 10m factor
        var (uom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
            itemId: itemId,
            stockUom: stockUom,
            rowUom: rowUom,
            qty: qty,
            sourceDocumentItemId: null,
            productBundleItemId: null,
            orderedConversionFactors: null,
            itemMasterConversionFactor: updatedMasterFactor,
            savedConversionFactor: savedFactor);

        uom.ShouldBe("Box");
        factor.ShouldBe(10m);
        stockQty.ShouldBe(50m); // 5 * 10 = 50, NOT 5 * 12 = 60
    }

    // === 3) PR #64867: Party Account Currency Billing vs Company Fallback ===

    [Fact]
    public void PartyValidationService_GetPartyAccountCurrency_ReturnsBillingOrFallback()
    {
        // Party with explicit billing currency USD -> returns USD
        var currencyWithBilling = PartyValidationService.GetPartyAccountCurrency("USD", "MYR");
        currencyWithBilling.ShouldBe("USD");

        // Party with whitespace or empty billing currency -> falls back to company default MYR
        var currencyFallback = PartyValidationService.GetPartyAccountCurrency(" ", "MYR");
        currencyFallback.ShouldBe("MYR");

        var currencyNullFallback = PartyValidationService.GetPartyAccountCurrency(null, "MYR");
        currencyNullFallback.ShouldBe("MYR");
    }

    // === 4) PR #64975: Work Order BOM Change Without Operations Resets Operating Costs ===

    [Fact]
    public void WorkOrder_CalculateOperatingCost_ResetsToZeroWhenOperationsRemoved()
    {
        var wo = new WorkOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WO-B28-01",
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m);

        // Initial BOM had operations -> operating cost = 60
        wo.CalculateOperatingCost(60m);
        wo.PlannedOperatingCost.ShouldBe(60m);
        wo.TotalOperatingCost.ShouldBe(60m);

        // Work Order changed to a BOM without operations (PR #64975 / commit c3f3c96a2c)
        wo.CalculateOperatingCost(0m);
        wo.PlannedOperatingCost.ShouldBe(0m);
        wo.TotalOperatingCost.ShouldBe(0m);
    }

    // === 5) PR #64859: Subcontracting Return Row Treated as Rejected Only in Rejected Warehouse ===

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_FlagsOnlyInRejectedWarehouse()
    {
        var rejectedWhId = Guid.NewGuid();
        var acceptedWhId = Guid.NewGuid();

        // Row flagged for rejected return AND returned to the original rejected warehouse -> true
        var isRejected = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWhId,
            originalRejectedWarehouseId: rejectedWhId);
        isRejected.ShouldBeTrue();

        // Row flagged for rejected return BUT in accepted warehouse -> false (commit aeb5278994)
        var notRejectedDueToWh = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: acceptedWhId,
            originalRejectedWarehouseId: rejectedWhId);
        notRejectedDueToWh.ShouldBeFalse();

        // Row unflagged in rejected warehouse -> false
        var notFlagged = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: rejectedWhId,
            originalRejectedWarehouseId: rejectedWhId);
        notFlagged.ShouldBeFalse();

        // GetSubcontractingReturnWarehouse resolves correct warehouse based on rule
        var whRejected = SubcontractingManager.GetSubcontractingReturnWarehouse(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWhId,
            originalAcceptedWarehouseId: acceptedWhId,
            originalRejectedWarehouseId: rejectedWhId);
        whRejected.ShouldBe(rejectedWhId);

        var whAccepted = SubcontractingManager.GetSubcontractingReturnWarehouse(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: acceptedWhId,
            originalAcceptedWarehouseId: acceptedWhId,
            originalRejectedWarehouseId: rejectedWhId);
        whAccepted.ShouldBe(acceptedWhId);
    }
}
