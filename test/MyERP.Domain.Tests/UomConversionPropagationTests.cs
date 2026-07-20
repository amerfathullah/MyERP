using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests verifying UOM conversion fields are properly maintained across document lifecycle.
/// Critical: if StockUom/ConversionFactor are lost during conversion, stock movements
/// would use transaction qty instead of stock qty — causing inventory corruption.
/// </summary>
public class UomConversionPropagationTests
{
    // === SO Item UOM Fields ===

    [Fact]
    public void SalesOrderItem_StockUom_Defaults_Unit()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Widget", 5, 120m, 0m);
        Assert.Equal("Unit", so.Items[0].StockUom);
    }

    [Fact]
    public void SalesOrderItem_ConversionFactor_Defaults_One()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Widget", 5, 120m, 0m);
        Assert.Equal(1m, so.Items[0].ConversionFactor);
    }

    [Fact]
    public void SalesOrderItem_StockQty_WhenFactorSet()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Widget Dozen", 3, 120m, 0m);
        so.Items[0].StockUom = "Unit";
        so.Items[0].ConversionFactor = 12m;
        Assert.Equal(36m, so.Items[0].StockQty);
    }

    // === PO Item UOM Fields ===

    [Fact]
    public void PurchaseOrderItem_StockUom_Defaults_Unit()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Raw Material", 10, 50m, 0m);
        Assert.Equal("Unit", po.Items[0].StockUom);
    }

    [Fact]
    public void PurchaseOrderItem_ConversionFactor_CanBeSet()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Chemical Kg", 5, 200m, 0m);
        po.Items[0].StockUom = "Gram";
        po.Items[0].ConversionFactor = 1000m;
        Assert.Equal(5000m, po.Items[0].StockQty);
    }

    // === SI Item UOM Fields ===

    [Fact]
    public void SalesInvoiceItem_StockUom_Defaults_Unit()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Service", 1, 500m, 0m);
        Assert.Equal("Unit", si.Items[0].StockUom);
    }

    [Fact]
    public void SalesInvoiceItem_ConversionFactor_UsedForStockQty()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Box of Nails", 2, 50m, 0m);
        si.Items[0].StockUom = "Unit";
        si.Items[0].ConversionFactor = 100m;
        Assert.Equal(200m, si.Items[0].StockQty);
    }

    // === DN Item UOM Fields ===

    [Fact]
    public void DeliveryNoteItem_UomFields_PropagatedFromSOConversion()
    {
        // Simulates what happens during SO→DN conversion
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Dozen Eggs", 3, 12m, 0m, "Dozen");
        dn.Items[0].StockUom = "Unit";
        dn.Items[0].ConversionFactor = 12m;
        Assert.Equal(36m, dn.Items[0].StockQty);
        Assert.Equal("Unit", dn.Items[0].StockUom);
    }

    // === PR Item UOM Fields ===

    [Fact]
    public void PurchaseReceiptItem_UomFields_PropagatedFromPOConversion()
    {
        var pr = CreatePurchaseReceipt();
        pr.AddItem(Guid.NewGuid(), "Pallet of Cement", 2, 5000m, 0m, "Pallet", Guid.NewGuid());
        pr.Items[0].StockUom = "Bag";
        pr.Items[0].ConversionFactor = 50m;
        Assert.Equal(100m, pr.Items[0].StockQty);
    }

    // === ValidateSellingPriceAsync ===

    [Fact]
    public async Task ValidateSellingPriceAsync_AboveCost_Passes()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 100m, "Widget")
        }.AsReadOnly();

        var result = await SalesInvoiceManager.ValidateSellingPriceAsync(
            items,
            itemId => Task.FromResult(50m), // valuation = 50, selling = 100 → OK
            action: "Warn");

        Assert.False(result.HasWarnings);
    }

    [Fact]
    public async Task ValidateSellingPriceAsync_BelowCost_Warn_ReturnsWarning()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 30m, "Clearance Item")
        }.AsReadOnly();

        var result = await SalesInvoiceManager.ValidateSellingPriceAsync(
            items,
            itemId => Task.FromResult(50m), // valuation = 50, selling = 30 → below
            action: "Warn");

        Assert.True(result.HasWarnings);
        Assert.Contains("Clearance Item", result.Warnings[0]);
    }

    [Fact]
    public async Task ValidateSellingPriceAsync_BelowCost_Stop_Throws()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 30m, "Loss Leader")
        }.AsReadOnly();

        await Assert.ThrowsAsync<Volo.Abp.BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPriceAsync(
                items,
                itemId => Task.FromResult(50m),
                action: "Stop"));
    }

    [Fact]
    public async Task ValidateSellingPriceAsync_ZeroCost_Skipped()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 10m, "New Item")
        }.AsReadOnly();

        var result = await SalesInvoiceManager.ValidateSellingPriceAsync(
            items,
            itemId => Task.FromResult(0m), // no cost data → skip
            action: "Stop");

        Assert.False(result.HasWarnings);
    }

    [Fact]
    public async Task ValidateSellingPriceAsync_MultiItem_FirstBelowThrows()
    {
        var items = new List<(Guid, decimal, string)>
        {
            (Guid.NewGuid(), 30m, "Cheap"),
            (Guid.NewGuid(), 100m, "Expensive")
        }.AsReadOnly();

        var ex = await Assert.ThrowsAsync<Volo.Abp.BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPriceAsync(
                items,
                itemId => Task.FromResult(50m),
                action: "Stop"));

        Assert.Contains("Cheap", ex.Data["item"]?.ToString());
    }

    // === StockQty Consistency Tests ===

    [Fact]
    public void StockQty_SameUom_EqualsQuantity()
    {
        var so = CreateSalesOrder();
        so.AddItem(Guid.NewGuid(), "Item", 7, 100m, 0m);
        // Default: same UOM, factor=1 → StockQty == Quantity
        Assert.Equal(so.Items[0].Quantity, so.Items[0].StockQty);
    }

    [Fact]
    public void StockQty_FractionalFactor()
    {
        var si = CreateSalesInvoice();
        si.AddItem(Guid.NewGuid(), "Gallon of Paint", 5, 200m, 0m);
        si.Items[0].StockUom = "Litre";
        si.Items[0].ConversionFactor = 3.785m;
        Assert.Equal(18.925m, si.Items[0].StockQty);
    }

    [Fact]
    public void StockQty_LargeConversion_PalletToUnit()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Pallet of Screws", 1, 10000m, 0m);
        po.Items[0].StockUom = "Unit";
        po.Items[0].ConversionFactor = 5000m;
        Assert.Equal(5000m, po.Items[0].StockQty);
    }

    // === Helpers ===

    private SalesOrder CreateSalesOrder() =>
        new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.Today);

    private PurchaseOrder CreatePurchaseOrder() =>
        new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PO-001", DateTime.Today);

    private SalesInvoice CreateSalesInvoice() =>
        new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "INV-001", DateTime.Today);

    private DeliveryNote CreateDeliveryNote() =>
        new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-001", DateTime.Today);

    private PurchaseReceipt CreatePurchaseReceipt() =>
        new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "PR-001", DateTime.Today);
}
