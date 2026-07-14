using System;
using System.Linq;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class ShippingRuleTests
{
    private readonly Guid _accountId = Guid.NewGuid();

    private ShippingRule CreateRule(
        ShippingCalculationMode mode = ShippingCalculationMode.BasedOnNetTotal,
        ShippingRuleType type = ShippingRuleType.Selling)
    {
        return new ShippingRule(Guid.NewGuid(), "Standard Shipping", type, mode, _accountId);
    }

    [Fact]
    public void Rule_DefaultState()
    {
        var rule = CreateRule();
        Assert.True(rule.IsEnabled);
        Assert.Equal(ShippingCalculationMode.BasedOnNetTotal, rule.CalculationMode);
        Assert.Equal(ShippingRuleType.Selling, rule.RuleType);
        Assert.Empty(rule.Conditions);
        Assert.Empty(rule.Countries);
        Assert.Equal(0m, rule.FixedAmount);
    }

    [Fact]
    public void Rule_FixedMode_ReturnsFixedAmount()
    {
        var rule = CreateRule(ShippingCalculationMode.Fixed);
        rule.FixedAmount = 15.00m;

        Assert.Equal(15.00m, rule.Calculate(100m));
        Assert.Equal(15.00m, rule.Calculate(10_000m));
        Assert.Equal(15.00m, rule.Calculate(0m));
    }

    [Fact]
    public void Rule_NetTotal_MatchesCondition()
    {
        var rule = CreateRule();
        rule.AddCondition(0, 100m, 20m);     // 0-100: RM 20
        rule.AddCondition(100.01m, 500m, 15m); // 100.01-500: RM 15
        rule.AddCondition(500.01m, 1000m, 10m); // 500.01-1000: RM 10

        Assert.Equal(20m, rule.Calculate(50m));
        Assert.Equal(15m, rule.Calculate(250m));
        Assert.Equal(10m, rule.Calculate(750m));
    }

    [Fact]
    public void Rule_NetTotal_BoundaryValues()
    {
        var rule = CreateRule();
        rule.AddCondition(0, 100m, 20m);
        rule.AddCondition(101m, 500m, 15m);

        Assert.Equal(20m, rule.Calculate(100m));  // At upper boundary
        Assert.Equal(15m, rule.Calculate(101m));  // At lower boundary of next tier
    }

    [Fact]
    public void Rule_NetTotal_CatchAll_ZeroToValue()
    {
        var rule = CreateRule();
        rule.AddCondition(0, 100m, 20m);
        rule.AddCondition(101m, 0m, 5m);  // to_value=0 means "and above"

        Assert.Equal(20m, rule.Calculate(50m));
        Assert.Equal(5m, rule.Calculate(200m));
        Assert.Equal(5m, rule.Calculate(10_000m));
    }

    [Fact]
    public void Rule_NoMatch_Throws()
    {
        var rule = CreateRule();
        rule.AddCondition(100m, 500m, 15m); // Only 100-500 range

        var ex = Assert.Throws<BusinessException>(() => rule.Calculate(50m));
        Assert.Equal("MyERP:03007", ex.Code);
    }

    [Fact]
    public void Rule_Validate_NoConditions_ForNonFixed_Throws()
    {
        var rule = CreateRule(ShippingCalculationMode.BasedOnNetTotal);
        // No conditions added

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:03004", ex.Code);
    }

    [Fact]
    public void Rule_Validate_FixedMode_NoConditionsRequired()
    {
        var rule = CreateRule(ShippingCalculationMode.Fixed);
        rule.FixedAmount = 10m;
        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_Validate_MultipleBlankToValues_Throws()
    {
        var rule = CreateRule();
        rule.AddCondition(0m, 0m, 20m);    // catch-all 1
        rule.AddCondition(100m, 0m, 15m);  // catch-all 2

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:03005", ex.Code);
    }

    [Fact]
    public void Rule_Validate_SingleBlankToValue_Valid()
    {
        var rule = CreateRule();
        rule.AddCondition(0m, 100m, 20m);
        rule.AddCondition(101m, 0m, 10m);  // Single catch-all OK
        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_Validate_OverlappingRanges_Throws()
    {
        var rule = CreateRule();
        rule.AddCondition(0m, 200m, 20m);
        rule.AddCondition(150m, 500m, 15m); // Overlaps with first (150 < 200)

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:03006", ex.Code);
    }

    [Fact]
    public void Rule_Validate_NonOverlapping_Valid()
    {
        var rule = CreateRule();
        rule.AddCondition(0m, 100m, 20m);
        rule.AddCondition(101m, 500m, 15m);
        rule.AddCondition(501m, 1000m, 10m);
        rule.Validate(); // Should not throw
    }

    [Fact]
    public void Rule_CountryFilter_NoRestrictions_AppliesGlobally()
    {
        var rule = CreateRule();
        Assert.True(rule.AppliesToCountry("MY"));
        Assert.True(rule.AppliesToCountry("SG"));
        Assert.True(rule.AppliesToCountry(null));
    }

    [Fact]
    public void Rule_CountryFilter_WithRestrictions()
    {
        var rule = CreateRule();
        rule.AddCountry("MY");
        rule.AddCountry("SG");

        Assert.True(rule.AppliesToCountry("MY"));
        Assert.True(rule.AppliesToCountry("SG"));
        Assert.False(rule.AppliesToCountry("US"));
    }

    [Fact]
    public void Rule_CountryFilter_CaseInsensitive()
    {
        var rule = CreateRule();
        rule.AddCountry("MY");

        Assert.True(rule.AppliesToCountry("my"));
        Assert.True(rule.AppliesToCountry("My"));
    }

    [Fact]
    public void Rule_WeightBased_Works()
    {
        var rule = CreateRule(ShippingCalculationMode.BasedOnNetWeight);
        rule.AddCondition(0m, 5m, 10m);      // 0-5kg: RM 10
        rule.AddCondition(5.01m, 20m, 25m);   // 5-20kg: RM 25
        rule.AddCondition(20.01m, 0m, 50m);   // 20kg+: RM 50

        Assert.Equal(10m, rule.Calculate(3m));
        Assert.Equal(25m, rule.Calculate(15m));
        Assert.Equal(50m, rule.Calculate(100m));
    }

    [Fact]
    public void Rule_AddCondition_IncrementsOrder()
    {
        var rule = CreateRule();
        rule.AddCondition(0m, 100m, 20m);
        rule.AddCondition(101m, 500m, 15m);

        var conditions = rule.Conditions.OrderBy(c => c.SortOrder).ToList();
        Assert.Equal(0, conditions[0].SortOrder);
        Assert.Equal(1, conditions[1].SortOrder);
    }

    [Fact]
    public void Rule_LabelRequired()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShippingRule(Guid.NewGuid(), "", ShippingRuleType.Selling,
                ShippingCalculationMode.Fixed, _accountId));
    }

    [Fact]
    public void Rule_BuyingType()
    {
        var rule = CreateRule(type: ShippingRuleType.Buying);
        Assert.Equal(ShippingRuleType.Buying, rule.RuleType);
    }
}
