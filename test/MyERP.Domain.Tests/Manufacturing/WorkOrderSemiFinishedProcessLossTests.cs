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

    [Fact]
    public void CalculateTransferredBatchConsumption_IgnoresSemiFinishedOutputRows_PR61500()
    {
        var service = new Services.WorkOrderProductionService(NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<WorkOrder, Guid>>());
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var wipWh = Guid.NewGuid();
        var fgWh = Guid.NewGuid();

        var transferred = new[]
        {
            new Inventory.Entities.StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), itemId, 10m, sourceWarehouseId: Guid.NewGuid(), targetWarehouseId: wipWh)
            {
                BatchId = batchId
            }
        };

        // Semi-finished output row has TargetWarehouseId set and IsFinishedItem == false (e.g. intermediate output)
        // Only the genuine consumption row (SourceWarehouseId set, TargetWarehouseId null) should be deducted.
        var priorConsumed = new[]
        {
            new Inventory.Entities.StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), itemId, 4m, sourceWarehouseId: wipWh, targetWarehouseId: null)
            {
                BatchId = batchId
            },
            new Inventory.Entities.StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), itemId, 6m, sourceWarehouseId: null, targetWarehouseId: fgWh)
            {
                BatchId = batchId,
                IsFinishedItem = false
            }
        };

        var picks = service.CalculateTransferredBatchConsumption(itemId, 6m, transferred, priorConsumed);
        picks.ShouldHaveSingleItem();
        picks[0].BatchId.ShouldBe(batchId);
        picks[0].Quantity.ShouldBe(6m);
    }
}
