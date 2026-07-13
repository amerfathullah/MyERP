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

    private static PurchaseOrder CreatePurchaseOrder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
}
