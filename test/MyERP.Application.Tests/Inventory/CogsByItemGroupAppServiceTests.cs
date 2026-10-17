using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class CogsByItemGroupAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly CogsByItemGroupAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public CogsByItemGroupAppServiceTests()
    {
        _appService = new CogsByItemGroupAppService(
            _sleRepo,
            _itemRepo,
            _itemGroupRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesHierarchicalCogsForParentAndChildGroups()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(company);

        var rootGroupId = Guid.NewGuid();
        var productsGroupId = Guid.NewGuid();
        var electronicsGroupId = Guid.NewGuid();
        var consumablesGroupId = Guid.NewGuid();

        var rootGroup = new ItemGroup(rootGroupId, "All Item Groups", isGroup: true, parentId: null);
        var productsGroup = new ItemGroup(productsGroupId, "Products", isGroup: true, parentId: rootGroupId);
        var electronicsGroup = new ItemGroup(electronicsGroupId, "Electronics", isGroup: false, parentId: productsGroupId);
        var consumablesGroup = new ItemGroup(consumablesGroupId, "Consumables", isGroup: false, parentId: productsGroupId);

        _itemGroupRepo.GetListAsync().Returns(new List<ItemGroup>
        {
            rootGroup,
            productsGroup,
            electronicsGroup,
            consumablesGroup
        });

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var item3Id = Guid.NewGuid();

        var item1 = new Item(item1Id, _companyId, "ITEM-ELEC", "Laptop", ItemType.Goods)
        {
            ItemGroupId = electronicsGroupId
        };
        var item2 = new Item(item2Id, _companyId, "ITEM-CONS", "Paper Pack", ItemType.Goods)
        {
            ItemGroupId = consumablesGroupId
        };
        var item3 = new Item(item3Id, _companyId, "ITEM-PROD", "Custom Bundle", ItemType.Goods)
        {
            ItemGroupId = productsGroupId
        };

        var items = new List<Item> { item1, item2, item3 };
        _itemRepo.GetQueryableAsync().Returns(items.AsQueryable());

        var whId = Guid.NewGuid();
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, item1Id, whId, today.AddDays(-2), -2m, 100m, 10m, 1000m)
        {
            StockValueDifference = -200m
        };
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, item2Id, whId, today.AddDays(-3), -5m, 10m, 50m, 500m)
        {
            StockValueDifference = -50m
        };
        var sle3 = new StockLedgerEntry(Guid.NewGuid(), _companyId, item3Id, whId, today.AddDays(-1), -1m, 30m, 5m, 150m)
        {
            StockValueDifference = -30m
        };

        var sles = new List<StockLedgerEntry> { sle1, sle2, sle3 };
        _sleRepo.GetQueryableAsync().Returns(sles.AsQueryable());

        var filter = new CogsByItemGroupFilterDto
        {
            CompanyId = _companyId,
            FromDate = today.AddDays(-10),
            ToDate = today
        };

        // Act
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.ShouldNotBeNull();
        result.TotalCogsDebit.ShouldBe(280m);

        // Check root group: 200 + 50 + 30 = 280
        var rootRow = result.Rows.FirstOrDefault(r => r.ItemGroupName == "All Item Groups");
        rootRow.ShouldNotBeNull();
        rootRow.CogsDebit.ShouldBe(280m);
        rootRow.Indent.ShouldBe(0);
        rootRow.IsGroup.ShouldBeTrue();
        rootRow.PercentageOfTotal.ShouldBe(100m);

        // Check Products group: 280
        var prodRow = result.Rows.FirstOrDefault(r => r.ItemGroupName == "Products");
        prodRow.ShouldNotBeNull();
        prodRow.CogsDebit.ShouldBe(280m);
        prodRow.Indent.ShouldBe(1);
        prodRow.IsGroup.ShouldBeTrue();

        // Check direct items for Products: 30
        var directProdRow = result.Rows.FirstOrDefault(r => r.ItemGroupName == "Products (Direct)");
        directProdRow.ShouldNotBeNull();
        directProdRow.CogsDebit.ShouldBe(30m);
        directProdRow.Indent.ShouldBe(2);

        // Check Electronics child group: 200
        var elecRow = result.Rows.FirstOrDefault(r => r.ItemGroupName == "Electronics");
        elecRow.ShouldNotBeNull();
        elecRow.CogsDebit.ShouldBe(200m);
        elecRow.Indent.ShouldBe(2);
        elecRow.PercentageOfTotal.ShouldBe(71.43m);

        // Check Consumables child group: 50
        var consRow = result.Rows.FirstOrDefault(r => r.ItemGroupName == "Consumables");
        consRow.ShouldNotBeNull();
        consRow.CogsDebit.ShouldBe(50m);
        consRow.Indent.ShouldBe(2);
        consRow.PercentageOfTotal.ShouldBe(17.86m);
    }

    [Fact]
    public async Task GetReportAsync_ThrowsIfDateRangeInvalid()
    {
        var filter = new CogsByItemGroupFilterDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.Date,
            ToDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        await Should.ThrowAsync<UserFriendlyException>(() => _appService.GetReportAsync(filter));
    }

    [Fact]
    public async Task GetReportAsync_ReturnsEmptyWhenNoOutgoingSles()
    {
        var today = DateTime.UtcNow.Date;
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(company);

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry>().AsQueryable());

        var filter = new CogsByItemGroupFilterDto
        {
            CompanyId = _companyId,
            FromDate = today.AddDays(-10),
            ToDate = today
        };

        var result = await _appService.GetReportAsync(filter);
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalCogsDebit.ShouldBe(0m);
    }
}
