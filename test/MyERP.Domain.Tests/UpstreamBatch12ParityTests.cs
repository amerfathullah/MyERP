using System;
using System.Collections.Generic;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 12:
/// 1) Cash Flow Report Accumulated Date Range Start Date (PR #64939, commit 461d586d8b):
///    - get_start_date uses max(periodYearStartDate, fiscalYearStartDate) when accumulatedValues is enabled.
///    - BuildPeriods sets EffectiveStartDate to range start date for custom date range mid-fiscal-year.
/// 2) Pick List Ordered Conversion Factor Preservation (PR #64935, commits 4305cb27f5, 0de4680ea9, c0f9618571):
///    - ResolveRowUomAndStockQty preserves the ordered row's conversion factor when item master UOM conversion changes.
///    - Product bundle items do not use the ordered conversion factor lookup.
/// 3) Subcontracting Receipt Return Rejected Warehouse Classification (PR #64938, commits aeb5278994, 0146c3b6d3, 7a711015db, addb262ffc):
///    - IsRejectedSubcontractingReturn requires returnQtyFromRejectedWarehouse == true AND warehouse == rejectedWarehouse.
///    - Return row without flag in rejected warehouse is treated as accepted return (not rejected return).
///    - GetSubcontractingReturnWarehouse resolves original rejected warehouse only when IsRejectedSubcontractingReturn is true.
/// </summary>
public class UpstreamBatch12ParityTests
{
    // === PR #64939 / commit 461d586d8b: Cash Flow Accumulated Start Date ===

    [Fact]
    public void CashFlow_GetEffectiveStartDate_NonAccumulated_ReturnsPeriodFromDate()
    {
        var fromDate = new DateTime(2026, 4, 1);
        var yearStart = new DateTime(2026, 4, 1);
        var fyStart = new DateTime(2026, 1, 1);

        var effective = CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: fromDate,
            periodYearStartDate: yearStart,
            fiscalYearStartDate: fyStart,
            accumulatedValues: false);

