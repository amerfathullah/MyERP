using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Purchasing.DomainServices;
using MyERP.Sales.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 22:
/// 1) Product bundle component qty derivation on update / qty increase (PR #65052 / commits 80fe289cd6 &amp; 1af1ab99de).
/// 2) Pick list bundle picking with repeated components (PR #65051 / commits 740575ad27, 3750ba894b, fe42da0c74).
/// 3) Subcontracting receipt return serial/batch consumed rate calculation (PR #64861 / commits c28d1f5bc6, c3ece0832e, eb0ee73b9d).
/// 4) Serial and batch return deduping across multiple return rows (PR #64590 / commits 9403e3df8d, c919eeb108, c4b462c136).
/// 5) Production plan required material merging across transfer and purchase UOMs (PR #65049 / commits fd4fbcf7ff, 5eabaab431).
/// </summary>
public class UpstreamBatch22ParityTests
{
    // === 1) Product Bundle Component Qty on Update Items (PR #65052) ===

    [Fact]
    public void ProductBundleDecomposition_GetComponentStockQtyPerUnit_ExactConversion_WhenConversionUnchanged()
    {
        // Scenario (PR #65052 / commit 80fe289cd6 & 6452d9d269):
        // Component conversion factor is 1/3 (0.333333...).
        // Initially ordered 1 bundle with 1 unit component -> rounded packed qty = 0.3333 (or orderedRatio = 0.3333).
        // When order qty increases to 30, exact conversion factor should be used (1 * 1/3 * 30 = 10),
        // NOT 0.3333 * 30 = 9.999.
        var bundleComponentQty = 1m;
        var conversionFactor = 1m / 3m; // 0.3333333...
        var originalOrderStockQty = 1m;
        var originalPackedQty = Math.Round(bundleComponentQty * conversionFactor * originalOrderStockQty, 4); // 0.3333m
        var soPackedQtyPerUnit = originalPackedQty / originalOrderStockQty; // 0.3333m

        var qtyPerUnit = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty: bundleComponentQty,
            conversionFactor: conversionFactor,
            soPackedQtyPerUnit: soPackedQtyPerUnit,
            originalPackedQty: originalPackedQty,
            originalOrderStockQty: originalOrderStockQty,
            precision: 4);

