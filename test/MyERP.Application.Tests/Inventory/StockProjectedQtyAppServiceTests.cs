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

public class StockProjectedQtyAppServiceTests
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<ItemReorder, Guid> _reorderRepository;
    private readonly StockProjectedQtyAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public StockProjectedQtyAppServiceTests()
    {
        _itemRepository = Substitute.For<IRepository<Item, Guid>>();
        _warehouseRepository = Substitute.For<IRepository<Warehouse, Guid>>();
        _binRepository = Substitute.For<IRepository<Bin, Guid>>();
        _reorderRepository = Substitute.For<IRepository<ItemReorder, Guid>>();

        _appService = new StockProjectedQtyAppService(
            _itemRepository,
            _warehouseRepository,
            _binRepository,
            _reorderRepository);
    }

    [Fact]
    public async Task GetReportAsync_NoItems_ReturnsEmptyReport()
    {
        // Arrange
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockProjectedQtyFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalItems.ShouldBe(0);
        result.ShortageCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_NormalBinProjection_CalculatesCorrectProjectedQtyAndNoShortage()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores WH") { IsActive = true, IsGroup = false };
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-001", "Precision Gear", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            ReorderLevel = 10m,
            ReorderQty = 20m,
            Uom = "Nos"
        };
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _itemId, _warehouseId)
        {
            ActualQty = 12m,
            PlannedQty = 5m,
            IndentedQty = 3m,
            OrderedQty = 4m,
            ReservedQty = 6m,
            ReservedQtyForProduction = 2m
        };
        _binRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));
        _reorderRepository.GetQueryableAsync().Returns(Task.FromResult(new List<ItemReorder>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockProjectedQtyFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.ShortageCount.ShouldBe(0);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.WarehouseName.ShouldBe("Stores WH");
        row.ActualQty.ShouldBe(12m);
        row.PlannedQty.ShouldBe(5m);
        row.IndentedQty.ShouldBe(3m);
        row.OrderedQty.ShouldBe(4m);
        row.ReservedQty.ShouldBe(6m);
        row.ReservedQtyForProduction.ShouldBe(2m);
        row.TotalReservedQty.ShouldBe(8m);
        row.ProjectedQty.ShouldBe(16m); // 12 + 5 + 3 + 4 - 6 - 2 = 16
        row.ReorderLevel.ShouldBe(10m);
        row.ShortageQty.ShouldBe(0m);
        row.HasShortage.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportAsync_ShortageDetected_CalculatesCorrectShortageQty()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { IsActive = true, IsGroup = false };
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-BEARING", "Ball Bearing 10mm", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            ReorderLevel = 25m,
            ReorderQty = 50m,
            Uom = "Pcs"
        };
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _itemId, _warehouseId)
        {
            ActualQty = 10m,
            ReservedQty = 5m
        };
        _binRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));
        _reorderRepository.GetQueryableAsync().Returns(Task.FromResult(new List<ItemReorder>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockProjectedQtyFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.ShortageCount.ShouldBe(1);
        result.TotalShortageQty.ShouldBe(20m);

        var row = result.Rows[0];
        row.ProjectedQty.ShouldBe(5m); // 10 - 5 = 5
        row.ReorderLevel.ShouldBe(25m);
        row.ShortageQty.ShouldBe(20m); // 25 - 5 = 20
        row.HasShortage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetReportAsync_WarehouseReorderOverride_OverridesItemDefault()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Retail Store") { IsActive = true, IsGroup = false };
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-FASTENER", "Hex Bolt M8", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            ReorderLevel = 5m,
            ReorderQty = 10m
        };
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _itemId, _warehouseId)
        {
            ActualQty = 8m
        };
        _binRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        var overrideReorder = new ItemReorder(
            Guid.NewGuid(), _itemId, _warehouseId,
            warehouseReorderLevel: 20m,
            warehouseReorderQty: 40m,
            materialRequestType: global::MyERP.Purchasing.MaterialRequestType.Purchase);
        _reorderRepository.GetQueryableAsync().Returns(Task.FromResult(new List<ItemReorder> { overrideReorder }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockProjectedQtyFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        var row = result.Rows[0];
        row.ReorderLevel.ShouldBe(20m);
        row.ReorderQty.ShouldBe(40m);
        row.ShortageQty.ShouldBe(12m); // 20 - 8 = 12
        row.HasShortage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetReportAsync_ShowOnlyShortage_FiltersNonShortageItems()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Central WH") { IsActive = true, IsGroup = false };
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item1 = new Item(_itemId, _companyId, "ITEM-SHORT", "Item With Shortage", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            ReorderLevel = 15m
        };
        var itemId2 = Guid.NewGuid();
        var item2 = new Item(itemId2, _companyId, "ITEM-SURPLUS", "Item Surplus", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            ReorderLevel = 5m
        };
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        var bin1 = new Bin(Guid.NewGuid(), _itemId, _warehouseId) { ActualQty = 5m }; // Projected 5 < 15 -> Shortage 10
        var bin2 = new Bin(Guid.NewGuid(), itemId2, _warehouseId) { ActualQty = 20m }; // Projected 20 >= 5 -> No Shortage
        _binRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));
        _reorderRepository.GetQueryableAsync().Returns(Task.FromResult(new List<ItemReorder>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new StockProjectedQtyFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId,
            ShowOnlyShortage = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ItemCode.ShouldBe("ITEM-SHORT");
    }
}
