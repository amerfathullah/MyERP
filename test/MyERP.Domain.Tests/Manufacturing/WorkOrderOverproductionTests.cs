using System;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Manufacturing;

public class WorkOrderOverproductionTests
{
    private static WorkOrder CreateWO(decimal qty = 100m)
    {
        var wo = new WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), qty);
        wo.Submit();
        wo.Start();
        return wo;
    }

    [Fact]
    public void RecordProduction_WithinLimit_Succeeds()
    {
        var wo = CreateWO(100m);
        wo.RecordProduction(50m, overproductionPercentage: 10m);
        wo.ProducedQuantity.ShouldBe(50m);
    }

    [Fact]
    public void RecordProduction_AtExactQty_Completes()
    {
        var wo = CreateWO(100m);
        wo.RecordProduction(100m, overproductionPercentage: 0m);
        wo.ProducedQuantity.ShouldBe(100m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void RecordProduction_WithinOverproduction_Allowed()
    {
        var wo = CreateWO(100m);
        // 10% overproduction → max 110 units
        wo.RecordProduction(105m, overproductionPercentage: 10m);
        wo.ProducedQuantity.ShouldBe(105m);
    }

    [Fact]
    public void RecordProduction_ExceedsOverproduction_Throws()
    {
        var wo = CreateWO(100m);
        // 5% overproduction → max 105 units
        Should.Throw<BusinessException>(() =>
            wo.RecordProduction(110m, overproductionPercentage: 5m));
    }

    [Fact]
    public void RecordProduction_ZeroOverproduction_ExactLimit()
    {
        var wo = CreateWO(50m);
        // 0% → max = 50 exactly
        wo.RecordProduction(50m, overproductionPercentage: 0m);
        wo.ProducedQuantity.ShouldBe(50m);

        // Cannot produce even 1 more
        Should.Throw<BusinessException>(() =>
            wo.RecordProduction(1m, overproductionPercentage: 0m));
    }

    [Fact]
    public void RecordProduction_Progressive_CumulativeCheck()
    {
        var wo = CreateWO(100m);
        wo.RecordProduction(40m, overproductionPercentage: 10m); // Total: 40
        wo.RecordProduction(40m, overproductionPercentage: 10m); // Total: 80
        wo.RecordProduction(25m, overproductionPercentage: 10m); // Total: 105 (≤ 110)
        wo.ProducedQuantity.ShouldBe(105m);

        // Next batch would exceed 110
        Should.Throw<BusinessException>(() =>
            wo.RecordProduction(10m, overproductionPercentage: 10m)); // 105+10=115 > 110
    }

    [Fact]
    public void RecordProduction_DefaultOverproduction_ZeroIsNoOverproduction()
    {
        var wo = CreateWO(100m);
        // Default overproductionPercentage = 0
        wo.RecordProduction(100m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void RecordProduction_WithProcessLoss_CumulativeExceedsAllowance_Throws()
    {
        // Per ERPNext PR #58847: validate cumulative manufactured quantity including process loss
        var wo = CreateWO(100m);
        // Batch 1: produce 1 with 90 process loss (total 91 < 100, remains InProcess)
        wo.RecordProduction(1m, overproductionPercentage: 10m, processLoss: 90m);
        wo.ProducedQuantity.ShouldBe(1m);
        wo.ProcessLossQty.ShouldBe(90m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // Batch 2: produce 10 with 15 process loss (cumulative 91 + 25 = 116 > 110 allowance)
        Should.Throw<BusinessException>(() =>
            wo.RecordProduction(10m, overproductionPercentage: 10m, processLoss: 15m));
    }

    [Fact]
    public void RecordProduction_WithProcessLoss_CumulativeWithinAllowance_Succeeds()
    {
        // Per ERPNext PR #58847: 10% allowance on 100 allows up to 110 total manufactured
        var wo = CreateWO(100m);
        wo.RecordProduction(50m, overproductionPercentage: 10m, processLoss: 40m); // Total: 90
        wo.ProducedQuantity.ShouldBe(50m);
        wo.ProcessLossQty.ShouldBe(40m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // Next batch: 10 produced + 10 loss = 20 (cumulative 90 + 20 = 110 <= 110)
        wo.RecordProduction(10m, overproductionPercentage: 10m, processLoss: 10m);
        wo.ProducedQuantity.ShouldBe(60m);
        wo.ProcessLossQty.ShouldBe(50m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void ReverseProduction_DecrementsProducedAndProcessLoss_ReopensWorkOrder()
    {
        var wo = CreateWO(100m);
        wo.RecordProduction(80m, overproductionPercentage: 0m, processLoss: 20m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
        wo.ActualEndDate.ShouldNotBeNull();

        // Reverse 30 produced and 10 process loss (StockEntry cancel)
        wo.ReverseProduction(30m, processLoss: 10m);
        wo.ProducedQuantity.ShouldBe(50m);
        wo.ProcessLossQty.ShouldBe(10m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
        wo.ActualEndDate.ShouldBeNull();
    }

    [Fact]
    public void SetProcessLossQty_ExceedsOverproduction_Throws()
    {
        var wo = CreateWO(100m);
        wo.RecordProduction(60m, overproductionPercentage: 5m); // maxAllowed = 105
        Should.Throw<BusinessException>(() =>
            wo.SetProcessLossQty(50m, overproductionPercentage: 5m)); // 60 + 50 = 110 > 105
    }
}
