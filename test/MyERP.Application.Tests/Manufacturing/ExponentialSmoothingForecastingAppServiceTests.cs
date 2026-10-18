using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class ExponentialSmoothingForecastingAppServiceTests
{
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly ExponentialSmoothingForecastingAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ExponentialSmoothingForecastingAppServiceTests()
    {
        _appService = new ExponentialSmoothingForecastingAppService(
            _soRepo,
            _dnRepo,
            _siRepo,
            _itemRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetForecastAsync_Monthly_CalculatesExactSmoothingFormula()
    {
        // Arrange
        // Historical demand: Mar 2026 = 7, Apr 2026 = 4, May 2026 = 9
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-WIDGET", "Widget A", ItemType.Goods);

        var soMar = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-01", new DateTime(2026, 3, 15));
        soMar.AddItem(itemId, "Widget A", 7m, 10m, 0m);
        soMar.Submit();

        var soApr = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-02", new DateTime(2026, 4, 15));
        soApr.AddItem(itemId, "Widget A", 4m, 10m, 0m);
        soApr.Submit();

        var soMay = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-03", new DateTime(2026, 5, 15));
        soMay.AddItem(itemId, "Widget A", 9m, 10m, 0m);
        soMay.Submit();

        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { item }));
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse>()));

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { soMar, soApr, soMay }.AsQueryable()));

        // Act
        var result = await _appService.GetForecastAsync(new ExponentialSmoothingForecastingFilterDto
        {
            CompanyId = _companyId,
            BasedOnDocument = "Sales Order",
            BasedOnField = "Qty",
            Periodicity = "Monthly",
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 8, 31),
            NoOfYears = 1,
            SmoothingConstant = 0.5m,
            ItemId = itemId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows.First();
        row.ItemCode.ShouldBe("ITEM-WIDGET");
        row.TotalDemand.ShouldBe(20m); // 7 + 4 + 9

        // Average of non-zero historical months: 20 / 3 = 6.6667
        Math.Round(row.AverageHistoricalDemand, 4).ShouldBe(6.6667m);

        // Smoothing steps:
        // Initial F = 6.6667
        // Apr: F = 6.6667 + 0.5 * (7 - 6.6667) = 6.8333
        // May: F = 6.8333 + 0.5 * (4 - 6.8333) = 5.4167
        // Jun (Forecast): F = 5.4167 + 0.5 * (9 - 5.4167) = 7.2083
        row.PeriodValues.TryGetValue("2026-06", out var junForecast).ShouldBeTrue();
        Math.Round(junForecast, 4).ShouldBe(7.2083m);

        result.TotalDemandPastData.ShouldBe(20m);
    }

    [Fact]
    public async Task GetForecastAsync_IgnoresDraftAndCancelledOrders()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-PART", "Part B", ItemType.Goods);

        var submittedSo = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-SUBMITTED", new DateTime(2026, 4, 10));
        submittedSo.AddItem(itemId, "Part B", 12m, 20m, 0m);
        submittedSo.Submit();

        var draftSo = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-DRAFT", new DateTime(2026, 4, 12));
        draftSo.AddItem(itemId, "Part B", 50m, 20m, 0m);
        // Do not submit

        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { item }));
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse>()));

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { submittedSo, draftSo }.AsQueryable()));

        // Act
        var result = await _appService.GetForecastAsync(new ExponentialSmoothingForecastingFilterDto
        {
            CompanyId = _companyId,
            BasedOnDocument = "Sales Order",
            FromDate = new DateTime(2026, 5, 1),
            ToDate = new DateTime(2026, 7, 31),
            SmoothingConstant = 0.5m,
            ItemId = itemId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalDemandPastData.ShouldBe(12m); // Draft 50m excluded
    }

    [Fact]
    public async Task GetForecastAsync_DeliveryNote_AggregatesWarehouseAndAmount()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-BULK", "Bulk Item", ItemType.Goods);

        var whId = Guid.NewGuid();
        var wh = new Warehouse(whId, _companyId, "Finished Goods Central");

        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, Guid.NewGuid(), whId, "DN-001", new DateTime(2026, 3, 20));
        dn.AddItem(itemId, "Bulk Item", 10m, 25m, 0m);
        dn.Submit();

        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { item }));
        _warehouseRepo.GetListAsync().Returns(Task.FromResult(new List<Warehouse> { wh }));

        _dnRepo.WithDetailsAsync(Arg.Any<Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        // Act
        var result = await _appService.GetForecastAsync(new ExponentialSmoothingForecastingFilterDto
        {
            CompanyId = _companyId,
            BasedOnDocument = "Delivery Note",
            BasedOnField = "Amount",
            FromDate = new DateTime(2026, 4, 1),
            ToDate = new DateTime(2026, 6, 30),
            SmoothingConstant = 0.3m,
            WarehouseId = whId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows.First();
        row.WarehouseName.ShouldBe("Finished Goods Central");
        row.TotalDemand.ShouldBe(250m); // 10 * 25
    }
}
