using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class IssuedItemsAgainstWorkOrderAppServiceTests
{
    private readonly IRepository<StockEntry, Guid> _seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<WorkOrder, Guid> _woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();

    private readonly IssuedItemsAgainstWorkOrderAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public IssuedItemsAgainstWorkOrderAppServiceTests()
    {
        _appService = new IssuedItemsAgainstWorkOrderAppService(
            _seRepo,
            _woRepo,
            _itemRepo,
            _warehouseRepo,
            _binRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsIssuedItemsWithProjectedQuantities()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-DESK", "Study Desk", ItemType.Goods);

        var rmItemId = Guid.NewGuid();
        var rmItem = new Item(rmItemId, _companyId, "RM-WOOD", "Wood Plank", ItemType.Goods);

        var srcWhId = Guid.NewGuid();
        var srcWh = new Warehouse(srcWhId, _companyId, "Stores - Central");

        var tgtWhId = Guid.NewGuid();
        var tgtWh = new Warehouse(tgtWhId, _companyId, "WIP - Assembly");

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-2026-001", fgItemId, Guid.NewGuid(), 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        wo.Submit();

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            EntryNumber = "STE-2026-001",
            WorkOrderId = wo.Id
        };
        se.AddItem(rmItemId, 20m, srcWhId, tgtWhId, 10m);
        se.Submit();

        var bin = new Bin(Guid.NewGuid(), rmItemId, srcWhId)
        {
            ActualQty = 50m,
            OrderedQty = 10m,
            ReservedQty = 15m
        }; // Projected = 50 + 10 - 15 = 45

        _seRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(new List<StockEntry> { se }.AsQueryable()));

        _woRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, rmItem }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { srcWh, tgtWh }.AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new IssuedItemsAgainstWorkOrderRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-7),
            ToDate = DateTime.UtcNow.AddDays(7)
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalWorkOrders.ShouldBe(1);
        result.TotalIssuedLines.ShouldBe(1);
        result.TotalQuantity.ShouldBe(20m);
        result.TotalAmount.ShouldBe(200m);

        var row = result.Rows[0];
        row.WorkOrderNumber.ShouldBe("WO-2026-001");
        row.StockEntryNumber.ShouldBe("STE-2026-001");
        row.FinishedGoodCode.ShouldBe("FG-DESK");
        row.ItemCode.ShouldBe("RM-WOOD");
        row.Quantity.ShouldBe(20m);
        row.Amount.ShouldBe(200m);
        row.SourceWarehouseName.ShouldBe("Stores - Central");
        row.TargetWarehouseName.ShouldBe("WIP - Assembly");
        row.ProjectedQtyAtSource.ShouldBe(45m);
        row.ActualQtyAtSource.ShouldBe(50m);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesReturnsAndDraftStockEntries()
    {
        // Arrange
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        wo.Submit();

        var draftSe = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id
        };
        draftSe.AddItem(Guid.NewGuid(), 5m);

        var returnSe = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };
        returnSe.AddItem(Guid.NewGuid(), 5m);
        returnSe.Submit();

        var validSe = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id
        };
        validSe.AddItem(Guid.NewGuid(), 15m);
        validSe.Submit();

        _seRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(new List<StockEntry> { draftSe, returnSe, validSe }.AsQueryable()));

        _woRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new IssuedItemsAgainstWorkOrderRequestDto
        {
            CompanyId = _companyId
        });

        // Assert: only validSe is included
        result.TotalIssuedLines.ShouldBe(1);
        result.TotalQuantity.ShouldBe(15m);
    }
}
