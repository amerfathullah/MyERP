using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class WarehouseWiseItemBalanceAgeAndValueAppServiceTests
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();

    private readonly WarehouseWiseItemBalanceAgeAndValueAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _wh1Id = Guid.NewGuid();
    private readonly Guid _wh2Id = Guid.NewGuid();
    private readonly Guid _item1Id = Guid.NewGuid();
    private readonly Guid _item2Id = Guid.NewGuid();

    public WarehouseWiseItemBalanceAgeAndValueAppServiceTests()
    {
        _appService = new WarehouseWiseItemBalanceAgeAndValueAppService(
            _warehouseRepo,
            _itemRepo,
            _itemGroupRepo,
            _sleRepo,
            _batchRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesWarehouseBalancesAndAgeCorrectly()
    {
        // Arrange
        var today = new DateTime(2026, 6, 30);
        var wh1 = new Warehouse(_wh1Id, _companyId, "Store A") { IsGroup = false, IsActive = true };
        var wh2 = new Warehouse(_wh2Id, _companyId, "Store B") { IsGroup = false, IsActive = true };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh1, wh2 }.AsQueryable());

        var item1 = new Item(_item1Id, _companyId, "ITEM-001", "Widget A", ItemType.Goods) { MaintainStock = true, IsActive = true };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1 }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());
        _batchRepo.GetQueryableAsync().Returns(new List<Batch>().AsQueryable());

        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _item1Id, _wh1Id, today.AddDays(-20), 10m, 50m, 10m, 500m)
        {
            StockValueDifference = 500m
        };
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, _item1Id, _wh2Id, today.AddDays(-10), 5m, 60m, 5m, 300m)
        {
            StockValueDifference = 300m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new WarehouseWiseItemBalanceAgeAndValueFilterDto
        {
            CompanyId = _companyId,
            ToDate = today,
            FilterTotalZeroQty = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.WarehouseColumns.Count.ShouldBe(2);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.TotalQty.ShouldBe(15m);
        row.TotalStockValue.ShouldBe(800m);
        row.WarehouseBalances[_wh1Id].ShouldBe(10m);
        row.WarehouseBalances[_wh2Id].ShouldBe(5m);
        row.AverageAgeDays.ShouldBeGreaterThan(0);

        result.TotalQuantity.ShouldBe(15m);
        result.TotalStockValue.ShouldBe(800m);
    }

    [Fact]
    public async Task GetReportAsync_FilterTotalZeroQty_ExcludesZeroStockItems()
    {
        // Arrange
        var today = new DateTime(2026, 6, 30);
        var wh1 = new Warehouse(_wh1Id, _companyId, "Store A") { IsGroup = false, IsActive = true };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh1 }.AsQueryable());

        var itemZero = new Item(_item2Id, _companyId, "ITEM-ZERO", "Zero Stock", ItemType.Goods) { MaintainStock = true, IsActive = true };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { itemZero }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());
        _batchRepo.GetQueryableAsync().Returns(new List<Batch>().AsQueryable());

        var sleIn = new StockLedgerEntry(Guid.NewGuid(), _companyId, _item2Id, _wh1Id, today.AddDays(-15), 5m, 40m, 5m, 200m)
        {
            StockValueDifference = 200m
        };
        var sleOut = new StockLedgerEntry(Guid.NewGuid(), _companyId, _item2Id, _wh1Id, today.AddDays(-5), -5m, 40m, 0m, 0m)
        {
            StockValueDifference = -200m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sleIn, sleOut }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new WarehouseWiseItemBalanceAgeAndValueFilterDto
        {
            CompanyId = _companyId,
            ToDate = today,
            FilterTotalZeroQty = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalQuantity.ShouldBe(0m);
    }
}
