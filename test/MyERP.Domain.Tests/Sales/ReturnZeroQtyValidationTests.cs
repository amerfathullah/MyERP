using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class ReturnZeroQtyValidationTests
{
    [Fact]
    public void SalesInvoice_AddItem_WhenReturnHasNonNegativeQty_Throws()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SINV-RET-001", DateTime.UtcNow.Date)
        {
            IsReturn = true
        };

        var ex = Should.Throw<ArgumentException>(() => si.AddItem(Guid.NewGuid(), "Widget", 0m, 100m, 0m));
        ex.Message.ShouldContain("Quantity must be negative for return invoices");
    }

    [Fact]
    public void SalesInvoice_Submit_WhenReturnHasNegativeQty_Succeeds()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SINV-RET-002", DateTime.UtcNow.Date)
        {
            IsReturn = true
        };
        si.AddItem(Guid.NewGuid(), "Returned Widget", -5m, 100m, 0m);

        Should.NotThrow(() => si.Submit());
        si.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void DeliveryNote_Submit_WhenReturnHasNegativeQty_Succeeds()
    {
        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-RET-001", DateTime.UtcNow.Date)
        {
            IsReturn = true
        };
        dn.AddItem(Guid.NewGuid(), "Returned Widget", -5m, 100m, 0m);

        Should.NotThrow(() => dn.Submit());
        dn.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void PurchaseInvoice_Submit_WhenReturnHasNegativeQty_Succeeds()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PINV-RET-001", DateTime.UtcNow.Date)
        {
            IsReturn = true
        };
        pi.AddItem(Guid.NewGuid(), "Returned Raw Material", -5m, 100m, 0m);

        Should.NotThrow(() => pi.Submit());
        pi.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }
}
