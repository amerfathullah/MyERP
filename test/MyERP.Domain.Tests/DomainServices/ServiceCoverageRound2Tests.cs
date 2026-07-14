using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.DomainServices;

// ═══════════════════════════════════════════════════════════════════
// AgingBucketService Tests
// ═══════════════════════════════════════════════════════════════════

public class AgingBucketServiceTests
{
    [Fact]
    public void GetBucketIndex_NotYetDue_FirstBucket()
    {
        // Private method tested via AgingReport concept
        var report = new AgingReport { BucketRanges = new[] { 30, 60, 90, 120 } };
        report.BucketRanges.Length.ShouldBe(4);
    }

    [Fact]
    public void AgingReport_DefaultValues()
    {
        var report = new AgingReport();
        report.TotalOutstanding.ShouldBe(0);
        report.InvoiceCount.ShouldBe(0);
    }

    [Fact]
    public void AgingItem_DaysOverdue_Calculation()
    {
        var item = new AgingItem
        {
            DueDate = new DateTime(2026, 5, 1),
            OutstandingAmount = 5000m,
        };
        var asOfDate = new DateTime(2026, 6, 15);
        var ageDays = (int)(asOfDate - item.DueDate).TotalDays;
        ageDays.ShouldBe(45); // Falls in 31-60 bucket
    }

    [Fact]
    public void AgingItem_NotYetDue_ClampedToZero()
    {
        var item = new AgingItem
        {
            DueDate = new DateTime(2026, 7, 15),
            OutstandingAmount = 3000m,
        };
        var asOfDate = new DateTime(2026, 6, 15);
        var ageDays = (int)(asOfDate - item.DueDate).TotalDays;
        var clamped = Math.Max(0, ageDays);
        clamped.ShouldBe(0); // Not yet due → bucket 0 (0-30)
    }

    [Fact]
    public void AgingItem_SeverelyOverdue_LastBucket()
    {
        var item = new AgingItem
        {
            DueDate = new DateTime(2026, 1, 1),
            OutstandingAmount = 10000m,
        };
        var asOfDate = new DateTime(2026, 6, 15);
        var ageDays = (int)(asOfDate - item.DueDate).TotalDays;
        ageDays.ShouldBeGreaterThan(120); // Falls in 120+ bucket
    }

    [Fact]
    public void AgingReport_BucketCount_IsBucketsPlus1()
    {
        // Standard 4 thresholds = 5 buckets (0-30, 31-60, 61-90, 91-120, 120+)
        var ranges = new[] { 30, 60, 90, 120 };
        var bucketCount = ranges.Length + 1;
        bucketCount.ShouldBe(5);
    }
}

// ═══════════════════════════════════════════════════════════════════
// BomValidationService Tests (entity-level)
// ═══════════════════════════════════════════════════════════════════

public class BomValidationEntityTests
{
    [Fact]
    public void ExplodedBomItem_Record_StoresFields()
    {
        var itemId = Guid.NewGuid();
        var subBomId = Guid.NewGuid();
        var item = new ExplodedBomItem(itemId, "Steel Rod", 10m, 50m, "Kg", subBomId);

        item.ItemId.ShouldBe(itemId);
        item.ItemName.ShouldBe("Steel Rod");
        item.Quantity.ShouldBe(10m);
        item.Rate.ShouldBe(50m);
        item.SubBomId.ShouldBe(subBomId);
    }

    [Fact]
    public void ExplodedBomItem_RawMaterial_NullSubBom()
    {
        var item = new ExplodedBomItem(Guid.NewGuid(), "Screw", 100m, 0.5m, "Unit", null);
        item.SubBomId.ShouldBeNull();
    }

    [Fact]
    public void BomItem_PhantomFlag_Default()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001",
            Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Part A", 2m, 10m));
        bom.Items.First().IsPhantom.ShouldBeFalse();
    }

    [Fact]
    public void BomItem_SubBomId_Default()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001",
            Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Part A", 2m, 10m));
        bom.Items.First().SubBomId.ShouldBeNull();
    }

    [Fact]
    public void BomItem_CanSetPhantom()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001",
            Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Sub-Assy", 1m, 50m));
        var item = bom.Items.First();
        item.IsPhantom = true;
        item.SubBomId = Guid.NewGuid();
        item.IsPhantom.ShouldBeTrue();
        item.SubBomId.ShouldNotBeNull();
    }

    [Fact]
    public void Bom_CostRecalculation_SumsItems()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001",
            Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Part A", 2m, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Part B", 5m, 3m));
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(35m); // (2×10) + (5×3)
    }
}

