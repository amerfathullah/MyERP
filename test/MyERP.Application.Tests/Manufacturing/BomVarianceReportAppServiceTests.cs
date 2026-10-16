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

public class BomVarianceReportAppServiceTests
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly BomVarianceReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public BomVarianceReportAppServiceTests()
    {
        _appService = new BomVarianceReportAppService(
            _workOrderRepo,
            _bomRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesVarianceForOverproducedWorkOrders()
    {
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();

        var fgItem = new Item(fgItemId, _companyId, "FG-001", "Finished Table", ItemType.Goods);
        var rmItem = new Item(rmItemId, _companyId, "RM-001", "Wood Plank", ItemType.Goods);

        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, _companyId, "BOM-TABLE-01", fgItemId);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-VAR-001", fgItemId, bomId, 10m)
        {
            ProducedQuantity = 12m
        };
        wo.Submit();
        wo.Start();

        var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rmItemId, "Wood Plank", 20m)
        {
            ConsumedQuantity = 26m
        };
        wo.RequiredItems.Add(woItem);

        _workOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _bomRepo.GetListAsync(Arg.Any<Expression<Func<BillOfMaterials, bool>>>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));

        var result = await _appService.GetReportAsync(new BomVarianceReportRequestDto
        {
            CompanyId = _companyId,
            OnlyOverproduced = true
        });

        result.TotalWorkOrders.ShouldBe(1);
        result.TotalOrderedQty.ShouldBe(10m);
        result.TotalProducedQty.ShouldBe(12m);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.WorkOrderNumber.ShouldBe("WO-VAR-001");
        row.BomNumber.ShouldBe("BOM-TABLE-01");
        row.FinishedGoodItemCode.ShouldBe("FG-001");
        row.RawMaterialItemCode.ShouldBe("RM-001");
        row.RequiredQty.ShouldBe(20m);
        row.ConsumedQty.ShouldBe(26m);
        row.VarianceQty.ShouldBe(6m); // 26 - 20
    }

    [Fact]
    public async Task GetReportAsync_FiltersByBomId_ReturnsOnlyMatching()
    {
        var fgItemId = Guid.NewGuid();
        var bomId1 = Guid.NewGuid();
        var bomId2 = Guid.NewGuid();

        var bom1 = new BillOfMaterials(bomId1, _companyId, "BOM-01", fgItemId);
        var bom2 = new BillOfMaterials(bomId2, _companyId, "BOM-02", fgItemId);

        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-01", fgItemId, bomId1, 10m)
        {
            ProducedQuantity = 15m
        };
        wo1.Submit();

        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-02", fgItemId, bomId2, 10m)
        {
            ProducedQuantity = 15m
        };
        wo2.Submit();

        _workOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { wo1, wo2 }.AsQueryable()));

        _bomRepo.GetListAsync(Arg.Any<Expression<Func<BillOfMaterials, bool>>>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom1 }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item>()));

        var result = await _appService.GetReportAsync(new BomVarianceReportRequestDto
        {
            CompanyId = _companyId,
            BomId = bomId1,
            OnlyOverproduced = true
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WorkOrderNumber.ShouldBe("WO-01");
    }

    [Fact]
    public async Task GetReportAsync_OnlyOverproducedFalse_IncludesNormalOrders()
    {
        var fgItemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var bom = new BillOfMaterials(bomId, _companyId, "BOM-01", fgItemId);

        var woNormal = new WorkOrder(Guid.NewGuid(), _companyId, "WO-NORM", fgItemId, bomId, 10m)
        {
            ProducedQuantity = 10m
        };
        woNormal.Submit();

        _workOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<WorkOrder, object>>[]>())
            .Returns(Task.FromResult(new List<WorkOrder> { woNormal }.AsQueryable()));

        _bomRepo.GetListAsync(Arg.Any<Expression<Func<BillOfMaterials, bool>>>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item>()));

        var result = await _appService.GetReportAsync(new BomVarianceReportRequestDto
        {
            CompanyId = _companyId,
            OnlyOverproduced = false
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WorkOrderNumber.ShouldBe("WO-NORM");
    }
}
