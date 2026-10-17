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

public class WorkOrderConsumedMaterialsAppServiceTests
{
    private readonly IRepository<WorkOrder, Guid> _woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<StockEntry, Guid> _seRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly WorkOrderConsumedMaterialsAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public WorkOrderConsumedMaterialsAppServiceTests()
    {
        _appService = new WorkOrderConsumedMaterialsAppService(
            _woRepo,
            _seRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesExtraConsumedAndReturnedQuantities()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-001", "Finished Table", ItemType.Goods);

        var rm1Id = Guid.NewGuid();
        var rm1 = new Item(rm1Id, _companyId, "RM-WOOD", "Wood Plank", ItemType.Goods);

        var rm2Id = Guid.NewGuid();
        var rm2 = new Item(rm2Id, _companyId, "RM-SCREW", "Extra Screws", ItemType.Goods);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-2026-001", fgItemId, Guid.NewGuid(), 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        wo.Submit();
        wo.Start();

        // RM1: required 20, consumed 25 -> extra 5
        var req1 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rm1Id, "Wood Plank", 20m)
        {
            TransferredQuantity = 25m,
            ConsumedQuantity = 25m,
            StockUom = "Nos"
        };
        wo.RequiredItems.Add(req1);

        // RM2: additional item, required 0, consumed 3 -> extra 3
        var req2 = new WorkOrderItem(Guid.NewGuid(), wo.Id, rm2Id, "Extra Screws", 0m)
        {
            TransferredQuantity = 3m,
            ConsumedQuantity = 3m,
            IsAdditionalItem = true,
            StockUom = "Nos"
        };
        wo.RequiredItems.Add(req2);

        // Return stock entry for RM1: quantity 2 returned
        var returnSe = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };
        returnSe.AddItem(rm1Id, 2m);
        returnSe.Submit();

        _woRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _seRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(new List<StockEntry> { returnSe }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, rm1, rm2 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new WorkOrderConsumedMaterialsRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-5),
            ToDate = DateTime.UtcNow.AddDays(5)
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalWorkOrders.ShouldBe(1);
        result.TotalRawMaterialLines.ShouldBe(2);
        result.TotalExtraConsumedItems.ShouldBe(2);
        result.TotalExtraConsumedQty.ShouldBe(8m); // 5 + 3
        result.TotalReturnedQty.ShouldBe(2m);

        var row1 = result.Rows.First(r => r.RawMaterialItemId == rm1Id);
        row1.WorkOrderNumber.ShouldBe("WO-2026-001");
        row1.ProductionItemCode.ShouldBe("FG-001");
        row1.RawMaterialItemCode.ShouldBe("RM-WOOD");
        row1.RequiredQty.ShouldBe(20m);
        row1.ConsumedQty.ShouldBe(25m);
        row1.ExtraConsumedQty.ShouldBe(5m);
        row1.ReturnedQty.ShouldBe(2m);
        row1.IsAdditionalItem.ShouldBeFalse();

        var row2 = result.Rows.First(r => r.RawMaterialItemId == rm2Id);
        row2.RawMaterialItemCode.ShouldBe("RM-SCREW");
        row2.ConsumedQty.ShouldBe(3m);
        row2.ExtraConsumedQty.ShouldBe(3m);
        row2.ReturnedQty.ShouldBe(0m);
        row2.IsAdditionalItem.ShouldBeTrue();
    }

    [Fact]
    public async Task GetReportAsync_WithShowExtraConsumedMaterialsOnly_FiltersOutNormalConsumption()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-001", "Finished Table", ItemType.Goods);

        var normalRmId = Guid.NewGuid();
        var normalRm = new Item(normalRmId, _companyId, "RM-NORMAL", "Normal RM", ItemType.Goods);

        var extraRmId = Guid.NewGuid();
        var extraRm = new Item(extraRmId, _companyId, "RM-EXTRA", "Extra RM", ItemType.Goods);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-2026-002", fgItemId, Guid.NewGuid(), 10m)
        {
            CreationTime = DateTime.UtcNow
        };
        wo.Submit();
        wo.Start();

        // Normal: required 20, consumed 20 -> extra 0
        var reqNormal = new WorkOrderItem(Guid.NewGuid(), wo.Id, normalRmId, "Normal RM", 20m)
        {
            ConsumedQuantity = 20m
        };
        wo.RequiredItems.Add(reqNormal);

        // Extra: required 10, consumed 14 -> extra 4
        var reqExtra = new WorkOrderItem(Guid.NewGuid(), wo.Id, extraRmId, "Extra RM", 10m)
        {
            ConsumedQuantity = 14m
        };
        wo.RequiredItems.Add(reqExtra);

        _woRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _seRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { fgItem, normalRm, extraRm }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new WorkOrderConsumedMaterialsRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-5),
            ToDate = DateTime.UtcNow.AddDays(5),
            ShowExtraConsumedMaterialsOnly = true
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].RawMaterialItemCode.ShouldBe("RM-EXTRA");
        result.Rows[0].ExtraConsumedQty.ShouldBe(4m);
    }

    [Fact]
    public async Task GetReportAsync_FiltersByWorkOrderIdAndStatus()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var rmId = Guid.NewGuid();

        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", fgItemId, Guid.NewGuid(), 5m)
        {
            CreationTime = DateTime.UtcNow
        };
        wo1.Submit();
        wo1.Start();
        wo1.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo1.Id, rmId, "RM", 10m));

        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", fgItemId, Guid.NewGuid(), 5m)
        {
            CreationTime = DateTime.UtcNow
        };
        wo2.Submit();
        wo2.Start();
        wo2.RecordProduction(5m);
        wo2.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo2.Id, rmId, "RM", 10m));

        _woRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo1, wo2 }.AsQueryable()));

        _seRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(new List<StockEntry>().AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));

        // Act - filter by status Completed
        var result = await _appService.GetReportAsync(new WorkOrderConsumedMaterialsRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-5),
            ToDate = DateTime.UtcNow.AddDays(5),
            Status = WorkOrderStatus.Completed
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WorkOrderId.ShouldBe(wo2.Id);
        result.Rows[0].Status.ShouldBe("Completed");
    }
}