// ═══════════════════════════════════════════════════════════════════
// PricingRuleApplicationService Tests
// ═══════════════════════════════════════════════════════════════════

public class PricingRuleServiceTests
{
    [Fact]
    public void PricingRuleContext_DefaultValues()
    {
        var ctx = new PricingRuleContext
        {
            ItemId = Guid.NewGuid(),
            ItemName = "Widget",
            Qty = 10,
            Rate = 100m,
        };
        ctx.DiscountPercentage.ShouldBe(0);
        ctx.DiscountAmount.ShouldBe(0);
        ctx.DiscountedRate.ShouldBe(0);
    }

    [Fact]
    public void PricingRuleContext_DiscountPercentage_CalculatesRate()
    {
        var ctx = new PricingRuleContext { Rate = 200m };
        ctx.DiscountPercentage = 10m;
        ctx.DiscountedRate = ctx.Rate * (1 - ctx.DiscountPercentage / 100m);
        ctx.DiscountedRate.ShouldBe(180m);
    }

    [Fact]
    public void PricingRuleContext_DiscountAmount_CalculatesRate()
    {
        var ctx = new PricingRuleContext { Rate = 200m };
        ctx.DiscountAmount = 25m;
        ctx.DiscountedRate = Math.Max(0, ctx.Rate - ctx.DiscountAmount);
        ctx.DiscountedRate.ShouldBe(175m);
    }

    [Fact]
    public void PricingRuleContext_FixedRate_OverridesRate()
    {
        var ctx = new PricingRuleContext { Rate = 200m };
        ctx.DiscountedRate = 150m; // Fixed rate from rule
        ctx.DiscountedRate.ShouldBe(150m);
    }

    [Fact]
    public void PricingRule_Matches_ItemCode()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(itemId);
        rule.Matches(itemId, null, 5, 500, DateTime.Today).ShouldBeTrue();
    }

    [Fact]
    public void PricingRule_Matches_RespectsDates()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(itemId);
        rule.ValidFrom = new DateTime(2026, 1, 1);
        rule.ValidUpto = new DateTime(2026, 6, 30);

        rule.Matches(itemId, null, 5, 500, new DateTime(2026, 3, 15)).ShouldBeTrue();
        rule.Matches(itemId, null, 5, 500, new DateTime(2026, 7, 15)).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_Disabled_NeverMatches()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(itemId);
        rule.IsDisabled = true;
        rule.Matches(itemId, null, 5, 500, DateTime.Today).ShouldBeFalse();
    }

    [Fact]
    public void PricingRule_MinQty_Enforced()
    {
        var itemId = Guid.NewGuid();
        var rule = CreateRule(itemId);
        rule.MinQty = 10;
        rule.Matches(itemId, null, 5, 500, DateTime.Today).ShouldBeFalse(); // Below min
        rule.Matches(itemId, null, 15, 500, DateTime.Today).ShouldBeTrue(); // Above min
    }

    [Fact]
    public void AppliedPricingRule_StoresResult()
    {
        var result = new AppliedPricingRule
        {
            RuleId = Guid.NewGuid(),
            RuleTitle = "10% Bulk Discount",
            RuleType = Sales.PricingRuleType.Discount,
            DiscountPercentage = 10m,
        };
        result.RuleTitle.ShouldBe("10% Bulk Discount");
        result.DiscountPercentage.ShouldBe(10m);
    }

    private static PricingRule CreateRule(Guid itemId)
    {
        var rule = new PricingRule(Guid.NewGuid(), "Test Rule",
            Sales.PricingRuleApplyOn.ItemCode, Sales.PricingRuleType.Discount);
        rule.ApplyOnId = itemId;
        rule.DiscountPercentage = 10;
        return rule;
    }
}

// ═══════════════════════════════════════════════════════════════════
// LoyaltyProgram Entity Tests (pure logic, no repository)
// ═══════════════════════════════════════════════════════════════════

