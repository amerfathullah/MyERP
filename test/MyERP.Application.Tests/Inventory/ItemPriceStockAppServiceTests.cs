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

public class ItemPriceStockAppServiceTests
{
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepo = Substitute.For<IRepository<ItemPrice, Guid>>();
    private readonly IRepository<PriceList, Guid> _priceListRepo = Substitute.For<IRepository<PriceList, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();

    private readonly ItemPriceStockAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _whId = Guid.NewGuid();
    private readonly Guid _item1Id = Guid.NewGuid();
    private readonly Guid _item2Id = Guid.NewGuid();
    private readonly Guid _sellingPriceListId = Guid.NewGuid();
    private readonly Guid _buyingPriceListId = Guid.NewGuid();

    public ItemPriceStockAppServiceTests()
    {
        _appService = new ItemPriceStockAppService(
            _itemRepo,
            _binRepo,
            _warehouseRepo,
            _itemPriceRepo,
            _priceListRepo,
            _itemGroupRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsItemWithPriceAndStock()
    {
        // Arrange
        var item1 = new Item(_item1Id, _companyId, "ITEM-LAPTOP", "Laptop Pro", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            Brand = "AcmeTech",
            StandardBuyingPrice = 1800m
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1 }.AsQueryable());

        var wh = new Warehouse(_whId, _companyId, "Main Warehouse") { IsGroup = false, IsActive = true };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh }.AsQueryable());

        var bin = new Bin(Guid.NewGuid(), _item1Id, _whId);
        bin.UpdateActualQty(8, 16000); // 8 units @ 2000
        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin }.AsQueryable());

        var sellingPl = new PriceList(_sellingPriceListId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        var buyingPl = new PriceList(_buyingPriceListId, "Standard Buying", "MYR", isSelling: false, isBuying: true);
        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList> { sellingPl, buyingPl }.AsQueryable());

        var sellingPrice = new ItemPrice(Guid.NewGuid(), _item1Id, _sellingPriceListId, 2500m, "Unit", "MYR");
        var buyingPrice = new ItemPrice(Guid.NewGuid(), _item1Id, _buyingPriceListId, 1900m, "Unit", "MYR");
        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice> { sellingPrice, buyingPrice }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new ItemPriceStockFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _whId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItems.ShouldBe(1);
        result.TotalStockAvailable.ShouldBe(8m);
        result.TotalStockValue.ShouldBe(16000m);
        result.TotalSellingValue.ShouldBe(20000m); // 8 * 2500

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-LAPTOP");
        row.WarehouseName.ShouldBe("Main Warehouse");
        row.StockAvailable.ShouldBe(8m);
        row.ValuationRate.ShouldBe(2000m);
        row.StockValue.ShouldBe(16000m);
        row.SellingRate.ShouldBe(2500m);
        row.BuyingRate.ShouldBe(1900m);
    }

    [Fact]
    public async Task GetReportAsync_InStockOnly_FiltersOutZeroStock()
    {
        // Arrange
        var item1 = new Item(_item1Id, _companyId, "ITEM-ACTIVE", "In Stock Item", ItemType.Goods) { MaintainStock = true, IsActive = true };
        var item2 = new Item(_item2Id, _companyId, "ITEM-EMPTY", "Zero Stock Item", ItemType.Goods) { MaintainStock = true, IsActive = true };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1, item2 }.AsQueryable());

        var wh = new Warehouse(_whId, _companyId, "Main Warehouse") { IsGroup = false, IsActive = true };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh }.AsQueryable());

        var bin1 = new Bin(Guid.NewGuid(), _item1Id, _whId);
        bin1.UpdateActualQty(5, 500);
        var bin2 = new Bin(Guid.NewGuid(), _item2Id, _whId);
        bin2.UpdateActualQty(0, 0);
        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin1, bin2 }.AsQueryable());

        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList>().AsQueryable());
        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice>().AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new ItemPriceStockFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _whId,
            InStockOnly = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ItemCode.ShouldBe("ITEM-ACTIVE");
        result.TotalStockAvailable.ShouldBe(5m);
    }
}
