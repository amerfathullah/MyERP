using System;
using System.Collections.Generic;
using MyERP.Core.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 24:
/// 1) Party account currency selection matches server accepted currency (PR #64936 / commit 9ecfac159f).
/// 2) Subcontracting receipt return row sends back rejected qty only while flagged and in rejected warehouse (PR #64938 / commit addb262ffc, 7a711015db, aeb5278994).
/// 3) Reset operating cost when draft Work Order changes to BOM without operations (PR #65001 / commit 00a1268b74, c3f3c96a2c).
/// 4) Preserving component stock qty per unit from Sales Order packed ratio on bundle decomposition (PR #65005 / commit ea7e451bcf, 710a59f031).
/// </summary>
public class UpstreamBatch24ParityTests
{
    // === 1) Party Account Billing Currency Selection (PR #64936) ===

    [Fact]
    public void PartyValidationService_GetPartyAccountCurrency_WithBillingCurrency_ReturnsBillingCurrency()
    {
        // When party has a billing currency (e.g. USD), only accounts in USD are accepted
        var currency = PartyValidationService.GetPartyAccountCurrency("USD", "MYR");
        currency.ShouldBe("USD");
    }

    [Fact]
    public void PartyValidationService_GetPartyAccountCurrency_WithWhitespaceBillingCurrency_Trims()
    {
        var currency = PartyValidationService.GetPartyAccountCurrency("  EUR  ", "MYR");
        currency.ShouldBe("EUR");
    }

    [Fact]
    public void PartyValidationService_GetPartyAccountCurrency_WithoutBillingCurrency_ReturnsCompanyCurrency()
    {
        // When party has no billing currency, default to company currency
        var currencyNull = PartyValidationService.GetPartyAccountCurrency(null, "MYR");
        currencyNull.ShouldBe("MYR");

        var currencyEmpty = PartyValidationService.GetPartyAccountCurrency("", "MYR");
        currencyEmpty.ShouldBe("MYR");

        var currencyWhitespace = PartyValidationService.GetPartyAccountCurrency("   ", "MYR");
        currencyWhitespace.ShouldBe("MYR");
    }

    // === 2) Subcontracting Receipt Return Rejected Flag Rule (PR #64938) ===

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_WhenFlaggedAndInRejectedWarehouse_ReturnsTrue()
    {
        var rejectedWarehouseId = Guid.NewGuid();

        var isRejected = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWarehouseId,
            originalRejectedWarehouseId: rejectedWarehouseId);