        effective.ShouldBe(fromDate);
    }

    [Fact]
    public void CashFlow_GetEffectiveStartDate_Accumulated_MidFiscalYear_ClampsToRangeStartDate()
    {
        // Date range begins 2026-04-01 (90 days into fiscal year starting 2026-01-01)
        var fromDate = new DateTime(2026, 4, 1);
        var rangeStartDate = new DateTime(2026, 4, 1);
        var fyStart = new DateTime(2026, 1, 1);

        // When accumulatedValues is true, start date must be max(rangeStart, fyStart) = 2026-04-01
        var effective = CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: fromDate,
            periodYearStartDate: rangeStartDate,
            fiscalYearStartDate: fyStart,
            accumulatedValues: true);

        effective.ShouldBe(new DateTime(2026, 4, 1));
    }

    [Fact]
    public void CashFlow_GetEffectiveStartDate_Accumulated_NextFiscalYear_ClampsToFiscalYearStart()
    {
        // Multi-year date range: range started 2025-06-01, but current period is in 2026 FY (starts 2026-01-01)
        var fromDate = new DateTime(2026, 1, 1);
        var rangeStartDate = new DateTime(2025, 6, 1);
        var fyStart = new DateTime(2026, 1, 1);

        var effective = CashFlowStatementAppService.GetEffectiveStartDate(
            periodFromDate: fromDate,
            periodYearStartDate: rangeStartDate,
            fiscalYearStartDate: fyStart,
            accumulatedValues: true);

        // max(2025-06-01, 2026-01-01) = 2026-01-01
        effective.ShouldBe(new DateTime(2026, 1, 1));
    }

    [Fact]
    public void CashFlow_BuildPeriods_AccumulatedMidYear_AllPeriodsUseRangeStartDate()
    {
        var companyId = Guid.NewGuid();
        var fiscalYears = new List<FiscalYear>
        {
            new FiscalYear(Guid.NewGuid(), companyId, "FY 2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31))
        };

        var rangeStart = new DateTime(2026, 4, 1);
        var rangeEnd = new DateTime(2026, 12, 31);

        var periods = CashFlowStatementAppService.BuildPeriods(
            rangeStart,
            rangeEnd,
            "Quarterly",
            fiscalYears,
            accumulatedValues: true);

        periods.ShouldNotBeEmpty();
        foreach (var p in periods)
        {
            p.EffectiveStartDate.ShouldBe(rangeStart);
        }
    }

    // === PR #64935 / commits 4305cb27f5, 0de4680ea9: Pick List Ordered Conversion Factor ===

    [Fact]
    public void PickList_ResolveRowUomAndStockQty_StockUom_ReturnsFactorOne()
    {
        var itemId = Guid.NewGuid();
        var (uom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
            itemId: itemId,
            stockUom: "Nos",
            rowUom: "Nos",
            qty: 5m,
            sourceDocumentItemId: null,
            productBundleItemId: null,
            orderedConversionFactors: null,
            itemMasterConversionFactor: 10m);

        uom.ShouldBe("Nos");
        factor.ShouldBe(1m);
        stockQty.ShouldBe(5m);
    }

    [Fact]
    public void PickList_ResolveRowUomAndStockQty_PreservesOrderedConversionFactorEvenIfItemMasterChanges()
    {
        var itemId = Guid.NewGuid();
        var soRowId = Guid.NewGuid();

        // Ordered with Box conversion factor = 10
        var orderedFactors = new Dictionary<(Guid RowId, Guid ItemId, string Uom), decimal>
        {
            [(soRowId, itemId, "BOX")] = 10m
        };

        // Item master conversion factor updated later to 12m
        var (uom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
            itemId: itemId,
            stockUom: "Nos",
            rowUom: "Box",
            qty: 2m,
            sourceDocumentItemId: soRowId,
            productBundleItemId: null,
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 12m);

        uom.ShouldBe("Box");
        factor.ShouldBe(10m); // Preserved from ordered row!
        stockQty.ShouldBe(20m); // 2 * 10
    }

    [Fact]
    public void PickList_ResolveRowUomAndStockQty_ProductBundleRow_DoesNotUseOrderedConversionFactor()
    {
        var itemId = Guid.NewGuid();
        var soRowId = Guid.NewGuid();
        var bundleItemId = Guid.NewGuid();

        var orderedFactors = new Dictionary<(Guid RowId, Guid ItemId, string Uom), decimal>
        {
            [(soRowId, itemId, "BOX")] = 10m
        };

        // Bundle component row: productBundleItemId is present
        var (uom, factor, stockQty) = PickListManager.ResolveRowUomAndStockQty(
            itemId: itemId,
            stockUom: "Nos",
            rowUom: "Box",
            qty: 2m,
            sourceDocumentItemId: soRowId,
            productBundleItemId: bundleItemId,
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 12m);

        uom.ShouldBe("Box");
        factor.ShouldBe(12m); // Bundle components use master/bundle factor, not ordered factor
        stockQty.ShouldBe(24m);
    }

    // === PR #64938 / commits aeb5278994, 0146c3b6d3, 7a711015db: Subcontracting Return Rejected Rules ===

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_FlaggedAndSameWarehouse_ReturnsTrue()
    {
        var rejectedWhId = Guid.NewGuid();

        var result = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWhId,
            originalRejectedWarehouseId: rejectedWhId);

        result.ShouldBeTrue();
    }

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_NotFlaggedInRejectedWarehouse_ReturnsFalse()
    {
        // Per upstream test_accepted_return_from_the_rejected_warehouse_sends_finished_goods_back_at_their_rate:
        // Even if warehouse matches rejected warehouse, if flag is false, row is accepted return!
        var rejectedWhId = Guid.NewGuid();

        var result = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: rejectedWhId,
            originalRejectedWarehouseId: rejectedWhId);

        result.ShouldBeFalse();
    }

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_FlaggedButDifferentWarehouse_ReturnsFalse()
    {
        var rejectedWhId = Guid.NewGuid();
        var otherWhId = Guid.NewGuid();

        var result = SubcontractingManager.IsRejectedSubcontractingReturn(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: otherWhId,
            originalRejectedWarehouseId: rejectedWhId);

        result.ShouldBeFalse();
    }

    [Fact]
    public void SubcontractingManager_GetSubcontractingReturnWarehouse_RejectedReturn_ReturnsRejectedWarehouse()
    {
        var acceptedWhId = Guid.NewGuid();
        var rejectedWhId = Guid.NewGuid();

        var targetWh = SubcontractingManager.GetSubcontractingReturnWarehouse(
            returnQtyFromRejectedWarehouse: true,
            returnWarehouseId: rejectedWhId,
            originalAcceptedWarehouseId: acceptedWhId,
            originalRejectedWarehouseId: rejectedWhId);

        targetWh.ShouldBe(rejectedWhId);
    }

    [Fact]
    public void SubcontractingManager_GetSubcontractingReturnWarehouse_AcceptedReturn_ReturnsAcceptedWarehouse()
    {
        var acceptedWhId = Guid.NewGuid();
        var rejectedWhId = Guid.NewGuid();

        var targetWh = SubcontractingManager.GetSubcontractingReturnWarehouse(
            returnQtyFromRejectedWarehouse: false,
            returnWarehouseId: rejectedWhId,
            originalAcceptedWarehouseId: acceptedWhId,
            originalRejectedWarehouseId: rejectedWhId);

        targetWh.ShouldBe(acceptedWhId);
    }
}
