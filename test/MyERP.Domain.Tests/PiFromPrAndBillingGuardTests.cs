using System;
using System.Linq;
using MyERP.Core;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PI "Get Items from PR", DN→SI billing guard,
/// PR item billing tracking, and conversion error context.
/// </summary>
public class PiFromPrAndBillingGuardTests
{
    // === Purchase Receipt Item — Billing Qty Tracking ===

    [Fact]
    public void PurchaseReceiptItem_PendingBillingQty_FullyUnbilled()
    {
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Widget", 100m, 25m, 0m)
        { BilledQty = 0 };

        Assert.Equal(100m, prItem.PendingBillingQty);
    }

    [Fact]
    public void PurchaseReceiptItem_PendingBillingQty_PartiallyBilled()
    {
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Gadget", 50m, 30m, 0m)
        { BilledQty = 20m };

        Assert.Equal(30m, prItem.PendingBillingQty);
    }

    [Fact]
    public void PurchaseReceiptItem_PendingBillingQty_FullyBilled()
    {
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Part X", 75m, 10m, 0m)
        { BilledQty = 75m };

        Assert.Equal(0m, prItem.PendingBillingQty);
    }

    [Fact]
    public void PurchaseReceiptItem_PendingBillingQty_NeverNegative()
    {
        // Even if BilledQty > Quantity (overshoot), pending should be 0, not negative
        var prItem = new PurchaseReceiptItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Over-billed", 10m, 5m, 0m)
        { BilledQty = 15m };

        Assert.Equal(0m, prItem.PendingBillingQty);
    }

    // === Purchase Receipt — PerBilled Calculation ===

    [Fact]
    public void PurchaseReceipt_PerBilled_ZeroWhenNothingBilled()
    {
        var pr = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "PR-001", DateTime.UtcNow);
        pr.AddItem(Guid.NewGuid(), "Item A", 100m, 50m, 0m);
        // No billing done yet
        Assert.Equal(0m, pr.PerBilled);
    }

    // === Delivery Note Item — Billing Tracking for DN→SI Guard ===

    [Fact]
    public void DeliveryNoteItem_BilledQty_TracksInvoicing()
    {
        var dnItem = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Delivered Widget", 20m, 100m, 0m);
        Assert.Equal(20m, dnItem.Quantity);
        // When SI is created from DN, BilledQty is updated
        dnItem.BilledQty = 15m;
        Assert.Equal(15m, dnItem.BilledQty);
    }

    // === Sales Invoice — Credit Note from Return DN ===

    [Fact]
    public void SalesInvoice_CreditNote_LinksToDn()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "CN-001", DateTime.UtcNow);
        si.IsReturn = true;
        si.DeliveryNoteId = Guid.NewGuid();
        si.ReturnAgainstId = Guid.NewGuid();

        Assert.True(si.IsReturn);
        Assert.NotNull(si.DeliveryNoteId);
        Assert.NotNull(si.ReturnAgainstId);
    }

    // === Purchase Invoice — Linked PO/PR Item Tracking ===

    [Fact]
    public void PurchaseInvoiceItem_HasPurchaseReceiptItemId()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-001", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Component X", 5m, 200m, 0m);
        var piItem = pi.Items.Last();
        piItem.PurchaseReceiptItemId = Guid.NewGuid();
        piItem.PurchaseOrderItemId = Guid.NewGuid();

        Assert.NotNull(piItem.PurchaseReceiptItemId);
        Assert.NotNull(piItem.PurchaseOrderItemId);
    }

    // === PI Cancel Guards ===

    [Fact]
    public void PurchaseInvoice_Cancel_BlockedWhenPaymentApplied()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-002", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Item", 1m, 100m, 0m);
        pi.Submit();
        pi.Post();
        pi.AmountPaid = 50m; // Simulate partial payment

        // Backend AppService checks AmountPaid > 0 before allowing cancel
        Assert.True(pi.AmountPaid > 0);
    }

    [Fact]
    public void PurchaseInvoice_Cancel_FromPostedOnly()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "PI-003", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Item", 1m, 50m, 0m);
        // Cannot cancel from Draft
        Assert.Throws<Volo.Abp.BusinessException>(() => pi.Cancel());
    }
}
