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

    [Fact]
    public async Task ValidateCanCancelAsync_UsedReservation_OnOpenOrClosedSubcontractingOrder_Throws()
    {
        var scoId = Guid.NewGuid();
        var sco = new Purchasing.Entities.SubcontractingOrder(scoId, CompanyId, "SCO-001", DateTime.UtcNow.Date, Guid.NewGuid());
        sco.AddItem(new Purchasing.Entities.SubcontractingOrderItem(Guid.NewGuid(), scoId, ItemId, "FG", 10m, 50m));
        sco.Submit();

        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Subcontracting Order", scoId, 10m);
        sre.Submit();
        sre.ConsumedQty = 3m;

        var scoRepo = Substitute.For<IRepository<Purchasing.Entities.SubcontractingOrder, Guid>>();
        scoRepo.FindAsync(scoId).Returns(Task.FromResult<Purchasing.Entities.SubcontractingOrder?>(sco));

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        var manager = new StockReservationManager(sreRepo, null!, null!);

        // Open SCO -> throws (commit 4bf8b4ded2)
        var exOpen = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCanCancelAsync(sre, null, scoRepo));
        exOpen.Code.ShouldBe(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled);

        // Closed SCO -> still throws because a closed SCO can be re-opened (commit 719a5cdd31)
        sco.Close();
        var exClosed = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateCanCancelAsync(sre, null, scoRepo));
        exClosed.Code.ShouldBe(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled);

        // Reopen and cancel SCO -> allowed on cancelled SCO
        sco.Reopen();
        sco.Cancel();
        await manager.ValidateCanCancelAsync(sre, null, scoRepo);
    }

    [Fact]
    public async Task TransferReservationEntriesAsync_SubcontractingOrder_UsesVoucherDemandQtyAndBatchFirst()
    {
        var planId = Guid.NewGuid();
        var scoId = Guid.NewGuid();
        var scoRowId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var qtySre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "ProductionPlan", planId, 5m)
        {
            CreationTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        qtySre.Submit();

        var batchSre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "ProductionPlan", planId, 4m)
        {
            BatchId = batchId,
            CreationTime = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        batchSre.Submit();

        var inserted = new List<StockReservationEntry>();
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { qtySre, batchSre }.AsQueryable()));
        sreRepo.InsertAsync(Arg.Do<StockReservationEntry>(x => inserted.Add(x))).Returns(ci => Task.FromResult(ci.Arg<StockReservationEntry>()));

        var manager = new StockReservationManager(sreRepo, null!, null!);

        // Transfer 6 units to SCO row with total required demand of 10 (commits 75c8bae283 & 3d4ca309aa)
        await manager.TransferReservationEntriesAsync(
            "ProductionPlan", planId,
            "Subcontracting Order", scoId,
            ItemId, WarehouseId,
            qty: 6m,
            toVoucherDetailId: scoRowId,
            voucherDemandQty: 10m);

        // Batch SRE (4 units) transferred first, then 2 units from qty SRE
        batchSre.TransferredQty.ShouldBe(4m);
        qtySre.TransferredQty.ShouldBe(2m);

        inserted.Count.ShouldBe(2);
        inserted[0].BatchId.ShouldBe(batchId);
        inserted[0].ReservedQty.ShouldBe(4m);
        inserted[0].VoucherQty.ShouldBe(10m);
        inserted[1].BatchId.ShouldBeNull();
        inserted[1].ReservedQty.ShouldBe(2m);
        inserted[1].VoucherQty.ShouldBe(10m);
    }

    [Fact]
    public async Task ApplyAndRevertSubcontractingConsumptionAsync_UpdatesAndRevertsConsumedQty()
    {
        var scoId = Guid.NewGuid();
        var supplierWhId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, supplierWhId, "Subcontracting Order", scoId, 10m);
        sre.Submit();

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { sre }.AsQueryable()));

        var manager = new StockReservationManager(sreRepo, null!, null!);

        var consumed = await manager.ApplySubcontractingConsumptionAsync(scoId, ItemId, supplierWhId, 5m);
        consumed.Length.ShouldBe(1);
        sre.ConsumedQty.ShouldBe(5m);

        await manager.RevertSubcontractingConsumptionAsync(scoId, ItemId, supplierWhId, 5m);
        sre.ConsumedQty.ShouldBe(0m);
    }

    [Fact]
    public async Task RevertSubcontractingTransferAsync_ReopensBatchMatchedLifoReservationFirst()
    {
        var scoId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var sre1 = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Subcontracting Order", scoId, 6m)
        {
            BatchId = batchId,
            CreationTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        sre1.Submit();
        sre1.TransferredQty = 6m;

        var sre2 = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "Subcontracting Order", scoId, 4m)
        {
            BatchId = batchId,
            CreationTime = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        sre2.Submit();
        sre2.TransferredQty = 4m;

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { sre1, sre2 }.AsQueryable()));

        var manager = new StockReservationManager(sreRepo, null!, null!);

        await manager.RevertSubcontractingTransferAsync(scoId, ItemId, WarehouseId, 4m, batchId);

        sre1.TransferredQty.ShouldBe(6m);
        sre2.TransferredQty.ShouldBe(0m);
    }

    [Fact]
    public async Task ApplyWorkOrderTransferAsync_PrioritizesSerialAndBatchReservationBeforeQtyReservation()
    {
        var woId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var olderQtySre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "WorkOrder", woId, 5m)
        {
            CreationTime = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        olderQtySre.Submit();

        var newerBundleSre = new StockReservationEntry(Guid.NewGuid(), CompanyId, ItemId, WarehouseId, "WorkOrder", woId, 4m)
        {
            SerialAndBatchBundleId = bundleId,
            CreationTime = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        newerBundleSre.Submit();

        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(new[] { olderQtySre, newerBundleSre }.AsQueryable()));

        var manager = new StockReservationManager(sreRepo, null!, null!);

        // Per ERPNext commit 75c8bae283: serial/batch reservations must be matched before qty-only ones even if newer
        await manager.ApplyWorkOrderTransferAsync(woId, ItemId, WarehouseId, 4m);

        newerBundleSre.TransferredQty.ShouldBe(4m);
        olderQtySre.TransferredQty.ShouldBe(0m);
    }
}
