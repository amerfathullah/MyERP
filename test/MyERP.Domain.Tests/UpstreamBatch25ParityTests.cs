using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 25:
/// 1) Pick different serial numbers and remaining batch quantities across return rows against the same original line (PR #64590 / commits 9403e3df8d, 86b8037915, c919eeb108, 4cb7ec0388, c4b462c136).
/// 2) Subcontracting return raw materials at consumed rate of batches/serials in bundle (PR #64861 / commits c28d1f5bc6, eb0ee73b9d, 5df9ac5b82).
/// 3) Production Plan: Download Required Materials shows UOM and merges transfer/purchase rows in row UOM (PR #65049 / commits 243fff334d, fd4fbcf7ff, 1182e4a8f4, 5eabaab431, 872632bf14).
/// </summary>
public class UpstreamBatch25ParityTests
{
    // === 1) PR #64590: Serial and Batch Return Depletion across Rows ===

    [Fact]
    public void PickSerialBatchesForReturn_SerializedItem_MultipleRowsPickDifferentSerialNos()
    {
        // Equivalent to test_return_rows_against_same_row_pick_different_serial_nos
        var pool = new ReturnSerialBatchPool(
            serialNos: new[] { "SN-001", "SN-002" });

        var warehouseId = Guid.NewGuid();

        // Row 1: return qty -1
        var row1Pick = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
            pool, requiredQty: 1m, warehouseId: warehouseId);

        row1Pick.SerialNos.Count.ShouldBe(1);
        row1Pick.SerialNos[0].ShouldBe("SN-001");

        // Remaining pool has only SN-002
        pool.SerialNos.ShouldNotContain("SN-001");
        pool.SerialNos.ShouldContain("SN-002");

