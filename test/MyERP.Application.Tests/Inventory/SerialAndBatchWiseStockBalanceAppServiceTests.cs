using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class SerialAndBatchWiseStockBalanceAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<SerialNo, Guid> _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();

    private readonly SerialAndBatchWiseStockBalanceAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public SerialAndBatchWiseStockBalanceAppServiceTests()
    {
        _appService = new SerialAndBatchWiseStockBalanceAppService(
            _sleRepo,
            _bundleRepo,
            _itemRepo,
            _warehouseRepo,
            _batchRepo,
            _serialNoRepo);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoItems_ReturnsEmptyReport()
    {
        // Arrange
        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));
        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));
        _batchRepo.GetListAsync()
            .Returns(Task.FromResult(new List<Batch>()));
        _serialNoRepo.GetListAsync()
            .Returns(Task.FromResult(new List<SerialNo>()));
        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry>().AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialAndBatchWiseStockBalanceFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItemsCount.ShouldBe(0);
        result.TotalBatchesCount.ShouldBe(0);
        result.TotalBalanceQty.ShouldBe(0);
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WithBatchItem_ReturnsParentAndChildRows()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-BATCH", "Batch Item", ItemType.Goods)
        {
            HasBatchNo = true,
            Uom = "Nos"
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse");
        var batchId = Guid.NewGuid();
        var batch = new Batch(batchId, _itemId, "BATCH-001");

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));
        _batchRepo.GetListAsync()
            .Returns(Task.FromResult(new List<Batch> { batch }));
        _serialNoRepo.GetListAsync()
            .Returns(Task.FromResult(new List<SerialNo>()));

        var sleIn = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, 10m, 100m, 10m, 1000m)
        {
            BatchId = batchId,
            StockValueDifference = 1000m,
            IsCancelled = false
        };

        var sleOut = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, -3m, 100m, 7m, 700m)
        {
            BatchId = batchId,
            StockValueDifference = -300m,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { sleIn, sleOut }.AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialAndBatchWiseStockBalanceFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItemsCount.ShouldBe(1);
        result.TotalBatchesCount.ShouldBe(1);
        result.TotalBalanceQty.ShouldBe(7m);
        result.TotalBalanceValue.ShouldBe(700m);
        result.Rows.Count.ShouldBe(2);

        var parent = result.Rows.First(r => r.Indent == 0);
        parent.ItemCode.ShouldBe("ITEM-BATCH");
        parent.WarehouseName.ShouldBe("Main Warehouse");
        parent.InQty.ShouldBe(10m);
        parent.OutQty.ShouldBe(3m);
        parent.BalanceQty.ShouldBe(7m);
        parent.BalanceValue.ShouldBe(700m);
        parent.BatchNo!.ShouldContain("BATCH-001");

        var child = result.Rows.First(r => r.Indent == 1);
        child.BatchId.ShouldBe(batchId);
        child.BatchNo.ShouldBe("BATCH-001");
        child.BalanceQty.ShouldBe(7m);
        child.BalanceValue.ShouldBe(700m);
        child.ValuationRate.ShouldBe(100m);
    }

    [Fact]
    public async Task GetReportAsync_WithSerialTracking_TracksInStockSerials()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-SERIAL", "Serialized Item", ItemType.Goods)
        {
            HasSerialNo = true,
            Uom = "Nos"
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores WH");

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));
        _batchRepo.GetListAsync()
            .Returns(Task.FromResult(new List<Batch>()));
        _serialNoRepo.GetListAsync()
            .Returns(Task.FromResult(new List<SerialNo>()));

        var bundleInId = Guid.NewGuid();
        var bundleIn = new SerialAndBatchBundle(bundleInId, _companyId, _itemId, _warehouseId,
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);
        bundleIn.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleInId, 1, 50m, serialNo: "SN-001"));
        bundleIn.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleInId, 1, 50m, serialNo: "SN-002"));

        var sleIn = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, 2m, 50m, 2m, 100m)
        {
            SerialAndBatchBundleId = bundleInId,
            StockValueDifference = 100m,
            IsCancelled = false
        };

        var bundleOutId = Guid.NewGuid();
        var bundleOut = new SerialAndBatchBundle(bundleOutId, _companyId, _itemId, _warehouseId,
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);
        bundleOut.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleOutId, 1, 50m, serialNo: "SN-001"));

        var sleOut = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, -1m, 50m, 1m, 50m)
        {
            SerialAndBatchBundleId = bundleOutId,
            StockValueDifference = -50m,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { sleIn, sleOut }.AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundleIn, bundleOut }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new SerialAndBatchWiseStockBalanceFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItemsCount.ShouldBe(1);
        result.TotalBalanceQty.ShouldBe(1m);
        result.Rows.Count.ShouldBe(1);

        var parent = result.Rows[0];
        parent.BalanceQty.ShouldBe(1m);
        parent.SerialNos.ShouldBe("SN-002");
    }
}
