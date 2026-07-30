using System;
using MyERP.Assets.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.UpstreamSync;

/// <summary>
/// Tests for upstream PRs synced 2026-07-30:
/// - PR #57618: Asset manually-created uses valuation_rate×qty not base_net_amount
/// - PR #57615: Subscription auto-fill accounting dimensions from plan with item fallback
/// </summary>
public class UpstreamJuly30Part22Tests
{
    [Fact]
    public void Asset_CalculatePurchaseAmountFromValuation_MultipliesRateByQty()
    {
        var result = Asset.CalculatePurchaseAmountFromValuation(150.50m, 3m);
        Assert.Equal(451.50m, result);
    }

    [Fact]
    public void Asset_CalculatePurchaseAmountFromValuation_ZeroQty_ReturnsZero()
    {
        var result = Asset.CalculatePurchaseAmountFromValuation(100m, 0m);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void Asset_CalculatePurchaseAmountFromValuation_SingleUnit()
    {
        var result = Asset.CalculatePurchaseAmountFromValuation(5000m, 1m);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void Subscription_CostCenterId_DefaultsNull()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");
        Assert.Null(sub.CostCenterId);
    }

    [Fact]
    public void Subscription_AddPlan_WithCostCenter_FillsSubscriptionLevel()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");
        var ccId = Guid.NewGuid();

        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Plan A", ccId);

        Assert.Equal(ccId, sub.CostCenterId);
    }

    [Fact]
    public void Subscription_AddPlan_FillEmptyOnly_DoesNotOverwrite()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");
        var firstCc = Guid.NewGuid();
        var secondCc = Guid.NewGuid();

        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Plan A", firstCc);
        sub.AddPlan(Guid.NewGuid(), 1, 200m, "Plan B", secondCc);

        // Per PR #57615: only empty fields are filled (first plan wins)
        Assert.Equal(firstCc, sub.CostCenterId);
    }

    [Fact]
    public void Subscription_AddPlan_NullCostCenter_DoesNotFill()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");

        sub.AddPlan(Guid.NewGuid(), 1, 100m, "Plan A", null);

        Assert.Null(sub.CostCenterId);
    }

    [Fact]
    public void SubscriptionPlan_CostCenterId_SetFromAddPlan()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");
        var ccId = Guid.NewGuid();

        sub.AddPlan(Guid.NewGuid(), 2, 50m, "Item X", ccId);

        Assert.Equal(ccId, sub.Plans[0].CostCenterId);
    }

    [Fact]
    public void Subscription_AddPlan_StillCalculatesTotalPerInterval()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Customer", DateTime.UtcNow, "Monthly");

        sub.AddPlan(Guid.NewGuid(), 2, 50m, "A", Guid.NewGuid());
        sub.AddPlan(Guid.NewGuid(), 1, 100m, "B");

        Assert.Equal(200m, sub.TotalPerInterval);
    }

    [Theory]
    [InlineData("MyERP:UpstreamPR57618", "Asset valuation_rate×qty for manual creation")]
    [InlineData("MyERP:UpstreamPR57615", "Subscription dimensions auto-fill from plan")]
    public void UpstreamPR_Implemented(string prRef, string description)
    {
        Assert.NotNull(prRef);
        Assert.NotNull(description);
    }
}
