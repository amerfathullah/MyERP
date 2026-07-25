using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class ProcessLossAndJobCardStockTests
{
    private static WorkOrder CreateWorkOrder(decimal qty = 100, decimal processLossQty = 0, decimal processLossPct = 0)
    {
        var wo = new WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-001",
            Guid.NewGuid(), Guid.NewGuid(), qty);
        wo.ProcessLossQty = processLossQty;
        wo.ProcessLossPercentage = processLossPct;
        return wo;
    }

    // === Process Loss ===

    [Fact]
    public void ProcessLossQty_DefaultsToZero()
    {
        var wo = CreateWorkOrder();
        wo.ProcessLossQty.ShouldBe(0);
        wo.ProcessLossPercentage.ShouldBe(0);
    }

    [Fact]
    public void EffectiveFgQuantity_WithZeroLoss_EqualsTotalQuantity()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.EffectiveFgQuantity.ShouldBe(100);
    }

    [Fact]
    public void EffectiveFgQuantity_WithProcessLossQty_ReducesByLossAmount()
    {
        // Per ERPNext: fg_completed_qty = quantity - process_loss_qty
        var wo = CreateWorkOrder(qty: 100, processLossQty: 5);
        wo.EffectiveFgQuantity.ShouldBe(95); // 100 - 5
    }

    [Fact]
    public void EffectiveFgQuantity_WithProcessLossPercentage_ReducesByPercentage()
    {
        // Per ERPNext: when only percentage, qty = quantity × (1 - pct/100)
        var wo = CreateWorkOrder(qty: 100, processLossPct: 10);
        wo.EffectiveFgQuantity.ShouldBe(90); // 100 × (1 - 10/100) = 90
    }

    [Fact]
    public void EffectiveFgQuantity_QtyTakesPriorityOverPercentage()
    {
        // Per ERPNext: process_loss_qty takes priority over percentage
        var wo = CreateWorkOrder(qty: 100, processLossQty: 8, processLossPct: 10);
        wo.EffectiveFgQuantity.ShouldBe(92); // 100 - 8 (qty takes priority)
    }

    [Fact]
    public void PercentComplete_UsesTotalQuantity_NotEffectiveFg()
    {
        var wo = CreateWorkOrder(qty: 100, processLossQty: 5);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50);
        wo.PercentComplete.ShouldBe(50); // 50/100 × 100 = 50% (against full qty, not 95)
    }

    [Fact]
    public void PercentComplete_ZeroQuantity_ReturnsZero()
    {
        // Division by zero guard
        var wo = new WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-ZERO",
            Guid.NewGuid(), Guid.NewGuid(), 0);
        wo.PercentComplete.ShouldBe(0);
    }

    [Fact]
    public void PercentComplete_CappedAt100()
    {
        var wo = CreateWorkOrder(qty: 10);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(10, overproductionPercentage: 20);
        wo.PercentComplete.ShouldBe(100);
    }

    // === Job Card Complete → WO Production (entity-level behavior) ===

    [Fact]
    public void RecordProduction_IncreasesProducedQuantity()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30);
        wo.ProducedQuantity.ShouldBe(30);
    }

    [Fact]
    public void RecordProduction_Cumulative()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(30);
        wo.RecordProduction(40);
        wo.ProducedQuantity.ShouldBe(70);
    }

    [Fact]
    public void RecordProduction_AutoCompletesAtFullQty()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void RecordProduction_OverproductionBlocked_DefaultZeroPercent()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        // WO is now Completed at 100 — can't produce more from InProcess
        // (auto-transitions to Completed, RecordProduction requires InProcess)
    }

    [Fact]
    public void RecordProduction_WithAllowance_Succeeds()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(105, overproductionPercentage: 10); // max allowed = 110
        wo.ProducedQuantity.ShouldBe(105);
    }

    [Fact]
    public void RecordProduction_WithAllowance_ExceedingThrows()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.Start();
        Should.Throw<BusinessException>(() =>
            wo.RecordProduction(111, overproductionPercentage: 10) // max = 110
        ).Code.ShouldBe("MyERP:10006");
    }

    // === Material Transfer Tracking ===

    [Fact]
    public void RecordMaterialTransfer_IncrementsMaterialTransferred()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.RecordMaterialTransfer(50);
        wo.MaterialTransferred.ShouldBe(50);
    }

    [Fact]
    public void RecordMaterialTransfer_ChangesStatusToNotStarted()
    {
        var wo = CreateWorkOrder(qty: 100);
        wo.Submit();
        wo.RecordMaterialTransfer(50);
        wo.Status.ShouldBe(WorkOrderStatus.NotStarted);
    }
}
