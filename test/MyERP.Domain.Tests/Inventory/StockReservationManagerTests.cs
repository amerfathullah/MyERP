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

    private static DomainServices.StockReservationManager CreateManager(List<StockReservationEntry> entries)
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(entries.AsQueryable()));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();

        return new DomainServices.StockReservationManager(sreRepo, binRepo);
    }
}
