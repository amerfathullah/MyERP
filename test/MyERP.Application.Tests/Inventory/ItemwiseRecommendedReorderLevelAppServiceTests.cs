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

public class ItemwiseRecommendedReorderLevelAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<StockEntry, Guid> _stockEntryRepo = Substitute.For<IRepository<StockEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly ItemwiseRecommendedReorderLevelAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemwiseRecommendedReorderLevelAppServiceTests()
    {
        _appService = new ItemwiseRecommendedReorderLevelAppService(
            _sleRepo,
            _stockEntryRepo,
            _itemRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesRecommendedReorderLevelUsingDailyOutgoingAndLeadTimeAndSafetyStock()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var fromDate = today.AddDays(-9); // 10 days inclusive
        var toDate = today;

        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(company);

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-001", "Ball Bearing", ItemType.Goods)
        {
            MaintainStock = true,
            SafetyStock = 10m,
            LeadTimeDays = 5,
            ReorderLevel = 15m,
            ItemGroup = "Mechanical"
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var whId = Guid.NewGuid();
        // SLE 1: Delivered 20 via DeliveryNote
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-5), -20m, 10m, 80m, 800m)
        {
            VoucherType = "DeliveryNote",
            StockValueDifference = -200m
        };

        // SLE 2: Consumed 10 via StockEntry
        var seId = Guid.NewGuid();
        var se = new StockEntry(seId, _companyId, StockEntryType.MaterialIssue, today.AddDays(-3));
        _stockEntryRepo.GetQueryableAsync().Returns(new List<StockEntry> { se }.AsQueryable());

        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-3), -10m, 10m, 70m, 700m)
        {
            VoucherType = "StockEntry",
            VoucherId = seId,
            StockValueDifference = -100m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable());

        var filter = new ItemwiseRecommendedReorderLevelFilterDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate
        };

        // Act
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.ShouldNotBeNull();
        result.TotalItems.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.DeliveredQty.ShouldBe(20m);
        row.ConsumedQty.ShouldBe(10m);
        row.TotalOutgoing.ShouldBe(30m);
        row.AvgDailyOutgoing.ShouldBe(3.0m); // 30 / 10 days
        row.SafetyStock.ShouldBe(10m);
        row.LeadTimeDays.ShouldBe(5);
        row.CurrentReorderLevel.ShouldBe(15m);
        // (3.0 * 5) + 10 = 25
        row.RecommendedReorderLevel.ShouldBe(25m);
        row.Variance.ShouldBe(10m);
        row.Status.ShouldBe("UnderConfigured");
        result.UnderConfiguredCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesMaterialTransferBetweenWarehouses()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(company);

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-002", "Transfer Part", ItemType.Goods)
        {
            MaintainStock = true,
            SafetyStock = 5m,
            LeadTimeDays = 2,
            ReorderLevel = 10m
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var transferSeId = Guid.NewGuid();
        var transferSe = new StockEntry(transferSeId, _companyId, StockEntryType.MaterialTransfer, today);
        _stockEntryRepo.GetQueryableAsync().Returns(new List<StockEntry> { transferSe }.AsQueryable());

        var whId = Guid.NewGuid();
        var sleTransfer = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today, -50m, 5m, 100m, 500m)
        {
            VoucherType = "StockEntry",
            VoucherId = transferSeId,
            StockValueDifference = -250m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sleTransfer }.AsQueryable());

        var filter = new ItemwiseRecommendedReorderLevelFilterDto
        {
            CompanyId = _companyId,
            FromDate = today.AddDays(-1),
            ToDate = today
        };

        // Act
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ConsumedQty.ShouldBe(0m); // Excluded!
        result.Rows[0].TotalOutgoing.ShouldBe(0m);
        result.Rows[0].RecommendedReorderLevel.ShouldBe(5m); // 0 + 5 safety stock
    }

    [Fact]
    public async Task GetReportAsync_ThrowsIfDateRangeInvalid()
    {
        var filter = new ItemwiseRecommendedReorderLevelFilterDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.Date,
            ToDate = DateTime.UtcNow.Date.AddDays(-1)
        };

        await Should.ThrowAsync<UserFriendlyException>(() => _appService.GetReportAsync(filter));
    }
}
