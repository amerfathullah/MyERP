using System;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class SubscriptionTests
{
    private static Subscription CreateSub() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            new DateTime(2026, 1, 1), "Monthly");

    [Fact]
    public void Create_SetsDefaults()
    {
        var sub = CreateSub();
        sub.Status.ShouldBe(SubscriptionStatus.Active);
        sub.BillingInterval.ShouldBe("Monthly");
        sub.Plans.ShouldBeEmpty();
    }

    [Fact]
    public void AddPlan_CalculatesTotal()
    {
        var sub = CreateSub();
        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Monthly Plan");
        sub.AddPlan(Guid.NewGuid(), 2, 50m, "Add-on");
        sub.TotalPerInterval.ShouldBe(200m); // 100 + 100
    }

    [Fact]
    public void AdvancePeriod_Monthly()
    {
        var sub = CreateSub();
        sub.AdvancePeriod();
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 1, 1));
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 1, 31));
    }

    [Fact]
    public void AdvancePeriod_Quarterly()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Quarterly");
        sub.AdvancePeriod();
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 3, 31));
    }

    [Fact]
    public void Cancel_Active_Succeeds()
    {
        var sub = CreateSub();
        sub.Cancel();
        sub.Status.ShouldBe(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var sub = CreateSub();
        sub.Cancel();
        Should.Throw<BusinessException>(() => sub.Cancel());
    }

    [Fact]
    public void Pause_Active_Succeeds()
    {
        var sub = CreateSub();
        sub.Pause();
        sub.Status.ShouldBe(SubscriptionStatus.PastDueDate);
    }

    [Fact]
    public void Reactivate_PastDue_Succeeds()
    {
        var sub = CreateSub();
        sub.Pause();
        sub.Reactivate();
        sub.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public void AdvancePeriod_Yearly()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Yearly");
        sub.AdvancePeriod();
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 1, 1));
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 12, 31));
    }

    [Fact]
    public void AdvancePeriod_HalfYearly()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Half-Yearly");
        sub.AdvancePeriod();
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 6, 30));
    }

    [Fact]
    public void AdvancePeriod_TwiceAdvances_ToSecondPeriod()
    {
        var sub = CreateSub();
        sub.AdvancePeriod(); // Jan 1 - Jan 31
        sub.AdvancePeriod(); // Feb 1 - Feb 28
        sub.CurrentInvoiceStart.ShouldBe(new DateTime(2026, 2, 1));
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void Reactivate_FromCancelled_Throws()
    {
        var sub = CreateSub();
        sub.Cancel();
        Should.Throw<BusinessException>(() => sub.Reactivate());
    }

    [Fact]
    public void Pause_FromCancelled_Throws()
    {
        var sub = CreateSub();
        sub.Cancel();
        Should.Throw<BusinessException>(() => sub.Pause());
    }

    [Fact]
    public void Plans_AreReadOnly()
    {
        var sub = CreateSub();
        sub.AddPlan(Guid.NewGuid(), 3, 25m, "Widget");
        sub.Plans.Count.ShouldBe(1);
        sub.Plans[0].Amount.ShouldBe(75m); // 3 × 25
    }

    [Fact]
    public void BillingIntervalCount_Multiplies()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", new DateTime(2026, 1, 1), "Monthly")
        {
            BillingIntervalCount = 2
        };
        sub.AdvancePeriod();
        // 2-month billing intervals
        sub.CurrentInvoiceEnd.ShouldBe(new DateTime(2026, 2, 28));
    }
}

public class DunningTests
{
    private static Dunning CreateDunning() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 1);

    [Fact]
    public void Create_SetsDefaults()
    {
        var d = CreateDunning();
        d.Status.ShouldBe(Core.DocumentStatus.Draft);
        d.DunningLevel.ShouldBe(1);
        d.TotalOutstanding.ShouldBe(0);
    }

    [Fact]
    public void AddOverduePayment_UpdatesTotal()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        d.AddOverduePayment(Guid.NewGuid(), 500m, DateTime.UtcNow.AddDays(-15), 15);
        d.TotalOutstanding.ShouldBe(1500m);
        d.OverduePayments.Count.ShouldBe(2);
    }

    [Fact]
    public void GrandTotal_IncludesFeeAndInterest()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        d.DunningFee = 50m;
        d.InterestAmount = 25m;
        d.GrandTotal.ShouldBe(1075m);
    }

    [Fact]
    public void Submit_WithPayments_Succeeds()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        d.Submit();
        d.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_WithoutPayments_Throws()
    {
        var d = CreateDunning();
        Should.Throw<BusinessException>(() => d.Submit());
    }

    [Fact]
    public void Resolve_Submitted_Succeeds()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        d.Submit();
        d.Resolve();
        d.Status.ShouldBe(Core.DocumentStatus.Posted);
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        d.Submit();
        d.Cancel();
        d.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDraft_Throws()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        Should.Throw<BusinessException>(() => d.Cancel());
    }

    [Fact]
    public void Cancel_FromResolved_Throws()
    {
        var d = CreateDunning();
        d.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);
        d.Submit();
        d.Resolve();
        Should.Throw<BusinessException>(() => d.Cancel());
    }
}
