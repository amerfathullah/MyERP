using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests for the latest batch of AppService entities:
/// ManufacturingSettings, AuthorizationRule, InstallationNote,
/// AssetCapitalization, EmailTemplate, and HierarchyMasterData.
/// </summary>
public class LatestAppServiceEntityTests
{
    // === ManufacturingSettings ===

    [Fact]
    public void ManufacturingSettings_Defaults()
    {
        var s = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        s.OverproductionPercentage.ShouldBe(5m);
        s.BackflushRawMaterialsBasedOn.ShouldBe("BOM");
        s.MinsBetweenOperations.ShouldBe(10);
        s.CapacityPlanningForDays.ShouldBe(30);
        s.ValidateComponentsQuantitiesPerBom.ShouldBeTrue();
        s.MaterialConsumption.ShouldBeFalse();
        s.AllowOvertime.ShouldBeFalse();
    }

    [Fact]
    public void ManufacturingSettings_MutualExclusion_BackflushDisablesValidation()
    {
        var s = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        s.ValidateComponentsQuantitiesPerBom = true;
        s.BackflushRawMaterialsBasedOn = "Material Transferred for Manufacture";

        s.EnforceMutualExclusions();

        // When backflush != "BOM", validate_components forced off
        s.ValidateComponentsQuantitiesPerBom.ShouldBeFalse();
    }

    [Fact]
    public void ManufacturingSettings_MutualExclusion_BOMKeepsValidation()
    {
        var s = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        s.ValidateComponentsQuantitiesPerBom = true;
        s.BackflushRawMaterialsBasedOn = "BOM";

        s.EnforceMutualExclusions();

        s.ValidateComponentsQuantitiesPerBom.ShouldBeTrue();
    }

    [Fact]
    public void ManufacturingSettings_AllFlags_CanBeSet()
    {
        var s = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        s.AllowOvertime = true;
        s.AllowProductionOnHolidays = true;
        s.DisableCapacityPlanning = true;
        s.JobCardExcessTransfer = true;
        s.EnforceTimeLogs = true;
        s.AddCorrectiveOpCostInFGValuation = true;
        s.MakeSerialNoBatchFromWorkOrder = true;
        s.UpdateBomCostsAutomatically = true;
        s.MaterialConsumption = true;

        s.AllowOvertime.ShouldBeTrue();
        s.AllowProductionOnHolidays.ShouldBeTrue();
        s.DisableCapacityPlanning.ShouldBeTrue();
        s.JobCardExcessTransfer.ShouldBeTrue();
        s.EnforceTimeLogs.ShouldBeTrue();
        s.AddCorrectiveOpCostInFGValuation.ShouldBeTrue();
        s.MakeSerialNoBatchFromWorkOrder.ShouldBeTrue();
        s.UpdateBomCostsAutomatically.ShouldBeTrue();
        s.MaterialConsumption.ShouldBeTrue();
    }

    // === AuthorizationRule ===

    [Fact]
    public void AuthorizationRule_Create_ValidRule()
    {
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "SalesInvoice", AuthorizationBasedOn.GrandTotal, 50000m);
        rule.ApprovingRole = "Sales Manager";

        rule.Validate(); // Should not throw

