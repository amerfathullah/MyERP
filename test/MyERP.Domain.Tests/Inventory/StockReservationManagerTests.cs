using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class StockReservationManagerTests
{
    [Fact]
    public async Task ValidateOrResolveWarehouseAsync_NoReservations_ReturnsCurrentUnchanged()
    {
        var itemId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var currentWarehouseId = Guid.NewGuid();
        var manager = CreateManager(new List<StockReservationEntry>());

        var resolved = await manager.ValidateOrResolveWarehouseAsync(itemId, salesOrderId, currentWarehouseId);

        resolved.ShouldBe(currentWarehouseId);
    }

    [Fact]
    public async Task ValidateOrResolveWarehouseAsync_UnsetWarehouse_AutoResolvesFromReservation()
    {
        var itemId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var reservedWarehouseId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), itemId, reservedWarehouseId,
            "SalesOrder", salesOrderId, reservedQty: 10);
        sre.Submit();
        var manager = CreateManager(new List<StockReservationEntry> { sre });

        var resolved = await manager.ValidateOrResolveWarehouseAsync(itemId, salesOrderId, currentWarehouseId: null);

        resolved.ShouldBe(reservedWarehouseId);
    }

    [Fact]
    public async Task ValidateOrResolveWarehouseAsync_MatchingWarehouse_Passes()
    {
        var itemId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var reservedWarehouseId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), itemId, reservedWarehouseId,
            "SalesOrder", salesOrderId, reservedQty: 10);
        sre.Submit();
        var manager = CreateManager(new List<StockReservationEntry> { sre });

        var resolved = await manager.ValidateOrResolveWarehouseAsync(itemId, salesOrderId, reservedWarehouseId);

        resolved.ShouldBe(reservedWarehouseId);
    }

    [Fact]
    public async Task ValidateOrResolveWarehouseAsync_MismatchedWarehouse_Throws()
    {
        var itemId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var reservedWarehouseId = Guid.NewGuid();
        var wrongWarehouseId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), itemId, reservedWarehouseId,
            "SalesOrder", salesOrderId, reservedQty: 10);
        sre.Submit();
        var manager = CreateManager(new List<StockReservationEntry> { sre });

        await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateOrResolveWarehouseAsync(itemId, salesOrderId, wrongWarehouseId));
    }

    [Fact]
    public async Task ValidateOrResolveWarehouseAsync_IgnoresCancelledReservations()
    {
        var itemId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();
        var reservedWarehouseId = Guid.NewGuid();
        var sre = new StockReservationEntry(Guid.NewGuid(), Guid.NewGuid(), itemId, reservedWarehouseId,
            "SalesOrder", salesOrderId, reservedQty: 10);
        sre.Submit();
        sre.Cancel();
        var manager = CreateManager(new List<StockReservationEntry> { sre });

        // No active (Submitted) reservations left — current warehouse passes through unchanged.
        var otherWarehouseId = Guid.NewGuid();
        var resolved = await manager.ValidateOrResolveWarehouseAsync(itemId, salesOrderId, otherWarehouseId);

        resolved.ShouldBe(otherWarehouseId);
    }

    [Fact]
    public async Task ValidateAvailabilityAsync_IgnoresFutureStockEntries()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var voucherDate = DateTime.UtcNow.AddDays(-1);

        var pastSle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), itemId, warehouseId,
            voucherDate, quantityChange: 5, valuationRate: 10, balanceQuantity: 5,
            balanceValue: 50);

        var futureSle = new StockLedgerEntry(
            Guid.NewGuid(), Guid.NewGuid(), itemId, warehouseId,
            DateTime.UtcNow.AddDays(2), quantityChange: 10, valuationRate: 10, balanceQuantity: 15,
            balanceValue: 150);

        var manager = CreateManager(
            new List<StockReservationEntry>(),
            sles: new List<StockLedgerEntry> { pastSle, futureSle });

        // Requesting 5 as of voucherDate should pass
        await manager.ValidateAvailabilityAsync(itemId, warehouseId, requestedQty: 5, asOfDate: voucherDate);

        // Requesting 6 as of voucherDate should throw even though future balance is 15
        await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateAvailabilityAsync(itemId, warehouseId, requestedQty: 6, asOfDate: voucherDate));
    }

    private static DomainServices.StockReservationManager CreateManager(
        List<StockReservationEntry> entries,
        List<StockLedgerEntry>? sles = null)
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(entries.AsQueryable()));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();

        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(Task.FromResult((sles ?? new List<StockLedgerEntry>()).AsQueryable()));

        return new DomainServices.StockReservationManager(sreRepo, binRepo, sleRepo);
    }
}