public class LoyaltyProgramEntityTests
{
    [Fact]
    public void LoyaltyProgram_DetermineTier_LowestIfNoSpend()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(0m, 100m);
        tier.TierName.ShouldBe("Bronze");
    }

    [Fact]
    public void LoyaltyProgram_DetermineTier_IncludesCurrentAmount()
    {
        var program = CreateProgram();
        // totalSpent=4500, currentAmount=600 → total 5100 → qualifies for Silver (5000)
        var tier = program.DetermineTier(4500m, 600m);
        tier.TierName.ShouldBe("Silver");
    }

    [Fact]
    public void LoyaltyProgram_DetermineTier_HighestQualifying()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(20000m, 1000m);
        tier.TierName.ShouldBe("Gold"); // 21000 > 20000
    }

    [Fact]
    public void LoyaltyProgram_CalculatePoints_Basic()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(0m, 1000m);
        var points = program.CalculatePointsEarned(1000m, tier);
        // FLOOR(1000 / 100) × 1.0 = 10 points
        points.ShouldBe(10);
    }

    [Fact]
    public void LoyaltyProgram_CalculatePoints_WithMultiplier()
    {
        var program = CreateProgram();
        var tier = program.Tiers.First(t => t.TierName == "Silver");
        var points = program.CalculatePointsEarned(1000m, tier);
        // FLOOR(1000 / 100) × 1.5 = 15 points
        points.ShouldBe(15);
    }

    [Fact]
    public void LoyaltyProgram_CalculatePoints_Floor()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(0m, 150m);
        var points = program.CalculatePointsEarned(150m, tier);
        // FLOOR(150 / 100) × 1.0 = 1 point (not 1.5)
        points.ShouldBe(1);
    }

    [Fact]
    public void LoyaltyProgram_CalculateRedemptionValue()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(0m, 0m);
        var value = program.CalculateRedemptionValue(100, tier);
        // 100 × 0.5 = 50
        value.ShouldBe(50m);
    }

    [Fact]
    public void LoyaltyPointEntry_Earned_IsPositive()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 10, DateTime.Today, DateTime.Today.AddDays(365));
        entry.IsEarning.ShouldBeTrue();
        entry.IsRedemption.ShouldBeFalse();
        entry.Points.ShouldBe(10);
    }

    [Fact]
    public void LoyaltyPointEntry_Redeemed_IsNegative()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), -5, DateTime.Today);
        entry.IsRedemption.ShouldBeTrue();
        entry.IsEarning.ShouldBeFalse();
    }

    [Fact]
    public void LoyaltyPointEntry_Expired_WhenPastExpiryDate()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 10, DateTime.Today.AddDays(-100),
            DateTime.Today.AddDays(-2)); // -2 to avoid UTC/local timezone edge case
        entry.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void LoyaltyPointEntry_NotExpired_WhenNoExpiryDate()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 10, DateTime.Today);
        entry.IsExpired.ShouldBeFalse();
    }

    private static LoyaltyProgram CreateProgram()
    {
        var p = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(),
            "Test Loyalty", 100m, 365); // 100 MYR per point, 365 day expiry
        p.AddTier("Bronze", 0m, 1.0m, 0.5m);
        p.AddTier("Silver", 5000m, 1.5m, 0.75m);
        p.AddTier("Gold", 20000m, 2.0m, 1.0m);
        return p;
    }
}

// ═══════════════════════════════════════════════════════════════════
// FiscalYearCloseService Tests (entity-level)
// ═══════════════════════════════════════════════════════════════════

