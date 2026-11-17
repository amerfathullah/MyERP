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

    [Fact]
    public void Matches_FloatNoiseAtSlabBoundary_StripsNoiseBeforeCompare()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Slab Boundary Rule",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            MinQty = 10m,
            MaxQty = 10m,
            MinAmount = 100m,
            MaxAmount = 100m,
        };

        rule.Matches(itemId, null, 9.999999999999996m, 99.99999999999996m, DateTime.UtcNow).ShouldBeTrue();
        rule.Matches(itemId, null, 10.000000000000004m, 100.00000000000004m, DateTime.UtcNow).ShouldBeTrue();
        rule.Matches(itemId, null, 9.99m, 100m, DateTime.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void Matches_WithConversionFactor_ScalesQtyAndPreservesTransactionUomAmount()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(Guid.NewGuid(), "Box Slab Rule",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            MinQty = 2m,
            MaxQty = 5m,
            MinAmount = 50m,
        };

        // 1 Box = 12 Nos -> min 24, max 60 Nos
        rule.Matches(itemId, null, 23m, 600m, DateTime.UtcNow, conversionFactor: 12m).ShouldBeFalse();
        rule.Matches(itemId, null, 24m, 600m, DateTime.UtcNow, conversionFactor: 12m).ShouldBeTrue();
        rule.Matches(itemId, null, 60m, 600m, DateTime.UtcNow, conversionFactor: 12m).ShouldBeTrue();
        rule.Matches(itemId, null, 61m, 600m, DateTime.UtcNow, conversionFactor: 12m).ShouldBeFalse();

        // Same transaction UOM: MinQty still scales by factor, MinAmount stays in transaction UOM
        rule.Matches(itemId, null, 24m, 50m, DateTime.UtcNow, conversionFactor: 12m, sameTransactionUom: true).ShouldBeTrue();
        rule.Matches(itemId, null, 24m, 49m, DateTime.UtcNow, conversionFactor: 12m, sameTransactionUom: true).ShouldBeFalse();
    }
}
