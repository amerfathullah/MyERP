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

public class StockAnalyticsAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly StockAnalyticsAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public StockAnalyticsAppServiceTests()
    {
        _appService = new StockAnalyticsAppService(
            _sleRepo,
            _itemRepo,
            _warehouseRepo,
            _itemGroupRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesPeriodicQuantityBalances_CarriedForwardCorrectly()
    {
        // Arrange
        var company = new Company(_companyId, "Apex Industries") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(company);

        var whId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-ANALYTICS", "Analytics Bearing", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true,
            Uom = "Nos"
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        var year = 2026;
        var fromDate = new DateTime(year, 1, 1);
        var toDate = new DateTime(year, 3, 31);

        // Pre-opening movement: +50 units on 2025-12-15
        var sle0 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, new DateTime(year - 1, 12, 15), 50m, 10m, 50m, 500m)
        {
            StockValueDifference = 500m
        };

        // Movement in Jan 2026: +20 units on 2026-01-10
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, new DateTime(year, 1, 10), 20m, 10m, 70m, 700m)
        {
            StockValueDifference = 200m
        };

        // Movement in Feb 2026: -10 units on 2026-02-15
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, new DateTime(year, 2, 15), -10m, 10m, 60m, 600m)
        {
            StockValueDifference = -100m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle0, sle1, sle2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new StockAnalyticsRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            Range = "Monthly",
            ValueOrQuantity = "Quantity"
        });

        // Assert
        result.ShouldNotBeNull();
        result.CompanyCurrency.ShouldBe("MYR");
        result.PeriodColumns.Count.ShouldBe(3); // Jan, Feb, Mar
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-ANALYTICS");
        row.OpeningBalance.ShouldBe(50m);
        row.ClosingBalance.ShouldBe(60m);

        // Jan balance: 50 + 20 = 70
        var janCol = result.PeriodColumns[0];
        row.PeriodBalances[janCol.Key].ShouldBe(70m);

        // Feb balance: 70 - 10 = 60
        var febCol = result.PeriodColumns[1];
        row.PeriodBalances[febCol.Key].ShouldBe(60m);

        // Mar balance: 60 + 0 = 60 (carried forward!)
        var marCol = result.PeriodColumns[2];
        row.PeriodBalances[marCol.Key].ShouldBe(60m);

        result.TotalRow.OpeningBalance.ShouldBe(50m);
        result.TotalRow.ClosingBalance.ShouldBe(60m);
        result.Chart.Datapoints.ShouldBe(new List<decimal> { 70m, 60m, 60m });
    }

    [Fact]
    public async Task GetReportAsync_ValueOrQuantity_Value_CalculatesValuationDifferences()
    {
        // Arrange
        var company = new Company(_companyId, "Apex Industries") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(company);

        var whId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-VAL", "Valuation Item", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        var year = 2026;
        var fromDate = new DateTime(year, 1, 1);
        var toDate = new DateTime(year, 1, 31);

        var slePre = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, new DateTime(year - 1, 12, 1), 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m
        };

        var sleJan = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, new DateTime(year, 1, 15), 5m, 120m, 15m, 1600m)
        {
            StockValueDifference = 600m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { slePre, sleJan }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new StockAnalyticsRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            Range = "Monthly",
            ValueOrQuantity = "Value"
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.OpeningBalance.ShouldBe(1000m);
        row.ClosingBalance.ShouldBe(1600m);
        result.Chart.Unit.ShouldBe("MYR");
    }

    [Fact]
    public async Task GetReportAsync_WarehouseFilter_AppliesCorrectly()
    {
        // Arrange
        var company = new Company(_companyId, "Apex") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(company);

        var wh1Id = Guid.NewGuid();
        var wh2Id = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-WH", "Warehouse Item", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        var today = DateTime.UtcNow.Date;
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, wh1Id, today, 100m, 5m, 100m, 500m)
        {
            StockValueDifference = 500m
        };
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, wh2Id, today, 250m, 5m, 250m, 1250m)
        {
            StockValueDifference = 1250m
        };

        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new StockAnalyticsRequestDto
        {
            CompanyId = _companyId,
            WarehouseId = wh1Id,
            FromDate = today.AddDays(-5),
            ToDate = today.AddDays(5)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ClosingBalance.ShouldBe(100m);
    }

    [Fact]
    public async Task GetReportAsync_WeeklyRangeAcrossYearBoundary_UsesEndOfWeekIsoYearAndWeek()
    {
        // Arrange: PR #60967 parity
        var company = new Company(_companyId, "Apex") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(company);

        var whId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-ISO-WEEK", "ISO Week Item", ItemType.Goods)
        {
            MaintainStock = true,
            IsActive = true
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        // Dec 25, 2022 to Jan 1, 2023. End of 7-day period is Jan 1, 2023.
        // Jan 1, 2023 is Sunday. In ISO calendar, Jan 1, 2023 belongs to W52 2022!
        var fromDate = new DateTime(2022, 12, 25);
        var toDate = new DateTime(2023, 1, 1);

        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, new DateTime(2023, 1, 1), 3m, 10m, 3m, 30m)
        {
            StockValueDifference = 30m
        };
        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new StockAnalyticsRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate,
            Range = "Weekly",
            ValueOrQuantity = "Quantity"
        });

        // Assert
        result.ShouldNotBeNull();
        result.PeriodColumns.Count.ShouldBeGreaterThanOrEqualTo(1);
        var firstCol = result.PeriodColumns[0];
        // Week end is Dec 31 / Jan 1 -> W52 2022
        firstCol.Label.ShouldBe("W52 2022");
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].ClosingBalance.ShouldBe(3m);
    }
}
