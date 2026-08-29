using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class PaymentEntryTemplateTests
{
    [Fact]
    public void SalesOrder_UnbilledCheck_WhenPerBilled100_ShouldDetectBilled()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        so.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0, "Nos");
        so.Submit();
        so.Items[0].BilledQty = 10;

        so.PerBilled.ShouldBe(100m);
        so.PerBilled.ShouldBeGreaterThanOrEqualTo(100m);
    }

    [Fact]
    public void PurchaseOrder_UnbilledCheck_WhenPerBilled100_ShouldDetectBilled()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
        po.AddItem(Guid.NewGuid(), "Material", 10, 50, 0, "Nos");
        po.Submit();
        po.Items[0].BilledQty = 10;

        po.PerBilled.ShouldBe(100m);
        po.PerBilled.ShouldBeGreaterThanOrEqualTo(100m);
    }

    [Fact]
    public void SalesInvoice_OutstandingCalculation_ShouldDeductAmountPaid()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.UtcNow);
        si.AddItem(Guid.NewGuid(), "Item", 5, 200, 0);
        si.Submit();
        si.GrandTotal.ShouldBe(1000m);

        var outstanding = Math.Max(0, si.GrandTotal - si.AmountPaid);
        outstanding.ShouldBe(1000m);
    }

    [Fact]
    public void PurchaseInvoice_OutstandingCalculation_ShouldDeductAmountPaid()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Raw Mat", 4, 150, 0);
        pi.Submit();
        pi.GrandTotal.ShouldBe(600m);

        var outstanding = Math.Max(0, pi.GrandTotal - pi.AmountPaid);
        outstanding.ShouldBe(600m);
    }
}
