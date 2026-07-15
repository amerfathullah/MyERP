using System;
using System.Linq;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for the new AppService entities added in the gap-fill session:
/// HierarchyMasterData (Territory, CustomerGroup, SupplierGroup),
/// ItemAttribute, AutoRepeat, ActivityType, LoyaltyProgram balance,
/// SupplierScorecard enforcement sync, ShippingRule calculation,
/// SalesPerson commission.
/// </summary>
public class AppServiceGapFillTests
{
    // === Territory ===

    [Fact]
    public void Territory_Create_SetsAllFields()
    {
        var id = Guid.NewGuid();
        var t = new Territory(id, "Malaysia", parentId: null, isGroup: true);

        t.Id.ShouldBe(id);
        t.Name.ShouldBe("Malaysia");
        t.ParentId.ShouldBeNull();
        t.IsGroup.ShouldBeTrue();
    }

    [Fact]
    public void Territory_ChildNode_HasParent()
    {
        var parentId = Guid.NewGuid();
        var t = new Territory(Guid.NewGuid(), "Selangor", parentId: parentId, isGroup: false);

        t.ParentId.ShouldBe(parentId);
        t.IsGroup.ShouldBeFalse();
    }

    [Fact]
    public void Territory_EmptyName_Throws()
    {
        Should.Throw<Exception>(() => new Territory(Guid.NewGuid(), ""));
    }

    // === CustomerGroup ===

    [Fact]
    public void CustomerGroup_DefaultCreditLimit_IsZero()
    {
        var g = new CustomerGroup(Guid.NewGuid(), "Retail");
        g.DefaultCreditLimit.ShouldBe(0m);
    }

    [Fact]
    public void CustomerGroup_CanSetDefaults()
    {
        var g = new CustomerGroup(Guid.NewGuid(), "Enterprise", isGroup: true);
        g.DefaultCreditLimit = 50000m;
        g.DefaultPriceListId = Guid.NewGuid();
        g.DefaultPaymentTermsTemplateId = Guid.NewGuid();

        g.IsGroup.ShouldBeTrue();
        g.DefaultCreditLimit.ShouldBe(50000m);
        g.DefaultPriceListId.ShouldNotBeNull();
    }

    // === SupplierGroup ===

    [Fact]
    public void SupplierGroup_Create()
    {
        var g = new SupplierGroup(Guid.NewGuid(), "Local Vendors");
        g.Name.ShouldBe("Local Vendors");
        g.IsGroup.ShouldBeFalse();
    }

    // === ItemAttribute ===

    [Fact]
    public void ItemAttribute_TextMode_AddValues()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Color");

        attr.AttributeName.ShouldBe("Color");
        attr.IsNumeric.ShouldBeFalse();

        attr.AddValue("Red", "R");
        attr.AddValue("Blue", "B");

