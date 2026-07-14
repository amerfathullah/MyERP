using System;
using System.Linq;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Purchasing;

public class PurchaseOrderManagerTests
{
    [Fact]
    public void PO_ReceiptQtyValidation_WithinLimit_Succeeds()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);

        // Should not throw — 50 <= 100 pending
        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        manager.ValidateReceiptQty(po, po.Items[0].ItemId, 50);
    }

    [Fact]
    public void PO_ReceiptQtyValidation_ExceedsLimit_Throws()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].ReceivedQty = 80; // 20 remaining

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateReceiptQty(po, itemId, 30));
        ex.Code.ShouldBe("MyERP:08006");
    }

    [Fact]
    public void PO_ReceiptQtyValidation_ExactPending_Succeeds()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].ReceivedQty = 60; // 40 remaining

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        manager.ValidateReceiptQty(po, itemId, 40); // exactly at limit
    }

    [Fact]
    public void PO_BillingQtyValidation_WithinLimit_Succeeds()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].BilledQty = 30;

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        manager.ValidateBillingQty(po, itemId, 60); // 30+60=90 <= 100
    }

    [Fact]
    public void PO_BillingQtyValidation_ExceedsLimit_Throws()
    {
        var po = CreatePO();
        var itemId = Guid.NewGuid();
        po.AddItem(itemId, "Widget", 100, 10, 0);
        po.Items[0].BilledQty = 80;

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        var ex = Should.Throw<BusinessException>(() =>
            manager.ValidateBillingQty(po, itemId, 30)); // 80+30=110 > 100
        ex.Code.ShouldBe("MyERP:08007");
    }

    [Fact]
    public void PO_BillingQtyValidation_UnknownItem_NoThrow()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);

        var manager = new DomainServices.PurchaseOrderManager(null!, null!, null!);
        // Unknown itemId should not throw — no matching PO item to validate against
        manager.ValidateBillingQty(po, Guid.NewGuid(), 999);
    }

    [Fact]
    public void PO_PendingReceiptQty_CalculatesCorrectly()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);
        po.Items[0].ReceivedQty = 40;

        po.Items[0].PendingReceiptQty.ShouldBe(60);
    }

    [Fact]
    public void PO_PendingBillingQty_NeverNegative()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 100, 10, 0);
        po.Items[0].BilledQty = 120; // Over-billed (shouldn't happen, but guard)

        po.Items[0].PendingBillingQty.ShouldBe(0); // Max(0, ...)
    }

    [Fact]
    public void PO_PerReceived_Uses_MinFormula()
    {
        var po = CreatePO();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        po.AddItem(itemA, "Widget A", 100, 10, 0);
        po.AddItem(itemB, "Widget B", 50, 20, 0);

        po.Items[0].ReceivedQty = 100; // 100% received
        po.Items[1].ReceivedQty = 25;  // 50% received

        // Min(100%, 50%) = 50%
        po.PerReceived.ShouldBe(50m);
    }

    [Fact]
    public void PO_PerBilled_Uses_NetTotal()
    {
        var po = CreatePO();
        po.AddItem(Guid.NewGuid(), "Widget", 10, 100, 0); // NetTotal = 1000
        po.Items[0].BilledQty = 5; // 5 * 100 = 500

        // 500 / 1000 * 100 = 50%
        po.PerBilled.ShouldBe(50m);
    }

    private static PurchaseOrder CreatePO()
    {
        return new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-001", DateTime.UtcNow);
    }
}
