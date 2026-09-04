using System;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseReceiptTests
{
    [Fact]
    public void Submit_WithItems_ShouldChangeStatus()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Steel Coil", 10, 500m, 0m);

        receipt.Submit();

        receipt.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutItems_ShouldThrow()
    {
        var receipt = CreateReceipt();

        Assert.Throws<BusinessException>(() => receipt.Submit());
    }

    [Fact]
    public void Cancel_AfterSubmit_ShouldChangeStatus()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Copper Wire", 100, 15m, 0m);
        receipt.Submit();

        receipt.Cancel();

        receipt.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_ShouldThrow()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Item", 1, 100m, 0m);

        Assert.Throws<BusinessException>(() => receipt.Cancel());
    }

    [Fact]
    public void AddItem_ShouldRecalculateTotals()
    {
        var receipt = CreateReceipt();

        receipt.AddItem(Guid.NewGuid(), "Part A", 5, 100m, 30m);   // 500 + 30
        receipt.AddItem(Guid.NewGuid(), "Part B", 20, 25m, 30m);   // 500 + 30

        receipt.NetTotal.ShouldBe(1000m);
        receipt.TaxAmount.ShouldBe(60m);
        receipt.GrandTotal.ShouldBe(1060m);
    }

    [Fact]
    public void AddItem_AfterSubmit_ShouldThrow()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Item", 1, 100m, 6m);
        receipt.Submit();

        Assert.Throws<BusinessException>(() =>
            receipt.AddItem(Guid.NewGuid(), "New Item", 1, 50m, 3m));
    }

    [Fact]
    public void CloseItem_WhenSettled_ShouldThrow()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Copper Wire", 100, 15m, 0m);
        receipt.Submit();
        var item = receipt.Items[0];
        item.BilledQty = 100;

        var ex = Assert.Throws<BusinessException>(() => receipt.CloseItem(item.Id));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void CloseItem_PartiallyBilled_ShouldSetClosed_AndAdjustPerBilled()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Part A", 10, 100m, 0m);
        receipt.AddItem(Guid.NewGuid(), "Part B", 10, 100m, 0m);
        receipt.Submit();

        var itemA = receipt.Items[0];
        var itemB = receipt.Items[1];

        itemA.BilledQty = 6; // 60%
        itemB.BilledQty = 0; // 0%
        receipt.PerBilled.ShouldBe(0m); // Min of 60% and 0% is 0%

        // Close item B (which had 0% billed)
        receipt.CloseItem(itemB.Id);
        itemB.IsClosed.ShouldBeTrue();
        itemB.PendingBillingQty.ShouldBe(0m);

        // PerBilled basis should now be open items only (Item A at 60%)
        receipt.PerBilled.ShouldBe(60m);
        receipt.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void CloseItem_AllItemsClosed_ShouldCloseDocument()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Part A", 10, 100m, 0m);
        receipt.Submit();

        var item = receipt.Items[0];
        receipt.CloseItem(item.Id);

        item.IsClosed.ShouldBeTrue();
        receipt.Status.ShouldBe(DocumentStatus.Closed);
        receipt.BillingStatus.ShouldBe("Closed");

        // When all items closed, fallback basis is full items: 0% billed
        receipt.PerBilled.ShouldBe(0m);
    }

    [Fact]
    public void ReopenItem_WhenDocumentClosed_ShouldReopenDocument()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Part A", 10, 100m, 0m);
        receipt.Submit();

        var item = receipt.Items[0];
        receipt.CloseItem(item.Id);
        receipt.Status.ShouldBe(DocumentStatus.Closed);

        receipt.ReopenItem(item.Id);
        item.IsClosed.ShouldBeFalse();
        receipt.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public void Reopen_WhenAllItemsClosed_ShouldThrow()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Part A", 10, 100m, 0m);
        receipt.Submit();

        var item = receipt.Items[0];
        receipt.CloseItem(item.Id);
        receipt.Status.ShouldBe(DocumentStatus.Closed);

        var ex = Assert.Throws<BusinessException>(() => receipt.Reopen());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void Close_And_Reopen_PurchaseReceipt_ShouldWork()
    {
        var receipt = CreateReceipt();
        receipt.AddItem(Guid.NewGuid(), "Part A", 10, 100m, 0m);
        receipt.Submit();

        receipt.Close();
        receipt.Status.ShouldBe(DocumentStatus.Closed);

        receipt.Reopen();
        receipt.Status.ShouldBe(DocumentStatus.Submitted);
    }

    private static PurchaseReceipt CreateReceipt()
    {
        return new PurchaseReceipt(
            Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            supplierId: Guid.NewGuid(),
            warehouseId: Guid.NewGuid(),
            receiptNumber: "PR-2026-00001",
            postingDate: DateTime.Today);
    }
}
