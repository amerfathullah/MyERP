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

    [Fact]
    public void CloseItem_WhenSettled_ShouldThrow()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);
        dn.Submit();
        var item = dn.Items[0];
        item.BilledQty = 10;

        var ex = Assert.Throws<BusinessException>(() => dn.CloseItem(item.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void CloseItem_PartiallyBilled_ShouldSetClosed_AndAdjustPerBilled()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget A", 10, 100m, 0m);
        dn.AddItem(Guid.NewGuid(), "Widget B", 10, 100m, 0m);
        dn.Submit();

        var itemA = dn.Items[0];
        var itemB = dn.Items[1];

        itemA.BilledQty = 5; // 50%
        itemB.BilledQty = 0; // 0%
        dn.PerBilled.ShouldBe(0m); // Min of 50% and 0% is 0%

        // Close item B (which had 0% billed)
        dn.CloseItem(itemB.Id);
        itemB.IsClosed.ShouldBeTrue();
        itemB.PendingBillingQty.ShouldBe(0m);

        // PerBilled basis should now be open items only (Item A at 50%)
        dn.PerBilled.ShouldBe(50m);
        dn.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void CloseItem_AllItemsClosed_ShouldCloseDocument()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        dn.CloseItem(item.Id);

        item.IsClosed.ShouldBeTrue();
        dn.Status.ShouldBe(DocumentStatus.Closed);
        dn.BillingStatus.ShouldBe("Closed");

        // When all items closed, fallback basis is full items: 0% billed
        dn.PerBilled.ShouldBe(0m);
    }

    [Fact]
    public void ReopenItem_WhenDocumentClosed_ShouldReopenDocument()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        dn.CloseItem(item.Id);
        dn.Status.ShouldBe(DocumentStatus.Closed);

        dn.ReopenItem(item.Id);
        item.IsClosed.ShouldBeFalse();
        dn.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Reopen_WhenAllItemsClosed_ShouldThrow()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        dn.CloseItem(item.Id);
        dn.Status.ShouldBe(DocumentStatus.Closed);

        var ex = Assert.Throws<BusinessException>(() => dn.Reopen());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Close_And_Reopen_DeliveryNote_ShouldWork()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget", 10, 100m, 0m);
        dn.Submit();

        dn.Close();
        dn.Status.ShouldBe(DocumentStatus.Closed);

        dn.Reopen();
        dn.Status.ShouldBe(DocumentStatus.Submitted);
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
