using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkOrderSemiFinishedProcessLossTests
{
    [Fact]
    public void SetProcessLossQty_WhenProducedPlusProcessLossCoversQuantity_CompletesWorkOrder()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            TrackSemiFinishedGoods = true
        };
        wo.Submit();
        wo.Start();

        // Produce 8 units
        wo.RecordProduction(8m, overproductionPercentage: 0m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
        wo.ProducedQuantity.ShouldBe(8m);

        // Process loss of 2 units on Job Card / operations
        wo.SetProcessLossQty(2m);

        wo.ProcessLossQty.ShouldBe(2m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
        wo.ActualEndDate.ShouldNotBeNull();
    }

    [Fact]
    public void SetProcessLossQty_WhenSumIsLessThanQuantity_RemainsInProcess()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            TrackSemiFinishedGoods = true
        };
        wo.Submit();
        wo.Start();
        wo.RecordProduction(5m, overproductionPercentage: 0m);

        wo.SetProcessLossQty(1m);

        wo.ProcessLossQty.ShouldBe(1m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }
}
