using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class WorkOrderBatchReservationAndPlanOffsetTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseA = Guid.NewGuid();
    private readonly Guid _warehouseB = Guid.NewGuid();
    private readonly Guid _warehouseC = Guid.NewGuid();

    // =========================================================================
    // ERPNext PR #59429 / commit a710111db9:
    // Offset plan reservations across warehouses
    // =========================================================================

    [Fact]
    public void CalculateRemainingReservedQty_DirectMatch_ReducesOnlyMatchedWarehouse()
    {
        // Plan: A: 6, B: 4 (Total 10)
        // Work Order: A: 5
        var plan = new Dictionary<Guid, decimal> { [_warehouseA] = 6m, [_warehouseB] = 4m };
        var wo = new Dictionary<Guid, decimal> { [_warehouseA] = 5m };

        var remainingA = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseA);
        var remainingB = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseB);

        remainingA.ShouldBe(1m);
        remainingB.ShouldBe(4m);
    }

    [Fact]
    public void CalculateRemainingReservedQty_UnmatchedWarehouse_OffsetsProportionally()
    {
        // Plan: A: 6, B: 4 (Total 10)
        // Work Order: C: 5 (unmatched warehouse)
        // Proportional distribution:
        // A remaining: 6 - (6 * 5 / 10) = 3
        // B remaining: 4 - (4 * 5 / 10) = 2
        var plan = new Dictionary<Guid, decimal> { [_warehouseA] = 6m, [_warehouseB] = 4m };
        var wo = new Dictionary<Guid, decimal> { [_warehouseC] = 5m };

        var remainingA = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseA);
        var remainingB = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseB);

        remainingA.ShouldBe(3m);
        remainingB.ShouldBe(2m);
    }

    [Fact]
    public void CalculateRemainingReservedQty_OverAllocatedWarehouse_OffsetsRemainingWarehouses()
    {
        // Plan: A: 6, B: 4 (Total 10)
        // Work Order: A: 8 (exceeds A by 2)
        // A remaining: 0
        // Excess 2 distributed over B (4 remaining):
        // B remaining: 4 - (4 * 2 / 4) = 2
        var plan = new Dictionary<Guid, decimal> { [_warehouseA] = 6m, [_warehouseB] = 4m };
        var wo = new Dictionary<Guid, decimal> { [_warehouseA] = 8m };

        var remainingA = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseA);
        var remainingB = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseB);

        remainingA.ShouldBe(0m);
        remainingB.ShouldBe(2m);
    }

    [Fact]
    public void CalculateRemainingReservedQty_ExcessDemandExceedsPlan_ReturnsZero()
    {
        // Plan: A: 6, B: 4 (Total 10)
        // Work Order: C: 20 (exceeds total plan)
        var plan = new Dictionary<Guid, decimal> { [_warehouseA] = 6m, [_warehouseB] = 4m };
        var wo = new Dictionary<Guid, decimal> { [_warehouseC] = 20m };

        var remainingA = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseA);
        var remainingB = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseB);

        remainingA.ShouldBe(0m);
        remainingB.ShouldBe(0m);
    }

    [Fact]
    public void CalculateRemainingReservedQty_SingleWarehousePlan_OffsetsCorrectly()
    {
        // Plan: A: 5
        // Work Order: B: 4
        // Remaining A: 5 - (5 * 4 / 5) = 1
        var plan = new Dictionary<Guid, decimal> { [_warehouseA] = 5m };
        var wo = new Dictionary<Guid, decimal> { [_warehouseB] = 4m };

        var remainingA = ProductionPlanReservationService.CalculateRemainingReservedQty(plan, wo, _warehouseA);
        remainingA.ShouldBe(1m);
    }

    // =========================================================================
    // ERPNext PR #59424 / commit dbada3f461:
    // Count only matched batches on work order reservations
    // =========================================================================

    [Fact]
    public async Task Transfer_Of_Other_Batch_Keeps_Reservation_Open()
    {
        var reservedBatchId = Guid.NewGuid();
        var otherBatchId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var sre = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseA,
            "WorkOrder", workOrderId, 50m)
        {
            BatchId = reservedBatchId
        };
        sre.Submit();

        var sres = new List<StockReservationEntry> { sre };
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(sres.AsQueryable()));
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();

        var manager = new StockReservationManager(sreRepo, binRepo, sleRepo);

        // 1. Transfer of a DIFFERENT batch: should NOT consume the reservation
        var consumedOther = await manager.ApplyWorkOrderTransferAsync(
            workOrderId, _itemId, _warehouseA, 50m, batchId: otherBatchId);

        consumedOther.ShouldBeEmpty();
        sre.TransferredQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(50m);
        sre.Status.ShouldBe(DocumentStatus.Submitted);

        // 2. Transfer of the MATCHING batch: should consume the reservation
        var consumedMatched = await manager.ApplyWorkOrderTransferAsync(
            workOrderId, _itemId, _warehouseA, 50m, batchId: reservedBatchId);

        consumedMatched.Length.ShouldBe(1);
        consumedMatched[0].ConsumedQty.ShouldBe(50m);
        sre.TransferredQty.ShouldBe(50m);
        sre.AvailableQty.ShouldBe(0m);

        // 3. Reverting the transfer: should restore the reservation
        await manager.RevertWorkOrderTransferAsync(
            workOrderId, _itemId, _warehouseA, 50m, batchId: reservedBatchId);

        sre.TransferredQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(50m);
    }

    [Fact]
    public async Task Material_Consumption_Of_Other_Batch_Keeps_Reservation_Open()
    {
        var reservedBatchId = Guid.NewGuid();
        var otherBatchId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();

        var sre = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseA,
            "WorkOrder", workOrderId, 30m)
        {
            BatchId = reservedBatchId
        };
        sre.Submit();

        var sres = new List<StockReservationEntry> { sre };
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(sres.AsQueryable()));
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();

        var manager = new StockReservationManager(sreRepo, binRepo, sleRepo);

        // Mismatched batch: keeps reservation open
        var consumedOther = await manager.ApplyWorkOrderConsumptionAsync(
            workOrderId, _itemId, _warehouseA, 30m, batchId: otherBatchId);

        consumedOther.ShouldBeEmpty();
        sre.ConsumedQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(30m);

        // Matching batch: consumes
        var consumedMatched = await manager.ApplyWorkOrderConsumptionAsync(
            workOrderId, _itemId, _warehouseA, 30m, batchId: reservedBatchId);

        consumedMatched.Length.ShouldBe(1);
        consumedMatched[0].ConsumedQty.ShouldBe(30m);
        sre.ConsumedQty.ShouldBe(30m);
        sre.AvailableQty.ShouldBe(0m);

        // Revert consumption: restores
        await manager.RevertWorkOrderConsumptionAsync(
            workOrderId, _itemId, _warehouseA, 30m, batchId: reservedBatchId);

        sre.ConsumedQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(30m);
    }

    [Fact]
    public void GetHeldQty_AccuratelyDeductsTransferredAndConsumedQuantities()
    {
        var sre = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseA,
            "WorkOrder", Guid.NewGuid(), 100m);

        StockReservationManager.GetHeldQty(sre).ShouldBe(100m);

        sre.DeliveredQty = 20m;
        StockReservationManager.GetHeldQty(sre).ShouldBe(80m);

        sre.TransferredQty = 30m;
        StockReservationManager.GetHeldQty(sre).ShouldBe(50m);

        sre.ConsumedQty = 40m;
        StockReservationManager.GetHeldQty(sre).ShouldBe(10m);

        sre.ConsumedQty = 55m; // exceeds remaining
        StockReservationManager.GetHeldQty(sre).ShouldBe(0m);
    }
}
