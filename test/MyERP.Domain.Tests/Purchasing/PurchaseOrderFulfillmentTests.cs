using System;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseOrderFulfillmentTests
{
    [Fact]
    public void PerReceived_NoReceipts_ReturnsZero()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Raw Material", 100, 25, 0);

        po.PerReceived.ShouldBe(0);
    }

    [Fact]
    public void PerReceived_PartialReceipt_ReturnsCorrectPercentage()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Steel Sheet", 20, 500, 0);

        po.Items[0].ReceivedQty = 15;

        po.PerReceived.ShouldBe(75m);
    }

    [Fact]
    public void PerBilled_NoBilling_ReturnsZero()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Component", 10, 100, 0);

        po.PerBilled.ShouldBe(0);
    }

    [Fact]
    public void PendingReceiptQty_CalculatesCorrectly()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Part", 50, 10, 0);

        po.Items[0].ReceivedQty = 35;

        po.Items[0].PendingReceiptQty.ShouldBe(15);
    }

    [Fact]
    public void PendingBillingQty_NeverNegative()
    {
        var po = CreatePurchaseOrder();
        po.AddItem(Guid.NewGuid(), "Part", 10, 10, 0);

        po.Items[0].BilledQty = 15; // Over-billed edge case

        po.Items[0].PendingBillingQty.ShouldBe(0);
    }

    [Fact]
    public void CloseItem_ExcludesFromPendingAndUpdatesStatus()
    {
        var po = CreatePurchaseOrder();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        po.AddItem(item1Id, "Part 1", 10, 100, 0);
        po.AddItem(item2Id, "Part 2", 5, 200, 0);

        po.Submit();
        po.Status.ShouldBe(Core.DocumentStatus.ToDeliverAndBill);

        // Fulfill item 1 completely
        po.Items[0].ReceivedQty = 10;
        po.Items[0].BilledQty = 10;
        po.UpdateFulfillmentStatus();
        po.Status.ShouldBe(Core.DocumentStatus.ToDeliverAndBill);

        // Close remaining item 2 (ERPNext PR #57596)
        var row2 = po.Items[1];
        po.CloseItem(row2.Id);

        row2.IsClosed.ShouldBeTrue();
        row2.PendingReceiptQty.ShouldBe(0m);
        row2.PendingBillingQty.ShouldBe(0m);

        // PO should now be completed because all active items are 100% fulfilled
        po.PerReceived.ShouldBe(100m);
        po.PerBilled.ShouldBe(100m);
        po.Status.ShouldBe(Core.DocumentStatus.Completed);

        // Reopen item 2
        po.ReopenItem(row2.Id);
        row2.IsClosed.ShouldBeFalse();
        row2.PendingReceiptQty.ShouldBe(5m);
        row2.PendingBillingQty.ShouldBe(5m);
        po.Status.ShouldBe(Core.DocumentStatus.ToDeliverAndBill);
    }

    private static PurchaseOrder CreatePurchaseOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
}