        attr.Values.Count.ShouldBe(2);
        attr.Values.First().AttributeValue.ShouldBe("Red");
        attr.Values.First().Abbreviation.ShouldBe("R");
    }

    [Fact]
    public void ItemAttribute_NumericMode_SetRange()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);

        attr.IsNumeric.ShouldBeTrue();
        attr.SetNumericRange(10, 100, 5);

        attr.FromRange.ShouldBe(10);
        attr.ToRange.ShouldBe(100);
        attr.Increment.ShouldBe(5);
    }

    [Fact]
    public void ItemAttribute_NumericValidation_OnIncrement()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Size", isNumeric: true);
        attr.SetNumericRange(10, 50, 10);

        // 30 = 10 + (2 × 10) → valid
        attr.IsValidNumericValue(30).ShouldBeTrue();
        // 35 = 10 + (2.5 × 10) → invalid
        attr.IsValidNumericValue(35).ShouldBeFalse();
    }

    [Fact]
    public void ItemAttribute_DuplicateValue_Throws()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Color");
        attr.AddValue("Red", "R");

        Should.Throw<Exception>(() => attr.AddValue("Red", "RD"));
    }

    // === ActivityType ===

    [Fact]
    public void ActivityType_Create_WithRates()
    {
        var at = new ActivityType(Guid.NewGuid(), "Consulting", 200m, 80m);

        at.Name.ShouldBe("Consulting");
        at.DefaultBillingRate.ShouldBe(200m);
        at.DefaultCostingRate.ShouldBe(80m);
        at.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void ActivityType_ZeroRates_Allowed()
    {
        var at = new ActivityType(Guid.NewGuid(), "Internal");

        at.DefaultBillingRate.ShouldBe(0m);
        at.DefaultCostingRate.ShouldBe(0m);
    }

    [Fact]
    public void ActivityCost_EmployeeOverride()
    {
        var empId = Guid.NewGuid();
        var atId = Guid.NewGuid();
        var cost = new ActivityCost(Guid.NewGuid(), empId, atId, 250m, 100m);

        cost.EmployeeId.ShouldBe(empId);
        cost.ActivityTypeId.ShouldBe(atId);
        cost.BillingRate.ShouldBe(250m);
        cost.CostingRate.ShouldBe(100m);
    }

    // === AutoRepeat Schedule ===

    [Fact]
    public void AutoRepeat_MonthlyAdvancement()
    {
        var ar = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly,
            new DateTime(2026, 1, 15));

        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 1, 15));

        ar.RecordGeneration(new DateTime(2026, 1, 15));
        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 2, 15));
        ar.GeneratedCount.ShouldBe(1);

        ar.RecordGeneration(new DateTime(2026, 2, 15));
        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 3, 15));
        ar.GeneratedCount.ShouldBe(2);
    }

    [Fact]
    public void AutoRepeat_MonthlyClamping_Jan31ToFeb28()
    {
        var ar = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(),
            "JournalEntry", Guid.NewGuid(),
            RepeatFrequency.Monthly,
            new DateTime(2026, 1, 31));

        ar.RecordGeneration(new DateTime(2026, 1, 31));
        // Feb has 28 days in 2026 (non-leap year)
        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void AutoRepeat_AutoDisable_PastEndDate()
    {
        var ar = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly,
            new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 3, 1));

        ar.IsEnabled.ShouldBeTrue();
        ar.RecordGeneration(new DateTime(2026, 1, 1)); // next = Feb 1
        ar.IsEnabled.ShouldBeTrue();
        ar.RecordGeneration(new DateTime(2026, 2, 1)); // next = Mar 1
        ar.IsEnabled.ShouldBeTrue();
        ar.RecordGeneration(new DateTime(2026, 3, 1)); // next = Apr 1 > end
        ar.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_IsDueOn_Correct()
    {
        var ar = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Daily,
            new DateTime(2026, 5, 10));

        ar.IsDueOn(new DateTime(2026, 5, 9)).ShouldBeFalse();
        ar.IsDueOn(new DateTime(2026, 5, 10)).ShouldBeTrue();
        ar.IsDueOn(new DateTime(2026, 5, 11)).ShouldBeTrue();
    }

    [Fact]
    public void AutoRepeat_Disabled_NeverDue()
    {
        var ar = new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Daily,
            new DateTime(2026, 5, 10));

        ar.Disable();
        ar.IsDueOn(new DateTime(2026, 5, 10)).ShouldBeFalse();
    }

    [Fact]
    public void AutoRepeat_EndDateBeforeStart_Throws()
    {
        Should.Throw<Exception>(() => new AutoRepeat(
            Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Monthly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 5, 1)));
    }

    // === LoyaltyProgram Tier + Points ===

    [Fact]
    public void LoyaltyProgram_DetermineTier_IncludesCurrentAmount()
    {
        var prog = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(), "Rewards", 10m, 365);
        prog.AddTier("Bronze", 0, 1, 0.01m);
        prog.AddTier("Silver", 5000, 1.5m, 0.015m);
        prog.AddTier("Gold", 20000, 2, 0.02m);

        // totalSpent=4500, current=600 → combined=5100 → Silver
        var tier = prog.DetermineTier(4500, 600);
        tier.TierName.ShouldBe("Silver");
    }

    [Fact]
    public void LoyaltyProgram_CalculatePoints_UsesFloor()
    {
        var prog = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(), "Rewards", 10m, 365);
        prog.AddTier("Bronze", 0, 1, 0.01m);

        var tier = prog.Tiers.First();
        // 95 / 10 = 9.5 → FLOOR = 9 × 1 = 9
        prog.CalculatePointsEarned(95m, tier).ShouldBe(9);
    }

    // === SupplierScorecard Enforcement ===

    [Fact]
    public void SupplierScorecard_ScoreDeterminesStanding()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        sc.AddStanding("Poor", 0, 40, preventPos: true, preventRfqs: true);
        sc.AddStanding("Average", 40, 70, warnPos: true);
        sc.AddStanding("Good", 70, 100);

        sc.UpdateScore(30); // Falls in Poor band
        var (preventPos, preventRfqs, _, _) = sc.GetEnforcementFlags();
        preventPos.ShouldBeTrue();
        preventRfqs.ShouldBeTrue();
    }

    [Fact]
    public void SupplierScorecard_HighScore_NoEnforcement()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        sc.AddStanding("Poor", 0, 40, preventPos: true, preventRfqs: true);
        sc.AddStanding("Good", 40, 100);

        sc.UpdateScore(80); // Falls in Good band
        var (preventPos, preventRfqs, _, _) = sc.GetEnforcementFlags();
        preventPos.ShouldBeFalse();
        preventRfqs.ShouldBeFalse();
    }

    // === ShippingRule Calculation ===

    [Fact]
    public void ShippingRule_Fixed_ReturnsFixedAmount()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Standard Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.FixedAmount = 15m;

        rule.Calculate(500).ShouldBe(15m);
        rule.Calculate(5000).ShouldBe(15m);
    }

    [Fact]
    public void ShippingRule_Tiered_MatchesCorrectRange()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Tiered Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal, Guid.NewGuid());
        rule.AddCondition(0, 100, 20m);
        rule.AddCondition(100, 500, 10m);
        rule.AddCondition(500, 0, 0m); // catch-all (free over 500)

        rule.Calculate(50).ShouldBe(20m);
        rule.Calculate(250).ShouldBe(10m);
        rule.Calculate(1000).ShouldBe(0m);
    }

    [Fact]
    public void ShippingRule_CountryRestriction()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "MY Only",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.AddCountry("MY");
        rule.FixedAmount = 10m;

        rule.AppliesToCountry("MY").ShouldBeTrue();
        rule.AppliesToCountry("SG").ShouldBeFalse();
        rule.AppliesToCountry("my").ShouldBeTrue(); // case-insensitive
    }

    // === SalesPerson Commission ===

    [Fact]
    public void SalesPerson_CommissionCalculation()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Ahmad");
        sp.SetCommissionRate(10);

        sp.CalculateCommission(5000).ShouldBe(500m); // 10% of 5000
    }

    [Fact]
    public void SalesPerson_InvalidCommissionRate_Throws()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Test");
        Should.Throw<Exception>(() => sp.SetCommissionRate(101));
        Should.Throw<Exception>(() => sp.SetCommissionRate(-1));
    }

    [Fact]
    public void SalesPerson_AddTarget()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Ahmad");
        var fyId = Guid.NewGuid();
        sp.AddTarget(fyId, 100, 500000);

        sp.Targets.Count.ShouldBe(1);
        sp.Targets.First().TargetQty.ShouldBe(100);
        sp.Targets.First().TargetAmount.ShouldBe(500000);
    }
}
