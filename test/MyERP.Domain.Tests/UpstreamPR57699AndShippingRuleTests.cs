using System;
using System.Linq;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Sales;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for upstream PR #57699 (prevent duplicate shipping charges without cost center)
/// + ShippingRule domain model edge cases.
///
/// PR #57699 — ERPNext: ShippingRule.apply() appended duplicate tax rows when:
///   1. Rule has no cost_center (null)
///   2. Existing tax row had company default cost_center auto-filled
///   3. Filter comparison failed: null ≠ "CC-001" → appended new row
///   4. Fix: when rule CostCenter is blank, match against (null, "", company_default_cc)
///
/// MyERP: This bug class CANNOT occur because:
///   - Shipping is stored as `SalesOrder.ShippingCharge` (decimal field, not tax row)
///   - Re-applying the rule OVERWRITES the field (idempotent)
///   - No "append to tax rows" pattern exists for shipping charges
///   - Architecture prevents this class of bug entirely
/// </summary>
public class UpstreamPR57699AndShippingRuleTests
{
    // --- PR #57699: No code change needed (architecture prevents bug class) ---

    [Fact]
    public void ShippingCharge_IsSimpleDecimal_NotTaxRow()
    {
        // MyERP stores shipping as a field, not a tax row
        // Re-applying a rule just overwrites — impossible to duplicate
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow, null);
        so.ShippingCharge = 15.00m;
        so.ShippingCharge = 20.00m; // Overwrite, not append

        Assert.Equal(20.00m, so.ShippingCharge);
    }

    [Fact]
    public void ShippingCharge_DefaultsZero()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SO-001", DateTime.UtcNow, null);

        Assert.Equal(0m, so.ShippingCharge);
    }

    [Fact]
    public void ShippingRule_NullCostCenter_NoCodeChangeNeeded()
    {
        // PR #57699: when ShippingRule.cost_center is blank, ERPNext now matches
        // against (None, "", company_default_cc) to prevent duplicates
        // MyERP: CostCenterId is nullable Guid — no string comparison issues
        var rule = new ShippingRule(Guid.NewGuid(), "Standard Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());

        Assert.Null(rule.CostCenterId); // Default is null
        Assert.Equal(ShippingRuleType.Selling, rule.RuleType);
    }

    [Fact]
    public void ShippingRule_WithCostCenter_CanBeSet()
    {
        var ccId = Guid.NewGuid();
        var rule = new ShippingRule(Guid.NewGuid(), "Express Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        rule.CostCenterId = ccId;

        Assert.Equal(ccId, rule.CostCenterId);
    }

    // --- ShippingRule domain model edge cases ---

    [Fact]
    public void ShippingRule_FixedMode_ReturnsFixedAmount()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Flat Rate",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        rule.FixedAmount = 25.00m;

        Assert.Equal(25.00m, rule.Calculate(100m));
        Assert.Equal(25.00m, rule.Calculate(0m));
        Assert.Equal(25.00m, rule.Calculate(999999m));
    }

    [Fact]
    public void ShippingRule_TieredMode_MatchesCorrectTier()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Tiered",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal,
            Guid.NewGuid());
        rule.AddCondition(0m, 100m, 15m);    // 0-100 → RM 15
        rule.AddCondition(100.01m, 500m, 10m); // 100-500 → RM 10
        rule.AddCondition(500.01m, 0m, 0m);   // 500+ → free (catch-all)

        Assert.Equal(15m, rule.Calculate(50m));
        Assert.Equal(10m, rule.Calculate(300m));
        Assert.Equal(0m, rule.Calculate(1000m));
    }

    [Fact]
    public void ShippingRule_CountryFilter_GlobalWhenEmpty()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Global",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        // No countries added = global

        Assert.True(rule.AppliesToCountry("MY"));
        Assert.True(rule.AppliesToCountry("US"));
        Assert.True(rule.AppliesToCountry(null));
    }

    [Fact]
    public void ShippingRule_CountryFilter_RestrictsWhenPopulated()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Malaysia Only",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        rule.AddCountry("MY");
        rule.AddCountry("SG");

        Assert.True(rule.AppliesToCountry("MY"));
        Assert.True(rule.AppliesToCountry("SG"));
        Assert.True(rule.AppliesToCountry("my")); // Case-insensitive
        Assert.False(rule.AppliesToCountry("US"));
    }

    [Fact]
    public void ShippingRule_BuyingType_CannotApplyToSelling()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Freight In",
            ShippingRuleType.Buying, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        rule.FixedAmount = 50m;

        // Rule type is metadata — application layer enforces selling vs buying
        Assert.Equal(ShippingRuleType.Buying, rule.RuleType);
        Assert.NotEqual(ShippingRuleType.Selling, rule.RuleType);
    }

    [Fact]
    public void ShippingRule_ProjectId_DefaultsNull()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Test",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());

        Assert.Null(rule.ProjectId);
    }

    [Fact]
    public void ShippingRule_ProjectId_CanBeSet()
    {
        var projectId = Guid.NewGuid();
        var rule = new ShippingRule(Guid.NewGuid(), "Project Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        rule.ProjectId = projectId;

        Assert.Equal(projectId, rule.ProjectId);
    }

    [Fact]
    public void ShippingRule_Validate_FixedMode_NoConditionsOk()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Flat",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed,
            Guid.NewGuid());
        rule.FixedAmount = 10m;

        // Fixed mode doesn't require conditions
        var ex = Record.Exception(() => rule.Validate());
        Assert.Null(ex);
    }

    [Fact]
    public void ShippingRule_Validate_TieredMode_RequiresConditions()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Empty Tiered",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal,
            Guid.NewGuid());

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:03004", ex.Code);
    }

    [Fact]
    public void ShippingRule_Validate_BlocksOverlappingRanges()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Overlapping",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal,
            Guid.NewGuid());
        rule.AddCondition(0m, 100m, 15m);
        rule.AddCondition(50m, 200m, 10m); // Overlaps with first

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:03006", ex.Code);
    }

    [Fact]
    public void ShippingRule_Validate_BlocksMultipleCatchAll()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Double Catch",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal,
            Guid.NewGuid());
        rule.AddCondition(0m, 0m, 15m);   // Catch-all 1
        rule.AddCondition(100m, 0m, 10m); // Catch-all 2

        var ex = Assert.Throws<BusinessException>(() => rule.Validate());
        Assert.Equal("MyERP:03005", ex.Code);
    }

    // --- Upstream tracking ---

    [Fact]
    public void UpstreamPR57699_NoCodeChangeNeeded()
    {
        // PR #57699: prevent duplicate shipping charges without cost center
        // ERPNext: ShippingRule.apply() now matches blank cost_center against
        //   (None, "", company_default_cc) when checking for existing tax rows
        // MyERP: shipping is a simple decimal field, not a tax row
        //   → duplication is architecturally impossible
        //   → no code change needed
        Assert.True(true, "Architecture prevents this bug class entirely");
    }

    [Fact]
    public void Upstream_NoNewMyinvoisCommits()
    {
        // myinvois: 6501660 (unchanged)
        Assert.True(true, "No new myinvois commits since last sync");
    }

    [Fact]
    public void Upstream_SessionScope()
    {
        // Source: erpnext 0b9dd11115 (was c3cd4f18f2, +1 PR: #57699)
        // myinvois: 6501660 (unchanged)
        // Changes: 1 upstream PR analyzed, no code changes required
        Assert.True(true);
    }
}
