using System;
using System.Linq;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Manufacturing;

public class ProductionPlanMinOrderQtyUomConversionTests
{
    [Fact]
    public void CalculatePurchaseUomQty_WhenNearestRoundingDipsBelowMinOrderQty_TakesGridCeiling()
    {
        // Setup matching ERPNext test_min_order_qty_conversion_takes_the_grid_ceiling:
        // MinOrderQty = 50000, ConversionFactor = 453.592292197, Precision = 3.
        // 50000 / 453.592292197 = 110.23117...
        // Nearest round(3) = 110.231 -> 110.231 * 453.592292197 = 49999.932 (< 50000)
        // Ceiling quantize(3) = 110.232 -> 110.232 * 453.592292197 = 50000.386 (>= 50000)

        var minOrderQty = 50000m;
        var conversionFactor = 453.592292197m;
        var plannedStockQty = 50000m;

        var result = UomConversionService.CalculatePurchaseUomQty(
            plannedStockQty,
            conversionFactor,
            minOrderQty,
            considerMinOrderQty: true,
            precision: 3);

        result.ShouldBe(110.232m);
        (result * conversionFactor).ShouldBeGreaterThanOrEqualTo(minOrderQty);
    }

    [Fact]
    public void CalculatePurchaseUomQty_WhenConsiderMinOrderQtyIsFalse_UsesNearestRounding()
    {
        var minOrderQty = 50000m;
        var conversionFactor = 453.592292197m;
        var plannedStockQty = 50000m;

        var result = UomConversionService.CalculatePurchaseUomQty(
            plannedStockQty,
            conversionFactor,
            minOrderQty,
            considerMinOrderQty: false,
            precision: 3);

        result.ShouldBe(110.231m);
    }

    [Fact]
    public void CalculatePurchaseUomQty_WhenStockQtyExceedsMinOrderQty_UsesNearestRounding()
    {
        var minOrderQty = 100m;
        var conversionFactor = 12m; // 1 Dozen = 12 Units
        var plannedStockQty = 150m; // 150 / 12 = 12.5 Dozen

        var result = UomConversionService.CalculatePurchaseUomQty(
            plannedStockQty,
            conversionFactor,
            minOrderQty,
            considerMinOrderQty: true,
            precision: 2);

        result.ShouldBe(12.5m);
    }

    [Fact]
    public void AllocateMaterialsFromOtherLocations_SplitsRequiredBomQtyOnceAcrossRows()
    {
        // Per ERPNext PR #66580: splitting a single BOM item across transfer + purchase rows
        // must divide RequiredBomQty across rows so the total reserved BOM qty equals original once.
        var item = new RawMaterialPlanRow
        {
            ItemCode = "RM-001",
            ItemName = "Steel Plate",
            StockUom = "Nos",
            Warehouse = "Main Stores",
            ItemId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            RequiredBomQty = 10m,
            Quantity = 10m,
            Uom = "Nos",
            ConversionFactor = 1m,
        };

        var splitRows = ProductionPlanMaterialPlanningService.AllocateMaterialsFromOtherLocations(
            item,
            new[]
            {
                ("Sub Store A", (Guid?)Guid.NewGuid(), 4m),
                ("Sub Store B", (Guid?)Guid.NewGuid(), 3m),
            });

        splitRows.Count.ShouldBe(3); // 2 transfer rows + 1 remaining purchase row
        splitRows[0].Quantity.ShouldBe(4m);
        splitRows[0].RequiredBomQty.ShouldBe(4m);
        splitRows[1].Quantity.ShouldBe(3m);
        splitRows[1].RequiredBomQty.ShouldBe(3m);
        splitRows[2].Quantity.ShouldBe(3m);
        splitRows[2].RequiredBomQty.ShouldBe(3m);

        splitRows.Sum(r => r.RequiredBomQty).ShouldBe(10m);
    }

    [Fact]
    public void SplitRequiredBomQty_And_BuildDownloadRows_RoundTripsBomQtyWithoutDoubleCount()
    {
        var sharedWhId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var transferRow = new RawMaterialPlanRow
        {
            ItemId = itemId,
            ItemCode = "RM-002",
            ItemName = "Copper Wire",
            StockUom = "Meter",
            Warehouse = "Main Stores",
            WarehouseId = sharedWhId,
            Quantity = 6m,
            Uom = "Meter",
            ConversionFactor = 1m,
        };
        var purchaseRow = new RawMaterialPlanRow
        {
            ItemId = itemId,
            ItemCode = "RM-002",
            ItemName = "Copper Wire",
            StockUom = "Meter",
            Warehouse = "Main Stores",
            WarehouseId = sharedWhId,
            Quantity = 4m,
            Uom = "Meter",
            ConversionFactor = 1m,
        };

        ProductionPlanMaterialPlanningService.SplitRequiredBomQty(10m, new[] { transferRow, purchaseRow });

        transferRow.RequiredBomQty.ShouldBe(6m);
        purchaseRow.RequiredBomQty.ShouldBe(4m);

        var downloadRows = ProductionPlanMaterialPlanningService.BuildDownloadRows(new[] { transferRow, purchaseRow });
        downloadRows.Count.ShouldBe(1);
        downloadRows[0].RequiredBomQty.ShouldBe(10m);
        downloadRows[0].RequiredQty.ShouldBe(10m);
    }
}
