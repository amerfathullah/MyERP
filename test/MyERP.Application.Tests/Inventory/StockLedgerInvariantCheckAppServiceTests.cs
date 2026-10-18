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

public class StockLedgerInvariantCheckAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();

    private readonly StockLedgerInvariantCheckAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public StockLedgerInvariantCheckAppServiceTests()
    {
        _appService = new StockLedgerInvariantCheckAppService(
            _sleRepo,
            _itemRepo,
            _warehouseRepo,
            _batchRepo);
    }

    [Fact]
    public async Task GetInvariantCheckAsync_ValidLedger_CalculatesInvariantsAndReturnsZeroViolations()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-WIDGET-01", "Widget A", ItemType.Goods)
        {
            ValuationMethod = ValuationMethod.FIFO,
            Uom = "Nos"
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };

        _itemRepo.GetAsync(_itemId).Returns(Task.FromResult(item));
        _warehouseRepo.GetAsync(_warehouseId).Returns(Task.FromResult(warehouse));
        _batchRepo.GetListAsync(Arg.Any<Expression<Func<Batch, bool>>>()).Returns(Task.FromResult(new List<Batch>()));

        var now = DateTime.UtcNow;
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-2), 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m,
            StockQueue = "[[10, 100]]",
            VoucherType = "PurchaseReceipt",
            VoucherId = Guid.NewGuid()
        };
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-1), -4m, 100m, 6m, 600m)
        {
            StockValueDifference = -400m,
            StockQueue = "[[6, 100]]",
            VoucherType = "DeliveryNote",
            VoucherId = Guid.NewGuid()
        };

        var sles = new List<StockLedgerEntry> { sle1, sle2 };
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        // Act
        var result = await _appService.GetInvariantCheckAsync(new StockLedgerInvariantCheckFilterDto
        {
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ShowIncorrectEntries = false
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalEntries.ShouldBe(2);
        result.ViolationCount.ShouldBe(0);
        result.FirstViolationIndex.ShouldBeNull();
        result.Rows.Count.ShouldBe(2);

        var row1 = result.Rows[0];
        row1.ExpectedQtyAfterTransaction.ShouldBe(10m);
        row1.DifferenceInQty.ShouldBe(0m);
        row1.StockValueFromDiff.ShouldBe(1000m);
        row1.DiffValueDiff.ShouldBe(0m);
        row1.FifoQueueQty.ShouldBe(10m);
        row1.FifoQtyDiff.ShouldBe(0m);
        row1.FifoStockValue.ShouldBe(1000m);
        row1.FifoValueDiff.ShouldBe(0m);
        row1.IsInvariantViolated.ShouldBeFalse();

        var row2 = result.Rows[1];
        row2.ExpectedQtyAfterTransaction.ShouldBe(6m);
        row2.DifferenceInQty.ShouldBe(0m);
        row2.StockValueFromDiff.ShouldBe(600m);
        row2.DiffValueDiff.ShouldBe(0m);
        row2.FifoQueueQty.ShouldBe(6m);
        row2.FifoQtyDiff.ShouldBe(0m);
        row2.ConsumptionRate.ShouldBe(100m);
        row2.IsInvariantViolated.ShouldBeFalse();
    }

    [Fact]
    public async Task GetInvariantCheckAsync_DiscrepancyInQty_FlagsViolation()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-WIDGET-01", "Widget A", ItemType.Goods)
        {
            ValuationMethod = ValuationMethod.FIFO
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };

        _itemRepo.GetAsync(_itemId).Returns(Task.FromResult(item));
        _warehouseRepo.GetAsync(_warehouseId).Returns(Task.FromResult(warehouse));
        _batchRepo.GetListAsync(Arg.Any<Expression<Func<Batch, bool>>>()).Returns(Task.FromResult(new List<Batch>()));

        // Inward 10, but BalanceQuantity is corrupted to 12
        var corruptedSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 10m, 100m, 12m, 1000m)
        {
            StockValueDifference = 1000m,
            StockQueue = "[[10, 100]]"
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { corruptedSle }.AsQueryable()));

        // Act
        var result = await _appService.GetInvariantCheckAsync(new StockLedgerInvariantCheckFilterDto
        {
            ItemId = _itemId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.ViolationCount.ShouldBe(1);
        result.FirstViolationIndex.ShouldBe(0);
        result.Rows[0].IsInvariantViolated.ShouldBeTrue();
        result.Rows[0].DifferenceInQty.ShouldBe(2m);
    }

    [Fact]
    public async Task GetInvariantCheckAsync_DiscrepancyInFifoQueue_FlagsFifoViolation()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-WIDGET-01", "Widget A", ItemType.Goods)
        {
            ValuationMethod = ValuationMethod.FIFO
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };

        _itemRepo.GetAsync(_itemId).Returns(Task.FromResult(item));
        _warehouseRepo.GetAsync(_warehouseId).Returns(Task.FromResult(warehouse));
        _batchRepo.GetListAsync(Arg.Any<Expression<Func<Batch, bool>>>()).Returns(Task.FromResult(new List<Batch>()));

        // BalanceQuantity is 10, but StockQueue only has 8 units
        var corruptedQueueSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m,
            StockQueue = "[[8, 100]]"
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { corruptedQueueSle }.AsQueryable()));

        // Act
        var result = await _appService.GetInvariantCheckAsync(new StockLedgerInvariantCheckFilterDto
        {
            ItemId = _itemId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.ViolationCount.ShouldBe(1);
        result.Rows[0].FifoQtyDiff.ShouldBe(2m);
        result.Rows[0].IsInvariantViolated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetInvariantCheckAsync_ShowIncorrectEntries_FiltersFromFirstViolation()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-WIDGET-01", "Widget A", ItemType.Goods)
        {
            ValuationMethod = ValuationMethod.WeightedAverage
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };

        _itemRepo.GetAsync(_itemId).Returns(Task.FromResult(item));
        _warehouseRepo.GetAsync(_warehouseId).Returns(Task.FromResult(warehouse));
        _batchRepo.GetListAsync(Arg.Any<Expression<Func<Batch, bool>>>()).Returns(Task.FromResult(new List<Batch>()));

        var now = DateTime.UtcNow;
        var validSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-3), 5m, 10m, 5m, 50m)
        {
            StockValueDifference = 50m
        };
        var corruptedSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-2), 5m, 10m, 15m, 100m) // balance should be 10, corrupted to 15
        {
            StockValueDifference = 50m
        };
        var subsequentSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-1), -2m, 10m, 13m, 80m)
        {
            StockValueDifference = -20m
        };

        var sles = new List<StockLedgerEntry> { validSle, corruptedSle, subsequentSle };
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        // Act
        var result = await _appService.GetInvariantCheckAsync(new StockLedgerInvariantCheckFilterDto
        {
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ShowIncorrectEntries = true
        });

        // Assert: first violation is at index 1. max(1 - 1, 0) = 0.
        result.ShouldNotBeNull();
        result.FirstViolationIndex.ShouldBe(1);
        result.ViolationCount.ShouldBe(2);
        result.Rows.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetStockLedgerVarianceAsync_IdentifiesBreachesAcrossWarehouses()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-WIDGET-01", "Widget A", ItemType.Goods)
        {
            ValuationMethod = ValuationMethod.WeightedAverage
        };
        var warehouse1 = new Warehouse(_warehouseId, _companyId, "Main WH") { IsActive = true, IsGroup = false };
        var wh2Id = Guid.NewGuid();
        var warehouse2 = new Warehouse(wh2Id, _companyId, "Branch WH") { IsActive = true, IsGroup = false };

        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse1, warehouse2 }.AsQueryable()));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(Task.FromResult(new List<Item> { item }));

        var now = DateTime.UtcNow;
        // Clean entry in WH1
        var cleanSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, now.AddHours(-2), 10m, 50m, 10m, 500m)
        {
            StockValueDifference = 500m
        };
        // Corrupted entry in WH2
        var brokenSle = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, wh2Id, now.AddHours(-1), 10m, 50m, 99m, 500m)
        {
            StockValueDifference = 500m
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { cleanSle, brokenSle }.AsQueryable()));

        // Act
        var result = await _appService.GetStockLedgerVarianceAsync(new StockLedgerVarianceFilterDto
        {
            CompanyId = _companyId,
            ShowOnlyViolations = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalViolations.ShouldBe(1);
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WarehouseId.ShouldBe(wh2Id);
        result.Rows[0].HasViolation.ShouldBeTrue();
        result.Rows[0].DifferenceInQty.ShouldBe(89m);
    }
}
