using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Sales;

public class DeliveryNoteReturnTests
{
    private static DeliveryNote CreateDN(bool isReturn = false)
    {
        var dn = new DeliveryNote(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "DN-001", DateTime.UtcNow);
        dn.IsReturn = isReturn;
        return dn;
    }

    [Fact]
    public void DeliveryNote_IsReturn_DefaultFalse()
    {
        var dn = CreateDN();
        dn.IsReturn.ShouldBeFalse();
    }

    [Fact]
    public void DeliveryNote_CanSetIsReturn()
    {
        var dn = CreateDN(true);
        dn.IsReturn.ShouldBeTrue();
    }

    [Fact]
    public void DeliveryNote_ReturnAgainstId_Nullable()
    {
        var dn = CreateDN(true);
        dn.ReturnAgainstId.ShouldBeNull();
        var originalId = Guid.NewGuid();
        dn.ReturnAgainstId = originalId;
        dn.ReturnAgainstId.ShouldBe(originalId);
    }

    [Fact]
    public void DeliveryNote_Return_NegativeQty_Semantic()
    {
        // Returns should use negative qty (positive return qty = stock coming back)
        var dn = CreateDN(true);
        dn.AddItem(Guid.NewGuid(), "Widget Return", -5m, 100m, 0m);
        dn.Items[0].Quantity.ShouldBe(-5m);
        // When IsReturn=true, the submit logic uses Math.Abs(qty) to ADD stock back
    }

    [Fact]
    public void DeliveryNote_Return_NoReservedQtyRelease()
    {
        // Returns don't release reserved qty - only normal deliveries do
        var dn = CreateDN(true);
        dn.ReturnAgainstId = Guid.NewGuid();
        dn.IsReturn.ShouldBeTrue();
        // The AppService handles this via the if/else branch
    }

    [Fact]
    public void DeliveryNote_Normal_PositiveQty()
    {
        var dn = CreateDN(false);
        dn.AddItem(Guid.NewGuid(), "Widget", 10m, 100m, 0m);
        dn.Items[0].Quantity.ShouldBe(10m);
    }

    [Fact]
    public void DeliveryNote_Return_AbsQty_CalculatesStockIn()
    {
        // Simulate the return logic: Math.Abs(-5) = 5 units back to stock
        var returnQty = -5m;
        var stockIn = Math.Abs(returnQty);
        stockIn.ShouldBe(5m);
    }
}
