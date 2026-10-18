using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class ReservedStockReportAppServiceTests
{
    private readonly IRepository<StockReservationEntry, Guid> _reservationRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();

    private readonly ReservedStockReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _soId = Guid.NewGuid();

    public ReservedStockReportAppServiceTests()
    {
        _appService = new ReservedStockReportAppService(
            _reservationRepo,
            _binRepo,
            _itemRepo,
            _warehouseRepo,
            _batchRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsReservedStockRows_WithAvailableQtyFromBin()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-RESERVE-01", "Precision Motor", ItemType.Goods)
        {
            MaintainStock = true,
            Uom = "Nos"
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var warehouse = new Warehouse(_warehouseId, _companyId, "Finished Goods WH")
        {
            IsGroup = false,
            IsActive = true
        };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        var batch = new Batch(_batchId, _itemId, "BATCH-MTR-01");
        _batchRepo.GetQueryableAsync().Returns(new List<Batch> { batch }.AsQueryable());

        var bin = new Bin(Guid.NewGuid(), _itemId, _warehouseId)
        {
            ActualQty = 100m,
            ReservedQty = 40m
        };
        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin }.AsQueryable());

        var reservation = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, "Sales Order", _soId, 40m, 50m)
        {
            BatchId = _batchId,
            TransferredQty = 5m,
            ConsumedQty = 2m
        };
        reservation.Submit();
        reservation.RecordDelivery(10m);
        typeof(StockReservationEntry).GetProperty("CreationTime")!.SetValue(reservation, DateTime.UtcNow);

        _reservationRepo.GetQueryableAsync().Returns(new List<StockReservationEntry> { reservation }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new ReservedStockFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalEntries.ShouldBe(1);
        result.TotalReservedQty.ShouldBe(40m);
        result.TotalDeliveredQty.ShouldBe(10m);
        result.TotalTransferredQty.ShouldBe(5m);
        result.TotalConsumedQty.ShouldBe(2m);
        result.TotalAvailableQty.ShouldBe(60m); // 100 - 40

        var row = result.Rows.ShouldHaveSingleItem();
        row.ItemCode.ShouldBe("ITEM-RESERVE-01");
        row.ItemName.ShouldBe("Precision Motor");
        row.WarehouseName.ShouldBe("Finished Goods WH");
        row.BatchNo.ShouldBe("BATCH-MTR-01");
        row.VoucherType.ShouldBe("Sales Order");
        row.VoucherQty.ShouldBe(50m);
        row.ReservedQty.ShouldBe(40m);
        row.DeliveredQty.ShouldBe(10m);
        row.TransferredQty.ShouldBe(5m);
        row.ConsumedQty.ShouldBe(2m);
        row.AvailableQtyToReserve.ShouldBe(60m);
        row.Status.ShouldBe(DocumentStatus.Submitted.ToString());
    }

    [Fact]
    public async Task GetReportAsync_FiltersByVoucherType()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-01", "Item One", ItemType.Goods) { MaintainStock = true };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores") { IsGroup = false, IsActive = true };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        _binRepo.GetQueryableAsync().Returns(new List<Bin>().AsQueryable());
        _batchRepo.GetQueryableAsync().Returns(new List<Batch>().AsQueryable());

        var soRes = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, "Sales Order", _soId, 20m, 20m);
        soRes.Submit();
        typeof(StockReservationEntry).GetProperty("CreationTime")!.SetValue(soRes, DateTime.UtcNow);

        var woRes = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, "Work Order", Guid.NewGuid(), 15m, 15m);
        woRes.Submit();
        typeof(StockReservationEntry).GetProperty("CreationTime")!.SetValue(woRes, DateTime.UtcNow);

        _reservationRepo.GetQueryableAsync().Returns(new List<StockReservationEntry> { soRes, woRes }.AsQueryable());

        // Act - filter by Sales Order only
        var result = await _appService.GetReportAsync(new ReservedStockFilterDto
        {
            CompanyId = _companyId,
            VoucherType = "Sales Order"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalEntries.ShouldBe(1);
        result.Rows.ShouldHaveSingleItem().VoucherType.ShouldBe("Sales Order");
    }
}
