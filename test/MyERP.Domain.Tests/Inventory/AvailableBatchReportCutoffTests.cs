using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Available Batch Report date cutoff and expired batch filtering
/// per ERPNext PR #60757 / commit 7485463837.
/// </summary>
public class AvailableBatchReportCutoffTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private StockBalanceAppService CreateAppService(
        List<StockLedgerEntry> sles,
        List<Batch> batches,
        List<Item> items,
        List<Warehouse> warehouses)
    {
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();
        var binService = new BinService(binRepo);

        itemRepo.GetQueryableAsync().Returns(Task.FromResult(items.AsQueryable()));
        whRepo.GetQueryableAsync().Returns(Task.FromResult(warehouses.AsQueryable()));
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));
        batchRepo.GetQueryableAsync().Returns(Task.FromResult(batches.AsQueryable()));

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(Arg.Any<string>()).Returns(Task.FromResult<string?>("false"));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetService<ISettingProvider>().Returns(settingProvider);
        lazyProvider.LazyGetRequiredService<ISettingProvider>().Returns(settingProvider);

        var service = new StockBalanceAppService(binRepo, itemRepo, whRepo, sleRepo, batchRepo, binService)
        {
            LazyServiceProvider = lazyProvider
        };

        return service;
    }

    [Fact]
    public void GetBatchWiseBalanceRequestDto_DefaultsIncludeExpiredBatchesToTrue()
    {
        var dto = new GetBatchWiseBalanceRequestDto();
        dto.IncludeExpiredBatches.ShouldBeTrue();
    }

    [Fact]
    public async Task GetBatchWiseBalance_EnforcesDateCutoff_EvenForToday()
    {
        // Per ERPNext PR #60757 / commit 7485463837:
        // fix(stock): apply batch report cutoff for today
        // PostingDateTime <= to_date 23:59:59 must be applied unconditionally.
        var today = DateTime.UtcNow.Date;
        var batchId = Guid.NewGuid();

        var item = new Item(_itemId, _companyId, "ITEM-001", "Test Item", ItemType.Goods)
        {
            HasBatchNo = true
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse");
        var batch = new Batch(batchId, _itemId, "BATCH-001");

        // 1. SLE yesterday at 14:00 (10 units) -> included
        var sleYesterday = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddDays(-1).AddHours(14),
            quantityChange: 10m, valuationRate: 5m, balanceQuantity: 10m, balanceValue: 50m)
        {
            BatchId = batchId
        };

        // 2. SLE earlier today at 10:00 AM (15 units) -> included
        var sleToday = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddHours(10),
            quantityChange: 15m, valuationRate: 5m, balanceQuantity: 25m, balanceValue: 125m)
        {
            BatchId = batchId
        };

        // 3. SLE tomorrow / future date (20 units) -> MUST BE EXCLUDED by cutoff!
        var sleTomorrow = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddDays(1).AddHours(9),
            quantityChange: 20m, valuationRate: 5m, balanceQuantity: 45m, balanceValue: 225m)
        {
            BatchId = batchId
        };

        var service = CreateAppService(
            new List<StockLedgerEntry> { sleYesterday, sleToday, sleTomorrow },
            new List<Batch> { batch },
            new List<Item> { item },
            new List<Warehouse> { warehouse });

        var result = await service.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ToDate = today, // Report as of today
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.BatchId.ShouldBe(batchId);
        // Balance must only include yesterday (10) + today (15) = 25. Tomorrow's 20 must NOT leak in!
        row.Balance.ShouldBe(25m);
        row.StockValue.ShouldBe(125m);
        result.TotalQuantity.ShouldBe(25m);
    }

    [Fact]
    public async Task GetBatchWiseBalance_ExcludesExpiredBatches_WhenRequested()
    {
        // Per ERPNext PR #60757 / available_batch_report.py:
        // if not filters.include_expired_batches: filter out batches where expiry_date < today
        var today = DateTime.UtcNow.Date;
        var expiredBatchId = Guid.NewGuid();
        var activeBatchId = Guid.NewGuid();

        var item = new Item(_itemId, _companyId, "ITEM-001", "Test Item", ItemType.Goods)
        {
            HasBatchNo = true
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse");

        var expiredBatch = new Batch(expiredBatchId, _itemId, "BATCH-EXPIRED")
        {
            ExpiryDate = today.AddDays(-2) // Expired 2 days ago
        };

        var activeBatch = new Batch(activeBatchId, _itemId, "BATCH-ACTIVE")
        {
            ExpiryDate = today.AddMonths(3) // Valid for 3 months
        };

        var sleExpired = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddDays(-5),
            quantityChange: 30m, valuationRate: 4m, balanceQuantity: 30m, balanceValue: 120m)
        {
            BatchId = expiredBatchId
        };

        var sleActive = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddDays(-5),
            quantityChange: 50m, valuationRate: 4m, balanceQuantity: 80m, balanceValue: 320m)
        {
            BatchId = activeBatchId
        };

        var service = CreateAppService(
            new List<StockLedgerEntry> { sleExpired, sleActive },
            new List<Batch> { expiredBatch, activeBatch },
            new List<Item> { item },
            new List<Warehouse> { warehouse });

        // Query with IncludeExpiredBatches = false
        var result = await service.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ToDate = today,
            IncludeExpiredBatches = false,
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].BatchId.ShouldBe(activeBatchId);
        result.Rows[0].Balance.ShouldBe(50m);
        result.TotalQuantity.ShouldBe(50m);
        result.TotalBatches.ShouldBe(1);
    }

    [Fact]
    public async Task GetBatchWiseBalance_IncludesExpiredBatches_WhenIncludeExpiredBatchesIsTrue()
    {
        var today = DateTime.UtcNow.Date;
        var expiredBatchId = Guid.NewGuid();
        var activeBatchId = Guid.NewGuid();

        var item = new Item(_itemId, _companyId, "ITEM-001", "Test Item", ItemType.Goods)
        {
            HasBatchNo = true
        };
        var warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse");

        var expiredBatch = new Batch(expiredBatchId, _itemId, "BATCH-EXPIRED")
        {
            ExpiryDate = today.AddDays(-2)
        };

        var activeBatch = new Batch(activeBatchId, _itemId, "BATCH-ACTIVE")
        {
            ExpiryDate = today.AddMonths(3)
        };

        var sleExpired = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddDays(-5),
            quantityChange: 30m, valuationRate: 4m, balanceQuantity: 30m, balanceValue: 120m)
        {
            BatchId = expiredBatchId
        };

        var sleActive = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId, today.AddDays(-5),
            quantityChange: 50m, valuationRate: 4m, balanceQuantity: 80m, balanceValue: 320m)
        {
            BatchId = activeBatchId
        };

        var service = CreateAppService(
            new List<StockLedgerEntry> { sleExpired, sleActive },
            new List<Batch> { expiredBatch, activeBatch },
            new List<Item> { item },
            new List<Warehouse> { warehouse });

        // Query with default IncludeExpiredBatches = true
        var result = await service.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ToDate = today,
            IncludeExpiredBatches = true,
        });

        result.Rows.Count.ShouldBe(2);
        result.TotalQuantity.ShouldBe(80m);
        result.TotalBatches.ShouldBe(2);
        result.ExpiredBatchCount.ShouldBe(1);
    }
}
