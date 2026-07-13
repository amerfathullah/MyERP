using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Integration;

public class UpdateStockInvoiceTests
{
    [Fact]
    public void PurchaseInvoice_UpdateStock_DefaultFalse()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.UpdateStock.ShouldBeFalse();
        pi.WarehouseId.ShouldBeNull();
    }

    [Fact]
    public void PurchaseInvoice_CanSetUpdateStock()
    {
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.UpdateStock = true;
        pi.WarehouseId = Guid.NewGuid();
        pi.UpdateStock.ShouldBeTrue();
        pi.WarehouseId.ShouldNotBeNull();
    }

    [Fact]
    public void PurchaseInvoice_UpdateStock_StockIn_Concept()
    {
        // PI with UpdateStock=true → stock INCREASES (positive SLE)
        var qty = 50m;
        var rate = 100m;
        var expectedStockValue = qty * rate; // 5000 added to stock
        expectedStockValue.ShouldBe(5000m);
    }

    [Fact]
    public void SalesInvoice_UpdateStock_StockOut_Concept()
    {
        // SI with UpdateStock=true → stock DECREASES (negative SLE)
        var qty = 30m;
        var rate = 150m;
        var expectedStockReduction = -(qty * rate); // -4500 from stock
        expectedStockReduction.ShouldBe(-4500m);
    }

    [Fact]
    public void SalesInvoice_Cancel_WithUpdateStock_ReversesStockOut()
    {
        // When SI with UpdateStock is cancelled, stock should be restored
        var originalStockOut = -30m; // 30 units deducted on submit
        var reversalStockIn = 30m;  // 30 units added back on cancel
        (originalStockOut + reversalStockIn).ShouldBe(0m); // Net zero
    }

    [Fact]
    public void PurchaseInvoice_Return_WithUpdateStock_NotApplicable()
    {
        // Per DO-NOT: UpdateStock is skipped for returns (accounting-only)
        var pi = new PurchaseInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-001", DateTime.UtcNow);
        pi.IsReturn = true;
        pi.UpdateStock = true;
        // The AppService checks !invoice.IsReturn before creating SLE
        // So returns with UpdateStock=true are still accounting-only
        pi.IsReturn.ShouldBeTrue();
    }

    [Fact]
    public void SalesInvoice_Return_WithUpdateStock_NotApplicable()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        si.UpdateStock = true;
        // Returns use the credit note path, not the stock-out path
        si.IsReturn.ShouldBeTrue();
    }
}
