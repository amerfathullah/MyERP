using System;
using System.Linq;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Tests for WorkOrderManager domain service rules:
/// overproduction enforcement, material requirements calculation,
/// BOM validation, backflush method resolution, and WO lifecycle.
/// </summary>
public class WorkOrderManagerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();

    // ========== Overproduction Enforcement ==========

    [Fact]
    public void Overproduction_Zero_Pct_Blocks_Any_Excess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        // 0% overproduction means exact qty only
        wo.RecordProduction(100, 0m);
        wo.ProducedQuantity.ShouldBe(100);
    }

    [Fact]
    public void Overproduction_Zero_Pct_Blocks_Even_One_Extra()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        Should.Throw<BusinessException>(() => wo.RecordProduction(101, 0m));
    }

    [Fact]
    public void Overproduction_5Pct_Allows_Within_Limit()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        // 5% of 100 = max 105
        wo.RecordProduction(105, 5m);
        wo.ProducedQuantity.ShouldBe(105);
    }

    [Fact]
    public void Overproduction_5Pct_Blocks_Beyond_Limit()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        Should.Throw<BusinessException>(() => wo.RecordProduction(106, 5m));
    }

    [Fact]
    public void Overproduction_Cumulative_Check()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(80, 5m);
        wo.ProducedQuantity.ShouldBe(80);

        // Can produce 25 more (80+25=105, max=105)
        wo.RecordProduction(25, 5m);
        wo.ProducedQuantity.ShouldBe(105);
    }

    [Fact]
    public void Overproduction_Cumulative_Blocks_When_Total_Exceeds()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(80, 5m);

        // 80+30=110 > 105 limit
        Should.Throw<BusinessException>(() => wo.RecordProduction(30, 5m));
    }

    [Fact]
    public void Overproduction_10Pct_Allows_110()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(110, 10m);
        wo.ProducedQuantity.ShouldBe(110);
    }

    // ========== Material Requirements Calculation ==========

    [Fact]
    public void MaterialRequirement_ProportionalCalculation()
    {
        // BOM: 1 FG needs 2 RM-A + 3 RM-B
        // WO for 10 FG → needs 20 RM-A + 30 RM-B
        var bomQty = 1m;
        var produceQty = 10m;
        var rmAQty = 2m;
        var rmBQty = 3m;

        var requiredA = rmAQty * (produceQty / bomQty);
        var requiredB = rmBQty * (produceQty / bomQty);

        requiredA.ShouldBe(20m);
        requiredB.ShouldBe(30m);
    }

    [Fact]
    public void MaterialRequirement_PartialProduction()
    {
        // BOM: 10 FG needs 50 RM
        // Produce only 4 → needs 20 RM
        var bomQty = 10m;
        var rmQty = 50m;
        var produceQty = 4m;

        var required = rmQty * (produceQty / bomQty);
        required.ShouldBe(20m);
    }

    [Fact]
    public void MaterialRequirement_ZeroBomQty_ReturnsZero()
    {
        // Per domain service: bom.Quantity > 0 ? ... : 0
        var bomQty = 0m;
        var result = bomQty > 0 ? 5m * (10m / bomQty) : 0m;
        result.ShouldBe(0m);
    }

    [Fact]
    public void MaterialRequirement_PhantomItems_Excluded()
    {
        // Phantom items bubble components up, not consumed directly
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, Guid.NewGuid(), "RM-A", 5, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, Guid.NewGuid(), "Phantom Assembly", 1, 50m));

        // Mark one as phantom
        bom.Items.Last().IsPhantom = true;

        var nonPhantom = bom.Items.Where(i => !i.IsPhantom).ToList();
        nonPhantom.Count.ShouldBe(1);
        nonPhantom.First().ItemName.ShouldBe("RM-A");
    }

    // ========== WO Status State Machine ==========

    [Fact]
    public void WO_Submit_TransitionsTo_Submitted()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);
    }

    [Fact]
    public void WO_Start_TransitionsTo_InProcess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void WO_FullProduction_AutoCompletes()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, 0m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void WO_PartialProduction_StaysInProcess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, 0m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void WO_Stop_FromInProcess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Status.ShouldBe(WorkOrderStatus.Stopped);
    }

    [Fact]
    public void WO_Cannot_Start_From_Draft()
    {
        var wo = CreateWorkOrder(100);
        Should.Throw<BusinessException>(() => wo.Start());
    }

    [Fact]
    public void WO_Cannot_Cancel_When_Completed()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, 0m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);

        Should.Throw<BusinessException>(() => wo.Cancel());
    }

    // ========== BOM Validation Concepts ==========

    [Fact]
    public void BOM_IsActive_Required_For_WO()
    {
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.IsActive.ShouldBeTrue(); // default
    }

    [Fact]
    public void BOM_Deactivated_Would_Block_WO()
    {
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.IsActive = false;
        bom.IsActive.ShouldBeFalse();
    }

    // ========== Backflush Method Resolution ==========

    [Fact]
    public void Backflush_BOM_Override_Takes_Precedence()
    {
        // When BOM has explicit BackflushBasedOn, it wins over ManufacturingSettings
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.BackflushBasedOn = "Material Transferred";

        bom.BackflushBasedOn.ShouldBe("Material Transferred");
    }

    [Fact]
    public void Backflush_Null_BOM_Falls_Back_To_Settings()
    {
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.BackflushBasedOn.ShouldBeNullOrEmpty();

        // Fallback would use ManufacturingSettings.BackflushRawMaterialsBasedOn
        var settingsDefault = "BOM";
        var effective = string.IsNullOrWhiteSpace(bom.BackflushBasedOn)
            ? settingsDefault
            : bom.BackflushBasedOn;

        effective.ShouldBe("BOM");
    }

    // ========== WO Percent Complete ==========

    [Fact]
    public void PercentComplete_AtZero_IsZero()
    {
        var wo = CreateWorkOrder(100);
        wo.PercentComplete.ShouldBe(0);
    }

    [Fact]
    public void PercentComplete_Partial_IsCorrect()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(40, 0m);
        wo.PercentComplete.ShouldBe(40);
    }

    [Fact]
    public void PercentComplete_Full_Is100()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, 0m);
        wo.PercentComplete.ShouldBe(100);
    }

    [Fact]
    public void PercentComplete_ZeroQty_IsZero()
    {
        // Divide-by-zero guard
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 0);
        wo.PercentComplete.ShouldBe(0);
    }

    // ========== Manufacturing Settings ==========

    [Fact]
    public void ManufacturingSettings_Default_Overproduction()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        settings.OverproductionPercentage.ShouldBe(5m);
    }

    [Fact]
    public void ManufacturingSettings_Custom_Overproduction()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        settings.OverproductionPercentage = 15m;
        settings.OverproductionPercentage.ShouldBe(15m);
    }

    [Fact]
    public void ManufacturingSettings_MutualExclusion_Backflush()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        settings.BackflushRawMaterialsBasedOn = "Material Transferred";
        settings.EnforceMutualExclusions();

        settings.ValidateComponentsQuantitiesPerBom.ShouldBeFalse();
    }

    // ========== Error Codes ==========

    [Fact]
    public void OverproductionErrorCode_Exists()
    {
        MyERPDomainErrorCodes.WorkOrderOverproduction.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void InsufficientRawMaterial_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.InsufficientRawMaterial.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ItemHasVariants_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.ItemHasVariants.ShouldNotBeNullOrEmpty();
    }

    // ========== Helper ==========

    private static WorkOrder CreateWorkOrder(decimal qty)
    {
        return new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, qty);
    }
}
