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

    [Fact]
    public async Task ReserveStockAsync_PreventsDuplicateReservationExceedingDemand()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var workOrderItemId = Guid.NewGuid();

        var bin = new Bin(Guid.NewGuid(), itemId, warehouseId) { ActualQty = 30m };
        var initialSre = new StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            "WorkOrder", workOrderId, reservedQty: 4m, voucherQty: 10m)
        {
            VoucherDetailId = workOrderItemId
        };
        initialSre.Submit();

        var sres = new List<StockReservationEntry> { initialSre };
        var manager = CreateManager(sres, bins: new List<Bin> { bin });

        // Demand is 10, 4 is already reserved. Remaining allowed = 6.
        // Trying to reserve 10 more (duplicate/exceeding requirement) must throw (ERPNext PR #59604)
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ReserveStockAsync(
                itemId, warehouseId, companyId,
                qty: 10m, voucherType: "WorkOrder", voucherId: workOrderId,
                voucherDemandQty: 10m, voucherDetailId: workOrderItemId));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        // Reserving remaining 6 must succeed
        await manager.ReserveStockAsync(
            itemId, warehouseId, companyId,
            qty: 6m, voucherType: "WorkOrder", voucherId: workOrderId,
            voucherDemandQty: 10m, voucherDetailId: workOrderItemId);
    }

    [Fact]
    public async Task ReserveStockAsync_CountsReservationsInOtherWarehouses_AgainstVoucherRowLimit()
    {
        var itemId = Guid.NewGuid();
        var warehouse1Id = Guid.NewGuid();
        var warehouse2Id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var workOrderId = Guid.NewGuid();
        var workOrderItemId = Guid.NewGuid();

        var bin1 = new Bin(Guid.NewGuid(), itemId, warehouse1Id) { ActualQty = 20m };
        var bin2 = new Bin(Guid.NewGuid(), itemId, warehouse2Id) { ActualQty = 20m };

        // 4 units already reserved in warehouse 1
        var initialSre = new StockReservationEntry(
            Guid.NewGuid(), companyId, itemId, warehouse1Id,
            "WorkOrder", workOrderId, reservedQty: 4m, voucherQty: 10m)
        {
            VoucherDetailId = workOrderItemId
        };
        initialSre.Submit();

        var sres = new List<StockReservationEntry> { initialSre };
        var manager = CreateManager(sres, bins: new List<Bin> { bin1, bin2 });

        // Reserving in warehouse 2 must still respect the 10m demand across all warehouses (PR #59604)
        // Requesting 10 in warehouse 2 must throw because 4 is already reserved in warehouse 1
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ReserveStockAsync(
                itemId, warehouse2Id, companyId,
                qty: 10m, voucherType: "WorkOrder", voucherId: workOrderId,
                voucherDemandQty: 10m, voucherDetailId: workOrderItemId));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

        // Requesting remaining 6 in warehouse 2 succeeds
        await manager.ReserveStockAsync(
            itemId, warehouse2Id, companyId,
            qty: 6m, voucherType: "WorkOrder", voucherId: workOrderId,
            voucherDemandQty: 10m, voucherDetailId: workOrderItemId);
    }

    private static DomainServices.StockReservationManager CreateManager(
        List<StockReservationEntry> entries,
        List<StockLedgerEntry>? sles = null,
        List<Bin>? bins = null)
    {
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(entries.AsQueryable()));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        binRepo.GetQueryableAsync().Returns(Task.FromResult((bins ?? new List<Bin>()).AsQueryable()));

        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(Task.FromResult((sles ?? new List<StockLedgerEntry>()).AsQueryable()));

        var manager = new DomainServices.StockReservationManager(sreRepo, binRepo, sleRepo);
        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>(Arg.Any<Func<IServiceProvider, object>>()).Returns(guidGen);
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        lazyProvider.LazyGetRequiredService(typeof(Volo.Abp.Guids.IGuidGenerator)).Returns(guidGen);
        manager.LazyServiceProvider = lazyProvider;

        return manager;
    }
}
