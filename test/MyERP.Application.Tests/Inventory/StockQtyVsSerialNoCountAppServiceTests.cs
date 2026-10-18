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

public class StockQtyVsSerialNoCountAppServiceTests
{
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<Bin, Guid> _binRepo;
    private readonly IRepository<SerialNo, Guid> _serialNoRepo;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo;
    private readonly StockQtyVsSerialNoCountAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public StockQtyVsSerialNoCountAppServiceTests()
    {
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();
        _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        _binRepo = Substitute.For<IRepository<Bin, Guid>>();
        _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();
        _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();

        _appService = new StockQtyVsSerialNoCountAppService(
            _itemRepo,
            _warehouseRepo,
            _binRepo,
            _serialNoRepo,
            _sleRepo,
            _bundleRepo);
    }

    [Fact]
    public async Task GetReportAsync_NoItems_ReturnsEmptyReport()
    {
        // Arrange
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockQtyVsSerialNoCountFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalItems.ShouldBe(0);
        result.TotalMismatches.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_SerialNoVsBinDifference_CalculatesCorrectDifferenceAndKPIs()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Store") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-001", "Serialized Widget", ItemType.Goods) { HasSerialNo = true, IsActive = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _itemId, _warehouseId) { ActualQty = 5m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        var serial1 = new SerialNo(Guid.NewGuid(), _itemId, "SN-001", _companyId, _warehouseId);
        var serial2 = new SerialNo(Guid.NewGuid(), _itemId, "SN-002", _companyId, _warehouseId);
        var serial3 = new SerialNo(Guid.NewGuid(), _itemId, "SN-003", _companyId, _warehouseId);
        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { serial1, serial2, serial3 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockQtyVsSerialNoCountFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.TotalMismatches.ShouldBe(1);
        result.TotalSerialCount.ShouldBe(3m);
        result.TotalStockQty.ShouldBe(5m);
        result.TotalDifference.ShouldBe(-2m);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.WarehouseName.ShouldBe("Main Store");
        row.SerialNoCount.ShouldBe(3m);
        row.StockQty.ShouldBe(5m);
        row.Difference.ShouldBe(-2m);
    }

    [Fact]
    public async Task GetReportAsync_ShowOnlyDifferences_FiltersOutZeroDifferences()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Store") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item1 = new Item(_itemId, _companyId, "ITEM-001", "Widget Mismatch", ItemType.Goods) { HasSerialNo = true, IsActive = true };
        var itemId2 = Guid.NewGuid();
        var item2 = new Item(itemId2, _companyId, "ITEM-002", "Widget Match", ItemType.Goods) { HasSerialNo = true, IsActive = true };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        var bin1 = new Bin(Guid.NewGuid(), _itemId, _warehouseId) { ActualQty = 3m };
        var bin2 = new Bin(Guid.NewGuid(), itemId2, _warehouseId) { ActualQty = 1m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));

        var serial1 = new SerialNo(Guid.NewGuid(), _itemId, "SN-001", _companyId, _warehouseId);
        var serial2 = new SerialNo(Guid.NewGuid(), itemId2, "SN-MATCH-01", _companyId, _warehouseId);
        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { serial1, serial2 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockQtyVsSerialNoCountFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId,
            ShowOnlyDifferences = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ItemCode.ShouldBe("ITEM-001");
    }

    [Fact]
    public async Task SyncSerialNoStatusAsync_DeliveredSerialInWarehouse_MarksDeliveredAndClearsWarehouse()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Store") { IsActive = true, IsGroup = false };
        _warehouseRepo.FindAsync(_warehouseId).Returns(Task.FromResult<Warehouse?>(warehouse));

        var serialId = Guid.NewGuid();
        var serial = new SerialNo(serialId, _itemId, "SN-DELIVERED-01", _companyId, _warehouseId);
        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { serial }.AsQueryable()));

        var sle = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, -1m, 100m, 0m, 0m)
        {
            SerialNoId = serialId,
            IsCancelled = false,
            VoucherType = "Delivery Note",
            VoucherId = Guid.NewGuid()
        };
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));
        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle>().AsQueryable()));

        // Act
        var result = await _appService.SyncSerialNoStatusAsync(new SyncSerialNoStatusInputDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.SyncedCount.ShouldBe(1);
        serial.WarehouseId.ShouldBeNull();
        serial.Status.ShouldBe(SerialNoStatus.Delivered);

        await _serialNoRepo.Received(1).UpdateAsync(serial);
    }
}
