using System;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for UOM conversion correctness across the full document lifecycle.
/// Critical: wrong conversion factor corrupts stock ledger data.
/// Verifies StockQty = Quantity × ConversionFactor across all entity types.
/// </summary>
public class UomConversionIntegrationTests
{
    // ========== StockQty Formula Verification ==========

    [Theory]
    [InlineData(5, 12, 60)]    // 5 Dozen = 60 Units
    [InlineData(2, 1000, 2000)] // 2 Kg = 2000 Grams
    [InlineData(3, 3.785, 11.355)] // 3 Gallons ≈ 11.355 Litres
    [InlineData(10, 1, 10)]    // Same UOM, factor = 1
    [InlineData(0, 12, 0)]     // Zero qty = zero stock qty regardless of factor
    public void SalesOrderItem_StockQty_IsQuantityTimesConversionFactor(
        decimal quantity, decimal factor, decimal expectedStockQty)
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", quantity, 100m, 0m, "Dozen");
        item.ConversionFactor = factor;

        Math.Round(item.StockQty, 3).ShouldBe(Math.Round(expectedStockQty, 3));
    }

    [Theory]
    [InlineData(10, 100, 1000)]  // 10 Boxes = 1000 Units (100 per box)
    [InlineData(1, 48, 48)]      // 1 Pallet = 48 Cases
    [InlineData(7.5, 1, 7.5)]   // Same UOM
    public void PurchaseOrderItem_StockQty_IsQuantityTimesConversionFactor(
        decimal quantity, decimal factor, decimal expectedStockQty)
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test Item", quantity, 50m, 0m, "Box");
        item.ConversionFactor = factor;

        item.StockQty.ShouldBe(expectedStockQty);
    }

    // ========== Conversion Factor Defaults ==========

    [Fact]
    public void SalesOrderItem_ConversionFactor_DefaultsToOne()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10m, 100m, 0m, "Unit");
        item.ConversionFactor.ShouldBe(1m);
        item.StockQty.ShouldBe(10m); // Same as Quantity when factor = 1
    }

    [Fact]
    public void PurchaseOrderItem_ConversionFactor_DefaultsToOne()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 5m, 50m, 0m, "Unit");
        item.ConversionFactor.ShouldBe(1m);
        item.StockQty.ShouldBe(5m);
    }

    [Fact]
    public void DeliveryNoteItem_ConversionFactor_DefaultsToOne()
    {
        var item = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 3m, 100m, 0m, "Unit");
        item.ConversionFactor.ShouldBe(1m);
        item.StockQty.ShouldBe(3m);
    }

    [Fact]
    public void PurchaseReceiptItem_ConversionFactor_DefaultsToOne()
    {
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 8m, 50m, 0m, "Unit");
        item.ConversionFactor.ShouldBe(1m);
        item.StockQty.ShouldBe(8m);
    }

    [Fact]
    public void SalesInvoiceItem_ConversionFactor_DefaultsToOne()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Widget", 4m, 100m, 0m);
        si.Items[0].ConversionFactor.ShouldBe(1m);
        si.Items[0].StockQty.ShouldBe(4m);
    }

    // ========== StockUom Field ==========

    [Fact]
    public void SalesOrderItem_StockUom_DefaultsToUnit()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test", 1m, 1m, 0m, "Dozen");
        item.StockUom.ShouldBe("Unit");
    }

    [Fact]
    public void SalesOrderItem_StockUom_CanBeSet()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test", 1m, 1m, 0m, "Bottle");
        item.StockUom = "ml";
        item.StockUom.ShouldBe("ml");
    }

    // ========== Rate Per Stock Unit ==========

    [Fact]
    public void RatePerStockUnit_IsDerivedFromConversionFactor()
    {
        // Customer orders 5 Dozen at RM 120/Dozen
        // Stock UOM is Unit, factor = 12
        // Rate per Unit = 120 / 12 = RM 10/Unit
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Screws", 5m, 120m, 0m, "Dozen");
        item.ConversionFactor = 12m;
        item.StockUom = "Unit";

        var ratePerStockUnit = item.UnitPrice / item.ConversionFactor;
        ratePerStockUnit.ShouldBe(10m);

        // Total value should be the same regardless of UOM perspective
        var totalFromTransaction = item.Quantity * item.UnitPrice; // 5 × 120 = 600
        var totalFromStock = item.StockQty * ratePerStockUnit;      // 60 × 10 = 600
        totalFromTransaction.ShouldBe(totalFromStock);
    }

    [Fact]
    public void RatePerStockUnit_SameUom_RateUnchanged()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Item", 10m, 50m, 0m, "Unit");
        item.ConversionFactor = 1m;

        var ratePerStockUnit = item.UnitPrice / item.ConversionFactor;
        ratePerStockUnit.ShouldBe(50m);
    }

    // ========== Fulfillment Qty in Transaction UOM ==========

    [Fact]
    public void PendingDeliveryQty_IsInTransactionUom()
    {
        // SO: 5 Dozen, delivered 2 Dozen
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Item", 5m, 100m, 0m, "Dozen");
        item.ConversionFactor = 12m;
        item.DeliveredQty = 2m; // 2 Dozen delivered (NOT 24 units)

        item.PendingDeliveryQty.ShouldBe(3m); // 3 Dozen remaining
        item.StockQty.ShouldBe(60m);          // Total stock qty for full order
    }

    [Fact]
    public void PendingBillingQty_IsInTransactionUom()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Item", 10m, 50m, 0m, "Box");
        item.ConversionFactor = 24m; // 24 units per box
        item.BilledQty = 7m; // 7 boxes billed

        item.PendingBillingQty.ShouldBe(3m); // 3 boxes remaining
    }

    // ========== Edge Cases ==========

    [Fact]
    public void ConversionFactor_FractionalValue()
    {
        // 1 Gallon = 3.785 Litres
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Paint", 4m, 200m, 0m, "Gallon");
        item.ConversionFactor = 3.785m;
        item.StockUom = "Litre";

        item.StockQty.ShouldBe(15.14m);
    }

    [Fact]
    public void ConversionFactor_LargeMultiplier()
    {
        // 1 Tonne = 1000 Kg
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel", 2.5m, 5000m, 0m, "Tonne");
        item.ConversionFactor = 1000m;
        item.StockUom = "Kg";

        item.StockQty.ShouldBe(2500m);
    }

    [Fact]
    public void ConversionFactor_SubUnitConversion()
    {
        // 500 ml → Litre (factor = 0.001 per ml, or qty=500 at factor=0.001)
        // Actually: transaction in ml, stock in Litre → factor = 0.001
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Chemical", 500m, 0.10m, 0m, "ml");
        item.ConversionFactor = 0.001m;
        item.StockUom = "Litre";

        item.StockQty.ShouldBe(0.5m);
    }

    [Fact]
    public void TotalValueInvariant_AcrossUomPerspectives()
    {
        // The total monetary value must be identical regardless of UOM:
        // 3 Boxes × RM 240/Box = RM 720
        // 72 Units × RM 10/Unit = RM 720 (same total)
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widgets", 3m, 240m, 0m, "Box");
        item.ConversionFactor = 24m; // 24 units per box
        item.StockUom = "Unit";

        var totalTransaction = item.Quantity * item.UnitPrice;
        var ratePerUnit = item.UnitPrice / item.ConversionFactor;
        var totalStock = item.StockQty * ratePerUnit;

        totalTransaction.ShouldBe(720m);
        totalStock.ShouldBe(720m);
        totalTransaction.ShouldBe(totalStock);
    }
}
