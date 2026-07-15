using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// End-to-end flow tests for the gap-fill AppService entities.
/// Validates entity lifecycle, domain rules, and cross-entity interactions
/// for: LoyaltyProgram, SupplierScorecard, ShippingRule, SalesPerson,
/// AutoRepeat, ActivityType, ManufacturingSettings, AuthorizationRule,
/// InstallationNote, AssetCapitalization, EmailTemplate, HierarchyMasterData.
/// </summary>
public class GapFillFlowTests
{
    // === Loyalty Program Flow: Create → Earn → Redeem ===

    [Fact]
    public void LoyaltyProgram_TierProgression()
    {
        var prog = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(), "MyRewards", 10m, 365);
        prog.AddTier("Bronze", 0, 1.0m, 0.01m);
        prog.AddTier("Silver", 5000, 1.5m, 0.015m);
        prog.AddTier("Gold", 20000, 2.0m, 0.02m);
        prog.Validate();

        // New customer (0 spend) → Bronze
        var tier1 = prog.DetermineTier(0, 500);
        tier1.TierName.ShouldBe("Bronze");

        // 4800 + 500 = 5300 → Silver
        var tier2 = prog.DetermineTier(4800, 500);
        tier2.TierName.ShouldBe("Silver");

        // 19500 + 1000 = 20500 → Gold
        var tier3 = prog.DetermineTier(19500, 1000);
        tier3.TierName.ShouldBe("Gold");

