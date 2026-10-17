using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class BomExplorerAppServiceTests
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly BomExplorerAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public BomExplorerAppServiceTests()
    {
        _appService = new BomExplorerAppService(
            _bomRepo,
            _itemRepo,
            _binRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetExplorerAsync_MultiLevelExplosion_ScalesQuantitiesAndSetsLevels()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-BIKE", "Bicycle", ItemType.Goods);

        var saItemId = Guid.NewGuid();
        var saItem = new Item(saItemId, _companyId, "SA-WHEEL", "Wheel Assembly", ItemType.Goods);

        var rmSpokeId = Guid.NewGuid();
        var rmSpoke = new Item(rmSpokeId, _companyId, "RM-SPOKE", "Wheel Spoke", ItemType.Goods);

        var rmFrameId = Guid.NewGuid();
        var rmFrame = new Item(rmFrameId, _companyId, "RM-FRAME", "Bike Frame", ItemType.Goods);

        // Sub-BOM for Wheel: 1 Wheel requires 36 spokes
        var subBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-WHEEL-001", saItemId)
        {
            Quantity = 1m,
            IsActive = true
        };
        var spokeLine = new BomItem(Guid.NewGuid(), subBom.Id, rmSpokeId, "Wheel Spoke", 36m, 1.5m);
        subBom.Items.Add(spokeLine);

        // Parent BOM for Bike: 1 Bike requires 1 Frame and 2 Wheels (Sub-assembly)
        var rootBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-BIKE-001", fgItemId)
        {
            Quantity = 1m,
            IsActive = true
        };
        var frameLine = new BomItem(Guid.NewGuid(), rootBom.Id, rmFrameId, "Bike Frame", 1m, 120m);
        var wheelLine = new BomItem(Guid.NewGuid(), rootBom.Id, saItemId, "Wheel Assembly", 2m, 50m)
        {
            SubBomId = subBom.Id
        };
        rootBom.Items.Add(frameLine);
        rootBom.Items.Add(wheelLine);

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { rootBom, subBom }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, saItem, rmSpoke, rmFrame }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        // Act: Explode for 5 Bikes
        var result = await _appService.GetExplorerAsync(new BomExplorerRequestDto
        {
            CompanyId = _companyId,
            BomId = rootBom.Id,
            Quantity = 5m
        });

        // Assert
        result.ShouldNotBeNull();
        result.BomNumber.ShouldBe("BOM-BIKE-001");
        result.ExplodedQuantity.ShouldBe(5m);
        result.TotalComponents.ShouldBe(3);
        result.TotalSubBoms.ShouldBe(1);

        // Row 1: Frame (Level 0, Qty = 1 * 5 = 5)
        var rowFrame = result.Rows[0];
        rowFrame.ItemCode.ShouldBe("RM-FRAME");
        rowFrame.BomLevel.ShouldBe(0);
        rowFrame.Qty.ShouldBe(5m);
        rowFrame.Amount.ShouldBe(600m); // 5 * 120

        // Row 2: Wheel Assembly (Level 0, Qty = 2 * 5 = 10, SubBomId = subBom.Id)
        var rowWheel = result.Rows[1];
        rowWheel.ItemCode.ShouldBe("SA-WHEEL");
        rowWheel.BomLevel.ShouldBe(0);
        rowWheel.Qty.ShouldBe(10m);
        rowWheel.SubBomNumber.ShouldBe("BOM-WHEEL-001");

        // Row 3: Spoke (Level 1, Qty = 10 wheels * 36 spokes = 360)
        var rowSpoke = result.Rows[2];
        rowSpoke.ItemCode.ShouldBe("RM-SPOKE");
        rowSpoke.BomLevel.ShouldBe(1);
        rowSpoke.Qty.ShouldBe(360m);
        rowSpoke.Amount.ShouldBe(540m); // 360 * 1.5
    }

    [Fact]
    public async Task GetExplorerAsync_DoNotExplode_SkipsSubBomChildren()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-001", "Product", ItemType.Goods);
        var saItemId = Guid.NewGuid();
        var saItem = new Item(saItemId, _companyId, "SA-001", "Sub Assembly", ItemType.Goods);
        var rmItemId = Guid.NewGuid();
        var rmItem = new Item(rmItemId, _companyId, "RM-001", "Raw Material", ItemType.Goods);

        var subBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-SUB", saItemId) { Quantity = 1m, IsActive = true };
        subBom.Items.Add(new BomItem(Guid.NewGuid(), subBom.Id, rmItemId, "Raw Material", 10m, 1m));

        var rootBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ROOT", fgItemId) { Quantity = 1m, IsActive = true };
        rootBom.Items.Add(new BomItem(Guid.NewGuid(), rootBom.Id, saItemId, "Sub Assembly", 1m, 10m)
        {
            SubBomId = subBom.Id,
            DoNotExplode = true
        });

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { rootBom, subBom }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, saItem, rmItemId == Guid.Empty ? null! : rmItem }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        // Act
        var result = await _appService.GetExplorerAsync(new BomExplorerRequestDto
        {
            CompanyId = _companyId,
            BomId = rootBom.Id,
            Quantity = 1m
        });

        // Assert: only SA-001 row, child RM-001 is skipped because DoNotExplode is true
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ItemCode.ShouldBe("SA-001");
        result.Rows[0].DoNotExplode.ShouldBeTrue();
    }

    [Fact]
    public async Task GetExplorerAsync_CycleSafe_PreventsInfiniteRecursion()
    {
        // Arrange
        var item1Id = Guid.NewGuid();
        var item1 = new Item(item1Id, _companyId, "ITEM-1", "Item 1", ItemType.Goods);
        var item2Id = Guid.NewGuid();
        var item2 = new Item(item2Id, _companyId, "ITEM-2", "Item 2", ItemType.Goods);

        var bom1 = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-1", item1Id) { Quantity = 1m, IsActive = true };
        var bom2 = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-2", item2Id) { Quantity = 1m, IsActive = true };

        // Cycle: BOM 1 references BOM 2, BOM 2 references BOM 1
        bom1.Items.Add(new BomItem(Guid.NewGuid(), bom1.Id, item2Id, "Item 2", 1m, 10m) { SubBomId = bom2.Id });
        bom2.Items.Add(new BomItem(Guid.NewGuid(), bom2.Id, item1Id, "Item 1", 1m, 10m) { SubBomId = bom1.Id });

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom1, bom2 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        // Act: Should complete without StackOverflowException
        var result = await _appService.GetExplorerAsync(new BomExplorerRequestDto
        {
            CompanyId = _companyId,
            BomId = bom1.Id,
            Quantity = 1m
        });

        // Assert: Explodes BOM 1 -> item 2 (BOM 2) -> item 1, then stops cycle
        result.Rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetBomListAsync_ReturnsActiveBoms()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "FG-DESK", "Study Desk", ItemType.Goods);

        var bomActive = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ACTIVE", itemId) { IsActive = true, Quantity = 1m };
        var bomInactive = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-INACTIVE", itemId) { IsActive = false, Quantity = 1m };

        _bomRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<BillOfMaterials> { bomActive, bomInactive }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        // Act
        var result = await _appService.GetBomListAsync(_companyId);

        // Assert
        result.Count.ShouldBe(1);
        result[0].BomNumber.ShouldBe("BOM-ACTIVE");
        result[0].ItemCode.ShouldBe("FG-DESK");
    }
}
