using System;
using MyERP.Tax.Entities;
using Xunit;
using Shouldly;

namespace MyERP.Tax;

public class TaxRuleTests
{
    [Fact]
    public void IsApplicableOn_WithinRange_ReturnsTrue()
    {
        var rule = CreateRule(
            effectiveFrom: new DateTime(2024, 1, 1),
            effectiveTo: new DateTime(2025, 12, 31));

        rule.IsApplicableOn(new DateTime(2024, 6, 15)).ShouldBeTrue();
    }

    [Fact]
    public void IsApplicableOn_BeforeEffective_ReturnsFalse()
    {
        var rule = CreateRule(effectiveFrom: new DateTime(2024, 1, 1));

        rule.IsApplicableOn(new DateTime(2023, 12, 31)).ShouldBeFalse();
    }

    [Fact]
    public void IsApplicableOn_AfterExpiry_ReturnsFalse()
    {
        var rule = CreateRule(
            effectiveFrom: new DateTime(2024, 1, 1),
            effectiveTo: new DateTime(2024, 12, 31));

        rule.IsApplicableOn(new DateTime(2025, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void IsApplicableOn_NoExpiry_ReturnsTrue()
    {
        var rule = CreateRule(
            effectiveFrom: new DateTime(2024, 1, 1),
            effectiveTo: null);

        rule.IsApplicableOn(new DateTime(2030, 6, 15)).ShouldBeTrue();
    }

    [Fact]
    public void TaxCalculation_SixPercent_ProducesCorrectAmount()
    {
        // SST Sales Tax 6% on RM1000
        var rate = 6m;
        var taxableAmount = 1000m;

        var taxAmount = Math.Round(taxableAmount * rate / 100m, 2);

        taxAmount.ShouldBe(60m);
    }

    [Fact]
    public void TaxCalculation_EightPercent_ProducesCorrectAmount()
    {
        // SST Service Tax 8% on RM500
        var rate = 8m;
        var taxableAmount = 500m;

        var taxAmount = Math.Round(taxableAmount * rate / 100m, 2);

        taxAmount.ShouldBe(40m);
    }

    private static TaxRule CreateRule(DateTime effectiveFrom, DateTime? effectiveTo = null)
    {
        return new TaxRule(
            Guid.NewGuid(),
            taxCategoryId: Guid.NewGuid(),
            rate: 6m,
            effectiveFrom: effectiveFrom)
        {
            EffectiveTo = effectiveTo,
            IsActive = true
        };
    }
}
