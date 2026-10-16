using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkOrderSummaryAppServiceTests
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<ManufacturingSettings, Guid> _mfgSettingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();

    private readonly WorkOrderSummaryAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public WorkOrderSummaryAppServiceTests()
    {
        _appService = new WorkOrderSummaryAppService(
            _workOrderRepo,
            _itemRepo,
            _salesOrderRepo,
            _mfgSettingsRepo,
            _stockEntryRepo);
    }

    [Fact]
    public async Task GetSummaryAsync_CalculatesAgeAndTotals_Successfully()
    {
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-01", "Motor Engine", ItemType.Goods);

        var soId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, customerId, "SO-2026-001", DateTime.UtcNow.AddDays(-10));

        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", itemId, Guid.NewGuid(), 100m)
        {
            PlannedStartDate = DateTime.UtcNow.AddDays(-15),
            PlannedEndDate = DateTime.UtcNow.AddDays(-5),
            ProducedQuantity = 40m,
            SalesOrderId = soId
        };
        wo1.CreationTime = DateTime.UtcNow.AddDays(-15);

        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", itemId, Guid.NewGuid(), 50m)
        {
            PlannedStartDate = DateTime.UtcNow.AddDays(-5),
            PlannedEndDate = DateTime.UtcNow.AddDays(5)
        };
        wo2.CreationTime = DateTime.UtcNow.AddDays(-5);
        wo2.Submit();
        wo2.Start();
        wo2.RecordProduction(50m); // Sets Completed status and ProducedQuantity = 50

        var workOrders = new List<WorkOrder> { wo1, wo2 };
        _workOrderRepo.GetQueryableAsync()
            .Returns(Task.FromResult(workOrders.AsQueryable()));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        _salesOrderRepo.GetListAsync(Arg.Any<Expression<Func<SalesOrder, bool>>>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }));

        var result = await _appService.GetSummaryAsync(new WorkOrderSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(30),
            BasedOn = "Creation Date",
            ChartsBasedOn = "Quantity"
        });

        result.Rows.Count.ShouldBe(2);
        result.TotalWorkOrders.ShouldBe(2);
        result.TotalProduceQty.ShouldBe(150m);
        result.TotalProducedQty.ShouldBe(90m);

        var row1 = result.Rows.First(r => r.Id == wo1.Id);
        row1.Age.ShouldBeGreaterThanOrEqualTo(14);
        row1.SalesOrderNumber.ShouldBe("SO-2026-001");
        row1.ProductionItemCode.ShouldBe("ITEM-01");

        var row2 = result.Rows.First(r => r.Id == wo2.Id);
        row2.Age.ShouldBe(0); // Completed WO has age 0
        row2.Status.ShouldBe(nameof(WorkOrderStatus.Completed));

        result.Chart.ChartType.ShouldBe("bar");
        result.Chart.Datasets.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetSummaryAsync_FiltersByAge_ExcludesYoungerOrders()
    {
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-02", "Gear", ItemType.Goods);

        var woOld = new WorkOrder(Guid.NewGuid(), _companyId, "WO-OLD", itemId, Guid.NewGuid(), 20m)
        {
            PlannedStartDate = DateTime.UtcNow.AddDays(-20)
        };
        woOld.CreationTime = DateTime.UtcNow.AddDays(-20);

        var woNew = new WorkOrder(Guid.NewGuid(), _companyId, "WO-NEW", itemId, Guid.NewGuid(), 10m)
        {
            PlannedStartDate = DateTime.UtcNow.AddDays(-3)
        };
        woNew.CreationTime = DateTime.UtcNow.AddDays(-3);

        var workOrders = new List<WorkOrder> { woOld, woNew };
        _workOrderRepo.GetQueryableAsync()
            .Returns(Task.FromResult(workOrders.AsQueryable()));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));
        _salesOrderRepo.GetListAsync(Arg.Any<Expression<Func<SalesOrder, bool>>>())
            .Returns(Task.FromResult(new List<SalesOrder>()));

        var result = await _appService.GetSummaryAsync(new WorkOrderSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(30),
            Age = 10
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].WorkOrderNumber.ShouldBe("WO-OLD");
    }

    [Fact]
    public async Task GetSummaryAsync_WithAlternativeFinishedGoods_FormatsOutputs()
    {
        var origItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();

        var origItem = new Item(origItemId, _companyId, "ITEM-STD", "Standard Chair", ItemType.Goods);
        var altItem = new Item(altItemId, _companyId, "ITEM-ALT", "Alternative Deluxe Chair", ItemType.Goods);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-REPACK", origItemId, Guid.NewGuid(), 10m)
        {
            ProducedQuantity = 10m
        };
        wo.CreationTime = DateTime.UtcNow.AddDays(-2);

        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId)
        {
            AllowAlternativeFinishedGoods = true
        };

        var se = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsFgConversion = true
        };
        // Convert 4 units of origItem to 4 units of altItem
        se.AddItem(origItemId, 4m, sourceWarehouseId: Guid.NewGuid(), isFinishedItem: false);
        se.AddItem(altItemId, 4m, targetWarehouseId: Guid.NewGuid(), isFinishedItem: true);
        se.Submit();

        _workOrderRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));

        _mfgSettingsRepo.FindAsync(Arg.Any<Expression<Func<ManufacturingSettings, bool>>>())
            .Returns(Task.FromResult<ManufacturingSettings?>(settings));

        _stockEntryRepo.WithDetailsAsync(Arg.Any<Expression<Func<StockEntry, object>>[]>())
            .Returns(Task.FromResult(new List<StockEntry> { se }.AsQueryable()));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { origItem, altItem }));

        _salesOrderRepo.GetListAsync(Arg.Any<Expression<Func<SalesOrder, bool>>>())
            .Returns(Task.FromResult(new List<SalesOrder>()));

        var result = await _appService.GetSummaryAsync(new WorkOrderSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-10),
            ToDate = DateTime.UtcNow.AddDays(10)
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ActualFinishedGoods.ShouldNotBeNull();
        result.Rows[0].ActualFinishedGoods!.ShouldContain("ITEM-STD: 6");
        result.Rows[0].ActualFinishedGoods!.ShouldContain("ITEM-ALT: 4");
    }

    [Fact]
    public async Task GetSummaryAsync_ChartsBasedOn_GeneratesDonutCharts()
    {
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-03", "Valve", ItemType.Goods);

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-STATUS", itemId, Guid.NewGuid(), 10m)
        {
            PlannedStartDate = DateTime.UtcNow.AddDays(-10)
        };
        wo.CreationTime = DateTime.UtcNow.AddDays(-10);

        _workOrderRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<WorkOrder> { wo }.AsQueryable()));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));
        _salesOrderRepo.GetListAsync(Arg.Any<Expression<Func<SalesOrder, bool>>>())
            .Returns(Task.FromResult(new List<SalesOrder>()));

        // Status chart
        var statusResult = await _appService.GetSummaryAsync(new WorkOrderSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-20),
            ToDate = DateTime.UtcNow.AddDays(10),
            ChartsBasedOn = "Status"
        });

        statusResult.Chart.ChartType.ShouldBe("donut");
        statusResult.Chart.Datasets[0].Name.ShouldBe("Status");

        // Age chart
        var ageResult = await _appService.GetSummaryAsync(new WorkOrderSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-20),
            ToDate = DateTime.UtcNow.AddDays(10),
            ChartsBasedOn = "Age"
        });

        ageResult.Chart.ChartType.ShouldBe("donut");
        ageResult.Chart.Labels.ShouldContain("0-30 Days");
        ageResult.Chart.Datasets[0].Values[0].ShouldBe(1); // 10 days old falls in 0-30 Days
    }
}
