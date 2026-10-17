using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class ItemShortageReportAppServiceTests
{
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly ItemShortageReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemShortageReportAppServiceTests()
    {
        _appService = new ItemShortageReportAppService(
            _binRepo,
            _warehouseRepo,
            _itemRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsOnlyNegativeProjectedQtyForActiveItems()
    {
        // Arrange
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Main Warehouse");

        var itemActiveShortage = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Widget A", ItemType.Goods)
        {
            IsActive = true,
            Description = "Standard Widget A"
        };
        var itemActiveSurplus = new Item(Guid.NewGuid(), _companyId, "ITEM-002", "Widget B", ItemType.Goods)
        {
            IsActive = true
        };
        var itemInactiveShortage = new Item(Guid.NewGuid(), _companyId, "ITEM-003", "Widget C Discontinued", ItemType.Goods)
        {
            IsActive = false
        };

        // Bin 1: Actual 5, Reserved 15 -> Projected = -10 (Shortage!)
        var bin1 = new Bin(Guid.NewGuid(), itemActiveShortage.Id, whId)
        {
            ActualQty = 5m,
            ReservedQty = 15m
        };

        // Bin 2: Actual 20, Reserved 5 -> Projected = 15 (Surplus)
        var bin2 = new Bin(Guid.NewGuid(), itemActiveSurplus.Id, whId)
        {
            ActualQty = 20m,
            ReservedQty = 5m
        };

        // Bin 3: Actual 0, Reserved 10 -> Projected = -10, but item inactive
        var bin3 = new Bin(Guid.NewGuid(), itemInactiveShortage.Id, whId)
        {
            ActualQty = 0m,
            ReservedQty = 10m
        };

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { itemActiveShortage, itemActiveSurplus, itemInactiveShortage }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin1, bin2, bin3 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemShortageReportRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.CompanyName.ShouldBe("Test Company");
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemId.ShouldBe(itemActiveShortage.Id);
        row.ItemCode.ShouldBe("ITEM-001");
        row.ItemName.ShouldBe("Widget A");
        row.Description.ShouldBe("Standard Widget A");
        row.WarehouseName.ShouldBe("Main Warehouse");
        row.ActualQty.ShouldBe(5m);
        row.ReservedQty.ShouldBe(15m);
        row.ProjectedQty.ShouldBe(-10m);

        result.TotalShortageItems.ShouldBe(1);
        result.TotalProjectedDeficit.ShouldBe(-10m);
        result.ChartData.Count.ShouldBe(1);
        result.ChartData[0].ItemCode.ShouldBe("ITEM-001");
        result.ChartData[0].ProjectedQty.ShouldBe(-10m);
    }

    [Fact]
    public async Task GetReportAsync_OrdersByWorstShortageAscending()
    {
        // Arrange
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Stores");

        var itemMild = new Item(Guid.NewGuid(), _companyId, "MILD", "Mild Shortage", ItemType.Goods) { IsActive = true };
        var itemSevere = new Item(Guid.NewGuid(), _companyId, "SEVERE", "Severe Shortage", ItemType.Goods) { IsActive = true };

        var binMild = new Bin(Guid.NewGuid(), itemMild.Id, whId) { ActualQty = 0m, ReservedQty = 5m }; // -5
        var binSevere = new Bin(Guid.NewGuid(), itemSevere.Id, whId) { ActualQty = 0m, ReservedQty = 50m }; // -50

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { itemMild, itemSevere }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { binMild, binSevere }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemShortageReportRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.Rows.Count.ShouldBe(2);
        result.Rows[0].ItemCode.ShouldBe("SEVERE");
        result.Rows[0].ProjectedQty.ShouldBe(-50m);
        result.Rows[1].ItemCode.ShouldBe("MILD");
        result.Rows[1].ProjectedQty.ShouldBe(-5m);
        result.TotalProjectedDeficit.ShouldBe(-55m);
    }

    [Fact]
    public async Task GetReportAsync_FiltersByWarehouseAndSearchText()
    {
        // Arrange
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));

        var wh1 = new Warehouse(Guid.NewGuid(), _companyId, "Central Warehouse");
        var wh2 = new Warehouse(Guid.NewGuid(), _companyId, "Remote Warehouse");

        var item1 = new Item(Guid.NewGuid(), _companyId, "ALPHA", "Alpha Bolt", ItemType.Goods) { IsActive = true };
        var item2 = new Item(Guid.NewGuid(), _companyId, "BETA", "Beta Nut", ItemType.Goods) { IsActive = true };

        var bin1 = new Bin(Guid.NewGuid(), item1.Id, wh1.Id) { ActualQty = 0m, ReservedQty = 10m };
        var bin2 = new Bin(Guid.NewGuid(), item2.Id, wh2.Id) { ActualQty = 0m, ReservedQty = 20m };

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { wh1, wh2 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));

        // Act 1: Warehouse filter
        var resWh1 = await _appService.GetReportAsync(new ItemShortageReportRequestDto
        {
            CompanyId = _companyId,
            WarehouseId = wh1.Id
        });

        // Assert 1
        resWh1.Rows.Count.ShouldBe(1);
        resWh1.Rows[0].ItemCode.ShouldBe("ALPHA");

        // Act 2: Search text
        var resSearch = await _appService.GetReportAsync(new ItemShortageReportRequestDto
        {
            CompanyId = _companyId,
            SearchText = "Nut"
        });

        // Assert 2
        resSearch.Rows.Count.ShouldBe(1);
        resSearch.Rows[0].ItemCode.ShouldBe("BETA");
    }

    [Fact]
    public async Task GetReportAsync_WhenNoWarehousesFound_ReturnsEmptyReport()
    {
        // Arrange
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(null));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemShortageReportRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalShortageItems.ShouldBe(0);
        result.TotalProjectedDeficit.ShouldBe(0m);
    }
}
