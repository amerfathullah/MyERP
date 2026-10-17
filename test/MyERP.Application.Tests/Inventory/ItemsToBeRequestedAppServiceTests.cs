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

public class ItemsToBeRequestedAppServiceTests
{
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();

    private readonly ItemsToBeRequestedAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemsToBeRequestedAppServiceTests()
    {
        _appService = new ItemsToBeRequestedAppService(
            _binRepo,
            _itemRepo,
            _warehouseRepo,
            _itemGroupRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsItemsWithNegativeProjectedQty_OrderedByDeficit()
    {
        // Arrange
        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Stores - Central")
        {
            IsActive = true,
            IsGroup = false
        };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        var item1Id = Guid.NewGuid();
        var item1 = new Item(item1Id, _companyId, "ITEM-001", "Widget Normal", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            StandardBuyingPrice = 10m,
            Uom = "Nos"
        };

        var item2Id = Guid.NewGuid();
        var item2 = new Item(item2Id, _companyId, "ITEM-002", "Widget Critical", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            StandardBuyingPrice = 25m,
            Uom = "Nos"
        };

        var item3Id = Guid.NewGuid();
        var item3 = new Item(item3Id, _companyId, "ITEM-003", "Widget Healthy", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            StandardBuyingPrice = 50m,
            Uom = "Nos"
        };

        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1, item2, item3 }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        // Bin 1: item 1 -> actual 10, reserved 30 -> projected -20
        var bin1 = new Bin(Guid.NewGuid(), item1Id, whId)
        {
            ActualQty = 10m,
            ReservedQty = 30m
        };

        // Bin 2: item 2 -> actual 5, reserved 55 -> projected -50
        var bin2 = new Bin(Guid.NewGuid(), item2Id, whId)
        {
            ActualQty = 5m,
            ReservedQty = 55m
        };

        // Bin 3: item 3 -> actual 100, reserved 10 -> projected +90 (should NOT appear)
        var bin3 = new Bin(Guid.NewGuid(), item3Id, whId)
        {
            ActualQty = 100m,
            ReservedQty = 10m
        };

        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin1, bin2, bin3 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new ItemsToBeRequestedFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalDeficitItems.ShouldBe(2);
        result.TotalShortfallQty.ShouldBe(70m); // 50 + 20
        result.TotalEstimatedCost.ShouldBe(1450m); // (50 * 25) + (20 * 10) = 1250 + 200

        // Ordered by ProjectedQty ascending (most negative first)
        result.Items[0].ItemCode.ShouldBe("ITEM-002");
        result.Items[0].ProjectedQty.ShouldBe(-50m);
        result.Items[0].ShortfallQty.ShouldBe(50m);
        result.Items[0].EstimatedShortfallCost.ShouldBe(1250m);

        result.Items[1].ItemCode.ShouldBe("ITEM-001");
        result.Items[1].ProjectedQty.ShouldBe(-20m);
        result.Items[1].ShortfallQty.ShouldBe(20m);
        result.Items[1].EstimatedShortfallCost.ShouldBe(200m);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoNegativeBins_ReturnsEmptyReport()
    {
        // Arrange
        _binRepo.GetQueryableAsync().Returns(new List<Bin>().AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new ItemsToBeRequestedFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalDeficitItems.ShouldBe(0);
        result.TotalShortfallQty.ShouldBe(0m);
        result.TotalEstimatedCost.ShouldBe(0m);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_FiltersByWarehouseCorrectly()
    {
        // Arrange
        var wh1Id = Guid.NewGuid();
        var wh2Id = Guid.NewGuid();

        var wh1 = new Warehouse(wh1Id, _companyId, "WH 1") { IsActive = true, IsGroup = false };
        var wh2 = new Warehouse(wh2Id, _companyId, "WH 2") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh1, wh2 }.AsQueryable());

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-FILTER", "Filter Item", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            StandardBuyingPrice = 15m
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        var binWh1 = new Bin(Guid.NewGuid(), itemId, wh1Id) { ActualQty = 0, ReservedQty = 10 };
        var binWh2 = new Bin(Guid.NewGuid(), itemId, wh2Id) { ActualQty = 0, ReservedQty = 25 };

        _binRepo.GetQueryableAsync().Returns(new List<Bin> { binWh1, binWh2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new ItemsToBeRequestedFilterDto
        {
            WarehouseId = wh1Id
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalDeficitItems.ShouldBe(1);
        result.Items.Single().WarehouseId.ShouldBe(wh1Id);
        result.Items.Single().ShortfallQty.ShouldBe(10m);
    }
}
