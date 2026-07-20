using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Integration tests verifying UOM conversion flows through the document lifecycle.
/// Ensures StockQty is correctly calculated and propagated across document conversions.
/// </summary>
public class UomConversionFlowTests
{
    #region SO → DN Conversion Preserves UOM

    [Fact]
    public void SO_Item_ConversionFactor_PropagatesToDN()
    {
        // Simulate: SO has items in Dozen, stock UOM is Unit
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget Box", 5, 120m, 0, "Dozen");
        soItem.StockUom = "Unit";
        soItem.ConversionFactor = 12m;

        // Simulate conversion: create DN item with SO values
        var dnItem = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), soItem.ItemId,
            soItem.Description, soItem.PendingDeliveryQty, soItem.UnitPrice, 0, soItem.Uom);
        dnItem.StockUom = soItem.StockUom;
        dnItem.ConversionFactor = soItem.ConversionFactor;

        // DN item should have correct StockQty for SLE
        Assert.Equal("Unit", dnItem.StockUom);
        Assert.Equal(12m, dnItem.ConversionFactor);
        Assert.Equal(60m, dnItem.StockQty); // 5 Dozen × 12 = 60 Units in stock
    }

    [Fact]
    public void SO_Item_SameUom_FactorOneCarriesForward()
    {
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Bolt", 100, 2.5m, 0, "Unit");
        // Same UOM = no conversion needed
        Assert.Equal(1m, soItem.ConversionFactor);
        Assert.Equal("Unit", soItem.StockUom);
        Assert.Equal(100m, soItem.StockQty);
    }

    #endregion

    #region PO → PR Conversion Preserves UOM

    [Fact]
    public void PO_Item_ConversionFactor_PropagatesToPR()
    {
        var poItem = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Cement", 10, 22m, 0, "Bag");
        poItem.StockUom = "Kg";
        poItem.ConversionFactor = 50m; // 1 Bag = 50 Kg

        // Simulate PR creation from PO
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), poItem.ItemId,
            poItem.Description, poItem.PendingReceiptQty, poItem.UnitPrice, 0, poItem.Uom);
        prItem.StockUom = poItem.StockUom;
        prItem.ConversionFactor = poItem.ConversionFactor;

        Assert.Equal("Kg", prItem.StockUom);
        Assert.Equal(50m, prItem.ConversionFactor);
        Assert.Equal(500m, prItem.StockQty); // 10 Bags × 50 = 500 Kg in stock
    }

    #endregion

    #region Stock Movement Uses StockQty

    [Fact]
    public void DN_StockOut_UsesStockQty_NotTransactionQty()
    {
        // Selling 3 Dozen at RM 120/Dozen
        var dnItem = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 3, 120m, 0, "Dozen");
        dnItem.StockUom = "Unit";
        dnItem.ConversionFactor = 12m;

        // SLE should debit 36 Units (not 3 Dozen)
        var sleQty = dnItem.StockQty;
        Assert.Equal(36m, sleQty);

        // Rate per stock unit = RM 120/Dozen ÷ 12 = RM 10/Unit
        var ratePerStockUnit = dnItem.UnitPrice / dnItem.ConversionFactor;
        Assert.Equal(10m, ratePerStockUnit);

        // Total stock value = 36 × 10 = 360 (same as 3 × 120)
        Assert.Equal(360m, sleQty * ratePerStockUnit);
        Assert.Equal(dnItem.LineTotal, sleQty * ratePerStockUnit);
    }

    [Fact]
    public void PR_StockIn_UsesStockQty_ForBinUpdate()
    {
        // Receiving 2 Pallets at RM 500/Pallet
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Drinks", 2, 500m, 0, "Pallet");
        prItem.StockUom = "Case";
        prItem.ConversionFactor = 48m; // 1 Pallet = 48 Cases

        // SLE should credit 96 Cases (not 2 Pallets)
        Assert.Equal(96m, prItem.StockQty);

        // Rate per case = RM 500/Pallet ÷ 48 ≈ RM 10.42
        var ratePerCase = prItem.UnitPrice / prItem.ConversionFactor;
        Assert.Equal(500m / 48m, ratePerCase);
    }

    [Fact]
    public void SI_UpdateStock_UsesStockQty()
    {
        // Direct sale (no DN): 1 Box at RM 300
        var siItem = new SalesInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Gift Set", 1, 300m, 0, "Box");
        siItem.StockUom = "Unit";
        siItem.ConversionFactor = 6m; // 1 Box = 6 Units

        // SLE deducts 6 Units from stock
        Assert.Equal(6m, siItem.StockQty);
        // Rate per unit = RM 300/6 = RM 50
        Assert.Equal(50m, siItem.UnitPrice / siItem.ConversionFactor);
    }

    [Fact]
    public void PI_UpdateStock_UsesStockQty()
    {
        // Direct purchase (no PR): 5 Rolls at RM 200/Roll
        var piItem = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Fabric", 5, 200m, 0, "Roll");
        piItem.StockUom = "Metre";
        piItem.ConversionFactor = 100m; // 1 Roll = 100 Metres

        // SLE adds 500 Metres to stock
        Assert.Equal(500m, piItem.StockQty);
    }

    #endregion

    #region Partial Delivery with UOM Conversion

    [Fact]
    public void PartialDelivery_StockQty_ProportionalToConversion()
    {
        var soItem = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Paint", 10, 80m, 0, "Gallon");
        soItem.StockUom = "Litre";
        soItem.ConversionFactor = 3.785m;

        // Deliver 4 of 10 Gallons
        var dnItem = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), soItem.ItemId,
            soItem.Description, 4, soItem.UnitPrice, 0, soItem.Uom);
        dnItem.StockUom = soItem.StockUom;
        dnItem.ConversionFactor = soItem.ConversionFactor;

        // Stock deduction = 4 × 3.785 = 15.14 Litres
        Assert.Equal(15.14m, dnItem.StockQty);

        // Remaining SO pending = 10 - 4 = 6 Gallons → 22.71 Litres in stock
        soItem.DeliveredQty = 4;
        Assert.Equal(6m, soItem.PendingDeliveryQty);
        // Next delivery stock impact = 6 × 3.785 = 22.71 Litres
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ConversionFactor_DefaultOne_NoConversionNeeded()
    {
        // When Uom == StockUom, factor should be 1 and StockQty == Quantity
        var item = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Part", 50, 10m, 0, "Unit");
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(50m, item.StockQty);
        Assert.Equal(item.Quantity, item.StockQty);
    }

    [Fact]
    public void TotalValue_SameRegardlessOfUom()
    {
        // 2 Dozen at RM 120/Dozen = RM 240
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 2, 120m, 0, "Dozen");
        item.ConversionFactor = 12m;
        item.StockUom = "Unit";

        // Total value via transaction: 2 × 120 = 240
        Assert.Equal(240m, item.LineTotal);
        // Total value via stock: 24 units × 10/unit = 240
        Assert.Equal(240m, item.StockQty * (item.UnitPrice / item.ConversionFactor));
    }

    #endregion
}
