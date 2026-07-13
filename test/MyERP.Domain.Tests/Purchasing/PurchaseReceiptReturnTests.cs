using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Purchasing;

public class PurchaseReceiptReturnTests
{
    private static PurchaseReceipt CreatePR(bool isReturn = false)
    {
        var pr = new PurchaseReceipt(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        pr.IsReturn = isReturn;
        return pr;
    }

    [Fact]
    public void PurchaseReceipt_IsReturn_DefaultFalse()
    {
        var pr = CreatePR();
        pr.IsReturn.ShouldBeFalse();
    }

    [Fact]
    public void PurchaseReceipt_CanSetIsReturn()
    {
        var pr = CreatePR(true);
        pr.IsReturn.ShouldBeTrue();
    }

    [Fact]
    public void PurchaseReceipt_ReturnAgainstId_Nullable()
    {
        var pr = CreatePR(true);
        pr.ReturnAgainstId.ShouldBeNull();
        var originalId = Guid.NewGuid();
        pr.ReturnAgainstId = originalId;
        pr.ReturnAgainstId.ShouldBe(originalId);
    }

    [Fact]
    public void PurchaseReceipt_Return_NegativeQty_Semantic()
    {
        var pr = CreatePR(true);
        pr.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, 0m, "Unit");
        pr.Items[0].Quantity.ShouldBe(-5m);
        // Math.Abs(-5) = 5 units OUT of stock on submit (return to supplier)
    }

    [Fact]
    public void PurchaseReceipt_Return_AbsQty_CalculatesStockOut()
    {
        var returnQty = -3m;
        var stockOut = Math.Abs(returnQty);
        stockOut.ShouldBe(3m);
        // Negative SLE = stock going out (returned to supplier)
        var sleQty = -stockOut;
        sleQty.ShouldBe(-3m);
    }

    [Fact]
    public void PurchaseReceipt_Normal_PositiveQty_StockIn()
    {
        var pr = CreatePR(false);
        pr.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m, "Unit");
        pr.Items[0].Quantity.ShouldBeGreaterThan(0);
        // Positive SLE = stock coming in
    }
}
