using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

/// <summary>
/// Unit tests for pooled batch valuation at warehouse rate per ERPNext PR #60673 / commits c520e2f067 & 90aa56065e.
/// When items share the item-warehouse pool (Moving Average with DoNotUseBatchwiseValuation or Serialized without serial-wise valuation),
/// batches are valued at the item-warehouse valuation rate at report cutoff.
/// </summary>
public class BatchWiseBalanceValuationTests
{
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly BinService _binService;
    private readonly IAbpLazyServiceProvider _lazySp = Substitute.For<IAbpLazyServiceProvider>();
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Warehouse _warehouse;

    public BatchWiseBalanceValuationTests()
    {
        _binService = new BinService(_binRepo);
        _warehouse = new Warehouse(_warehouseId, _companyId, "Main Warehouse") { WarehouseCode = "WH-MAIN" };

        _lazySp.LazyGetService<ISettingProvider>().Returns(_settingProvider);
        _lazySp.LazyGetRequiredService<ISettingProvider>().Returns(_settingProvider);

        var whList = new List<Warehouse> { _warehouse }.AsQueryable();
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(whList));
    }

    [Fact]
    public async Task NonPooled_FifoBatch_UsesSumOfBatchStockValueDifference()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-FIFO", "FIFO Item", ItemType.Goods)
        {
            HasBatchNo = true,
            ValuationMethod = ValuationMethod.FIFO
        };
        var batch = new Batch(batchId, itemId, "BATCH-001");

        _settingProvider.GetOrNullAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation).Returns(Task.FromResult<string?>("false"));

        var sles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-2),
                quantityChange: 10m, valuationRate: 5m, balanceQuantity: 10m, balanceValue: 50m)
            {
                BatchId = batchId,
                StockValue = 50m
            },
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-1),
                quantityChange: 5m, valuationRate: 6m, balanceQuantity: 15m, balanceValue: 80m)
            {
                BatchId = batchId,
                StockValue = 30m
            }
        }.AsQueryable();

        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));

        var service = new StockBalanceAppService(_binRepo, _itemRepo, _warehouseRepo, _sleRepo, _batchRepo, _binService)
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await service.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = _companyId,
            ItemId = itemId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Balance.ShouldBe(15m);
        row.StockValue.ShouldBe(80m); // 50 + 30
        row.ValuationRate.ShouldBe(Math.Round(80m / 15m, 4));
    }

    [Fact]
    public async Task Pooled_MovingAverage_DoNotUseBatchwiseValuation_ValuesAtWarehouseCutoffRate()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-MA", "MA Item", ItemType.Goods)
        {
            HasBatchNo = true,
            ValuationMethod = ValuationMethod.WeightedAverage
        };
        var batch = new Batch(batchId, itemId, "BATCH-POOL-01");

        _settingProvider.GetOrNullAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation).Returns(Task.FromResult<string?>("true"));

        // Batch-level SLE recorded rate 5, but warehouse pool valuation evolved to 7.5
        var sles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-2),
                quantityChange: 10m, valuationRate: 5m, balanceQuantity: 10m, balanceValue: 50m)
            {
                BatchId = batchId,
                StockValue = 50m
            },
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-1),
                quantityChange: 10m, valuationRate: 7.5m, balanceQuantity: 20m, balanceValue: 150m)
            {
                BatchId = Guid.NewGuid(), // other batch moved warehouse rate up
                StockValue = 100m
            }
        }.AsQueryable();

        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));

        var service = new StockBalanceAppService(_binRepo, _itemRepo, _warehouseRepo, _sleRepo, _batchRepo, _binService)
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await service.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = _companyId,
            ItemId = itemId,
            WarehouseId = _warehouseId,
            BatchId = batchId
        });

        // Assert: Batch has 10 qty, valued at warehouse cutoff rate 7.5 => 75 (not original batch sum 50)
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Balance.ShouldBe(10m);
        row.ValuationRate.ShouldBe(7.5m);
        row.StockValue.ShouldBe(75m);
        result.TotalStockValue.ShouldBe(75m);
    }

    [Fact]
    public async Task Pooled_SerializedItem_WithoutSerialWiseValuation_ValuesAtWarehouseCutoffRate()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-SN", "Serialized Item", ItemType.Goods)
        {
            HasSerialNo = true,
            UseSerialNoWiseValuation = false // pooled
        };

        _settingProvider.GetOrNullAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation).Returns(Task.FromResult<string?>("false"));

        var sles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-1),
                quantityChange: 4m, valuationRate: 12.5m, balanceQuantity: 4m, balanceValue: 50m)
            {
                SerialNoId = Guid.NewGuid(),
                StockValue = 40m // older sum
            }
        }.AsQueryable();

        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch>().AsQueryable()));
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));

        var service = new StockBalanceAppService(_binRepo, _itemRepo, _warehouseRepo, _sleRepo, _batchRepo, _binService)
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await service.GetBatchWiseBalanceAsync(new GetBatchWiseBalanceRequestDto
        {
            CompanyId = _companyId,
            ItemId = itemId,
            WarehouseId = _warehouseId
        });

        // Assert: Serialized pooled units valued at warehouse rate 12.5 => 4 * 12.5 = 50
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.Balance.ShouldBe(4m);
        row.ValuationRate.ShouldBe(12.5m);
        row.StockValue.ShouldBe(50m);
    }

    [Fact]
    public async Task BatchAppService_GetStockBalanceAsync_ValuesPooledBatchAtWarehouseRate()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-POOL-APP", "Pooled Item", ItemType.Goods)
        {
            HasBatchNo = true,
            ValuationMethod = ValuationMethod.WeightedAverage
        };
        var batch = new Batch(batchId, itemId, "BATCH-APP-01");

        _settingProvider.GetOrNullAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation).Returns(Task.FromResult<string?>("true"));

        var sles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-2),
                quantityChange: 20m, valuationRate: 10m, balanceQuantity: 20m, balanceValue: 200m)
            {
                BatchId = batchId,
                StockValue = 200m
            },
            new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, _warehouseId, DateTime.UtcNow.AddDays(-1),
                quantityChange: 10m, valuationRate: 15m, balanceQuantity: 30m, balanceValue: 450m)
            {
                BatchId = Guid.NewGuid(),
                StockValue = 150m
            }
        }.AsQueryable();

        _batchRepo.GetAsync(batchId).Returns(Task.FromResult(batch));
        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { _warehouse }.AsQueryable()));

        _itemRepo.FindAsync(itemId).Returns(Task.FromResult<Item?>(item));
        _lazySp.LazyGetService<IRepository<Item, Guid>>().Returns(_itemRepo);

        var batchService = new BatchAppService(_batchRepo, _sleRepo, _warehouseRepo, Substitute.For<IStockEntryAppService>())
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await batchService.GetStockBalanceAsync(batchId);

        // Assert: 20 qty valued at warehouse rate 15m => 300m
        result.TotalQuantity.ShouldBe(20m);
        result.TotalValue.ShouldBe(300m);
        result.WarehouseBalances.Count.ShouldBe(1);
        result.WarehouseBalances[0].ValuationRate.ShouldBe(15m);
        result.WarehouseBalances[0].StockValue.ShouldBe(300m);
    }
}
