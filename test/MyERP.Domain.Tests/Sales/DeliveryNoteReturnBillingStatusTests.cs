using System;
using MyERP.Core;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Domain unit tests for Delivery Note billing status and return calculation
/// per ERPNext PR #58953 (commit be8208e7cb) and PR #58869 (commit f864333afa).
/// </summary>
public class DeliveryNoteReturnBillingStatusTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private DeliveryNote CreateDeliveryNote(string number = "DN-2026-001", bool isReturn = false)
    {
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, number, DateTime.UtcNow);
        dn.IsReturn = isReturn;
        return dn;
    }

    /// <summary>
    /// Per ERPNext PR #58953 & PR #58869:
    /// Delivery Note invoiced for 2 of 5 qty, the remaining 3 returned -> 100% billed -> Completed.
    /// </summary>
    [Fact]
    public void DeliveryNote_PartiallyInvoiced_RemainingReturned_BecomesCompleted()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Item A", 5m, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        item.BilledQty = 2m; // 2 of 5 billed (40%)
        dn.BillingStatus.ShouldBe("Partially Billed");
        dn.PerBilled.ShouldBe(40m);

        // Return remaining 3 units
        item.ReturnedQty = 3m;
        dn.UpdateBillingStatus();

        // Net delivered = 5 - 3 = 2; Billed = 2 -> 100% billed
        dn.PerBilled.ShouldBe(100m);
        dn.BillingStatus.ShouldBe("Completed");
        dn.PerReturned.ShouldBe(60m);
    }

    /// <summary>
    /// Per ERPNext PR #58953:
    /// Delivery Note linked to Sales Order, nothing invoiced, partly returned -> 0% billed -> To Bill.
    /// </summary>
    [Fact]
    public void DeliveryNote_Uninvoiced_PartiallyReturned_RemainsToBill()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Item B", 5m, 100m, 0m);
        dn.Submit();

        var item = dn.Items[0];
        item.BilledQty = 0m;
        item.ReturnedQty = 2m; // 2 returned, 3 remain unbilled
        dn.UpdateBillingStatus();

        dn.PerBilled.ShouldBe(0m);
        dn.BillingStatus.ShouldBe("To Bill");
        dn.PerReturned.ShouldBe(40m);
        item.BillableQty.ShouldBe(3m);
        item.PendingBillingQty.ShouldBe(3m);
    }

    /// <summary>
    /// Per ERPNext PR #58869 (test_dn_is_completed_when_unbilled_item_is_returned):
    /// Item 1 (1 delivered, 1 billed, 0 returned), Item 2 (1 delivered, 0 billed, 1 returned).
    /// When Item 2 is returned, the DN reaches 100% billing and becomes Completed.
    /// When return is cancelled, the DN reverts to Partially Billed.
    /// </summary>
    [Fact]
    public void DeliveryNote_MultiItem_UnbilledItemReturned_Reaches100PercentAndRevertsOnCancel()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Item 1", 1m, 100m, 0m);
        dn.AddItem(Guid.NewGuid(), "Item 2", 1m, 100m, 0m);
        dn.Submit();

        // Initially submitted: To Bill
        dn.BillingStatus.ShouldBe("To Bill");
        dn.PerBilled.ShouldBe(0m);

        // Item 1 billed
        dn.Items[0].BilledQty = 1m;
        dn.BillingStatus.ShouldBe("Partially Billed");
        dn.PerBilled.ShouldBe(0m); // Min(100%, 0%) = 0%

        // Item 2 returned
        dn.Items[1].ReturnedQty = 1m;
        dn.UpdateBillingStatus();

        // Item 1 is fully billed (1/1), Item 2 is fully returned (net 0) -> Completed
        dn.PerBilled.ShouldBe(100m);
        dn.BillingStatus.ShouldBe("Completed");
        dn.PerReturned.ShouldBe(50m);

        // Cancel return -> ReturnedQty reverts to 0
        dn.Items[1].ReturnedQty = 0m;
        dn.UpdateBillingStatus();

        dn.PerBilled.ShouldBe(0m);
        dn.BillingStatus.ShouldBe("Partially Billed");
        dn.PerReturned.ShouldBe(0m);
    }

    /// <summary>
    /// Verifies PerReturned property tracks returned percentage correctly.
    /// </summary>
    [Fact]
    public void DeliveryNote_PerReturned_CalculatesAccurately()
    {
        var dn = CreateDeliveryNote();
        dn.AddItem(Guid.NewGuid(), "Item X", 10m, 50m, 0m);
        dn.AddItem(Guid.NewGuid(), "Item Y", 10m, 50m, 0m);
        dn.Submit();

        dn.PerReturned.ShouldBe(0m);

        dn.Items[0].ReturnedQty = 5m;
        dn.PerReturned.ShouldBe(25m); // 5 of 20 = 25%

        dn.Items[1].ReturnedQty = 5m;
        dn.PerReturned.ShouldBe(50m); // 10 of 20 = 50%

        dn.Items[0].ReturnedQty = 10m;
        dn.Items[1].ReturnedQty = 10m;
        dn.PerReturned.ShouldBe(100m); // 20 of 20 = 100%
    }

    /// <summary>
    /// A return document itself (IsReturn = true) with 0 billed has status "Return".
    /// </summary>
    [Fact]
    public void DeliveryNote_ReturnDocument_HasReturnStatus()
    {
        var dn = CreateDeliveryNote("RET-2026-001", isReturn: true);
        dn.AddItem(Guid.NewGuid(), "Item Return", -2m, 100m, 0m);
        dn.Submit();

        dn.BillingStatus.ShouldBe("Return");
    }
}
