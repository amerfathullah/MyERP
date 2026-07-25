using System;
using System.Linq;
using MyERP.Purchasing.Entities;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for 3-way matching validation (PO ↔ PR ↔ PI).
/// Per ERPNext buying_controller.validate_received_qty:
/// - When pr_required=true, PI cannot bill more than received
/// - Prevents billing fraud (invoicing before goods verification)
/// </summary>
public class ThreeWayMatchingTests
{
    private static PurchaseInvoice CreatePI(Guid poItemId, decimal qty)
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-TEST", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Test Item", qty, 50m, 0m);
        // Set PO item link on the item
        pi.Items.First().PurchaseOrderItemId = poItemId;
        return pi;
    }

    [Fact]
    public void ThreeWayMatch_QtyWithinReceived_Passes()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 10m);

        // Simulate PR received 10 units for this PO item
        Func<Guid, decimal> getReceived = (id) => id == poItemId ? 10m : 0m;

        var ex = Record.Exception(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ThreeWayMatch_QtyExceedsReceived_Throws()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 15m);

        // Only 10 received but billing 15
        Func<Guid, decimal> getReceived = (id) => id == poItemId ? 10m : 0m;

        Should.Throw<Volo.Abp.BusinessException>(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
    }

    [Fact]
    public void ThreeWayMatch_ExactReceived_Passes()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 50m);

        Func<Guid, decimal> getReceived = (id) => 50m;

        var ex = Record.Exception(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ThreeWayMatch_PrNotRequired_Skips()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 100m);

        // Even with zero received, should not throw when pr_required=false
        Func<Guid, decimal> getReceived = (id) => 0m;

        var ex = Record.Exception(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: false));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ThreeWayMatch_ReturnInvoice_Skips()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-RET", DateTime.UtcNow);
        pi.IsReturn = true;
        pi.AddItem(Guid.NewGuid(), "Return Item", -5m, 50m, 0m);
        pi.Items.First().PurchaseOrderItemId = Guid.NewGuid();

        Func<Guid, decimal> getReceived = (id) => 0m;

        // Returns should bypass 3-way matching
        var ex = Record.Exception(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ThreeWayMatch_NoPOLink_Skips()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-DIRECT", DateTime.UtcNow);
        pi.AddItem(Guid.NewGuid(), "Direct Purchase", 20m, 50m, 0m);
        // No PurchaseOrderItemId set — direct PI without PO

        Func<Guid, decimal> getReceived = (id) => 0m;

        // Should pass — no PO link means no 3-way matching needed
        var ex = Record.Exception(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ThreeWayMatch_PartialReceipt_PartialBilling_Passes()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 5m); // Billing only 5

        // 8 received out of 10 ordered
        Func<Guid, decimal> getReceived = (id) => 8m;

        var ex = Record.Exception(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
        ex.ShouldBeNull();
    }

    [Fact]
    public void ThreeWayMatch_ZeroReceived_Blocks()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 1m); // Trying to bill 1 but nothing received

        Func<Guid, decimal> getReceived = (id) => 0m;

        Should.Throw<Volo.Abp.BusinessException>(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
    }

    [Fact]
    public void ThreeWayMatch_ErrorCode_IsCorrect()
    {
        var poItemId = Guid.NewGuid();
        var pi = CreatePI(poItemId, 20m);
        Func<Guid, decimal> getReceived = (id) => 10m;

        var ex = Should.Throw<Volo.Abp.BusinessException>(() =>
            PurchaseInvoiceManager.ValidateThreeWayMatching(pi, getReceived, prRequired: true));
        ex.Code.ShouldBe("MyERP:04015");
    }
}
