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

    /// <summary>
    /// Per ERPNext PR #58869 / commit f864333afa:
    /// test_dn_is_completed_when_unbilled_item_is_returned
    /// When unbilled item is returned, original DN reaches 100% billing and becomes Completed.
    /// When return is cancelled, original DN reverts to Partially Billed.
    /// </summary>
    [Fact]
    public void DeliveryNote_IsCompleted_WhenUnbilledItemIsReturned()
    {
        var dn = CreateDeliveryNote();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        dn.AddItem(item1Id, "Widget 1", 1, 100m, 0m);
        dn.AddItem(item2Id, "Widget 2", 1, 100m, 0m);
        dn.Submit();

        // 1. Initial submitted state: unbilled -> To Bill
        dn.BillingStatus.ShouldBe("To Bill");
        dn.PerBilled.ShouldBe(0m);

        // 2. Item 1 billed (qty 1)
        dn.Items[0].BilledQty = 1;
        dn.BillingStatus.ShouldBe("Partially Billed");

        // 3. Item 2 returned (ReturnedQty = 1) -> net billable qty becomes 0 for item 2
        dn.Items[1].ReturnedQty = 1;
        dn.UpdateBillingStatus();
        dn.PerBilled.ShouldBe(100m);
        dn.BillingStatus.ShouldBe("Completed");

        // 4. Return cancelled -> ReturnedQty reverts to 0
        dn.Items[1].ReturnedQty = 0;
        dn.UpdateBillingStatus();
        dn.BillingStatus.ShouldBe("Partially Billed");
    }

    [Fact]
    public void DeliveryNote_BillingStatus_ShowsPartiallyBilled_WhenAnyItemBilled()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Widget A", 10, 100m, 0m);
        dn.AddItem(Guid.NewGuid(), "Widget B", 10, 100m, 0m);
        dn.Submit();

        // Item A partially billed, Item B not billed
        dn.Items[0].BilledQty = 5;
        dn.Items[1].BilledQty = 0;

        dn.PerBilled.ShouldBe(0m); // Min(50%, 0%) = 0%
        dn.BillingStatus.ShouldBe("Partially Billed");
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
