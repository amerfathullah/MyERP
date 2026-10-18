using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class ProductionPlanningReportAppServiceTests
{
    private readonly IRepository<WorkOrder, Guid> _woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<MaterialRequest, Guid> _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<PurchaseOrder, Guid> _poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly ProductionPlanningReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ProductionPlanningReportAppServiceTests()
    {
        _appService = new ProductionPlanningReportAppService(
            _woRepo,
            _soRepo,
            _mrRepo,
            _bomRepo,
            _poRepo,
            _binRepo,
            _itemRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetReportAsync_WorkOrder_AllotsStock_CalculatesShortageAndPoArrival()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-DESK", "Study Desk", ItemType.Goods);

        var rmItemId = Guid.NewGuid();
        var rmItem = new Item(rmItemId, _companyId, "RM-WOOD", "Oak Wood", ItemType.Goods);

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Raw Material Stores");

        var bomId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-2026-001", fgItemId, bomId, 10m)
        {
            FgWarehouseId = whId,
            SourceWarehouseId = whId,
            PlannedStartDate = DateTime.UtcNow.Date.AddDays(2)
        };
        wo.Submit();

        var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItemId, "Oak Wood", 20m)
        {
            SourceWarehouseId = whId
        };
        wo.RequiredItems.Add(woItem);

        // Bin has 15 units available
        var bin = new Bin(Guid.NewGuid(), rmItemId, whId) { ActualQty = 15m };

        // Open PO has 10 units pending delivery
        var arrivalDate = DateTime.UtcNow.Date.AddDays(5);
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "PO-2026-001", DateTime.UtcNow.Date)
        {
            ExpectedDeliveryDate = arrivalDate
        };
        po.AddItem(rmItemId, "Oak Wood", 10m, 50m, 0m, "Unit", whId);
        po.Submit();

        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }));
        _binRepo.GetListAsync().Returns(Task.FromResult(new List<Bin> { bin }));

        _woRepo.WithDetailsAsync(Arg.Any<Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _poRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { po }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ProductionPlanningReportFilterDto
        {
            CompanyId = _companyId,
            BasedOn = "WorkOrder"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalOrdersCount.ShouldBe(1);
        result.TotalRawMaterialsCount.ShouldBe(1);
        result.TotalRequiredQty.ShouldBe(20m);
        result.TotalAllottedQty.ShouldBe(15m);
        result.TotalShortageQty.ShouldBe(5m);
        result.TotalExpectedArrivalQty.ShouldBe(10m);

        var row = result.Rows.First();
        row.OrderNo.ShouldBe("WO-2026-001");
        row.OrderType.ShouldBe("Work Order");
        row.ProductionItemCode.ShouldBe("FG-DESK");
        row.RawMaterialCode.ShouldBe("RM-WOOD");
        row.RequiredQty.ShouldBe(20m);
        row.AllottedQty.ShouldBe(15m);
        row.ShortageQty.ShouldBe(5m);
        row.ExpectedArrivalQty.ShouldBe(10m);
        row.ExpectedArrivalDate.ShouldBe(arrivalDate);
    }

    [Fact]
    public async Task GetReportAsync_SalesOrder_ExplodesBom_AllotsStock()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-CHAIR", "Dining Chair", ItemType.Goods);

        var rmItemId = Guid.NewGuid();
        var rmItem = new Item(rmItemId, _companyId, "RM-STEEL", "Steel Leg", ItemType.Goods);

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Assembly Store");

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-CHAIR-001", fgItemId)
        {
            Quantity = 1m,
            IsActive = true,
            SourceWarehouseId = whId,
            TargetWarehouseId = whId
        };
        var bomItem = new BomItem(Guid.NewGuid(), bom.Id, rmItemId, "Steel Leg", 4m, 12m)
        {
            SourceWarehouseId = whId
        };
        bom.Items.Add(bomItem);

        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-2026-001", DateTime.UtcNow.Date);
        so.AddItem(fgItemId, "Dining Chair", 5m, 150m, 0m);
        so.Submit();

        var bin = new Bin(Guid.NewGuid(), rmItemId, whId) { ActualQty = 12m };

        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }));
        _binRepo.GetListAsync().Returns(Task.FromResult(new List<Bin> { bin }));

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _poRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ProductionPlanningReportFilterDto
        {
            CompanyId = _companyId,
            BasedOn = "SalesOrder"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalOrdersCount.ShouldBe(1);
        result.TotalRawMaterialsCount.ShouldBe(1);
        result.TotalRequiredQty.ShouldBe(20m); // 5 chairs * 4 legs = 20
        result.TotalAllottedQty.ShouldBe(12m); // Bin has 12
        result.TotalShortageQty.ShouldBe(8m);

        var row = result.Rows.First();
        row.OrderNo.ShouldBe("SO-2026-001");
        row.OrderType.ShouldBe("Sales Order");
        row.ProductionItemCode.ShouldBe("FG-CHAIR");
        row.RawMaterialCode.ShouldBe("RM-STEEL");
        row.RequiredQty.ShouldBe(20m);
        row.AllottedQty.ShouldBe(12m);
        row.ShortageQty.ShouldBe(8m);
    }

    [Fact]
    public async Task GetReportAsync_MaterialRequest_FiltersByItemAndWarehouse()
    {
        // Arrange
        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-CABINET", "Office Cabinet", ItemType.Goods);

        var rmItemId = Guid.NewGuid();
        var rmItem = new Item(rmItemId, _companyId, "RM-HANDLE", "Metal Handle", ItemType.Goods);

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Cabinet Warehouse");

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-CAB-001", fgItemId)
        {
            Quantity = 1m,
            IsActive = true,
            SourceWarehouseId = whId,
            TargetWarehouseId = whId
        };
        var bomItem = new BomItem(Guid.NewGuid(), bom.Id, rmItemId, "Metal Handle", 2m, 5m)
        {
            SourceWarehouseId = whId
        };
        bom.Items.Add(bomItem);

        var mr = new MaterialRequest(Guid.NewGuid(), _companyId, "MR-2026-001", MaterialRequestType.Manufacture, DateTime.UtcNow.Date);
        mr.AddItem(fgItemId, "Office Cabinet", 3m, "Unit", whId);
        mr.Submit();

        var bin = new Bin(Guid.NewGuid(), rmItemId, whId) { ActualQty = 10m };

        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }));
        _binRepo.GetListAsync().Returns(Task.FromResult(new List<Bin> { bin }));

        _mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _poRepo.WithDetailsAsync(Arg.Any<Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ProductionPlanningReportFilterDto
        {
            CompanyId = _companyId,
            BasedOn = "MaterialRequest",
            RawMaterialWarehouseId = whId,
            SearchTerm = "HANDLE"
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalOrdersCount.ShouldBe(1);
        result.TotalRequiredQty.ShouldBe(6m); // 3 cabinets * 2 handles = 6
        result.TotalAllottedQty.ShouldBe(6m); // Bin has 10, all 6 allotted
        result.TotalShortageQty.ShouldBe(0m);
    }
}
