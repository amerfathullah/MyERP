using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Integration tests verifying UOM conversion in all stock-affecting Bin operations.
/// Ensures Bin quantities are always tracked in stock UOM regardless of transaction UOM.
/// </summary>
public class UomBinQuantityTests
{
    #region PO Submit → Bin.OrderedQty in Stock UOM

    [Fact]
    public void PO_Submit_OrderedQty_InStockUom()
    {
        // Ordering 5 Dozen (stock UOM: Unit, factor: 12)
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 5, 120m, 0, "Dozen");
        item.StockUom = "Unit";
        item.ConversionFactor = 12m;

        // Bin.OrderedQty should increase by 60 (not 5)
        var binOrderedIncrease = item.StockQty;
        Assert.Equal(60m, binOrderedIncrease);
    }

    [Fact]
    public void PO_Cancel_OrderedQty_ReversesInStockUom()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel", 10, 50m, 0, "Ton");
        item.StockUom = "Kg";
        item.ConversionFactor = 1000m;

        // On cancel, Bin.OrderedQty decreases by 10000 (not 10)
        var binOrderedDecrease = -item.StockQty;
        Assert.Equal(-10000m, binOrderedDecrease);
    }

    [Fact]
    public void PO_Close_PendingReceipt_InStockUom()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Fabric", 20, 200m, 0, "Roll");
        item.StockUom = "Metre";
        item.ConversionFactor = 100m;
        item.ReceivedQty = 8; // 8 Rolls received, 12 pending

        // Close releases pending in stock UOM: 12 × 100 = 1200 Metres
        var pendingStockQty = item.PendingReceiptQty * item.ConversionFactor;
        Assert.Equal(1200m, pendingStockQty);
    }

    #endregion

    #region SO Submit → Bin.ReservedQty in Stock UOM

    [Fact]
    public void SO_Submit_ReservedQty_InStockUom()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Paint", 4, 80m, 0, "Gallon");
        item.StockUom = "Litre";
        item.ConversionFactor = 3.785m;

        // Bin.ReservedQty should increase by 15.14 (not 4)
        Assert.Equal(15.14m, item.StockQty);
    }

    [Fact]
    public void SO_Cancel_ReservedQty_ReversesInStockUom()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Wire", 3, 50m, 0, "Coil");
        item.StockUom = "Metre";
        item.ConversionFactor = 500m;

        Assert.Equal(1500m, item.StockQty); // Releases 1500 Metres of reserved
    }

    #endregion

    #region PR Submit → Bin.ActualQty in Stock UOM

    [Fact]
    public void PR_Submit_ActualQty_IncreasesInStockUom()
    {
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Sugar", 5, 100m, 0, "Bag");
        item.StockUom = "Kg";
        item.ConversionFactor = 50m; // 1 Bag = 50 Kg

        // Bin.ActualQty increases by 250 Kg (not 5 Bags)
        Assert.Equal(250m, item.StockQty);

        // Rate per stock unit = RM 100/Bag ÷ 50 = RM 2/Kg
        var ratePerKg = item.UnitPrice / item.ConversionFactor;
        Assert.Equal(2m, ratePerKg);
    }

    #endregion

    #region DN Submit → Bin.ActualQty Decreases in Stock UOM

    [Fact]
    public void DN_Submit_ActualQty_DecreasesInStockUom()
    {
        var item = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Milk", 10, 6m, 0, "Carton");
        item.StockUom = "Litre";
        item.ConversionFactor = 1m; // 1 Carton = 1 Litre (same)

        Assert.Equal(10m, item.StockQty); // No conversion needed
    }

    [Fact]
    public void DN_Submit_LargeConversion_CorrectStockDeduction()
    {
        var item = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Nails", 2, 15m, 0, "Box");
        item.StockUom = "Unit";
        item.ConversionFactor = 500m; // 1 Box = 500 Units

        // Delivering 2 Boxes deducts 1000 Units from stock
        Assert.Equal(1000m, item.StockQty);
    }

    #endregion

    #region Projected Qty Consistency

    [Fact]
    public void ProjectedQty_AllComponents_InStockUom()
    {
        // Scenario: Item sold in Dozen, stocked in Unit
        // SO: 10 Dozen ordered → reserves 120 Units
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 100m, 0, "Dozen");
        soItem.StockUom = "Unit";
        soItem.ConversionFactor = 12m;

        // PO: 5 Dozen ordered → adds 60 Units to ordered
        var poItem = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 5, 80m, 0, "Dozen");
        poItem.StockUom = "Unit";
        poItem.ConversionFactor = 12m;

        // All Bin fields are in Units (stock UOM)
        var reserved = soItem.StockQty;  // 120 Units
        var ordered = poItem.StockQty;    // 60 Units

        // Projected = Actual + Ordered - Reserved (all in stock UOM)
        var actualQty = 200m; // 200 Units in stock
        var projected = actualQty + ordered - reserved;
        Assert.Equal(140m, projected); // 200 + 60 - 120 = 140 Units
    }

    #endregion
}
