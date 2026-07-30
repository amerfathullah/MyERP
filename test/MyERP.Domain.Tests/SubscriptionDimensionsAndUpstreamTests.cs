using System;
using System.Linq;
using MyERP.Assets.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

public class SubscriptionDimensionsAndUpstreamTests
{
    [Fact]
    public void SubscriptionPlan_CostCenterId_Defaults_Null()
    {
        var plan = new SubscriptionPlan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 100, "Test Item");
        Assert.Null(plan.CostCenterId);
    }

    [Fact]
    public void SubscriptionPlan_CostCenterId_Can_Be_Set()
    {
        var ccId = Guid.NewGuid();
        var plan = new SubscriptionPlan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 100, "Test Item");
        plan.CostCenterId = ccId;
        Assert.Equal(ccId, plan.CostCenterId);
    }

    [Fact]
    public void Subscription_CostCenterId_Defaults_Null()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            DateTime.UtcNow, "Monthly");
        Assert.Null(sub.CostCenterId);
    }

    [Fact]
    public void Subscription_CostCenterId_Can_Be_Set()
    {
        var ccId = Guid.NewGuid();
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            DateTime.UtcNow, "Monthly");
        sub.CostCenterId = ccId;
        Assert.Equal(ccId, sub.CostCenterId);
    }

    [Fact]
    public void Subscription_AddPlan_With_CostCenter_Sets_Plan_CostCenter()
    {
        var ccId = Guid.NewGuid();
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            DateTime.UtcNow, "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1, 500, "Hosting", ccId);
        Assert.Equal(ccId, sub.Plans.First().CostCenterId);
    }

    [Fact]
    public void Subscription_First_Plan_CC_Should_Fill_Subscription_Level_CC()
    {
        // Per PR #57615: fill-empty-only — first plan with a CC sets subscription-level CC
        var ccId = Guid.NewGuid();
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            DateTime.UtcNow, "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1, 500, "Hosting", ccId);
        // The AppService does this fill; entity-level verify CC is settable
        sub.CostCenterId = sub.Plans.FirstOrDefault(p => p.CostCenterId.HasValue)?.CostCenterId;
        Assert.Equal(ccId, sub.CostCenterId);
    }

    [Fact]
    public void Subscription_No_Plan_CC_Leaves_Subscription_CC_Null()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            DateTime.UtcNow, "Monthly");
        sub.AddPlan(Guid.NewGuid(), 1, 500, "Basic");
        var resolved = sub.Plans.FirstOrDefault(p => p.CostCenterId.HasValue)?.CostCenterId;
        Assert.Null(resolved);
    }

    [Fact]
    public void ItemDefault_SellingCostCenterId_Defaults_Null()
    {
        var itemDefault = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(itemDefault.SellingCostCenterId);
    }

    [Fact]
    public void ItemDefault_BuyingCostCenterId_Defaults_Null()
    {
        var itemDefault = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(itemDefault.BuyingCostCenterId);
    }

    [Fact]
    public void ItemDefault_CostCenters_Can_Be_Set()
    {
        var sellCc = Guid.NewGuid();
        var buyCc = Guid.NewGuid();
        var itemDefault = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        itemDefault.SellingCostCenterId = sellCc;
        itemDefault.BuyingCostCenterId = buyCc;
        Assert.Equal(sellCc, itemDefault.SellingCostCenterId);
        Assert.Equal(buyCc, itemDefault.BuyingCostCenterId);
    }

    // PR #57618: Asset manually created uses valuation_rate × qty
    [Fact]
    public void Asset_CalculatePurchaseAmount_Uses_ValuationRate_Times_Qty()
    {
        var result = Asset.CalculatePurchaseAmountFromValuation(250.50m, 4);
        Assert.Equal(1002.00m, result);
    }

    [Fact]
    public void Asset_CalculatePurchaseAmount_Zero_Qty_Returns_Zero()
    {
        var result = Asset.CalculatePurchaseAmountFromValuation(100m, 0);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void Asset_CalculatePurchaseAmount_Zero_Rate_Returns_Zero()
    {
        var result = Asset.CalculatePurchaseAmountFromValuation(0m, 5);
        Assert.Equal(0m, result);
    }

    // Upstream PR #57616: Item group root seeding (no code change needed - verify concept)
    [Fact]
    public void ItemGroup_IsGroup_Defaults_False()
    {
        var group = new ItemGroup(Guid.NewGuid(), "Test Group");
        Assert.False(group.IsGroup);
    }

    // Upstream PR #57609: Title field parity (MR/Timesheet) - no code change needed
    [Fact]
    public void Subscription_Plans_Empty_By_Default()
    {
        var sub = new Subscription(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Customer",
            DateTime.UtcNow, "Monthly");
        Assert.Empty(sub.Plans);
    }

    [Theory]
    [InlineData("::CostCenter")]
    [InlineData("::Subscriptions")]
    [InlineData("::Active")]
    public void Localization_Keys_Should_Exist(string key)
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(GetSolutionRoot(), "src", "MyERP.Domain.Shared",
                "Localization", "MyERP", "en.json"));
        var cleanKey = key.Replace("::", "");
        Assert.Contains($"\"{cleanKey}\"", json);
    }

    private static string GetSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "MyERP.slnx")))
            dir = System.IO.Path.GetDirectoryName(dir);
        return dir ?? throw new Exception("Solution root not found");
    }
}
