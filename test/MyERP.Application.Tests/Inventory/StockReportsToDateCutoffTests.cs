using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

/// <summary>
/// Regression tests for ERPNext PR #66255 & #66275:
/// Stock reports using ToDate / AsOfDate must include intra-day SLEs up to 23:59:59.9999999 of ToDate
/// and exclude cancelled entries.
/// </summary>
public class StockReportsToDateCutoffTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public async Task StockLedger_GetStockLedger_IncludesIntraDayEntriesOnToDate()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var fromDate = new DateTime(2026, 6, 1);
        var toDate = new DateTime(2026, 6, 30);

        var sleSameDayEvening = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddHours(23).AddMinutes(59).AddSeconds(59), 5m, 10m, 5m, 50m);
        var sleNextDay = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddDays(1), 8m, 10m, 13m, 130m);
        sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sleSameDayEvening, sleNextDay }.AsQueryable());

        var item = new Item(_itemId, _companyId, "ITEM-1", "Widget", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Item> { item });
        var wh = new Warehouse(_warehouseId, _companyId, "Main WH");
        whRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Warehouse> { wh });

        var svc = new StockLedgerAppService(sleRepo, itemRepo, whRepo);

        var result = await svc.GetStockLedgerAsync(new StockLedgerRequestDto
        {
            CompanyId = _companyId,
            FromDate = fromDate,
            ToDate = toDate
        });

        result.Rows.ShouldHaveSingleItem();
        result.Rows[0].QuantityChange.ShouldBe(5m);
    }

    [Fact]
    public async Task StockLedger_GetStockMovementSummary_IncludesIntraDayEntriesAndExcludesCancelled()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var fromDate = new DateTime(2026, 6, 1);
        var toDate = new DateTime(2026, 6, 30);

        var sleIntraDay = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddHours(18).AddMinutes(30), 6m, 20m, 6m, 120m);
        var sleCancelled = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddHours(10), 99m, 20m, 105m, 2100m) { IsCancelled = true };
        var sleNextDay = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddDays(1), 4m, 20m, 10m, 200m);
        sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sleIntraDay, sleCancelled, sleNextDay }.AsQueryable());

        var item = new Item(_itemId, _companyId, "ITEM-1", "Widget", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>(), Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new List<Item> { item });

        var svc = new StockLedgerAppService(sleRepo, itemRepo, whRepo);

        var result = await svc.GetStockMovementSummaryAsync(_companyId, fromDate, toDate);

        result.TotalStockIn.ShouldBe(6m);
        result.Items.ShouldHaveSingleItem();
        result.Items[0].ClosingQty.ShouldBe(6m);
    }

    [Fact]
    public async Task StockAgeing_GetReport_IncludesIntraDayEntriesOnToDate()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var groupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();

        var toDate = new DateTime(2026, 6, 30);
        var wh = new Warehouse(_warehouseId, _companyId, "Main WH") { WarehouseCode = "MAIN", IsActive = true };
        whRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh }.AsQueryable());

        var item = new Item(_itemId, _companyId, "ITEM-1", "Widget", ItemType.Goods) { MaintainStock = true, IsActive = true };
        itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        groupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());
        batchRepo.GetQueryableAsync().Returns(new List<Batch>().AsQueryable());

        var sleLateSameDay = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddHours(23).AddMinutes(45), 3m, 15m, 3m, 45m)
        {
            StockValueDifference = 45m
        };
        var sleNextDay = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddDays(1), 9m, 15m, 12m, 180m)
        {
            StockValueDifference = 135m
        };
        sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sleLateSameDay, sleNextDay }.AsQueryable());

        var svc = new StockAgeingAppService(sleRepo, itemRepo, whRepo, groupRepo, batchRepo);

        var result = await svc.GetReportAsync(new StockAgeingFilterDto
        {
            CompanyId = _companyId,
            ToDate = toDate
        });

        result.Rows.ShouldHaveSingleItem();
        result.Rows[0].TotalQty.ShouldBe(3m);
        result.TotalStockValue.ShouldBe(45m);
    }

    [Fact]
    public async Task Batch_GetAvailableBatches_IncludesIntraDayEntriesOnToDate()
    {
        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var seSvc = Substitute.For<IStockEntryAppService>();
        var lazySp = Substitute.For<IAbpLazyServiceProvider>();

        var toDate = new DateTime(2026, 6, 30);
        var batchId = Guid.NewGuid();
        var batch = new Batch(batchId, _itemId, "BATCH-001");
        batchRepo.GetQueryableAsync().Returns(new List<Batch> { batch }.AsQueryable());

        var wh = new Warehouse(_warehouseId, _companyId, "Main WH") { IsActive = true };
        whRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh }.AsQueryable());

        var item = new Item(_itemId, _companyId, "ITEM-1", "Widget", ItemType.Goods);
        itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        lazySp.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(itemRepo);

        var sleSameDayEvening = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddHours(22).AddMinutes(15), 8m, 10m, 8m, 80m)
        {
            BatchId = batchId
        };
        var sleNextDay = new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, toDate.AddDays(1), 12m, 10m, 20m, 200m)
        {
            BatchId = batchId
        };
        sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sleSameDayEvening, sleNextDay }.AsQueryable());

        var svc = new BatchAppService(batchRepo, sleRepo, whRepo, seSvc)
        {
            LazyServiceProvider = lazySp
        };

        var result = await svc.GetAvailableBatchesAsync(new GetAvailableBatchesDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ToDate = toDate,
            IgnoreReservedStock = true
        });

        result.ShouldHaveSingleItem();
        result[0].AvailableQuantity.ShouldBe(8m);
    }
}
