using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class TotalStockSummaryAppServiceTests
{
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly TotalStockSummaryAppService _appService;
    private readonly Guid _company1Id = Guid.NewGuid();
    private readonly Guid _company2Id = Guid.NewGuid();
    private readonly Guid _wh1Id = Guid.NewGuid();
    private readonly Guid _wh2Id = Guid.NewGuid();
    private readonly Guid _wh3Id = Guid.NewGuid();
    private readonly Guid _item1Id = Guid.NewGuid();
    private readonly Guid _item2Id = Guid.NewGuid();

    public TotalStockSummaryAppServiceTests()
    {
        _appService = new TotalStockSummaryAppService(
            _binRepo,
            _warehouseRepo,
            _itemRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_GroupByWarehouse_ReturnsStockGroupedByWarehouse()
    {
        // Arrange
        var company1 = new Company(_company1Id, "Acme Corp");
        _companyRepo.GetQueryableAsync().Returns(new List<Company> { company1 }.AsQueryable());

        var wh1 = new Warehouse(_wh1Id, _company1Id, "Main Warehouse");
        var wh2 = new Warehouse(_wh2Id, _company1Id, "Distribution Center");
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh1, wh2 }.AsQueryable());

        var item1 = new Item(_item1Id, _company1Id, "ITEM-001", "Widget A", ItemType.Goods);
        var item2 = new Item(_item2Id, _company1Id, "ITEM-002", "Widget B", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1, item2 }.AsQueryable());

        var bin1 = new Bin(Guid.NewGuid(), _item1Id, _wh1Id);
        bin1.UpdateActualQty(10, 500);
        var bin2 = new Bin(Guid.NewGuid(), _item2Id, _wh1Id);
        bin2.UpdateActualQty(5, 250);
        var bin3 = new Bin(Guid.NewGuid(), _item1Id, _wh2Id);
        bin3.UpdateActualQty(20, 1000);
        var binZero = new Bin(Guid.NewGuid(), _item2Id, _wh2Id);
        binZero.UpdateActualQty(0, 0);

        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin1, bin2, bin3, binZero }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new TotalStockSummaryFilterDto
        {
            GroupBy = "Warehouse"
        });

        // Assert
        result.ShouldNotBeNull();
        result.GroupBy.ShouldBe("Warehouse");
        result.Rows.Count.ShouldBe(3); // 2 items in wh1, 1 item in wh2 (zero qty excluded)
        result.TotalItems.ShouldBe(2);
        result.TotalQuantity.ShouldBe(35m);
        result.TotalStockValue.ShouldBe(1750m);

        var wh1Item1 = result.Rows.First(r => r.GroupName == "Main Warehouse" && r.ItemCode == "ITEM-001");
        wh1Item1.CurrentQty.ShouldBe(10m);
        wh1Item1.StockValue.ShouldBe(500m);
        wh1Item1.ValuationRate.ShouldBe(50m);
    }

    [Fact]
    public async Task GetReportAsync_GroupByCompany_AggregatesStockAcrossWarehouses()
    {
        // Arrange
        var company1 = new Company(_company1Id, "Acme Corp");
        var company2 = new Company(_company2Id, "Beta Corp");
        _companyRepo.GetQueryableAsync().Returns(new List<Company> { company1, company2 }.AsQueryable());

        var wh1 = new Warehouse(_wh1Id, _company1Id, "Acme WH 1");
        var wh2 = new Warehouse(_wh2Id, _company1Id, "Acme WH 2");
        var wh3 = new Warehouse(_wh3Id, _company2Id, "Beta WH");
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh1, wh2, wh3 }.AsQueryable());

        var item1 = new Item(_item1Id, _company1Id, "ITEM-001", "Widget A", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1 }.AsQueryable());

        var bin1 = new Bin(Guid.NewGuid(), _item1Id, _wh1Id);
        bin1.UpdateActualQty(15, 300);
        var bin2 = new Bin(Guid.NewGuid(), _item1Id, _wh2Id);
        bin2.UpdateActualQty(25, 500);
        var bin3 = new Bin(Guid.NewGuid(), _item1Id, _wh3Id);
        bin3.UpdateActualQty(10, 200);

        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin1, bin2, bin3 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new TotalStockSummaryFilterDto
        {
            GroupBy = "Company"
        });

        // Assert
        result.ShouldNotBeNull();
        result.GroupBy.ShouldBe("Company");
        result.Rows.Count.ShouldBe(2); // 1 item in Acme Corp, 1 item in Beta Corp

        var acmeRow = result.Rows.First(r => r.GroupName == "Acme Corp");
        acmeRow.CurrentQty.ShouldBe(40m); // 15 + 25
        acmeRow.StockValue.ShouldBe(800m); // 300 + 500
        acmeRow.ValuationRate.ShouldBe(20m);

        var betaRow = result.Rows.First(r => r.GroupName == "Beta Corp");
        betaRow.CurrentQty.ShouldBe(10m);
        betaRow.StockValue.ShouldBe(200m);

        result.TotalQuantity.ShouldBe(50m);
        result.TotalStockValue.ShouldBe(1000m);
    }
}
