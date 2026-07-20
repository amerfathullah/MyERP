using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Tests for UOM conversion fields on transaction items.
/// Verifies StockQty = Quantity × ConversionFactor for all transaction item types.
/// </summary>
public class UomConversionFieldTests
{
    #region SalesOrderItem

    [Fact]
    public void SalesOrderItem_DefaultConversionFactor_IsOne()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 25m, 0, "Unit");
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal("Unit", item.StockUom);
    }

    [Fact]
    public void SalesOrderItem_StockQty_EqualsQuantity_WhenFactorIsOne()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget", 10, 25m, 0, "Unit");
        Assert.Equal(10m, item.StockQty); // 10 × 1 = 10
    }

    [Fact]
    public void SalesOrderItem_StockQty_MultipliesByFactor()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Widget Box", 5, 300m, 0, "Dozen");
        item.ConversionFactor = 12m; // 1 Dozen = 12 Units
        item.StockUom = "Unit";

        Assert.Equal(60m, item.StockQty); // 5 Dozen × 12 = 60 Units
    }

    [Fact]
    public void SalesOrderItem_FractionalConversion()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Chemical", 2.5m, 100m, 0, "Litre");
        item.ConversionFactor = 1000m; // 1 Litre = 1000 ml
        item.StockUom = "ml";

        Assert.Equal(2500m, item.StockQty); // 2.5 L × 1000 = 2500 ml
    }

    #endregion

    #region PurchaseOrderItem

    [Fact]
    public void PurchaseOrderItem_DefaultValues()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Steel Rod", 100, 15m, 0, "Kg");
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal("Unit", item.StockUom);
        Assert.Equal(100m, item.StockQty);
    }

    [Fact]
    public void PurchaseOrderItem_BoxToUnit_Conversion()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Screws", 3, 50m, 0, "Box");
        item.ConversionFactor = 100m; // 1 Box = 100 Units
        item.StockUom = "Unit";

        Assert.Equal(300m, item.StockQty); // 3 Boxes × 100 = 300 Units
    }

    #endregion

    #region DeliveryNoteItem

    [Fact]
    public void DeliveryNoteItem_StockQty_UsedForSLE()
    {
        var item = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Flour", 2, 45m, 0, "Bag");
        item.ConversionFactor = 25m; // 1 Bag = 25 Kg
        item.StockUom = "Kg";

        // SLE should use StockQty (50 Kg) not Quantity (2 Bags)
        Assert.Equal(50m, item.StockQty);
    }

    [Fact]
    public void DeliveryNoteItem_SameUom_FactorOne()
    {
        var item = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Part", 7, 10m, 0, "Unit");
        // Same UOM as stock → factor stays 1
        Assert.Equal(7m, item.StockQty);
    }

    #endregion

    #region PurchaseReceiptItem

    [Fact]
    public void PurchaseReceiptItem_PalletToCase_Conversion()
    {
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Drinks", 2, 500m, 0, "Pallet");
        item.ConversionFactor = 48m; // 1 Pallet = 48 Cases
        item.StockUom = "Case";

        Assert.Equal(96m, item.StockQty); // 2 Pallets × 48 = 96 Cases
    }

    #endregion

    #region SalesInvoiceItem

    [Fact]
    public void SalesInvoiceItem_ConversionForUpdateStock()
    {
        var item = new SalesInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Paint", 3, 80m, 0, "Gallon");
        item.ConversionFactor = 3.785m; // 1 Gallon ≈ 3.785 Litres
        item.StockUom = "Litre";

        Assert.Equal(11.355m, item.StockQty); // 3 × 3.785
    }

    #endregion

    #region PurchaseInvoiceItem

    [Fact]
    public void PurchaseInvoiceItem_DefaultAndConversion()
    {
        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Cement", 10, 22m, 0, "Bag");
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(10m, item.StockQty);

        item.ConversionFactor = 50m; // 1 Bag = 50 Kg
        item.StockUom = "Kg";
        Assert.Equal(500m, item.StockQty);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void UomConversion_ZeroQuantity_ZeroStockQty()
    {
        var item = new SalesOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Test", 0, 10m, 0, "Dozen");
        item.ConversionFactor = 12m;
        Assert.Equal(0m, item.StockQty); // 0 × 12 = 0
    }

    [Fact]
    public void UomConversion_LargeConversionFactor()
    {
        var item = new PurchaseOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Tiny Parts", 0.5m, 1000m, 0, "Gross");
        item.ConversionFactor = 144m; // 1 Gross = 144 units
        item.StockUom = "Unit";
        Assert.Equal(72m, item.StockQty); // 0.5 × 144
    }

    [Fact]
    public void UomConversion_SubUnitConversion()
    {
        // Buying in kg, stocking in grams
        var item = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Gold Leaf", 0.1m, 5000m, 0, "Kg");
        item.ConversionFactor = 1000m; // 1 Kg = 1000 g
        item.StockUom = "Gram";
        Assert.Equal(100m, item.StockQty); // 0.1 × 1000
    }

    #endregion
}
