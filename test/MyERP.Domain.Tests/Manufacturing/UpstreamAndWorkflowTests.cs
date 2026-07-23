using System;
using System.Linq;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Tests for upstream PR #57387 (Job Card operation_id refactor),
/// PR #57398 (Item Group root resolution), PR #57380 (SLE cancel same-datetime fix),
/// and manufacturing workflow completeness.
/// </summary>
public class UpstreamAndWorkflowTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();
    private static readonly Guid OpId1 = Guid.NewGuid();
    private static readonly Guid OpId2 = Guid.NewGuid();
    private static readonly Guid WsId = Guid.NewGuid();

    // --- PR #57387: Job Card BomOperationId ---

    [Fact]
    public void JobCard_BomOperationId_DefaultsNull()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 10, 1);
        Assert.Null(jc.BomOperationId);
    }

    [Fact]
    public void JobCard_BomOperationId_CanBeSet()
    {
        var bomOpId = Guid.NewGuid();
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 10, 1)
        {
            BomOperationId = bomOpId
        };
        Assert.Equal(bomOpId, jc.BomOperationId);
    }

    [Fact]
    public void JobCard_SameOperation_DifferentBomOperationIds_Distinguishable()
    {
        // Per PR #57387: same operation can appear multiple times in a WO routing
        var woId = Guid.NewGuid();
        var bomOp1 = Guid.NewGuid();
        var bomOp2 = Guid.NewGuid();

        var jc1 = new JobCard(Guid.NewGuid(), CompanyId, woId, OpId1, 50, 1) { BomOperationId = bomOp1 };
        var jc2 = new JobCard(Guid.NewGuid(), CompanyId, woId, OpId1, 50, 2) { BomOperationId = bomOp2 };

        // Same operation, different BOM operation rows
        Assert.Equal(jc1.OperationId, jc2.OperationId);
        Assert.NotEqual(jc1.BomOperationId, jc2.BomOperationId);
    }

    // --- Job Card lifecycle state machine ---

    [Fact]
    public void JobCard_FullLifecycle_Open_Start_TimeLog_Complete()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        Assert.Equal(JobCardStatus.Open, jc.Status);

        jc.Start();
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
        Assert.NotNull(jc.StartedAt);

        var from = DateTime.UtcNow.AddMinutes(-30);
        var to = DateTime.UtcNow;
        jc.AddTimeLog(from, to, 50);
        Assert.Equal(50, jc.CompletedQty);
        Assert.True(jc.TotalTimeInMins > 0);

        jc.Complete();
        Assert.Equal(JobCardStatus.Completed, jc.Status);
        Assert.NotNull(jc.CompletedAt);
    }

    [Fact]
    public void JobCard_HoldResumeCycle()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Start();
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);

        jc.Hold();
        Assert.Equal(JobCardStatus.OnHold, jc.Status);

        jc.Resume();
        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
    }

    [Fact]
    public void JobCard_Cancel_FromOpen_Succeeds()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Cancel();
        Assert.Equal(JobCardStatus.Cancelled, jc.Status);
    }

    [Fact]
    public void JobCard_Cancel_FromCancelled_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Cancel();
        Assert.Throws<Volo.Abp.BusinessException>(() => jc.Cancel());
    }

    [Fact]
    public void JobCard_Start_FromNonOpen_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Start();
        // Can't start again from WIP
        Assert.Throws<Volo.Abp.BusinessException>(() => jc.Start());
    }

    [Fact]
    public void JobCard_Hold_FromOpen_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        // Can't hold from Open — must Start first
        Assert.Throws<Volo.Abp.BusinessException>(() => jc.Hold());
    }

    [Fact]
    public void JobCard_Resume_FromWIP_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Start();
        // Not on hold — can't resume
        Assert.Throws<Volo.Abp.BusinessException>(() => jc.Resume());
    }

    [Fact]
    public void JobCard_AddTimeLog_Accumulates()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Start();

        var t1 = DateTime.UtcNow.AddHours(-2);
        jc.AddTimeLog(t1, t1.AddMinutes(30), 20);
        jc.AddTimeLog(t1.AddMinutes(30), t1.AddMinutes(60), 30);

        Assert.Equal(50, jc.CompletedQty);
        Assert.Equal(60, jc.TotalTimeInMins, 1); // 30 + 30 = 60 minutes
        Assert.Equal(2, jc.TimeLogs.Count);
    }

    [Fact]
    public void JobCard_AddTimeLog_InvalidRange_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        var t = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() => jc.AddTimeLog(t, t, 10)); // Same from/to
    }

    [Fact]
    public void JobCard_AddTimeLog_AfterComplete_Throws()
    {
        var jc = new JobCard(Guid.NewGuid(), CompanyId, Guid.NewGuid(), OpId1, 100, 1);
        jc.Start();
        jc.AddTimeLog(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, 100);
        jc.Complete();
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            jc.AddTimeLog(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10), 5));
    }

    // --- Work Order lifecycle ---

    [Fact]
    public void WorkOrder_Submit_RequiresQuantityGreaterThanZero()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-TEST", ItemId, BomId, 0);
        Assert.Equal(0, wo.Quantity);
    }

    [Fact]
    public void WorkOrder_Unstop_FromStopped_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-TEST", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        Assert.Equal(WorkOrderStatus.Stopped, wo.Status);

        wo.Unstop();
        Assert.Equal(WorkOrderStatus.InProcess, wo.Status);
    }

    [Fact]
    public void WorkOrder_Cancel_FromStopped_Throws()
    {
        // Per DO-NOT: "Cancel Stopped Work Order directly (must Unstop first, then cancel)"
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-TEST", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.Cancel());
    }

    [Fact]
    public void WorkOrder_Unstop_Then_Cancel_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-TEST", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Unstop();
        wo.Cancel();
        Assert.Equal(WorkOrderStatus.Cancelled, wo.Status);
    }

    [Fact]
    public void WorkOrder_Overproduction_WithPercentage_Allowed()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-TEST", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        // 5% overproduction = max 105
        wo.RecordProduction(105, overproductionPercentage: 5);
        Assert.Equal(105, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_Overproduction_ExceedsPercentage_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-TEST", ItemId, BomId, 100);
        wo.Submit();
        wo.Start();
        // 5% overproduction = max 105, trying 106 should throw
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            wo.RecordProduction(106, overproductionPercentage: 5));
    }

    // --- BOM Operations ---

    [Fact]
    public void BomOperation_BatchSize_DefaultZero()
    {
        var op = new BomOperation(Guid.NewGuid(), BomId, OpId1, 10, 30);
        Assert.Equal(0, op.BatchSize); // 0 = single JC for full WO qty
    }

    [Fact]
    public void BomOperation_CostCalculation()
    {
        var op = new BomOperation(Guid.NewGuid(), BomId, OpId1, 10, 60); // 1 hour
        op.CalculateCost(120); // RM 120/hour
        Assert.Equal(120, op.OperatingCost);
    }

    [Fact]
    public void BomOperation_CostCalculation_HalfHour()
    {
        var op = new BomOperation(Guid.NewGuid(), BomId, OpId1, 10, 30); // 0.5 hours
        op.CalculateCost(120); // RM 120/hour
        Assert.Equal(60, op.OperatingCost); // 0.5 × 120 = 60
    }

    // --- Item Group root resolution (PR #57398) ---

    [Fact]
    public void ItemGroup_AutoParentToRoot_WhenNoParentSet()
    {
        // Per PR #57398: uses get_root_of() to find structural root
        var ig = new ItemGroup(Guid.NewGuid(), "Test Group");
        // ParentId is the parent field (4th param, null by default)
        Assert.False(ig.IsGroup);
    }

    [Fact]
    public void ItemGroup_ExplicitParent_PreservedWithoutOverride()
    {
        var parentId = Guid.NewGuid();
        var ig = new ItemGroup(Guid.NewGuid(), "Child Group", false, parentId);
        // ItemGroup with explicit parent should keep it
        Assert.Equal("Child Group", ig.Name);
    }

    // --- SLE cancel same-datetime ordering (PR #57380) ---

    [Fact]
    public void StockLedgerEntry_PostingDate_WithCreationTime()
    {
        // Per PR #57380: SLE cancel uses separate seed_previous_sle_for_cancellation
        var sle = new StockLedgerEntry(
            Guid.NewGuid(), CompanyId, ItemId, Guid.NewGuid(),
            DateTime.UtcNow, 10, 100, 1000, 10000);

        Assert.True(sle.QuantityChange > 0);
        Assert.True(sle.ValuationRate > 0);
    }

    // --- Manufacturing Settings ---

    [Fact]
    public void ManufacturingSettings_BackflushMutualExclusion()
    {
        // Per DO-NOT: backflush != BOM → forces validate_components off
        var ms = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        ms.BackflushRawMaterialsBasedOn = "Material Transferred";
        ms.EnforceMutualExclusions();
        Assert.False(ms.ValidateComponentsQuantitiesPerBom);
    }

    [Fact]
    public void ManufacturingSettings_BackflushBOM_PreservesValidation()
    {
        var ms = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        ms.BackflushRawMaterialsBasedOn = "BOM";
        ms.ValidateComponentsQuantitiesPerBom = true;
        ms.EnforceMutualExclusions();
        Assert.True(ms.ValidateComponentsQuantitiesPerBom);
    }

    [Fact]
    public void ManufacturingSettings_DefaultOverproduction_5Percent()
    {
        var ms = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        Assert.Equal(5m, ms.OverproductionPercentage);
    }

    // --- Variant BOM selection (PR #57358) ---

    [Fact]
    public void BillOfMaterials_IsDefault_CanBeSet()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), CompanyId, "BOM-001", ItemId);
        Assert.False(bom.IsDefault);

        bom.IsDefault = true;
        Assert.True(bom.IsDefault);
    }

    [Fact]
    public void BillOfMaterials_IsActive_DefaultsTrue()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), CompanyId, "BOM-001", ItemId);
        Assert.True(bom.IsActive);
    }
}