        // Exact conversion factor is returned because conversion hasn't changed
        qtyPerUnit.ShouldBe(conversionFactor);
        Math.Round(qtyPerUnit * 30m, 4).ShouldBe(10m);
    }

    [Fact]
    public void ProductBundleDecomposition_GetComponentStockQtyPerUnit_KeepsOrderedRatio_WhenConversionChanged()
    {
        // Scenario (PR #65052 / commit e0617e75ba & 1af1ab99de):
        // Originally ordered 1 Box with conversion factor 10 -> packed 10 Nos (ordered ratio = 10).
        // Later, component Box conversion factor changed to 12.
        // On update, the existing ordered ratio (10) must be preserved because conversion changed!
        var bundleComponentQty = 1m;
        var newConversionFactor = 12m;
        var originalOrderStockQty = 2m; // 2 bundles
        var originalPackedQty = 20m; // 2 * 10
        var soPackedQtyPerUnit = 10m; // originally 10 per unit

        var qtyPerUnit = ProductBundleDecompositionService.GetComponentStockQtyPerUnit(
            bundleComponentQty: bundleComponentQty,
            conversionFactor: newConversionFactor,
            soPackedQtyPerUnit: soPackedQtyPerUnit,
            originalPackedQty: originalPackedQty,
            originalOrderStockQty: originalOrderStockQty,
            precision: 4);

        // Preserves original packed ratio of 10
        qtyPerUnit.ShouldBe(10m);
    }

    // === 2) Pick List Bundle with Repeated Components (PR #65051) ===

    [Fact]
    public void PickListManager_ComputePickedQtyForBundle_RepeatedComponents_AccumulatesPickedQty()
    {
        // Scenario (PR #65051 / commit 740575ad27 & 2f254b7907):
        // Bundle contains repeated component:
        // Component A: row 1 requires 1 unit, row 2 requires 2 units -> Total required = 3 units per bundle.
        // Sales order has 3 bundles -> total required = 9 units.
        // Pick list has two locations for Component A: row 1 picked 3 units, row 2 picked 6 units -> Total 9 units picked.
        var soItemId = Guid.NewGuid();
        var componentAId = Guid.NewGuid();
        var pickListId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var pickItems = new List<PickListItem>
        {
            new PickListItem(
                id: Guid.NewGuid(),
                pickListId: pickListId,
                itemId: componentAId,
                warehouseId: warehouseId,
                qty: 3m,
                stockQty: 3m,
                itemName: "Component A",
                batchId: null,
                productBundleItemId: Guid.NewGuid())
            {
                SourceDocumentItemId = soItemId
            },
            new PickListItem(
                id: Guid.NewGuid(),
                pickListId: pickListId,
                itemId: componentAId,
                warehouseId: warehouseId,
                qty: 6m,
                stockQty: 6m,
                itemName: "Component A",
                batchId: null,
                productBundleItemId: Guid.NewGuid())
            {
                SourceDocumentItemId = soItemId
            }
        };

        // Total required across repeated rows is 3 per bundle
        var bundleMap = new Dictionary<Guid, decimal>
        {
            [componentAId] = 3m
        };

        var pickedBundles = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, bundleMap, pendingOnly: false);
        pickedBundles.ShouldBe(3);
    }

    [Fact]
    public void PickListManager_ComputePickedQtyForBundle_RepeatedComponents_PendingOnly_CorrectlyLeavesRemainingPending()
    {
        // Scenario (PR #65051 / commit fe42da0c74 & cfa681da8c):
        // 3 bundles picked (9 component units total).
        // 1 bundle delivered (3 units delivered across the components: 1 on row 1, 2 on row 2).
        // Pending bundles should be exactly 2!
        var soItemId = Guid.NewGuid();
        var componentAId = Guid.NewGuid();
        var pickListId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var row1 = new PickListItem(
            id: Guid.NewGuid(),
            pickListId: pickListId,
            itemId: componentAId,
            warehouseId: warehouseId,
            qty: 3m,
            stockQty: 3m,
            itemName: "Component A",
            batchId: null,
            productBundleItemId: Guid.NewGuid())
        {
            SourceDocumentItemId = soItemId
        };
        row1.RecordDelivery(1m); // 1 delivered, 2 remaining

        var row2 = new PickListItem(
            id: Guid.NewGuid(),
            pickListId: pickListId,
            itemId: componentAId,
            warehouseId: warehouseId,
            qty: 6m,
            stockQty: 6m,
            itemName: "Component A",
            batchId: null,
            productBundleItemId: Guid.NewGuid())
        {
            SourceDocumentItemId = soItemId
        };
        row2.RecordDelivery(2m); // 2 delivered, 4 remaining

        var bundleMap = new Dictionary<Guid, decimal>
        {
            [componentAId] = 3m
        };

        var pendingBundles = PickListManager.ComputePickedQtyForBundle(
            new[] { row1, row2 }, soItemId, bundleMap, pendingOnly: true);

        // (2 + 4) / 3 = 2 bundles pending
        pendingBundles.ShouldBe(2);
    }

    // === 3) Subcontracting Receipt Return Consumed Rate (PR #64861) ===

    [Fact]
    public void SubcontractingManager_GetSerialBatchReturnRate_WeightedAverage_ByBatchAndSerial()
    {
        // Scenario (PR #64861 / commit eb0ee73b9d):
        // Subcontracting Receipt consumed:
        // Batch 1: rate 100
        // Batch 2: rate 300
        // Return row sends back 4 of Batch 1 and 6 of Batch 2 (total 10).
        // Weighted rate = (4 * 100 + 6 * 300) / 10 = 2200 / 10 = 220.
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        var consumedRates = new Dictionary<(Guid? BatchId, string? SerialNo), decimal>
        {
            [(batch1, null)] = 100m,
            [(batch2, null)] = 300m
        };

        var returnedEntries = new List<(Guid? BatchId, string? SerialNo, decimal Qty)>
        {
            (batch1, null, 4m),
            (batch2, null, 6m)
        };

        var rate = SubcontractingManager.GetSerialBatchReturnRate(returnedEntries, consumedRates);
        rate.ShouldNotBeNull();
        rate.Value.ShouldBe(220m);
    }

    [Fact]
    public void SubcontractingManager_GetSerialBatchReturnRate_ReturnsNull_WhenNoMatchingConsumedEntries()
    {
        var consumedRates = new Dictionary<(Guid? BatchId, string? SerialNo), decimal>
        {
            [(Guid.NewGuid(), null)] = 150m
        };

        var returnedEntries = new List<(Guid? BatchId, string? SerialNo, decimal Qty)>
        {
            (Guid.NewGuid(), null, 5m)
        };

        var rate = SubcontractingManager.GetSerialBatchReturnRate(returnedEntries, consumedRates);
        rate.ShouldBeNull();
    }

    // === 4) Serial and Batch Return Deduping (PR #64590) ===

    [Fact]
    public void SerialBatchBundleValidationService_RemovePickedSerialBatches_DropsSerialsAndDeductsBatchQty()
    {
        // Scenario (PR #64590 / commits 9403e3df8d, c919eeb108, c4b462c136):
        // Original row had Serial Nos SN1, SN2 and Batches B1: 5, B2: 5 (total 10).
        // Return Row 1 picks SN1 and 3 of B1.
        // After RemovePickedSerialBatches, available pool must have SN2 only, and B1: 2, B2: 5.
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        var availableSerials = new HashSet<string> { "SN1", "SN2" };
        var availableBatches = new Dictionary<Guid, decimal>
        {
            [batch1] = 5m,
            [batch2] = 5m
        };

        var pickedSerials = new[] { "SN1" };
        var pickedBatches = new Dictionary<Guid, decimal>
        {
            [batch1] = 3m
        };

        SerialBatchBundleValidationService.RemovePickedSerialBatches(
            availableSerials, availableBatches, pickedSerials, pickedBatches);

        availableSerials.ShouldContain("SN2");
        availableSerials.ShouldNotContain("SN1");

        availableBatches[batch1].ShouldBe(2m);
        availableBatches[batch2].ShouldBe(5m);

        // Next return row picks remaining 2 of B1 and 5 of B2
        SerialBatchBundleValidationService.RemovePickedSerialBatches(
            availableSerials, availableBatches, new[] { "SN2" }, new Dictionary<Guid, decimal> { [batch1] = 2m, [batch2] = 5m });

        availableSerials.ShouldBeEmpty();
        availableBatches.ShouldBeEmpty();
    }

    // === 5) Production Plan Required Material UOM Merging (PR #65049) ===

    [Fact]
    public void ProductionPlanAppService_MergeRequiredQuantities_ConvertsToTargetUom_AndRoundsOnce()
    {
        // Scenario (PR #65049 / commits fd4fbcf7ff, 5eabaab431):
        // Target row is in Purchase UOM "Box" (conversion factor 10 Nos per Box). Currently target has 3 Box (30 Nos).
        // Incoming row is in Stock UOM "Nos" (conversion factor 1). Incoming qty is 20 Nos.
        // Merging 20 Nos into Box: 20 / 10 = 2 Box.
        // Total merged = 3 + 2 = 5 Box.
        var merged = ProductionPlanAppService.MergeRequiredQuantities(
            targetRowQty: 3m,
            targetConversionFactor: 10m,
            incomingQty: 20m,
            incomingConversionFactor: 1m,
            precision: 4);

        merged.ShouldBe(5m);
    }
}