        // Gold multiplier: FLOOR(1000/10) × 2.0 = 200 points
        prog.CalculatePointsEarned(1000, tier3).ShouldBe(200);
    }

    [Fact]
    public void LoyaltyProgram_RedemptionValue()
    {
        var prog = new LoyaltyProgram(Guid.NewGuid(), Guid.NewGuid(), "Rewards", 10m, 365);
        prog.AddTier("Standard", 0, 1.0m, 0.01m);
        prog.Validate();

        var tier = prog.Tiers.First();
        // 500 points × RM 0.01/point = RM 5.00
        prog.CalculateRedemptionValue(500, tier).ShouldBe(5.00m);
    }

    // === Supplier Scorecard → Enforcement Sync ===

    [Fact]
    public void SupplierScorecard_ScoreChange_UpdatesEnforcement()
    {
        var sc = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        sc.AddStanding("Critical", 0, 30, preventPos: true, preventRfqs: true);
        sc.AddStanding("Poor", 30, 50, preventPos: true);
        sc.AddStanding("Average", 50, 70);
        sc.AddStanding("Good", 70, 100);
        sc.Validate();

        // Score 80 → Good band, no enforcement
        sc.UpdateScore(80);
        var (pp1, pr1, _, _) = sc.GetEnforcementFlags();
        pp1.ShouldBeFalse();
        pr1.ShouldBeFalse();

        // Score drops to 25 → Critical band, both blocked
        sc.UpdateScore(25);
        var (pp2, pr2, _, _) = sc.GetEnforcementFlags();
        pp2.ShouldBeTrue();
        pr2.ShouldBeTrue();

        // Score recovers to 40 → Poor band, PO blocked only
        sc.UpdateScore(40);
        var (pp3, pr3, _, _) = sc.GetEnforcementFlags();
        pp3.ShouldBeTrue();
        pr3.ShouldBeFalse();
    }

    // === Shipping Rule — Tiered Calculation ===

    [Fact]
    public void ShippingRule_TieredCalculation_WithCountry()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Malaysia Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal, Guid.NewGuid());
        rule.AddCondition(0, 100, 15m);
        rule.AddCondition(100, 500, 10m);
        rule.AddCondition(500, 0, 0m); // free shipping over 500
        rule.AddCountry("MY");
        rule.Validate();

        rule.Calculate(50).ShouldBe(15m);
        rule.Calculate(250).ShouldBe(10m);
        rule.Calculate(1000).ShouldBe(0m);
        rule.AppliesToCountry("MY").ShouldBeTrue();
        rule.AppliesToCountry("SG").ShouldBeFalse();
    }

    // === SalesPerson — Commission Chain ===

    [Fact]
    public void SalesPerson_CommissionChain()
    {
        var manager = new SalesPerson(Guid.NewGuid(), "Sales Manager");
        manager.IsGroup = true;
        manager.SetCommissionRate(5);

        var rep = new SalesPerson(Guid.NewGuid(), "Ahmad", manager.Id);
        rep.SetCommissionRate(10);

        // RM 10,000 sale
        rep.CalculateCommission(10000).ShouldBe(1000m); // 10% = RM 1000
        manager.CalculateCommission(10000).ShouldBe(500m); // 5% = RM 500
    }

    [Fact]
    public void SalesPerson_Target_Tracking()
    {
        var sp = new SalesPerson(Guid.NewGuid(), "Ahmad");
        var fyId = Guid.NewGuid();
        sp.AddTarget(fyId, 500, 2_000_000);

        sp.Targets.Count.ShouldBe(1);
        var target = sp.Targets.First();
        target.TargetQty.ShouldBe(500);
        target.TargetAmount.ShouldBe(2_000_000m);
    }

    // === AutoRepeat — Quarterly Advancement ===

    [Fact]
    public void AutoRepeat_QuarterlyAdvancement()
    {
        var ar = new AutoRepeat(Guid.NewGuid(), Guid.NewGuid(),
            "SalesInvoice", Guid.NewGuid(),
            RepeatFrequency.Quarterly,
            new DateTime(2026, 1, 15),
            endDate: new DateTime(2026, 12, 31));

        ar.RecordGeneration(new DateTime(2026, 1, 15));
        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 4, 15));
        ar.GeneratedCount.ShouldBe(1);

        ar.RecordGeneration(new DateTime(2026, 4, 15));
        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 7, 15));

        ar.RecordGeneration(new DateTime(2026, 7, 15));
        ar.NextScheduleDate.ShouldBe(new DateTime(2026, 10, 15));

        ar.RecordGeneration(new DateTime(2026, 10, 15));
        // Next would be 2027-01-15 > end date → auto-disabled
        ar.IsEnabled.ShouldBeFalse();
        ar.GeneratedCount.ShouldBe(4);
    }

    // === ActivityType — Rate Resolution ===

    [Fact]
    public void ActivityType_RateFallback()
    {
        var actType = new ActivityType(Guid.NewGuid(), "Consulting", 200m, 80m);

        // Default rates from activity type
        actType.DefaultBillingRate.ShouldBe(200m);
        actType.DefaultCostingRate.ShouldBe(80m);

        // Employee-specific override
        var empId = Guid.NewGuid();
        var override_ = new ActivityCost(Guid.NewGuid(), empId, actType.Id, 300m, 120m);

        // Override takes precedence
        override_.BillingRate.ShouldBe(300m);
        override_.CostingRate.ShouldBe(120m);
    }

    // === ManufacturingSettings — Full Configuration Cycle ===

    [Fact]
    public void ManufacturingSettings_FullConfigCycle()
    {
        var s = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());

        // Change to material-transferred mode
        s.BackflushRawMaterialsBasedOn = "Material Transferred for Manufacture";
        s.OverproductionPercentage = 10m;
        s.AllowProductionOnHolidays = true;
        s.EnforceTimeLogs = true;

        s.EnforceMutualExclusions();

        // Validate components forced off when not BOM
        s.ValidateComponentsQuantitiesPerBom.ShouldBeFalse();
        s.OverproductionPercentage.ShouldBe(10m);
        s.AllowProductionOnHolidays.ShouldBeTrue();
    }

    // === AuthorizationRule — Tier Evaluation ===

    [Fact]
    public void AuthorizationRule_TierEvaluation()
    {
        var userId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        // Tier 1: user-specific rule
        var rule1 = new AuthorizationRule(Guid.NewGuid(), "SalesInvoice",
            AuthorizationBasedOn.GrandTotal, 50000m);
        rule1.SystemUserId = userId;
        rule1.ApprovingUserId = approverId;
        rule1.Validate();

        // Tier 2: role-specific
        var rule2 = new AuthorizationRule(Guid.NewGuid(), "SalesInvoice",
            AuthorizationBasedOn.GrandTotal, 100000m);
        rule2.SystemRole = "Sales User";
        rule2.ApprovingRole = "Sales Manager";
        rule2.Validate();

        // Tier 3: global
        var rule3 = new AuthorizationRule(Guid.NewGuid(), "SalesInvoice",
            AuthorizationBasedOn.GrandTotal, 500000m);
        rule3.ApprovingRole = "CEO";
        rule3.Validate();

        // Check thresholds
        rule1.IsExceeded(60000m).ShouldBeTrue();
        rule1.IsExceeded(40000m).ShouldBeFalse();
        rule2.IsExceeded(150000m).ShouldBeTrue();
        rule3.IsExceeded(400000m).ShouldBeFalse();

        // Tier determination
        rule1.GetTier().ShouldBe(1); // Has SystemUserId
        rule2.GetTier().ShouldBe(2); // Has SystemRole
        rule3.GetTier().ShouldBe(3); // Neither → global
    }

    [Fact]
    public void AuthorizationRule_IsAuthorizedApprover()
    {
        var approverId = Guid.NewGuid();
        var rule = new AuthorizationRule(Guid.NewGuid(), "PurchaseOrder",
            AuthorizationBasedOn.GrandTotal, 100000m);
        rule.ApprovingUserId = approverId;
        rule.ApprovingRole = "Purchasing Manager";
        rule.Validate();

        rule.IsAuthorizedApprover(approverId, new[] { "Staff" }).ShouldBeTrue();
        rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "Purchasing Manager" }).ShouldBeTrue();
        rule.IsAuthorizedApprover(Guid.NewGuid(), new[] { "Staff" }).ShouldBeFalse();
    }

    // === EmailTemplate — Variable Substitution Pipeline ===

    [Fact]
    public void EmailTemplate_FullRenderPipeline()
    {
        var tmpl = new EmailTemplate(Guid.NewGuid(), "Dunning Level 1",
            "OVERDUE: Invoice {{invoice_no}} — RM {{amount}}",
            @"Dear {{customer}},

Your invoice {{invoice_no}} for RM {{amount}} is overdue by {{days}} days.

Please remit payment to avoid further action.

Regards,
{{company_name}}");

        tmpl.DocumentType = "SalesInvoice";

        var vars = new Dictionary<string, string>
        {
            { "customer", "Acme Sdn Bhd" },
            { "invoice_no", "SI-2026-00042" },
            { "amount", "15,250.00" },
            { "days", "30" },
            { "company_name", "MyERP Sdn Bhd" }
        };

        var subject = tmpl.RenderSubject(vars);
        subject.ShouldBe("OVERDUE: Invoice SI-2026-00042 — RM 15,250.00");

        var body = tmpl.RenderBody(vars);
        body.ShouldContain("Acme Sdn Bhd");
        body.ShouldContain("SI-2026-00042");
        body.ShouldContain("15,250.00");
        body.ShouldContain("30 days");
        body.ShouldContain("MyERP Sdn Bhd");
    }

    // === Hierarchy — Tree Operations ===

    [Fact]
    public void Territory_ThreeLevelTree()
    {
        var root = new Territory(Guid.NewGuid(), "All Territories", isGroup: true);
        var my = new Territory(Guid.NewGuid(), "Malaysia", root.Id, isGroup: true);
        var kl = new Territory(Guid.NewGuid(), "Kuala Lumpur", my.Id, isGroup: false);
        var pg = new Territory(Guid.NewGuid(), "Penang", my.Id, isGroup: false);

        root.ParentId.ShouldBeNull();
        my.ParentId.ShouldBe(root.Id);
        kl.ParentId.ShouldBe(my.Id);
        pg.ParentId.ShouldBe(my.Id);
        kl.IsGroup.ShouldBeFalse(); // Can assign customers
    }

    [Fact]
    public void CustomerGroup_CreditLimitInheritance()
    {
        var parent = new CustomerGroup(Guid.NewGuid(), "Corporate", isGroup: true);
        parent.DefaultCreditLimit = 100_000m;

        var child = new CustomerGroup(Guid.NewGuid(), "SME", parent.Id, isGroup: false);
        child.DefaultCreditLimit = 25_000m;

        // Child overrides parent default
        child.DefaultCreditLimit.ShouldBe(25_000m);
        parent.DefaultCreditLimit.ShouldBe(100_000m);
    }

    // === InstallationNote — DN-linked Lifecycle ===

    [Fact]
    public void InstallationNote_FullLifecycle()
    {
        var note = new InstallationNote(Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 7, 15));

        note.AddItem(Guid.NewGuid(), 2, "SN-001");
        note.AddItem(Guid.NewGuid(), 1, "SN-002");

        note.Items.Count.ShouldBe(2);
        note.Items.First().Qty.ShouldBe(2);

        note.Submit();
        note.Status.ShouldBe(DocumentStatus.Submitted);

        // Cannot add after submit
        Should.Throw<Exception>(() => note.AddItem(Guid.NewGuid(), 1));

        note.Cancel();
        note.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    // === AssetCapitalization — Multi-Source Totaling ===

    [Fact]
    public void AssetCapitalization_MultiSourceTotal()
    {
        var cap = new AssetCapitalization(Guid.NewGuid(), Guid.NewGuid(), "CAP-001",
            DateTime.Today, Guid.NewGuid());

        // Stock items: 5 units × RM 200 = RM 1,000
        cap.AddStockItem(Guid.NewGuid(), "Raw Material A", 5, 200m);

        // Service items: installation = RM 500
        cap.AddServiceItem(Guid.NewGuid(), "Installation Service", 500m);

        // Consumed assets: old machine worth RM 10,000
        cap.AddConsumedAsset(Guid.NewGuid(), "Old CNC Machine", 10_000m);

        // Total: 1000 + 500 + 10000 = 11500
        cap.TotalCapitalizedAmount.ShouldBe(11_500m);

        cap.Submit();
        cap.Status.ShouldBe(AssetCapitalizationStatus.Submitted);
    }

    // === ItemAttribute — Variant Configuration ===

    [Fact]
    public void ItemAttribute_TextAndNumericModes()
    {
        // Text attribute: Color with discrete values
        var color = new ItemAttribute(Guid.NewGuid(), "Color");
        color.AddValue("Red", "R");
        color.AddValue("Blue", "B");
        color.AddValue("Green", "G");
        color.Values.Count.ShouldBe(3);

        // Numeric attribute: Weight range
        var weight = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);
        weight.SetNumericRange(100, 500, 50);
        weight.IsValidNumericValue(150).ShouldBeTrue(); // 100 + 1×50
        weight.IsValidNumericValue(175).ShouldBeFalse(); // Not on increment
        weight.IsValidNumericValue(500).ShouldBeTrue(); // Upper bound
    }

    // === NotificationLog — Lifecycle ===

    [Fact]
    public void NotificationLog_RetryLimit()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Payment Reminder", "user@example.com");

        log.Status.ShouldBe(NotificationStatus.Queued);

        // Attempt 1: fail + retry
        log.MarkFailed("SMTP timeout");
        log.Status.ShouldBe(NotificationStatus.Failed);
        log.RetryCount.ShouldBe(1); // MarkFailed increments count

        log.QueueRetry(); // count=1 < 3 → re-queue
        log.Status.ShouldBe(NotificationStatus.Queued);

        // Attempt 2: fail + retry
        log.MarkFailed("SMTP timeout again");
        log.RetryCount.ShouldBe(2);
        log.QueueRetry(); // count=2 < 3 → re-queue
        log.Status.ShouldBe(NotificationStatus.Queued);

        // Attempt 3: fail + retry reaches limit
        log.MarkFailed("Final failure");
        log.RetryCount.ShouldBe(3);
        log.QueueRetry(); // count=3 >= 3 → PermanentlyFailed
        log.Status.ShouldBe(NotificationStatus.PermanentlyFailed);
    }

    [Fact]
    public void NotificationLog_SuccessPath()
    {
        var log = new NotificationLog(Guid.NewGuid(), NotificationChannel.Email,
            "Invoice Sent", "user@example.com");

        log.MarkSent();
        log.Status.ShouldBe(NotificationStatus.Sent);
        log.SentAt.ShouldNotBeNull();
    }
}