        isRejected.ShouldBeTrue();
    }

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_WhenAcceptedReturnFromSameWarehouseAsRejected_ReturnsFalse()
    {
        // Per ERPNext PR #64938 (commit addb262ffc):
        // An accepted return (returnQtyFromRejectedWarehouse = false) whose warehouse happens
        // to match original rejected warehouse is NOT treated as rejected; it retains finished goods rate!
        var warehouseId = Guid.NewGuid();

        var isRejected = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: warehouseId,
            originalRejectedWarehouseId: warehouseId);

        isRejected.ShouldBeFalse();
    }

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_WhenFlaggedButWarehouseMismatch_ReturnsFalse()
    {
        var rejectedWarehouseId = Guid.NewGuid();
        var otherWarehouseId = Guid.NewGuid();

        var isRejected = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: otherWarehouseId,
            originalRejectedWarehouseId: rejectedWarehouseId);

        isRejected.ShouldBeFalse();
    }

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_WhenWarehouseNull_ReturnsFalse()
    {
        var rejectedWarehouseId = Guid.NewGuid();

        var isRejected1 = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: null,
            originalRejectedWarehouseId: rejectedWarehouseId);
        isRejected1.ShouldBeFalse();

        var isRejected2 = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWarehouseId,
            originalRejectedWarehouseId: null);
        isRejected2.ShouldBeFalse();
    }

    [Fact]
    public void SubcontractingManager_GetSubcontractingReturnWarehouse_SelectsCorrectOriginalWarehouse()
    {
        var acceptedWh = Guid.NewGuid();
        var rejectedWh = Guid.NewGuid();

        // When flagged and returning from rejected warehouse -> routes to original rejected warehouse
        var wh1 = SubcontractingManager.GetSubcontractingReturnWarehouse(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWh,
            originalAcceptedWarehouseId: acceptedWh,
            originalRejectedWarehouseId: rejectedWh);
        wh1.ShouldBe(rejectedWh);

        // When unflagged (accepted return) even if return warehouse is rejectedWh -> routes to original accepted warehouse
        var wh2 = SubcontractingManager.GetSubcontractingReturnWarehouse(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: rejectedWh,
            originalAcceptedWarehouseId: acceptedWh,
            originalRejectedWarehouseId: rejectedWh);
        wh2.ShouldBe(acceptedWh);
    }

    // === 3) Work Order Operating Cost Reset (PR #65001) ===

    [Fact]
    public void WorkOrder_CalculateOperatingCost_ResetsOperatingCost_WhenBOMHasNoOperations()
    {
        // Work Order initially with operating cost from operations
        var wo = new WorkOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WO-2026-0001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            5m);

        wo.AdditionalOperatingCost = 50m;
        wo.CalculateOperatingCost(120m);

        wo.PlannedOperatingCost.ShouldBe(120m);
        wo.TotalOperatingCost.ShouldBe(170m); // 50 + 120

        // Draft Work Order BOM changes to a BOM without operations (PR #65001 / commit 00a1268b74)
        wo.CalculateOperatingCost(0m);

        wo.PlannedOperatingCost.ShouldBe(0m);
        wo.TotalOperatingCost.ShouldBe(50m); // 50 + 0
    }

    [Fact]
    public void WorkOrder_CalculateOperatingCost_UsesActualCost_WhenPresent()
    {
        var wo = new WorkOrder(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WO-2026-0002",
            Guid.NewGuid(),
            Guid.NewGuid(),
            10m);

        wo.ActualOperatingCost = 200m;
        wo.CalculateOperatingCost(100m);

        wo.PlannedOperatingCost.ShouldBe(100m);
        // Total cost prefers ActualOperatingCost when non-zero
        wo.TotalOperatingCost.ShouldBe(200m);
    }

    // === 4) Product Bundle Component Stock Qty Per Unit Preservation (PR #65005) ===

    [Fact]
    public void ProductBundleDecomposition_GetComponentStockQtyPerUnit_PreservesPackedRatio_WhenConversionFactorChanges()
    {
        // Scenario (PR #65005 / commit ea7e451bcf & commit f0243bace9):
        // 1 bundle has 1 Box of component.
        // At order time, 1 Box = 10 Nos (soPackedQtyPerUnit = 10 Nos, originalPackedQty = 20 for qty = 2).
        // Later, Item UOM conversion factor changes: 1 Box = 12 Nos.
        // When checking against original order/packed document, the original packed ratio (10) must be preserved!
        var bundleComponentQty = 1m;
        var newConversionFactor = 12m;
        var soPackedQtyPerUnit = 10m;
        var originalPackedQty = 20m;
        var originalOrderStockQty = 2m;

        var qtyPerUnit = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty,
            newConversionFactor,
            soPackedQtyPerUnit: soPackedQtyPerUnit,
            originalPackedQty: originalPackedQty,
            originalOrderStockQty: originalOrderStockQty);

        qtyPerUnit.ShouldBe(10m); // Preserved original packed ratio, NOT new 12
    }

    [Fact]
    public void ProductBundleDecomposition_GetComponentStockQtyPerUnit_UsesExactFactor_WhenConversionFactorUnchanged()
    {
        // When conversion factor hasn't changed (PR #65052), uses exact factor to avoid compounding rounding errors
        var bundleComponentQty = 1.3333m;
        var conversionFactor = 3m;
        var exactQtyPerUnit = bundleComponentQty * conversionFactor; // 3.9999m
        var originalOrderStockQty = 10m;
        var originalPackedQty = Math.Round(exactQtyPerUnit * originalOrderStockQty, 4); // 39.9990m

        var qtyPerUnit = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty,
            conversionFactor,
            soPackedQtyPerUnit: Math.Round(exactQtyPerUnit, 4),
            originalPackedQty: originalPackedQty,
            originalOrderStockQty: originalOrderStockQty);

        qtyPerUnit.ShouldBe(exactQtyPerUnit);
    }
}
