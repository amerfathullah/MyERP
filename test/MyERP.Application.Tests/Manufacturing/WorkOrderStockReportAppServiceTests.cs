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

public class WorkOrderStockReportAppServiceTests
{
    private readonly IRepository<WorkOrder, Guid> _woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly WorkOrderStockReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public WorkOrderStockReportAppServiceTests()
    {
        _appService = new WorkOrderStockReportAppService(
            _woRepo,
            _bomRepo,
            _binRepo,
            _itemRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesBuildableQtyAndReadyToBuild()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-DESK", "Study Desk", ItemType.Goods);

        var rm1Id = Guid.NewGuid();
        var rm1 = new Item(rm1Id, _companyId, "RM-TOP", "Desk Top", ItemType.Goods);

        var rm2Id = Guid.NewGuid();
        var rm2 = new Item(rm2Id, _companyId, "RM-LEG", "Desk Leg", ItemType.Goods);

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Stores - Central");

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-DESK-001", fgItemId)
        {
            Quantity = 1m
        };
        var bomItem1 = new BomItem(Guid.NewGuid(), bom.Id, rm1Id, "Desk Top", 2m, 50m);
        var bomItem2 = new BomItem(Guid.NewGuid(), bom.Id, rm2Id, "Desk Leg", 5m, 10m);
        bom.Items.Add(bomItem1);
        bom.Items.Add(bomItem2);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-2026-001", fgItemId, bom.Id, 10m)
        {
            SourceWarehouseId = whId,
            CreationTime = DateTime.UtcNow
        };
        wo.Submit();

        // 10 desks need 20 tops, 50 legs
        var req1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rm1Id, "Desk Top", 20m)
        {
            SourceWarehouseId = whId
        };
        var req2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rm2Id, "Desk Leg", 50m)
        {
            SourceWarehouseId = whId
        };
        wo.RequiredItems.Add(req1);
        wo.RequiredItems.Add(req2);

        // Bins: rm1 has 20 (buildable = 10), rm2 has 30 (buildable = 6)
        var bin1 = new Bin(Guid.NewGuid(), rm1Id, whId) { ActualQty = 20m };
        var bin2 = new Bin(Guid.NewGuid(), rm2Id, whId) { ActualQty = 30m };

        _woRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, rm1, rm2 }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new WorkOrderStockReportRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalWorkOrders.ShouldBe(1);
        result.ReadyToBuildCount.ShouldBe(0);
        result.NotReadyCount.ShouldBe(1);
        result.TotalBuildableQty.ShouldBe(6m);

        var row = result.Rows.First();
        row.WorkOrderNumber.ShouldBe("WO-2026-001");
        row.QtyToBuild.ShouldBe(10m);
        row.BuildableQty.ShouldBe(6m);
        row.ReadyToBuild.ShouldBeFalse();
        row.RequiredItemsCount.ShouldBe(2);
        row.InStockItemsCount.ShouldBe(1);
        row.ComponentDetails.Count.ShouldBe(2);

        var topDetail = row.ComponentDetails.First(c => c.ItemId == rm1Id);
        topDetail.AvailableQty.ShouldBe(20m);
        topDetail.RequiredQty.ShouldBe(20m);
        topDetail.IsInStock.ShouldBeTrue();

        var legDetail = row.ComponentDetails.First(c => c.ItemId == rm2Id);
        legDetail.AvailableQty.ShouldBe(30m);
        legDetail.RequiredQty.ShouldBe(50m);
        legDetail.IsInStock.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportAsync_WhenAllInStock_MarksReadyToBuild()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-CHAIR", "Dining Chair", ItemType.Goods);

        var rm1Id = Guid.NewGuid();
        var rm1 = new Item(rm1Id, _companyId, "RM-FRAME", "Chair Frame", ItemType.Goods);

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Stores - Central");

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-CHAIR-001", fgItemId) { Quantity = 1m };
        var bomItem = new BomItem(Guid.NewGuid(), bom.Id, rm1Id, "Chair Frame", 1m, 25m);
        bom.Items.Add(bomItem);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-2026-002", fgItemId, bom.Id, 5m)
        {
            SourceWarehouseId = whId,
            CreationTime = DateTime.UtcNow
        };
        wo.Submit();

        var req1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rm1Id, "Chair Frame", 5m)
        {
            SourceWarehouseId = whId
        };
        wo.RequiredItems.Add(req1);

        var bin = new Bin(Guid.NewGuid(), rm1Id, whId) { ActualQty = 15m };

        _woRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, rm1 }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new WorkOrderStockReportRequestDto
        {
            CompanyId = _companyId,
            ReadyToBuildOnly = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalWorkOrders.ShouldBe(1);
        result.ReadyToBuildCount.ShouldBe(1);
        result.NotReadyCount.ShouldBe(0);

        var row = result.Rows.First();
        row.ReadyToBuild.ShouldBeTrue();
        row.BuildableQty.ShouldBe(5m);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesCompletedAndCancelledWorkOrders()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-001", "Product A", ItemType.Goods);
        var bomId = Guid.NewGuid();

        var activeWo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-ACTIVE", fgItemId, bomId, 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        activeWo.Submit();

        var completedWo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-COMPLETED", fgItemId, bomId, 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        completedWo.Submit();
        completedWo.Start();
        completedWo.RecordProduction(10m);

        var cancelledWo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-CANCELLED", fgItemId, bomId, 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        cancelledWo.Submit();
        cancelledWo.Cancel();

        _woRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { activeWo, completedWo, cancelledWo }.AsQueryable()));

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials>().AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new WorkOrderStockReportRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.TotalWorkOrders.ShouldBe(1);
        result.Rows.First().WorkOrderNumber.ShouldBe("WO-ACTIVE");
    }
}
