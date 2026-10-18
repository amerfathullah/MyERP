using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class BatchWiseBalanceHistoryAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<StockReservationEntry, Guid> _reservationRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();

    private readonly BatchWiseBalanceHistoryAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public BatchWiseBalanceHistoryAppServiceTests()
    {
        _appService = new BatchWiseBalanceHistoryAppService(
            _sleRepo,
            _bundleRepo,
            _itemRepo,
            _warehouseRepo,
            _batchRepo,
            _reservationRepo);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoBatches_ReturnsEmptyReport()
    {
        // Arrange
        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));
        _batchRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Batch>().AsQueryable()));
        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry>().AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));
        _reservationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockReservationEntry>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BatchWiseBalanceHistoryFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItemsCount.ShouldBe(0);
        result.TotalBatchesCount.ShouldBe(0);
        result.TotalBalanceQty.ShouldBe(0m);
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WithInAndOutTransactions_CalculatesAccurateBalances()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-BATCH-01", "Chemical Lot Item", ItemType.Goods)
        {
            HasBatchNo = true,
            Uom = "Kg"
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores WH");
        var batchId = Guid.NewGuid();
        var batch = new Batch(batchId, _itemId, "BATCH-2026-X1");

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _batchRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var sleIn = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, 50m, 20m, 50m, 1000m)
        {
            BatchId = batchId,
            StockValueDifference = 1000m,
            IsCancelled = false
        };

        var sleOut = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, -15m, 20m, 35m, 700m)
        {
            BatchId = batchId,
            StockValueDifference = -300m,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { sleIn, sleOut }.AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));
        _reservationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockReservationEntry>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BatchWiseBalanceHistoryFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItemsCount.ShouldBe(1);
        result.TotalBatchesCount.ShouldBe(1);
        result.TotalBalanceQty.ShouldBe(35m);
        result.TotalBalanceValue.ShouldBe(700m);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-BATCH-01");
        row.BatchNo.ShouldBe("BATCH-2026-X1");
        row.WarehouseName.ShouldBe("Stores WH");
        row.StockUom.ShouldBe("Kg");
        row.InQty.ShouldBe(50m);
        row.OutQty.ShouldBe(15m);
        row.BalanceQty.ShouldBe(35m);
        row.BalanceValue.ShouldBe(700m);
        row.ValuationRate.ShouldBe(20m);
        row.ReservedQty.ShouldBe(0m);
        row.AvailableQty.ShouldBe(35m);
    }

    [Fact]
    public async Task GetReportAsync_WithStockReservation_CalculatesReservedAndAvailableQty()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-RESERVE", "Reserved Batch Item", ItemType.Goods)
        {
            HasBatchNo = true,
            Uom = "Nos"
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Finished Goods WH");
        var batchId = Guid.NewGuid();
        var batch = new Batch(batchId, _itemId, "BATCH-RES-01");

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _batchRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var sleIn = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, 100m, 10m, 100m, 1000m)
        {
            BatchId = batchId,
            StockValueDifference = 1000m,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { sleIn }.AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));

        var sre = new StockReservationEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            "SalesOrder", Guid.NewGuid(), 40m, 40m)
        {
            BatchId = batchId
        };

        _reservationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockReservationEntry> { sre }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BatchWiseBalanceHistoryFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalBalanceQty.ShouldBe(100m);
        result.TotalReservedQty.ShouldBe(40m);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.BalanceQty.ShouldBe(100m);
        row.ReservedQty.ShouldBe(40m);
        row.AvailableQty.ShouldBe(60m);
    }
}