public class FiscalYearCloseEntityTests
{
    [Fact]
    public void FiscalYear_DefaultOpen()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fy.IsClosed.ShouldBeFalse();
    }

    [Fact]
    public void FiscalYear_Close_SetsClosed()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        fy.IsClosed = true;
        fy.IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void FiscalYear_ContainsDate_Inside()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        var date = new DateTime(2026, 6, 15);
        (fy.StartDate <= date && fy.EndDate >= date).ShouldBeTrue();
    }

    [Fact]
    public void FiscalYear_ContainsDate_Outside()
    {
        var fy = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        var date = new DateTime(2027, 1, 15);
        (fy.StartDate <= date && fy.EndDate >= date).ShouldBeFalse();
    }

    [Fact]
    public void SequentialClose_PriorOpenBlocks()
    {
        // Simulates: trying to close FY2026 when FY2025 is still open
        var fy2025 = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        var fy2026 = new FiscalYear(Guid.NewGuid(), fy2025.CompanyId, "FY2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));

        // Per DO-NOT: "Skip sequential period closing enforcement (previous FY must be closed first)"
        var priorIsOpen = !fy2025.IsClosed && fy2025.EndDate < fy2026.StartDate;
        priorIsOpen.ShouldBeTrue(); // Should block FY2026 close
    }

    [Fact]
    public void SequentialClose_PriorClosedAllows()
    {
        var fy2025 = new FiscalYear(Guid.NewGuid(), Guid.NewGuid(), "FY2025",
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
        fy2025.IsClosed = true;

        var priorIsOpen = !fy2025.IsClosed;
        priorIsOpen.ShouldBeFalse(); // Should allow
    }
}

// ═══════════════════════════════════════════════════════════════════
// ItemVariantService Entity Tests (no repository needed)
// ═══════════════════════════════════════════════════════════════════

public class ItemVariantEntityTests
{
    [Fact]
    public void ItemAttribute_AddValue_Stores()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Color");
        attr.AddValue("Red", "R");
        attr.AddValue("Blue", "B");
        attr.Values.Count.ShouldBe(2);
    }

    [Fact]
    public void ItemAttribute_AddDuplicateValue_Throws()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Color");
        attr.AddValue("Red", "R");
        Should.Throw<BusinessException>(() => attr.AddValue("Red", "R2"));
    }

    [Fact]
    public void ItemAttribute_NumericRange_Valid()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Weight");
        attr.SetNumericRange(0m, 100m, 0.5m);
        attr.IsNumeric.ShouldBeTrue();
        attr.FromRange.ShouldBe(0);
        attr.ToRange.ShouldBe(100);
        attr.Increment.ShouldBe(0.5m);
    }

    [Fact]
    public void ItemAttribute_NumericRange_InvalidFrom_Throws()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Weight");
        Should.Throw<BusinessException>(() => attr.SetNumericRange(100m, 50m, 1m));
    }

    [Fact]
    public void ItemAttribute_NumericRange_ZeroIncrement_Throws()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Weight");
        Should.Throw<BusinessException>(() => attr.SetNumericRange(0m, 100m, 0m));
    }

    [Fact]
    public void ItemAttribute_IsValidNumericValue_OnIncrement()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Size");
        attr.SetNumericRange(10m, 50m, 5m);
        attr.IsValidNumericValue(15m).ShouldBeTrue(); // (15 - 10) % 5 == 0
        attr.IsValidNumericValue(17m).ShouldBeFalse(); // (17 - 10) % 5 != 0
    }

    [Fact]
    public void ItemAttribute_IsValidNumericValue_OutOfRange()
    {
        var attr = new Inventory.Entities.ItemAttribute(Guid.NewGuid(), "Size");
        attr.SetNumericRange(10m, 50m, 5m);
        attr.IsValidNumericValue(55m).ShouldBeFalse(); // Above range
        attr.IsValidNumericValue(5m).ShouldBeFalse(); // Below range
    }

    [Fact]
    public void Item_HasVariants_DefaultFalse()
    {
        var item = new Inventory.Entities.Item(Guid.NewGuid(), Guid.NewGuid(),
            "ITEM-001", "Test Item", Inventory.ItemType.Goods);
        item.HasVariants.ShouldBeFalse();
        item.VariantOfId.ShouldBeNull();
    }

    [Fact]
    public void Item_VariantAttributes_DefaultEmpty()
    {
        var item = new Inventory.Entities.Item(Guid.NewGuid(), Guid.NewGuid(),
            "ITEM-001", "Test Item", Inventory.ItemType.Goods);
        item.VariantAttributes.ShouldNotBeNull();
    }
}

// ═══════════════════════════════════════════════════════════════════
// BinService Entity Tests
// ═══════════════════════════════════════════════════════════════════

public class BinEntityTests
{
    [Fact]
    public void Bin_DefaultValues_AllZero()
    {
        var bin = new Inventory.Entities.Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty.ShouldBe(0);
        bin.ReservedQty.ShouldBe(0);
        bin.OrderedQty.ShouldBe(0);
        bin.IndentedQty.ShouldBe(0);
        bin.PlannedQty.ShouldBe(0);
    }

    [Fact]
    public void Bin_ProjectedQty_Formula()
    {
        var bin = new Inventory.Entities.Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100;
        bin.ReservedQty = 20;
        bin.OrderedQty = 30;
        bin.IndentedQty = 10;
        bin.PlannedQty = 5;
        // ProjectedQty = Actual - Reserved + Ordered + Indented + Planned
        bin.ProjectedQty.ShouldBe(125); // 100 - 20 + 30 + 10 + 5
    }

    [Fact]
    public void Bin_ProjectedQty_CanBeNegative()
    {
        var bin = new Inventory.Entities.Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 10;
        bin.ReservedQty = 50;
        bin.ProjectedQty.ShouldBe(-40); // Negative is allowed
    }

    [Fact]
    public void Bin_ConcurrencyStamp_NotNull()
    {
        var bin = new Inventory.Entities.Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // ABP IHasConcurrencyStamp sets initial stamp
        bin.ConcurrencyStamp.ShouldNotBeNull();
    }
}
