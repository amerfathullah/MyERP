using System;
using System.Linq;
using MyERP.Core;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class SalesInvoiceManagerTests
{
    [Fact]
    public void ValidateCanCancel_WithPayments_Throws()
    {
        var si = CreateSI();
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        si.Submit();
        si.Post();
        si.AmountPaid = 500m;

        var manager = new SalesInvoiceManager(null!, null!);
        var ex = Should.Throw<BusinessException>(() => manager.ValidateCanCancel(si));
        ex.Code.ShouldBe("MyERP:01002");
    }

    [Fact]
    public void ValidateCanCancel_NoPayments_Succeeds()
    {
        var si = CreateSI();
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0);
        si.Submit();
        si.Post();

        var manager = new SalesInvoiceManager(null!, null!);
        manager.ValidateCanCancel(si);
    }

    [Fact]
    public void SI_ReturnInvoice_DefaultFalse()
    {
        var si = CreateSI();
        si.IsReturn.ShouldBeFalse();
        si.ReturnAgainstId.ShouldBeNull();
    }

    [Fact]
    public void SI_Outstanding_ReducedByPayment()
    {
        var si = CreateSI();
        si.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0); // GrandTotal = 1000
        si.AmountPaid = 400m;

        si.OutstandingAmount.ShouldBe(600m);
    }

    [Fact]
    public void SI_Amendment_DefaultValues()
    {
        var si = CreateSI();
        si.AmendedFromId.ShouldBeNull();
        si.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void SI_UpdateStock_DefaultFalse()
    {
        var si = CreateSI();
        si.UpdateStock.ShouldBeFalse();
    }

    [Fact]
    public void SI_AddItem_PositiveQty_ForNormal()
    {
        var si = CreateSI();
        si.AddItem(Guid.NewGuid(), "Widget", 5, 100, 0);
        si.Items.Count.ShouldBe(1);
        si.Items[0].Quantity.ShouldBe(5);
    }

    [Fact]
    public void SI_AddItem_ZeroQty_Throws()
    {
        var si = CreateSI();
        Should.Throw<ArgumentException>(() =>
            si.AddItem(Guid.NewGuid(), "Widget", 0, 100, 0));
    }

    [Fact]
    public void SI_PerBilled_Concept()
    {
        // SI doesn't track PerBilled (that's on SO), but NetTotal should match
        var si = CreateSI();
        si.AddItem(Guid.NewGuid(), "Widget A", 2, 100, 0);
        si.AddItem(Guid.NewGuid(), "Widget B", 3, 50, 0);

        si.NetTotal.ShouldBe(350m); // 200 + 150
        si.GrandTotal.ShouldBe(350m); // No tax
    }

    private static SalesInvoice CreateSI()
    {
        return new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
    }
}
