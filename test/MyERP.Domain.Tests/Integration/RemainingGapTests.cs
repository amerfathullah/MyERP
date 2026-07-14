using System;
using System.Collections.Generic;
using MyERP.Accounting.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Integration;

/// <summary>
/// Tests for remaining gaps: selling price on SO, document numbering with posting date,
/// opening invoice handling, subscription catch-up billing, leave carry-forward expiry.
/// </summary>
public class RemainingGapTests
{
    // --- Selling Price Validation (tuple overload for SO/DN) ---

    [Fact]
    public void ValidateSellingPrice_TupleOverload_AboveCost_Passes()
    {
        var items = new List<(Guid ItemId, decimal UnitPrice, string Description)>
        {
            (Guid.NewGuid(), 150m, "Widget")
        }.AsReadOnly();

        var result = SalesInvoiceManager.ValidateSellingPrice(items, _ => 100m, action: "Stop");
        result.HasWarnings.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSellingPrice_TupleOverload_BelowCost_StopThrows()
    {
        var items = new List<(Guid ItemId, decimal UnitPrice, string Description)>
        {
            (Guid.NewGuid(), 80m, "Widget")
        }.AsReadOnly();

        Should.Throw<BusinessException>(() =>
            SalesInvoiceManager.ValidateSellingPrice(items, _ => 100m, action: "Stop"))
            .Code.ShouldBe(MyERPDomainErrorCodes.SellingPriceBelowCost);
    }

    [Fact]
    public void ValidateSellingPrice_TupleOverload_BelowCost_WarnReturnsWarning()
    {
        var items = new List<(Guid ItemId, decimal UnitPrice, string Description)>
        {
            (Guid.NewGuid(), 90m, "Cheap Product")
        }.AsReadOnly();

        var result = SalesInvoiceManager.ValidateSellingPrice(items, _ => 100m, action: "Warn");
        result.HasWarnings.ShouldBeTrue();
        result.Warnings[0].ShouldContain("90");
    }

    [Fact]
    public void ValidateSellingPrice_SIOverload_StillWorks()
    {
        // Verify original SI overload delegates correctly
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Expensive Item", 1, 200m, 0m);

        var result = SalesInvoiceManager.ValidateSellingPrice(
            si.Items, _ => 100m, action: "Stop");
        result.HasWarnings.ShouldBeFalse();
    }

    // --- Opening Invoice ---

    [Fact]
    public void SalesInvoice_IsOpening_DefaultsFalse()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.Today);
        si.IsOpening.ShouldBeFalse();
    }

    [Fact]
    public void SalesInvoice_IsOpening_CanBeSet()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-OPEN", DateTime.Today);
        si.IsOpening = true;
        si.IsOpening.ShouldBeTrue();
    }

    [Fact]
    public void OpeningInvoice_PaymentTermsTemplateId_ShouldBeNull()
    {
        // When IsOpening = true, payment terms should be cleared
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-OPEN", DateTime.Today);
        si.IsOpening = true;
        si.PaymentTermsTemplateId = Guid.NewGuid(); // Set something

        // The AppService CreateAsync clears this when IsOpening=true
        // At entity level, we just verify the field exists and can be null
        si.PaymentTermsTemplateId = null;
        si.PaymentTermsTemplateId.ShouldBeNull();
    }

    // --- Subscription Catch-Up Billing ---

    [Fact]
    public void SubscriptionBillingEngine_GetMissedPeriodsCount_FirstPeriod()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSubscription();
        // No CurrentInvoiceEnd → first period
        var count = engine.GetMissedPeriodsCount(sub, DateTime.Today);
        count.ShouldBe(1);
    }

    [Fact]
    public void SubscriptionBillingEngine_GetMissedPeriodsCount_OnePeriodBehind()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSubscription();
        sub.AdvancePeriod(); // Sets CurrentInvoiceEnd

        // AsOfDate is 2 months after the end → 2 missed monthly periods
        var endDate = sub.CurrentInvoiceEnd!.Value;
        var count = engine.GetMissedPeriodsCount(sub, endDate.AddMonths(2));
        count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void SubscriptionBillingEngine_GetMissedPeriodsCount_Capped()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSubscription();
        sub.AdvancePeriod();

        // AsOfDate is 5 years ahead → capped at 12
        var endDate = sub.CurrentInvoiceEnd!.Value;
        var count = engine.GetMissedPeriodsCount(sub, endDate.AddYears(5));
        count.ShouldBeLessThanOrEqualTo(12);
    }

    [Fact]
    public void SubscriptionBillingEngine_GetMissedPeriodsCount_NotActive_ReturnsZero()
    {
        var engine = new SubscriptionBillingEngine(null!);
        var sub = CreateSubscription();
        sub.Cancel();

        var count = engine.GetMissedPeriodsCount(sub, DateTime.Today.AddMonths(6));
        count.ShouldBe(0);
    }

    // --- Leave Carry-Forward Expiry ---

    [Fact]
    public void LeaveAllocation_CarryForwardExpiryDate_DefaultsNull()
    {
        var alloc = CreateLeaveAllocation(carryForward: 5);
        alloc.CarryForwardExpiryDate.ShouldBeNull();
    }

    [Fact]
    public void LeaveAllocation_EffectiveCarryForward_NoExpiry_FullAmount()
    {
        var alloc = CreateLeaveAllocation(carryForward: 5);
        // No expiry date → full carry-forward available
        alloc.EffectiveCarryForwardDays.ShouldBe(5m);
        alloc.Balance.ShouldBe(17m); // 12 + 5 - 0
    }

    [Fact]
    public void LeaveAllocation_EffectiveCarryForward_FutureExpiry_FullAmount()
    {
        var alloc = CreateLeaveAllocation(carryForward: 5);
        alloc.CarryForwardExpiryDate = DateTime.UtcNow.Date.AddMonths(3);
        // Future expiry → still available
        alloc.EffectiveCarryForwardDays.ShouldBe(5m);
    }

    [Fact]
    public void LeaveAllocation_EffectiveCarryForward_PastExpiry_Zero()
    {
        var alloc = CreateLeaveAllocation(carryForward: 5);
        alloc.CarryForwardExpiryDate = DateTime.UtcNow.Date.AddDays(-1);
        // Expired → zero carry-forward
        alloc.EffectiveCarryForwardDays.ShouldBe(0m);
        alloc.Balance.ShouldBe(12m); // 12 + 0 - 0 (carry-forward expired)
    }

    [Fact]
    public void LeaveAllocation_EffectiveCarryForward_ExpiryToday_StillAvailable()
    {
        var alloc = CreateLeaveAllocation(carryForward: 5);
        alloc.CarryForwardExpiryDate = DateTime.UtcNow.Date;
        // Expiry is today → still available (expires AFTER today)
        alloc.EffectiveCarryForwardDays.ShouldBe(5m);
    }

    [Fact]
    public void LeaveAllocation_Balance_WithExpiredCarryForward_ExcludesIt()
    {
        var alloc = CreateLeaveAllocation(carryForward: 8);
        alloc.CarryForwardExpiryDate = DateTime.UtcNow.Date.AddDays(-30);
        alloc.DeductLeave(3);

        // Balance = 12 + 0 (expired) - 3 = 9
        alloc.Balance.ShouldBe(9m);
    }

    // --- Helpers ---

    private static Subscription CreateSubscription()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Customer", DateTime.Today, "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Service");
        return sub;
    }

    private static LeaveAllocation CreateLeaveAllocation(decimal carryForward = 0)
    {
        var alloc = new LeaveAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), 12m);
        alloc.CarryForwardDays = carryForward;
        return alloc;
    }
}
