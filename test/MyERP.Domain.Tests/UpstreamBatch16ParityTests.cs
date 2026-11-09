using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 16:
/// 1) Work Order operating cost recalculation resets when BOM has no operations (PR #64974, commit 00a1268b74 / c3f3c96a2c).
/// 2) Pick List retains ordered conversion factor over subsequent Item master UOM factor changes (PR #64935, commit d0796ff33b / 0de4680ea9).
/// 3) Item Price standard selling rate conversion rounds to target currency precision (PR #64830, commit 1853ef83ea; PR #64828, commit e8a8b0b260).
/// 4) Cash Flow effective start date calculation in accumulated mode with mid-year date range (PR #64939, commit 461d586d8b).
/// </summary>
public class UpstreamBatch16ParityTests
{
    // === PR #64974 / commit 00a1268b74: Work Order Operating Cost Recalculation on BOM Change ===

    [Fact]
    public void WorkOrder_CalculateOperatingCost_ResetsOperatingCostsWhenOperationsAreCleared_00a1268b74()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var wo = new WorkOrder(
            id: Guid.NewGuid(),
            companyId: companyId,
            workOrderNumber: "MFG-WO-2026-0001",
            itemId: itemId,
            bomId: bomId,
            quantity: 10m);

        // Initial BOM had operations totaling 120
        wo.CalculateOperatingCost(plannedOperatingCostFromOperations: 120m);
        wo.PlannedOperatingCost.ShouldBe(120m);
        wo.TotalOperatingCost.ShouldBe(120m);

        // Work Order changes BOM to a BOM without operations: planned operating cost resets to 0
        wo.CalculateOperatingCost(plannedOperatingCostFromOperations: 0m);
        wo.PlannedOperatingCost.ShouldBe(0m);
        wo.TotalOperatingCost.ShouldBe(0m);
    }

    [Fact]
    public void WorkOrder_CalculateOperatingCost_IncludesAdditionalAndCorrectiveCosts()
    {
        var wo = new WorkOrder(
            id: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            workOrderNumber: "MFG-WO-2026-0002",
            itemId: Guid.NewGuid(),
            bomId: Guid.NewGuid(),
            quantity: 5m)
        {
            AdditionalOperatingCost = 25m,
            CorrectiveOperationCost = 15m
        };

        wo.CalculateOperatingCost(plannedOperatingCostFromOperations: 80m);
        wo.PlannedOperatingCost.ShouldBe(80m);
        // Total = 25 (Additional) + 80 (Planned) + 15 (Corrective) = 120
        wo.TotalOperatingCost.ShouldBe(120m);
    }

    // === PR #64935 / commit d0796ff33b / 0de4680ea9: Pick List Ordered Conversion Factor Retention ===

    [Fact]
    public void PickListManager_ResolveRowUomAndStockQty_PreservesOrderedConversionFactor_0de4680ea9()
    {
        var rowId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var uom = "Box";

        // Source sales order had conversion factor 12 for Box
        var orderedFactors = new Dictionary<(Guid RowId, Guid ItemId, string Uom), decimal>
        {
            [(rowId, itemId, "BOX")] = 12m
        };

        // Master item was subsequently edited to have conversion factor 10 for Box
        var (resolvedUom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
            itemId: itemId,
            stockUom: "Nos",
            rowUom: uom,
            qty: 5m,
            sourceDocumentItemId: rowId,
            productBundleItemId: null,
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 10m);

        resolvedUom.ShouldBe("Box");
        // Must preserve the ordered factor 12, NOT the live item master factor 10
        factor.ShouldBe(12m);
        stockQty.ShouldBe(60m); // 5 * 12
    }

    [Fact]
    public void PickListManager_ResolveRowUomAndStockQty_UsesMasterFactor_WhenNoOrderedFactorExists()
    {
        var itemId = Guid.NewGuid();

        var (resolvedUom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
            itemId: itemId,
            stockUom: "Nos",
            rowUom: "Box",
            qty: 4m,
            sourceDocumentItemId: null,
            productBundleItemId: null,
            orderedConversionFactors: null,
            itemMasterConversionFactor: 8m);

        resolvedUom.ShouldBe("Box");
        factor.ShouldBe(8m);
        stockQty.ShouldBe(32m);
    }

    // === PR #64830 / commit 1853ef83ea; PR #64828 / commit e8a8b0b260: Item Price Standard Selling Rate Rounding ===

    [Fact]
    public void ItemPriceAutoInsertService_CalculateStandardRateItemPrice_RoundsToTargetCurrencyPrecision_1853ef83ea()
    {
        // Standard selling price: 100 USD
        // Price list currency: MYR, exchange rate = 0.22222 USD/MYR (1 / 4.5)
        // Rate = 100 / 0.22222 = 450.004500045...
        // Target currency precision: 2 decimal places -> 450.00
        var convertedRate = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "USD",
            priceListCurrencyCode: "MYR",
            exchangeRate: 0.22222m,
            currencyPrecision: 2);

        convertedRate.ShouldNotBeNull();
        convertedRate.Value.ShouldBe(450.00m);

        // Target currency precision: 0 decimal places (e.g., JPY)
        var jpyRate = ItemPriceAutoInsertService.CalculateStandardRateItemPrice(
            standardSellingRate: 100m,
            defaultCurrencyCode: "USD",
            priceListCurrencyCode: "JPY",
            exchangeRate: 0.0066666m,
            currencyPrecision: 0);

        jpyRate.ShouldNotBeNull();
        jpyRate.Value.ShouldBe(15000m);
    }

    // === PR #64939 / commit 461d586d8b: Cash Flow Date Range Accumulated Start ===

    [Fact]
    public void CashFlow_GetEffectiveStartDate_CountsFromRangeStartWhenStartingMidFiscalYear_461d586d8b()
    {
        var fyStart = new DateTime(2026, 1, 1);
        var periodFromDate = new DateTime(2026, 4, 1);
        var periodYearStartDate = new DateTime(2026, 1, 1);

        // When not accumulated: returns period's own start date
        var nonAccumulatedStart = MyERP.Accounting.CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: periodFromDate,
            periodYearStartDate: periodYearStartDate,
            fiscalYearStartDate: fyStart,
            accumulatedValues: false);
        nonAccumulatedStart.ShouldBe(new DateTime(2026, 4, 1));

        // When accumulated but range starts after fiscal year start (e.g. range starts 2026-03-01):
        // periodYearStartDate is 2026-03-01, fyStart is 2026-01-01 -> max(rangeStart, fyStart) = rangeStart
        var rangeStart = new DateTime(2026, 3, 1);
        var accumulatedRangeStart = MyERP.Accounting.CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: periodFromDate,
            periodYearStartDate: rangeStart,
            fiscalYearStartDate: fyStart,
            accumulatedValues: true);
        accumulatedRangeStart.ShouldBe(new DateTime(2026, 3, 1));
    }
}
