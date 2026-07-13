using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

public class PricingRuleTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var rule = new PricingRule(Guid.NewGuid(), "10% Off Widgets",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount);
        rule.Title.ShouldBe("10% Off Widgets");
        rule.ApplyOn.ShouldBe(PricingRuleApplyOn.ItemCode);
        rule.RuleType.ShouldBe(PricingRuleType.Discount);
        rule.IsDisabled.ShouldBeFalse();
    }

    [Fact]
    public void Matches_ItemCode_Correct()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Item Discount",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
        };
        rule.Matches(itemId, null, 5, 100, DateTime.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void Matches_WrongItem_False()
    {
        var rule = new PricingRule(Guid.NewGuid(), "Item Discount",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = Guid.NewGuid(),
        };
        rule.Matches(Guid.NewGuid(), null, 5, 100, DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void Matches_BelowMinQty_False()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Bulk Discount",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            MinQty = 10,
        };
        rule.Matches(itemId, null, 5, 100, DateTime.UtcNow).ShouldBeFalse();
        rule.Matches(itemId, null, 15, 100, DateTime.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void Matches_Disabled_False()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Disabled",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            IsDisabled = true,
        };
        rule.Matches(itemId, null, 5, 100, DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void Matches_ExpiredDate_False()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Expired",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            ValidFrom = new DateTime(2025, 1, 1),
            ValidUpto = new DateTime(2025, 12, 31),
        };
        rule.Matches(itemId, null, 5, 100, new DateTime(2026, 6, 1)).ShouldBeFalse();
    }

    [Fact]
    public void Matches_TransactionTotal_Always()
    {
        var rule = new PricingRule(Guid.NewGuid(), "Order Discount",
            PricingRuleApplyOn.TransactionTotal, PricingRuleType.Discount)
        {
            MinAmount = 1000,
        };
        rule.Matches(null, null, 0, 500, DateTime.UtcNow).ShouldBeFalse();
        rule.Matches(null, null, 0, 1500, DateTime.UtcNow).ShouldBeTrue();
    }
}
