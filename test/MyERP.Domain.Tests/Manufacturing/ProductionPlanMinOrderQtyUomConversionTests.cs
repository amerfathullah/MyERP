using System;
using MyERP.Inventory.DomainServices;
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
}