        rule.TransactionType.ShouldBe("SalesInvoice");
        rule.BasedOn.ShouldBe(AuthorizationBasedOn.GrandTotal);
        rule.ThresholdValue.ShouldBe(50000m);
    }

    [Fact]
    public void AuthorizationRule_SelfApproval_Throws()
    {
        var userId = Guid.NewGuid();
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "PurchaseOrder", AuthorizationBasedOn.GrandTotal, 10000m);
        rule.SystemUserId = userId;
        rule.ApprovingUserId = userId; // Same user = self-approval

        Should.Throw<Exception>(() => rule.Validate());
    }

    [Fact]
    public void AuthorizationRule_NoApprover_Throws()
    {
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "SalesInvoice", AuthorizationBasedOn.GrandTotal, 50000m);
        // No ApprovingRole or ApprovingUserId set

        Should.Throw<Exception>(() => rule.Validate());
    }

    [Fact]
    public void AuthorizationRule_DiscountExceeds100_Throws()
    {
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "SalesInvoice", AuthorizationBasedOn.AverageDiscount, 150m);
        rule.ApprovingRole = "Sales Manager";

        Should.Throw<Exception>(() => rule.Validate());
    }

    [Fact]
    public void AuthorizationRule_CustomerwiseRequiresCustomer_Throws()
    {
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "SalesInvoice", AuthorizationBasedOn.CustomerwiseDiscount, 20m);
        rule.ApprovingRole = "Sales Manager";
        // No CustomerId set

        Should.Throw<Exception>(() => rule.Validate());
    }

    [Fact]
    public void AuthorizationRule_CompanyScope()
    {
        var companyId = Guid.NewGuid();
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "PurchaseOrder", AuthorizationBasedOn.GrandTotal, 100000m, companyId);
        rule.ApprovingRole = "CFO";

        rule.CompanyId.ShouldBe(companyId);
        rule.Validate(); // Company-scoped rules are valid
    }

    [Fact]
    public void AuthorizationRule_IsExceeded_Works()
    {
        var rule = new AuthorizationRule(
            Guid.NewGuid(), "SalesInvoice", AuthorizationBasedOn.GrandTotal, 50000m);

        rule.IsExceeded(60000m).ShouldBeTrue();
        rule.IsExceeded(50000m).ShouldBeFalse(); // Equal = not exceeded
        rule.IsExceeded(30000m).ShouldBeFalse();
    }

    // === InstallationNote ===

    [Fact]
    public void InstallationNote_Create()
    {
        var note = new InstallationNote(
            Guid.NewGuid(), Guid.NewGuid(), "IN-001",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        note.InstallationNumber.ShouldBe("IN-001");
        note.Status.ShouldBe(DocumentStatus.Draft);
    }

    [Fact]
    public void InstallationNote_AddItem()
    {
        var note = new InstallationNote(
            Guid.NewGuid(), Guid.NewGuid(), "IN-002",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        note.AddItem(Guid.NewGuid(), 5);
        note.Items.Count.ShouldBe(1);
        note.Items.First().Qty.ShouldBe(5);
    }

    [Fact]
    public void InstallationNote_Submit_Cancel_Lifecycle()
    {
        var note = new InstallationNote(
            Guid.NewGuid(), Guid.NewGuid(), "IN-003",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        note.AddItem(Guid.NewGuid(), 1);
        note.Submit();
        note.Status.ShouldBe(DocumentStatus.Submitted);

        note.Cancel();
        note.Status.ShouldBe(DocumentStatus.Cancelled);
    }

    [Fact]
    public void InstallationNote_AddAfterSubmit_Throws()
    {
        var note = new InstallationNote(
            Guid.NewGuid(), Guid.NewGuid(), "IN-004",
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        note.AddItem(Guid.NewGuid(), 1);
        note.Submit();

        Should.Throw<Exception>(() => note.AddItem(Guid.NewGuid(), 2));
    }

    // === AssetCapitalization ===

    [Fact]
    public void AssetCapitalization_Create_Default()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), Guid.NewGuid(), "CAP-001", DateTime.Today, Guid.NewGuid());

        cap.Status.ShouldBe(AssetCapitalizationStatus.Draft);
        cap.TotalCapitalizedAmount.ShouldBe(0m);
    }

    [Fact]
    public void AssetCapitalization_AddStockItem_IncreasesTotal()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), Guid.NewGuid(), "CAP-002", DateTime.Today, Guid.NewGuid());

        cap.AddStockItem(Guid.NewGuid(), "Widget", 10, 100m);
        cap.TotalCapitalizedAmount.ShouldBe(1000m); // 10 × 100
    }

    [Fact]
    public void AssetCapitalization_MixedSources_TotalCorrect()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), Guid.NewGuid(), "CAP-003", DateTime.Today, Guid.NewGuid());

        cap.AddStockItem(Guid.NewGuid(), "Part A", 5, 200m);  // 1000
        cap.AddServiceItem(Guid.NewGuid(), "Installation", 500m); // 500
        cap.AddConsumedAsset(Guid.NewGuid(), "Old Machine", 3000m); // 3000

        cap.TotalCapitalizedAmount.ShouldBe(4500m);
    }

    [Fact]
    public void AssetCapitalization_Submit_Cancel()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), Guid.NewGuid(), "CAP-004", DateTime.Today, Guid.NewGuid());
        cap.AddStockItem(Guid.NewGuid(), "Part", 1, 500m);

        cap.Submit();
        cap.Status.ShouldBe(AssetCapitalizationStatus.Submitted);

        cap.Cancel();
        cap.Status.ShouldBe(AssetCapitalizationStatus.Cancelled);
    }

    [Fact]
    public void AssetCapitalization_AddAfterSubmit_Throws()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), Guid.NewGuid(), "CAP-005", DateTime.Today, Guid.NewGuid());
        cap.AddStockItem(Guid.NewGuid(), "Part", 1, 100m);
        cap.Submit();

        Should.Throw<Exception>(() => cap.AddStockItem(Guid.NewGuid(), "Extra", 1, 50m));
    }

    // === EmailTemplate ===

    [Fact]
    public void EmailTemplate_RenderSubject_ReplacesVariables()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Payment Reminder",
            "Payment Due: {{invoice_number}}", "Dear {{customer_name}}, please pay.");

        var result = t.RenderSubject(new Dictionary<string, string>
        {
            { "invoice_number", "SI-2026-00042" }
        });

        result.ShouldBe("Payment Due: SI-2026-00042");
    }

    [Fact]
    public void EmailTemplate_RenderBody_MultipleVariables()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Dunning",
            "Overdue: {{amount}}", "Dear {{customer}}, your invoice {{invoice}} for RM {{amount}} is overdue.");

        var result = t.RenderBody(new Dictionary<string, string>
        {
            { "customer", "Acme Corp" },
            { "invoice", "SI-001" },
            { "amount", "5,000.00" }
        });

        result.ShouldContain("Acme Corp");
        result.ShouldContain("SI-001");
        result.ShouldContain("5,000.00");
    }

    [Fact]
    public void EmailTemplate_MissingVariable_KeptAsIs()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Test",
            "Hello {{name}}", "Body");

        var result = t.RenderSubject(new Dictionary<string, string>());
        // Missing variables should be kept as-is (not crash)
        result.ShouldContain("{{name}}");
    }

    [Fact]
    public void EmailTemplate_EmptyVariables_NoChange()
    {
        var t = new EmailTemplate(Guid.NewGuid(), "Test",
            "No variables here", "Plain body");

        t.RenderSubject(new Dictionary<string, string>()).ShouldBe("No variables here");
        t.RenderBody(new Dictionary<string, string>()).ShouldBe("Plain body");
    }

    [Fact]
    public void EmailTemplate_RequiredFields()
    {
        Should.Throw<Exception>(() => new EmailTemplate(Guid.NewGuid(), "", "Subject", "Body"));
        Should.Throw<Exception>(() => new EmailTemplate(Guid.NewGuid(), "Name", "", "Body"));
        Should.Throw<Exception>(() => new EmailTemplate(Guid.NewGuid(), "Name", "Subject", ""));
    }

    // === HierarchyMasterData (Territory/CustomerGroup/SupplierGroup) ===

    [Fact]
    public void Territory_LeafAndGroup()
    {
        var root = new Territory(Guid.NewGuid(), "All Territories", isGroup: true);
        var leaf = new Territory(Guid.NewGuid(), "Kuala Lumpur", root.Id, isGroup: false);

        root.IsGroup.ShouldBeTrue();
        leaf.IsGroup.ShouldBeFalse();
        leaf.ParentId.ShouldBe(root.Id);
    }

    [Fact]
    public void CustomerGroup_LeafOnly_PerDoNot()
    {
        // Per DO-NOT: customers can only be assigned to leaf nodes (is_group=false)
        var group = new CustomerGroup(Guid.NewGuid(), "Enterprise", isGroup: true);
        var leaf = new CustomerGroup(Guid.NewGuid(), "SME", group.Id, isGroup: false);

        group.IsGroup.ShouldBeTrue();
        leaf.IsGroup.ShouldBeFalse();
    }

    [Fact]
    public void SupplierGroup_DefaultPaymentTerms()
    {
        var g = new SupplierGroup(Guid.NewGuid(), "International");
        var ptId = Guid.NewGuid();
        g.DefaultPaymentTermsTemplateId = ptId;

        g.DefaultPaymentTermsTemplateId.ShouldBe(ptId);
    }
}
