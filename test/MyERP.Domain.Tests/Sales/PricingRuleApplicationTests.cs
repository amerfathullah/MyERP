using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Sales;

public class PricingRuleApplicationTests
{
    private static PricingRule CreateRule(
        PricingRuleApplyOn applyOn, PricingRuleType ruleType,
        Guid? applyOnId = null, int priority = 1)
    {
        var rule = new PricingRule(Guid.NewGuid(), "Test Rule", applyOn, ruleType);
        rule.ApplyOnId = applyOnId;
        rule.Priority = priority;
        return rule;
    }

    [Fact]
    public void PricingRuleContext_Amount_Computed()
    {
        var ctx = new PricingRuleContext { ItemId = Guid.NewGuid(), Qty = 5, Rate = 100 };
        ctx.Amount.ShouldBe(500m);
    }

    [Fact]
    public void DiscountPercentage_ReducesRate()
    {
        var ctx = new PricingRuleContext { ItemId = Guid.NewGuid(), Qty = 10, Rate = 200 };
        ctx.DiscountPercentage = 10;
        ctx.DiscountedRate = ctx.Rate * (1 - ctx.DiscountPercentage / 100m);
        ctx.DiscountedRate.ShouldBe(180m);
    }

    [Fact]
    public void DiscountAmount_ReducesRate()
    {
        var ctx = new PricingRuleContext { ItemId = Guid.NewGuid(), Qty = 10, Rate = 200 };
        ctx.DiscountAmount = 25;
        ctx.DiscountedRate = Math.Max(0, ctx.Rate - ctx.DiscountAmount);
        ctx.DiscountedRate.ShouldBe(175m);
    }

    [Fact]
    public void PricingRule_Matches_ByItemCode()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, itemId);
        rule.Matches(itemId, null, 10, 100, DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void PricingRule_NoMatch_DifferentItem()
    {
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, Guid.NewGuid());
        rule.Matches(Guid.NewGuid(), null, 10, 100, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_Disabled_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, itemId);
        rule.IsDisabled = true;
        rule.Matches(itemId, null, 10, 100, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_Expired_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, itemId);
        rule.ValidUpto = DateTime.Today.AddDays(-1);
        rule.Matches(itemId, null, 10, 100, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_NotYetValid_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, itemId);
        rule.ValidFrom = DateTime.Today.AddDays(5);
        rule.Matches(itemId, null, 10, 100, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_BelowMinQty_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, itemId);
        rule.MinQty = 50;
        rule.Matches(itemId, null, 10, 100, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_AboveMaxQty_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemCode, PricingRuleType.Discount, itemId);
        rule.MaxQty = 5;
        rule.Matches(itemId, null, 10, 100, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_Matches_ByItemGroup()
    {
        var groupId = Guid.NewGuid();
        var rule = CreateRule(PricingRuleApplyOn.ItemGroup, PricingRuleType.Rate, groupId);
        rule.Matches(Guid.NewGuid(), groupId, 10, 100, DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void PricingRule_TransactionTotal_MatchesAll()
    {
        var rule = CreateRule(PricingRuleApplyOn.TransactionTotal, PricingRuleType.Discount);
        rule.MinAmount = 1000;
        rule.Matches(Guid.NewGuid(), null, 1, 1500, DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void PricingRule_TransactionTotal_BelowMin_NoMatch()
    {
        var rule = CreateRule(PricingRuleApplyOn.TransactionTotal, PricingRuleType.Discount);
        rule.MinAmount = 1000;
        rule.Matches(Guid.NewGuid(), null, 1, 500, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void AppliedPricingRule_FreeItem_HasDetails()
    {
        var freeItemId = Guid.NewGuid();
        var result = new AppliedPricingRule
        {
            RuleId = Guid.NewGuid(),
            RuleTitle = "Buy 5 Get 1 Free",
            RuleType = PricingRuleType.FreeItem,
            FreeItemId = freeItemId,
            FreeItemQty = 1,
        };
        result.FreeItemId.ShouldBe(freeItemId);
        result.FreeItemQty.ShouldBe(1);
    }

    [Theory]
    [InlineData("Sales Invoice", "Selling")]
    [InlineData("Sales Order", "Selling")]
    [InlineData("Quotation", "Selling")]
    [InlineData("Delivery Note", "Selling")]
    [InlineData("Purchase Order", "Buying")]
    [InlineData("Purchase Invoice", "Buying")]
    [InlineData("Purchase Receipt", "Buying")]
    [InlineData("Supplier Quotation", "Buying")]
    public void PricingRule_ResolveTransactionType_FromDoctype(string doctype, string expected)
    {
        var resolved = PricingRuleApplicationService.ResolveTransactionType(null, doctype);
        resolved.ShouldBe(expected);
    }

    [Fact]
    public void PricingRule_ResolveTransactionType_ItemOverrideTakesPrecedence()
    {
        var resolved = PricingRuleApplicationService.ResolveTransactionType("Buying", "Sales Invoice");
        resolved.ShouldBe("Buying");
    }

    [Fact]
    public async Task PricingRule_ApplyToItems_EvaluatesItemWiseTransactionType()
    {
        var ruleRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<PricingRule, Guid>>();
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Item, Guid>>();
        var ceilingService = new DiscountCeilingValidationService(itemRepo);

        var itemId = Guid.NewGuid();
        var sellingRule = new PricingRule(Guid.NewGuid(), "Selling Rule", PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            ApplicableFor = "Selling",
            DiscountPercentage = 15m,
            Priority = 1
        };
        var buyingRule = new PricingRule(Guid.NewGuid(), "Buying Rule", PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            ApplicableFor = "Buying",
            DiscountPercentage = 25m,
            Priority = 1
        };

        var rules = new List<PricingRule> { sellingRule, buyingRule }.AsQueryable();
        ruleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(rules));

        var service = new PricingRuleApplicationService(ruleRepo, ceilingService);

        // Per ERPNext PR #59406: Item 1 has Doctype = "Sales Invoice", Item 2 has Doctype = "Purchase Order"
        var items = new List<PricingRuleContext>
        {
            new PricingRuleContext { ItemId = itemId, Qty = 1, Rate = 100m, Doctype = "Sales Invoice" },
            new PricingRuleContext { ItemId = itemId, Qty = 1, Rate = 100m, Doctype = "Purchase Order" },
        };

        var applied = await service.ApplyToItemsAsync(items, DateTime.Today);

        applied.Count.ShouldBe(2);
        items[0].DiscountPercentage.ShouldBe(15m);
        items[0].DiscountedRate.ShouldBe(85m);

        items[1].DiscountPercentage.ShouldBe(25m);
        items[1].DiscountedRate.ShouldBe(75m);
    }
}
