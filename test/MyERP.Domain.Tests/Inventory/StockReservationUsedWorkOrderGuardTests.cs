using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for Work Order stock reservation guards and closed Work Order holding reservations.
/// Per ERPNext PR #60510 (commits b7e3383f5c, 2bee21840a, eecf91e143, 99ea89af89).
/// </summary>
public class StockReservationUsedWorkOrderGuardTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    [Fact]
    public async Task ValidateCanCancelAsync_UsedReservation_OnOpenWorkOrder_Throws()
    {
        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, CompanyId, "WO-001", ItemId, Guid.NewGuid(), 10m);
        wo.Submit();

        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Work Order", woId, 10m);
        sre.Submit();
        sre.TransferredQty = 4m;

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var manager = new StockReservationManager(sreRepo, null!, null!);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCanCancelAsync(sre, woRepo));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled);
    }

    [Fact]
    public async Task ValidateCanCancelAsync_UsedReservation_OnClosedWorkOrder_Allowed()
    {
        var woId = Guid.NewGuid();
        var wo = new WorkOrder(woId, CompanyId, "WO-001", ItemId, Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Close();

        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Work Order", woId, 10m);
        sre.Submit();
        sre.TransferredQty = 4m;

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var manager = new StockReservationManager(sreRepo, null!, null!);

        // Should not throw when WO is Closed
        await manager.ValidateCanCancelAsync(sre, woRepo);
    }

    [Fact]
    public async Task CancelReservationsForVoucherAsync_PartlyTransferred_TrimsReservedQtyAndReleasesUnusedFromBin()
    {
        var woId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Work Order", woId, 10m);
        sre.Submit();
        sre.TransferredQty = 4m;

        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId, CompanyId)
        {
            ReservedQty = 10m
        };

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { sre }.AsQueryable()));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { bin }.AsQueryable()));

        var manager = new StockReservationManager(sreRepo, binRepo, null!);

        await manager.CancelReservationsForVoucherAsync(woId);

        // ReservedQty cut to used qty (4)
        sre.ReservedQty.ShouldBe(4m);
        sre.Status.ShouldBe(DocumentStatus.Submitted);

        // Bin has unused balance (6) deducted -> 4 remaining
        bin.ReservedQty.ShouldBe(4m);
    }

    [Fact]
    public async Task CancelReservationsForVoucherAsync_ZeroUsage_CancelsAndReleasesAllFromBin()
    {
        var woId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Work Order", woId, 10m);
        sre.Submit();

        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId, CompanyId)
        {
            ReservedQty = 10m
        };

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { sre }.AsQueryable()));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { bin }.AsQueryable()));

        var manager = new StockReservationManager(sreRepo, binRepo, null!);

        await manager.CancelReservationsForVoucherAsync(woId);

        // Reservation fully cancelled
        sre.Status.ShouldBe(DocumentStatus.Cancelled);

        // Bin has full 10 deducted -> 0
        bin.ReservedQty.ShouldBe(0m);
    }

    [Fact]
    public async Task ApplyWorkOrderConsumptionAsync_OnClosedWorkOrderHoldingReservation_ConsumesRemaining()
    {
        var woId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Work Order", woId, 10m);
        sre.Submit();
        sre.TransferredQty = 10m;
        sre.ConsumedQty = 4m;

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { sre }.AsQueryable()));

        var manager = new StockReservationManager(sreRepo, null!, null!);

        // Consume remaining 6 against holding reservation
        var consumed = await manager.ApplyWorkOrderConsumptionAsync(woId, ItemId, WarehouseId, 6m);

        consumed.Length.ShouldBe(1);
        consumed[0].ConsumedQty.ShouldBe(6m);
        sre.ConsumedQty.ShouldBe(10m);
    }
}
