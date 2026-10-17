using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class BomOperationsTimeAppServiceTests
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<BomItem, Guid> _bomItemRepo = Substitute.For<IRepository<BomItem, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Operation, Guid> _operationRepo = Substitute.For<IRepository<Operation, Guid>>();
    private readonly IRepository<Workstation, Guid> _workstationRepo = Substitute.For<IRepository<Workstation, Guid>>();

    private readonly BomOperationsTimeAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public BomOperationsTimeAppServiceTests()
    {
        _appService = new BomOperationsTimeAppService(
            _bomRepo,
            _bomItemRepo,
            _itemRepo,
            _operationRepo,
            _workstationRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsOperationRowsWithExpectedValues()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-001", "Widget Finished Good", ItemType.Goods) { Uom = "Nos" };

        var opId = Guid.NewGuid();
        var operation = new Operation(opId, "Assembly");

        var wsId = Guid.NewGuid();
        var workstation = new Workstation(wsId, _companyId, "Assembly Station 1");

        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, _companyId, "BOM-001", itemId)
        {
            IsActive = true,
            WithOperations = true,
            Uom = "Nos"
        };
        var bomOp = new BomOperation(Guid.NewGuid(), bomId, opId, 1, 45m, wsId);
        bom.Operations.Add(bomOp);

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _operationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Operation> { operation }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation> { workstation }.AsQueryable()));

        _bomItemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<BomItem>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BomOperationsTimeRequestDto
        {
            CompanyId = _companyId,
            BomId = bomId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalBoms.ShouldBe(1);
        result.TotalOperations.ShouldBe(1);
        result.TotalTimeInMinutes.ShouldBe(45m);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.BomId.ShouldBe(bomId);
        row.BomNumber.ShouldBe("BOM-001");
        row.ItemId.ShouldBe(itemId);
        row.ItemCode.ShouldBe("ITEM-001");
        row.ItemName.ShouldBe("Widget Finished Good");
        row.OperationId.ShouldBe(opId);
        row.OperationName.ShouldBe("Assembly");
        row.WorkstationId.ShouldBe(wsId);
        row.WorkstationName.ShouldBe("Assembly Station 1");
        row.TimeInMins.ShouldBe(45m);
        row.UsedAsSubassemblyItems.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_FiltersByItemId_IncludesMatchingAndExcludesOther()
    {
        // Arrange
        var item1Id = Guid.NewGuid();
        var item1 = new Item(item1Id, _companyId, "FG-001", "Item One", ItemType.Goods);
        var item2Id = Guid.NewGuid();
        var item2 = new Item(item2Id, _companyId, "FG-002", "Item Two", ItemType.Goods);

        var opId = Guid.NewGuid();
        var operation = new Operation(opId, "Drilling");

        var bom1 = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001", item1Id) { IsActive = true, WithOperations = true };
        bom1.Operations.Add(new BomOperation(Guid.NewGuid(), bom1.Id, opId, 1, 30m));

        var bom2 = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-002", item2Id) { IsActive = true, WithOperations = true };
        bom2.Operations.Add(new BomOperation(Guid.NewGuid(), bom2.Id, opId, 1, 20m));

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom1, bom2 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        _operationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Operation> { operation }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation>().AsQueryable()));

        _bomItemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<BomItem>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BomOperationsTimeRequestDto
        {
            CompanyId = _companyId,
            ItemId = item1Id
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ItemId.ShouldBe(item1Id);
        result.Rows[0].BomNumber.ShouldBe("BOM-001");
    }

    [Fact]
    public async Task GetReportAsync_FiltersByWorkstationId()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "FG-001", "Item One", ItemType.Goods);

        var ws1Id = Guid.NewGuid();
        var ws2Id = Guid.NewGuid();
        var ws1 = new Workstation(ws1Id, _companyId, "WS-1");
        var ws2 = new Workstation(ws2Id, _companyId, "WS-2");

        var opId = Guid.NewGuid();
        var operation = new Operation(opId, "Welding");

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-WS", itemId) { IsActive = true, WithOperations = true };
        bom.Operations.Add(new BomOperation(Guid.NewGuid(), bom.Id, opId, 1, 15m, ws1Id));
        bom.Operations.Add(new BomOperation(Guid.NewGuid(), bom.Id, opId, 2, 25m, ws2Id));

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _operationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Operation> { operation }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation> { ws1, ws2 }.AsQueryable()));

        _bomItemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<BomItem>().AsQueryable()));

        // Act: Filter by WS-1
        var result = await _appService.GetReportAsync(new BomOperationsTimeRequestDto
        {
            CompanyId = _companyId,
            WorkstationId = ws1Id
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WorkstationId.ShouldBe(ws1Id);
        result.Rows[0].TimeInMins.ShouldBe(15m);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesInactiveOrNonOperationBoms()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "FG-001", "Item One", ItemType.Goods);
        var opId = Guid.NewGuid();
        var operation = new Operation(opId, "Cutting");

        var inactiveBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-INACTIVE", itemId)
        {
            IsActive = false,
            WithOperations = true
        };
        inactiveBom.Operations.Add(new BomOperation(Guid.NewGuid(), inactiveBom.Id, opId, 1, 10m));

        var noOpsBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-NOOPS", itemId)
        {
            IsActive = true,
            WithOperations = false
        };

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { inactiveBom, noOpsBom }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _operationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Operation> { operation }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation>().AsQueryable()));

        _bomItemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<BomItem>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BomOperationsTimeRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.Rows.ShouldBeEmpty();
        result.TotalBoms.ShouldBe(0);
        result.TotalOperations.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesSubAssemblyUsageCount()
    {
        // Arrange
        var subBomItemId = Guid.NewGuid();
        var subBomItem = new Item(subBomItemId, _companyId, "SUB-001", "Sub Assembly Motor", ItemType.Goods);

        var opId = Guid.NewGuid();
        var operation = new Operation(opId, "Winding");

        var subBomId = Guid.NewGuid();
        var subBom = new BillOfMaterials(subBomId, _companyId, "BOM-SUB-01", subBomItemId)
        {
            IsActive = true,
            WithOperations = true
        };
        subBom.Operations.Add(new BomOperation(Guid.NewGuid(), subBomId, opId, 1, 60m));

        // Two different parent BOM items reference this sub-BOM
        var parentBomItem1 = new BomItem(Guid.NewGuid(), Guid.NewGuid(), subBomItemId, "Sub Assembly Motor", 1m, 100m)
        {
            SubBomId = subBomId
        };
        var parentBomItem2 = new BomItem(Guid.NewGuid(), Guid.NewGuid(), subBomItemId, "Sub Assembly Motor", 2m, 100m)
        {
            SubBomId = subBomId
        };

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { subBom }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { subBomItem }.AsQueryable()));

        _operationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Operation> { operation }.AsQueryable()));

        _workstationRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Workstation>().AsQueryable()));

        _bomItemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<BomItem> { parentBomItem1, parentBomItem2 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BomOperationsTimeRequestDto
        {
            CompanyId = _companyId,
            BomId = subBomId
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].UsedAsSubassemblyItems.ShouldBe(2);
    }
}
