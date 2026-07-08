using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class DeliveryNoteTests
{
    [Fact]
    public void Submit_WithItems_ShouldChangeStatus()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);

        dn.Submit();

        dn.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_ShouldThrow()
    {
        var dn = CreateDeliveryNote();

        Assert.Throws<BusinessException>(() => dn.Submit());
    }

    [Fact]
    public void Submit_AlreadySubmitted_ShouldThrow()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 5, 50m, 0m);
        dn.Submit();

        Assert.Throws<BusinessException>(() => dn.Submit());
    }

    [Fact]
    public void Cancel_AfterSubmit_ShouldChangeStatus()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Item", 1, 200m, 12m);
        dn.Submit();

        dn.Cancel();

        dn.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldThrow()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Item", 1, 200m, 12m);

        Assert.Throws<BusinessException>(() => dn.Cancel());
    }

    [Fact]
    public void AddItem_ShouldRecalculateTotals()
    {
        var dn = CreateDeliveryNote();

        dn.AddItem(Guid.NewGuid(), "Product X", 3, 300m, 54m);  // 900 + 54
        dn.AddItem(Guid.NewGuid(), "Product Y", 2, 150m, 18m);  // 300 + 18

        dn.NetTotal.ShouldBe(1200m);
        dn.TaxAmount.ShouldBe(72m);
        dn.GrandTotal.ShouldBe(1272m);
    }

    [Fact]
    public void AddItem_AfterSubmit_ShouldThrow()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 6m);
        dn.Submit();

        Assert.Throws<BusinessException>(() =>
            dn.AddItem(Guid.NewGuid(), "Extra", 1, 50m, 3m));
    }

    private static DeliveryNote CreateDeliveryNote()
    {
        return new DeliveryNote(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            warehouseId: Guid.NewGuid(),
            deliveryNumber: "DN-2026-00001",
            postingDate: DateTime.Today);
    }
}
