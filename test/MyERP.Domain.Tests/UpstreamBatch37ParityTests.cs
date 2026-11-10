using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP;

public class UpstreamBatch37ParityTests
{
    // === 1) PR #65146 / commit 9d105dfa3b: Later Pick List Rows Get Next Serial Numbers ===

    [Fact]
    public void PickList_AllocateItemLocations_LaterRowsGetNextSerialNumbersInFifoOrder()
    {
        // 5 serial numbers available in warehouse
        var availableLocations = new List<PickLocationAllocation>
        {
            new()
            {
                WarehouseId = Guid.NewGuid(),
                Qty = 5m,
                SerialNumbers = new List<string> { "SN-001", "SN-002", "SN-003", "SN-004", "SN-005" }
            }
        };

        // Row 1 requests 1 unit
        var row1Allocations = PickListManager.AllocateItemLocations(
            itemQty: 1m,
            itemStockQty: 1m,
            conversionFactor: 1m,
            availableLocations: availableLocations);

        row1Allocations.Count.ShouldBe(1);
        row1Allocations[0].SerialNumbers.ShouldBe(new[] { "SN-001" });
        row1Allocations[0].Qty.ShouldBe(1m);

        // Location has 4 left: SN-002, SN-003, SN-004, SN-005
        availableLocations.Count.ShouldBe(1);
        availableLocations[0].Qty.ShouldBe(4m);

        // Row 2 requests 2 units
        var row2Allocations = PickListManager.AllocateItemLocations(
            itemQty: 2m,
            itemStockQty: 2m,
            conversionFactor: 1m,
            availableLocations: availableLocations);

        row2Allocations.Count.ShouldBe(1);
        // PR #65146: Row 2 gets next serial numbers (SN-002, SN-003), NOT newest (SN-004, SN-005)
        row2Allocations[0].SerialNumbers.ShouldBe(new[] { "SN-002", "SN-003" });
        row2Allocations[0].Qty.ShouldBe(2m);

        // Location has 2 left: SN-004, SN-005
        availableLocations.Count.ShouldBe(1);
        availableLocations[0].Qty.ShouldBe(2m);
        availableLocations[0].SerialNumbers.ShouldBe(new[] { "SN-004", "SN-005" });
    }

    [Fact]
    public void PickList_AllocateItemLocations_FractionalUomAllocation_SlicesCorrectSerialNumbers()
    {
        var whId = Guid.NewGuid();
        var availableLocations = new List<PickLocationAllocation>
        {
            new()
            {
                WarehouseId = whId,
                Qty = 10m,
                SerialNumbers = new List<string> { "SN-01", "SN-02", "SN-03", "SN-04", "SN-05", "SN-06", "SN-07", "SN-08", "SN-09", "SN-10" }
            }
        };

        // Row 1: 1 unit with conversionFactor 1
        var row1 = PickListManager.AllocateItemLocations(1m, 1m, 1m, availableLocations);
        row1[0].SerialNumbers.ShouldBe(new[] { "SN-01" });

        // Row 2: 2 units with conversionFactor 1
        var row2 = PickListManager.AllocateItemLocations(2m, 2m, 1m, availableLocations);
        row2[0].SerialNumbers.ShouldBe(new[] { "SN-02", "SN-03" });

        // Remaining in available locations
        availableLocations[0].Qty.ShouldBe(7m);
        availableLocations[0].SerialNumbers.ShouldBe(new[] { "SN-04", "SN-05", "SN-06", "SN-07", "SN-08", "SN-09", "SN-10" });
    }

    // === 2) PR #65145 / commit 1ca3aa000e: Transfer Taken Off Shortage Before Rounding to Purchase UOM ===

    [Fact]
    public void ProductionPlan_CalculateTransferAndPurchaseQuantities_TransferOffUnroundedShortage()
    {
        // Total shortage = 47 Nos. Other warehouse has 7 Nos available for transfer.
        // Purchase UOM is Box (conversion factor = 10 Nos per Box).
        // Transfer covers 7 Nos (unrounded, exact).
        // Remaining shortage = 47 - 7 = 40 Nos -> 40 / 10 = 4 Box.
        var (transferQty, purchaseQty) = ProductionPlanMaterialPlanningService.CalculateTransferAndPurchaseQuantities(
            totalShortage: 47m,
            availableInOtherWarehouses: 7m,
            purchaseConversionFactor: 10m,
            roundUpWholeUom: true);

        transferQty.ShouldBe(7m);
        purchaseQty.ShouldBe(4m); // Exactly 4 Box
    }

    [Fact]
    public void ProductionPlan_CalculateTransferAndPurchaseQuantities_RoundsRemainingShortageUpToWholeBox()
    {
        // Total shortage = 45 Nos. Other warehouse has 7 Nos available for transfer.
        // Transfer covers 7 Nos.
        // Remaining shortage = 45 - 7 = 38 Nos -> 38 / 10 = 3.8 Box -> rounded up to 4 Box.
        var (transferQty, purchaseQty) = ProductionPlanMaterialPlanningService.CalculateTransferAndPurchaseQuantities(
            totalShortage: 45m,
            availableInOtherWarehouses: 7m,
            purchaseConversionFactor: 10m,
            roundUpWholeUom: true);

        transferQty.ShouldBe(7m);
        purchaseQty.ShouldBe(4m); // 3.8 rounded up to 4 Box
    }

    // === 3) PR #65183 / commit 98e9ed6914 & d15ae49fe4: Job Card Required Qty Rounding ===

    [Fact]
    public void JobCard_CalculateJobCardItemRequiredQty_RoundsToConfiguredPrecision()
    {
        // 1/3 item requirement for 1 unit out of 10 WO qty: (1m * 1m) / 3m = 0.33333333...
        var rounded4 = JobCardManager.CalculateJobCardItemRequiredQty(
            itemRequiredQty: 1m,
            jobCardForQuantity: 1m,
            operationOrWoQty: 3m,
            precision: 4);

        rounded4.ShouldBe(0.3333m);

        var rounded6 = JobCardManager.CalculateJobCardItemRequiredQty(
            itemRequiredQty: 1m,
            jobCardForQuantity: 1m,
            operationOrWoQty: 3m,
            precision: 6);

        rounded6.ShouldBe(0.333333m);
    }

    // === 4) PR #65210 / commit e8c9d1948f: Pick List Row Stock UOM ===

    [Fact]
    public void PickList_AddItem_AssignsStockUomAndDefaultsUom()
    {
        var pl = new PickList(Guid.NewGuid(), Guid.NewGuid(), "Delivery");
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        pl.AddItem(
            itemId,
            whId,
            qty: 5m,
            stockQty: 5m,
            itemName: "Component A",
            uom: "Nos",
            conversionFactor: 1m,
            stockUom: "Nos");

        pl.Items.Count.ShouldBe(1);
        pl.Items[0].Uom.ShouldBe("Nos");
        pl.Items[0].StockUom.ShouldBe("Nos");
        pl.Items[0].ConversionFactor.ShouldBe(1m);
    }
}
