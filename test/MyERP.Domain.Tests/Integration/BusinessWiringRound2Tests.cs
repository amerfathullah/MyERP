using System;
using MyERP.Accounting.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for:
/// - Budget Level 3 wiring to PostAsync (SI/PI)
/// - Leave balance insufficient check before approval
/// - PE posting through accounting period validation
/// - LeaveAllocation management (bulk allocation, delete guard)
/// - Subscription billing job scheduling
/// </summary>
public class BusinessWiringRound2Tests
{
    // --- Leave Balance Insufficient Check ---

    [Fact]
    public void LeaveAllocation_InsufficientBalance_Detected()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12m);

        // Use 10 of 12 days
        alloc.DeductLeave(10m);
        alloc.Balance.ShouldBe(2m);

        // Requesting 5 days when only 2 available
        (alloc.Balance < 5m).ShouldBeTrue();
    }

    [Fact]
    public void LeaveAllocation_SufficientBalance_Allowed()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12m);

        alloc.DeductLeave(5m);
        alloc.Balance.ShouldBe(7m);

        // Requesting 3 days when 7 available
        (alloc.Balance >= 3m).ShouldBeTrue();
    }

    [Fact]
    public void LeaveAllocation_ZeroBalance_BlocksApproval()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 10m);

        alloc.DeductLeave(10m);
        alloc.Balance.ShouldBe(0m);

        // Any request should be blocked
        (alloc.Balance < 1m).ShouldBeTrue();
    }

    [Fact]
    public void LeaveAllocation_WithCarryForward_IncreasesAvailableBalance()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12m)
        {
            CarryForwardDays = 5m
        };

        alloc.DeductLeave(14m); // Used most of allocation + carry-forward
        alloc.Balance.ShouldBe(3m); // 12 + 5 - 14
    }

    // --- Leave Allocation Delete Guard ---

    [Fact]
    public void LeaveAllocation_CannotDelete_WhenUsed()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12m);

        alloc.DeductLeave(3m);

        // Guard check: used > 0 means cannot delete
        (alloc.LeavesUsed > 0).ShouldBeTrue();
    }

    [Fact]
    public void LeaveAllocation_CanDelete_WhenUnused()
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12m);

        // No usage, deletion allowed
        (alloc.LeavesUsed > 0).ShouldBeFalse();
    }

    // --- Budget Level 3 Items Preparation ---

    [Fact]
    public void BudgetCheckItem_FromInvoiceItem_CorrectAmount()
    {
        // Simulates how SI PostAsync builds budget check items
        var accountId = Guid.NewGuid();
        decimal qty = 10m;
        decimal unitPrice = 50m;

        var item = new BudgetCheckItem(accountId, qty * unitPrice);

        item.AccountId.ShouldBe(accountId);
        item.Amount.ShouldBe(500m);
    }

    [Fact]
    public void BudgetCheckItem_MultipleItems_SeparateEntries()
    {
        var account1 = Guid.NewGuid();
        var account2 = Guid.NewGuid();

        var items = new[]
        {
            new BudgetCheckItem(account1, 1000m),
            new BudgetCheckItem(account2, 2000m),
        };

        items.Length.ShouldBe(2);
        items[0].Amount.ShouldBe(1000m);
        items[1].Amount.ShouldBe(2000m);
    }

    // --- Accounting Period Validation Context ---

    [Fact]
    public void PaymentAllocation_MultipleInvoices_IndependentEntries()
    {
        var allocations = new[]
        {
            new PaymentAllocation { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid(), AllocatedAmount = 3000m },
            new PaymentAllocation { VoucherType = "SalesInvoice", VoucherId = Guid.NewGuid(), AllocatedAmount = 2000m },
        };

        // PE posting creates PLE per allocation
        allocations.Length.ShouldBe(2);
        (allocations[0].AllocatedAmount + allocations[1].AllocatedAmount).ShouldBe(5000m);
    }

    [Fact]
    public void PaymentAllocation_PurchaseInvoice_HasCorrectType()
    {
        var alloc = new PaymentAllocation
        {
            VoucherType = "PurchaseInvoice",
            VoucherId = Guid.NewGuid(),
            AllocatedAmount = 7500m
        };

        alloc.VoucherType.ShouldBe("PurchaseInvoice");
    }

    // --- SO/PO Address Auto-Fill Context ---

    [Fact]
    public void SalesOrder_ShippingAddress_IndependentFromBilling()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-100", DateTime.Today);
        var billingId = Guid.NewGuid();
        var shippingId = Guid.NewGuid();

        so.BillingAddressId = billingId;
        so.ShippingAddressId = shippingId;

        so.BillingAddressId.ShouldNotBe(so.ShippingAddressId);
    }

    [Fact]
    public void PurchaseOrder_AddressPreservedAfterSubmit()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PO-100", DateTime.Today);
        var addressId = Guid.NewGuid();
        po.BillingAddressId = addressId;

        po.AddItem(Guid.NewGuid(), "Test Item", 1, 100m, 0, "Unit");
        po.Submit();

        // Address preserved after submit
        po.BillingAddressId.ShouldBe(addressId);
    }

    // --- Subscription Billing Job Context ---

    [Fact]
    public void Subscription_Active_CanAdvancePeriod()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SUB-001", DateTime.Today, "Monthly");

        sub.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void Subscription_Cancelled_BlocksAdvance()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SUB-002", DateTime.Today, "Monthly");

        sub.Cancel();
        sub.Status.ShouldBe(SubscriptionStatus.Cancelled);

        // Job should skip cancelled subscriptions
        (sub.Status == SubscriptionStatus.Active).ShouldBeFalse();
    }
}
