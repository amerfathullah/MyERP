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
}
