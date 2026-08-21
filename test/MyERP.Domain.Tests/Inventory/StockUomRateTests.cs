using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for stock_uom_rate calculation across 7 transaction item types (Gotcha #198):
/// PR, PI, PO, SI, SO, DN, Quotation.
/// Formula: rate / (conversion_factor or 1).
/// </summary>
public class StockUomRateTests
{
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public void PurchaseOrderItem_StockUomRate_CalculatesCorrectly()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(_itemId, "Box of 12", 2m, 120m, 0m); // 120 per Box of 12
        po.Items[0].ConversionFactor = 12m;

        // 120 / 12 = 10 per unit
        Assert.Equal(10m, po.Items[0].StockUomRate);
    }

    [Fact]
    public void PurchaseReceiptItem_StockUomRate_CalculatesCorrectly()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        pr.AddItem(_itemId, "Box of 12", 2m, 240m, 0m);
        pr.Items[0].ConversionFactor = 12m;

        // 240 / 12 = 20 per unit
        Assert.Equal(20m, pr.Items[0].StockUomRate);
    }

    [Fact]
    public void PurchaseInvoiceItem_StockUomRate_CalculatesCorrectly()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        pi.AddItem(_itemId, "Pack of 5", 4m, 50m, 0m);
        pi.Items[0].ConversionFactor = 5m;

        // 50 / 5 = 10 per unit
        Assert.Equal(10m, pi.Items[0].StockUomRate);
    }

    [Fact]
    public void SalesOrderItem_StockUomRate_CalculatesCorrectly()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(_itemId, "Box of 10", 3m, 150m, 0m);
        so.Items[0].ConversionFactor = 10m;

        // 150 / 10 = 15 per unit
        Assert.Equal(15m, so.Items[0].StockUomRate);
    }

    [Fact]
    public void DeliveryNoteItem_StockUomRate_CalculatesCorrectly()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.AddItem(_itemId, "Box of 10", 3m, 200m, 0m);
        dn.Items[0].ConversionFactor = 10m;

        // 200 / 10 = 20 per unit
        Assert.Equal(20m, dn.Items[0].StockUomRate);
    }

    [Fact]
    public void SalesInvoiceItem_StockUomRate_CalculatesCorrectly()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(_itemId, "Box of 24", 1m, 480m, 0m);
        si.Items[0].ConversionFactor = 24m;

        // 480 / 24 = 20 per unit
        Assert.Equal(20m, si.Items[0].StockUomRate);
    }

    [Fact]
    public void QuotationItem_StockUomRate_CalculatesCorrectly()
    {
        var quot = new Quotation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QUOT-001", DateTime.UtcNow);
        quot.AddItem(_itemId, "Pack of 4", 10m, 80m, 0m);
        quot.Items[0].ConversionFactor = 4m;

        // 80 / 4 = 20 per unit
        Assert.Equal(20m, quot.Items[0].StockUomRate);
    }
}
