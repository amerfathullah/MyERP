using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkOrderRequiredItemsAndBatchJobCardTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    [Fact]
    public void DistributeConsumedQuantity_SplitsConsumptionAcrossMultipleRowsOfSameItem()
    {
        // Per ERPNext PR #60647 / commits 8c663d9608, 4d809d375a, 260b828b09:
        // Multiple required items rows for same raw material receive proportional consumption
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _fgItemId, _bomId, 4m);

        var row1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, _rmItemId, "Raw Material", 4m)
        {
            SourceWarehouseId = _warehouseId
        };
        var row2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, _rmItemId, "Raw Material", 4m)
        {
            SourceWarehouseId = _warehouseId
        };
        wo.RequiredItems.Add(row1);
        wo.RequiredItems.Add(row2);

        // Consume 4 units total
        WorkOrderManager.DistributeConsumedQuantity(wo, _rmItemId, _warehouseId, 4m, isDeduction: false);

        row1.ConsumedQuantity.ShouldBe(2m);
        row2.ConsumedQuantity.ShouldBe(2m);
        (row1.ConsumedQuantity + row2.ConsumedQuantity).ShouldBe(4m);
    }

    [Fact]
    public void DistributeConsumedQuantity_WithUnequalRequiredQty_DistributesProportionally()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", _fgItemId, _bomId, 4m);

        var row1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, _rmItemId, "Raw Material", 3m)
        {
            SourceWarehouseId = _warehouseId
        };
        var row2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, _rmItemId, "Raw Material", 1m)
        {
            SourceWarehouseId = _warehouseId
        };
        wo.RequiredItems.Add(row1);
        wo.RequiredItems.Add(row2);

        // Consume 2 units total: row1 gets 3/4 * 2 = 1.5, row2 gets remaining 0.5
        WorkOrderManager.DistributeConsumedQuantity(wo, _rmItemId, _warehouseId, 2m, isDeduction: false);

        row1.ConsumedQuantity.ShouldBe(1.5m);
        row2.ConsumedQuantity.ShouldBe(0.5m);
    }

    [Fact]
    public void DistributeConsumedQuantity_Deduction_ReducesConsumedQuantity()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-003", _fgItemId, _bomId, 4m);

        var row1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, _rmItemId, "Raw Material", 4m)
        {
            SourceWarehouseId = _warehouseId,
            ConsumedQuantity = 2m
        };
        var row2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, _rmItemId, "Raw Material", 4m)
        {
            SourceWarehouseId = _warehouseId,
            ConsumedQuantity = 2m
        };
        wo.RequiredItems.Add(row1);
        wo.RequiredItems.Add(row2);

        // Revert 4 units on cancel
        WorkOrderManager.DistributeConsumedQuantity(wo, _rmItemId, _warehouseId, 4m, isDeduction: true);

        row1.ConsumedQuantity.ShouldBe(0m);
        row2.ConsumedQuantity.ShouldBe(0m);
    }

    [Fact]
    public async Task CreateJobCardsFromRouting_PreservesOperationSequenceId_OnAllBatchJobCards()
    {
        // Per ERPNext PR #60650 / commit ce93fa10b5:
        // Batch job cards for the same operation retain the operation's sequence ID
        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        var wsRepo = Substitute.For<IRepository<Workstation, Guid>>();
        var manager = new JobCardManager(jcRepo, wsRepo);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-004", _fgItemId, _bomId, 10m);

        var routing = new Routing(Guid.NewGuid(), "Routing 1");
        var op1Id = Guid.NewGuid();
        var op2Id = Guid.NewGuid();

        // Op 1: SequenceId = 1, batch size = 5 (will produce 2 job cards)
        routing.AddOperation(op1Id, sequenceId: 1, timeInMins: 20m);
        routing.Operations[0].BatchSize = 5m;

        // Op 2: SequenceId = 2, batch size = 10 (will produce 1 job card)
        routing.AddOperation(op2Id, sequenceId: 2, timeInMins: 30m);
        routing.Operations[1].BatchSize = 10m;

        var jobCards = await manager.CreateJobCardsFromWorkOrderAsync(wo, routing);

        jobCards.Length.ShouldBe(3);

        // Op 1 split cards both have SequenceId = 1
        jobCards[0].OperationId.ShouldBe(op1Id);
        jobCards[0].SequenceId.ShouldBe(1);
        jobCards[0].ForQuantity.ShouldBe(5m);

        jobCards[1].OperationId.ShouldBe(op1Id);
        jobCards[1].SequenceId.ShouldBe(1);
        jobCards[1].ForQuantity.ShouldBe(5m);

        // Op 2 card has SequenceId = 2
        jobCards[2].OperationId.ShouldBe(op2Id);
        jobCards[2].SequenceId.ShouldBe(2);
        jobCards[2].ForQuantity.ShouldBe(10m);
    }

    [Fact]
    public async Task CreateJobCardsFromBomOperations_PreservesOperationSequenceId_OnAllBatchJobCards()
    {
        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        var wsRepo = Substitute.For<IRepository<Workstation, Guid>>();
        var manager = new JobCardManager(jcRepo, wsRepo);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-005", _fgItemId, _bomId, 6m);

        var op1Id = Guid.NewGuid();
        var op2Id = Guid.NewGuid();

        var bomOps = new[]
        {
            new BomOperation(Guid.NewGuid(), _bomId, op1Id, sequenceId: 1, timeInMins: 15m, workstationId: Guid.NewGuid())
            {
                BatchSize = 3 // 2 job cards of 3 each
            },
            new BomOperation(Guid.NewGuid(), _bomId, op2Id, sequenceId: 2, timeInMins: 25m, workstationId: Guid.NewGuid())
            {
                BatchSize = 6 // 1 job card of 6
            }
        };

        var jobCards = await manager.CreateJobCardsFromBomOperationsAsync(wo, bomOps);

        jobCards.Length.ShouldBe(3);

        jobCards[0].OperationId.ShouldBe(op1Id);
        jobCards[0].SequenceId.ShouldBe(1);
        jobCards[0].ForQuantity.ShouldBe(3m);

        jobCards[1].OperationId.ShouldBe(op1Id);
        jobCards[1].SequenceId.ShouldBe(1);
        jobCards[1].ForQuantity.ShouldBe(3m);

        jobCards[2].OperationId.ShouldBe(op2Id);
        jobCards[2].SequenceId.ShouldBe(2);
        jobCards[2].ForQuantity.ShouldBe(6m);
    }
}