        // Row 2: return qty -1 against the same source line
        var row2Pick = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
            pool, requiredQty: 1m, warehouseId: warehouseId);

        row2Pick.SerialNos.Count.ShouldBe(1);
        row2Pick.SerialNos[0].ShouldBe("SN-002");

        // Pool is now empty
        pool.SerialNos.ShouldBeEmpty();
    }

    [Fact]
    public void PickSerialBatchesForReturn_BatchedItem_MultipleRowsPickRemainingBatchQty()
    {
        // Equivalent to test_return_rows_against_same_row_pick_the_remaining_batch_qty
        var batchA = Guid.NewGuid();
        var batchB = Guid.NewGuid();
        var pool = new ReturnSerialBatchPool(
            batches: new Dictionary<Guid, decimal>
            {
                [batchA] = 5m,
                [batchB] = 5m
            });

        var warehouseId = Guid.NewGuid();
        var whStock = new Dictionary<Guid, decimal>
        {
            [batchA] = 10m,
            [batchB] = 10m
        };

        // Row 1: return qty 3
        var row1Pick = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
            pool, requiredQty: 3m, warehouseId: warehouseId, availableBatchQtyInWh: whStock);

        row1Pick.Batches.Count.ShouldBe(1);
        row1Pick.Batches[batchA].ShouldBe(3m);

        // Pool remaining: batchA = 2, batchB = 5
        pool.Batches[batchA].ShouldBe(2m);
        pool.Batches[batchB].ShouldBe(5m);

        // Row 2: return qty 7 against the same source line
        var row2Pick = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
            pool, requiredQty: 7m, warehouseId: warehouseId, availableBatchQtyInWh: whStock);

        row2Pick.Batches[batchA].ShouldBe(2m);
        row2Pick.Batches[batchB].ShouldBe(5m);

        // Pool is now completely depleted
        pool.Batches.ShouldBeEmpty();
    }

    [Fact]
    public void PickSerialBatchesForReturn_BatchedItemByNo_DepletesCorrectlyAndRounds()
    {
        var pool = new ReturnSerialBatchPool(
            batchesByNo: new Dictionary<string, decimal>
            {
                ["BATCH-001"] = 10.3333m
            });

        var warehouseId = Guid.NewGuid();
        var whStock = new Dictionary<string, decimal>
        {
            ["BATCH-001"] = 20m
        };

        var row1Pick = SerialBatchBundleValidationService.PickSerialBatchesForReturn(
            pool, requiredQty: 5.1111m, warehouseId: warehouseId, availableBatchQtyByNoInWh: whStock, precision: 4);

        row1Pick.BatchesByNo["BATCH-001"].ShouldBe(5.1111m);
        pool.BatchesByNo["BATCH-001"].ShouldBe(5.2222m);
    }

    [Fact]
    public void FilterReturnBatches_CapsByAvailableInWarehouse()
    {
        // PR #64590 commit c919eeb108: batch_qty = min(batch_qty, available_batch_qty)
        var batchId = Guid.NewGuid();
        var sourceBatches = new Dictionary<Guid, decimal> { [batchId] = 10m };
        var whStock = new Dictionary<Guid, decimal> { [batchId] = 4m }; // only 4 available in warehouse
        var whId = Guid.NewGuid();

        var filtered = SerialBatchBundleValidationService.FilterReturnBatches(
            sourceBatches, requiredReturnQty: 8m, warehouseId: whId,
            availableBatchQuantitiesInWarehouse: whStock, isPurchaseReturn: true);

        // Capped by available warehouse qty 4
        filtered[batchId].ShouldBe(4m);
    }

    // === 2) PR #64861: Subcontracting Return RM at Consumed Serial/Batch Rate ===

    [Fact]
    public void SubcontractingManager_GetSerialBatchReturnRate_WeightedAverageBatches()
    {
        // 4 units of Batch1 consumed at rate 100, 6 units of Batch2 consumed at rate 300
        // Expected weighted rate = (4 * 100 + 6 * 300) / 10 = (400 + 1800) / 10 = 220
        var batch1 = "BATCH-A";
        var batch2 = "BATCH-B";

        var returnedEntries = new List<(string? BatchNo, string? SerialNo, decimal Qty)>
        {
            (batch1, null, 4m),
            (batch2, null, 6m)
        };

        var consumedRates = new Dictionary<(string? BatchNo, string? SerialNo), decimal>
        {
            [(batch1, null)] = 100m,
            [(batch2, null)] = 300m
        };

        var rate = SubcontractingManager.GetSerialBatchReturnRate(returnedEntries, consumedRates);

        rate.ShouldNotBeNull();
        rate.Value.ShouldBe(220m);
    }

    [Fact]
    public void SubcontractingManager_GetSerialBatchReturnRate_SerialNos_CaseInsensitiveMean()
    {
        var consumedRates = new Dictionary<(string? BatchNo, string? SerialNo), decimal>
        {
            [(null, "SN-001")] = 120m,
            [(null, "SN-002")] = 180m
        };

        var rate = SubcontractingManager.GetSerialBatchReturnRate(
            batchNo: null,
            serialNos: "sn-001\nsn-002",
            qty: 2m,
            consumedRates: consumedRates);

        rate.ShouldNotBeNull();
        rate.Value.ShouldBe(150m); // (120 + 180) / 2
    }

    [Fact]
    public void SubcontractingManager_CalculateSuppliedItemRate_ReturnRetainsConsumedRate()
    {
        // On return, supplied items keep consumed rate rather than using standard rate
        var returnRateWithBundle = SubcontractingManager.CalculateSuppliedItemRate(
            isReturn: true,
            serialBatchConsumedRate: 220m,
            originalConsumedRate: 200m,
            standardCostRate: 999m);

        returnRateWithBundle.ShouldBe(220m);

        var returnRateFallback = SubcontractingManager.CalculateSuppliedItemRate(
            isReturn: true,
            serialBatchConsumedRate: null,
            originalConsumedRate: 200m,
            standardCostRate: 999m);

        returnRateFallback.ShouldBe(200m);

        // Standard receipt (not return) uses incoming standard rate
        var standardRate = SubcontractingManager.CalculateSuppliedItemRate(
            isReturn: false,
            serialBatchConsumedRate: 220m,
            originalConsumedRate: 200m,
            standardCostRate: 999m);

        standardRate.ShouldBe(999m);
    }

    // === 3) PR #65049: Production Plan Download Required Materials UOM and Merge ===

    [Fact]
    public void ProductionPlanMaterialPlanningService_BuildDownloadRows_IncludesUomColumn()
    {
        var rows = new List<RawMaterialPlanRow>
        {
            new()
            {
                ItemCode = "RM-001",
                ItemName = "Raw Material 1",
                StockUom = "Nos",
                Warehouse = "Stores - TC",
                Quantity = 10m,
                Uom = "Nos",
                ConversionFactor = 1m
            }
        };

        var download = ProductionPlanMaterialPlanningService.BuildDownloadRows(rows);

        download.Count.ShouldBe(1);
        download[0].Uom.ShouldBe("Nos");
        download[0].RequiredQty.ShouldBe(10m);

        ProductionPlanMaterialPlanningService.DownloadHeaders.ShouldContain("UOM");
        ProductionPlanMaterialPlanningService.DownloadHeaders.ShouldContain("Required Qty");
    }

    [Fact]
    public void ProductionPlanMaterialPlanningService_BuildDownloadRows_MergesTransferAndPurchaseInOneUom()
    {
        // Equivalent to test_download_raw_materials_merges_transfer_and_purchase_in_one_uom
        // Row 1: 20 Nos transferred from Finished Goods (Stock UOM = Nos, Conversion Factor = 1)
        // Row 2: 3 Boxes purchased (Purchase UOM = Box, Conversion Factor = 10 => 30 Nos)
        // Total expected = 20 + 30 = 50 Nos
        var rows = new List<RawMaterialPlanRow>
        {
            new()
            {
                ItemCode = "RM-MERGE",
                ItemName = "Merge Item",
                StockUom = "Nos",
                Warehouse = "Stores - TC",
                Quantity = 20m,
                Uom = "Nos",
                ConversionFactor = 1m
            },
            new()
            {
                ItemCode = "RM-MERGE",
                ItemName = "Merge Item",
                StockUom = "Nos",
                Warehouse = "Stores - TC",
                Quantity = 3m,
                Uom = "Box",
                ConversionFactor = 10m
            }
        };

        var download = ProductionPlanMaterialPlanningService.BuildDownloadRows(rows);

        download.Count.ShouldBe(1);
        download[0].ItemCode.ShouldBe("RM-MERGE");
        download[0].RequiredQty.ShouldBe(50m);
        download[0].Uom.ShouldBe("Nos");
    }

    [Fact]
    public void ProductionPlanMaterialPlanningService_GenerateCsv_ContainsUomHeaderAndValues()
    {
        var rows = new List<DownloadRawMaterialRow>
        {
            new()
            {
                ItemCode = "RM-01",
                ItemName = "Raw 1",
                StockUom = "Nos",
                Warehouse = "Stores",
                RequiredQty = 50m,
                Uom = "Nos"
            }
        };

        var csv = ProductionPlanMaterialPlanningService.GenerateCsv(rows);

        csv.ShouldContain("\"Required Qty\",\"UOM\"");
        csv.ShouldContain("\"50\",\"Nos\"");
    }
}
