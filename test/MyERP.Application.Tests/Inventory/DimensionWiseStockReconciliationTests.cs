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
/// Unit tests for dimension-wise stock reconciliation change calculation per ERPNext PR #60698 / commit c8eb481d32.
/// When Stock Reconciliation has no inventory dimension and Show Dimension Wise Stock is enabled,
/// the change must be calculated against the total item-warehouse balance rather than the dimensionless row's balance,
/// preventing the dimensionless row from absorbing other dimensions' stock and doubling totals.
/// </summary>
public class DimensionWiseStockReconciliationTests
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
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _batch1Id = Guid.NewGuid();
    private readonly Guid _batch2Id = Guid.NewGuid();

    private readonly Item _item;
    private readonly Warehouse _warehouse;
    private readonly Batch _batch1;
    private readonly Batch _batch2;

    public DimensionWiseStockReconciliationTests()
    {
        _binService = new BinService(_binRepo);
        _item = new Item(_itemId, _companyId, "ITEM-DIM", "Dimension Test Item", ItemType.Goods)
        {
            HasBatchNo = true,
            ValuationMethod = ValuationMethod.FIFO
        };
        _warehouse = new Warehouse(_warehouseId, _companyId, "Warehouse 01") { WarehouseCode = "WH-01" };
        _batch1 = new Batch(_batch1Id, _itemId, "BATCH-ALPHA");
        _batch2 = new Batch(_batch2Id, _itemId, "BATCH-BETA");

        _lazySp.LazyGetService<ISettingProvider>().Returns(_settingProvider);
        _lazySp.LazyGetRequiredService<ISettingProvider>().Returns(_settingProvider);

        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item }.AsQueryable()));
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { _warehouse }.AsQueryable()));
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { _batch1, _batch2 }.AsQueryable()));
    }

    [Fact]
    public async Task DimensionWise_ReconciliationWithoutDimension_ComputesDeltaFromWarehouseTotal_NotDimensionRow()
    {
        // Arrange
        // Initial movements: Batch 1 has 10 units @ 10 = 100; Batch 2 has 20 units @ 10 = 200. Total WH = 30 units, 300 value.
        // A Stock Reconciliation entry without batch (dimensionless) sets warehouse balance to 35 units @ 10 = 350.
        var sles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow.AddDays(-5),
                quantityChange: 10m, valuationRate: 10m, balanceQuantity: 10m, balanceValue: 100m)
            {
                BatchId = _batch1Id,
                StockValue = 100m,
                VoucherType = "StockEntry"
            },
            new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow.AddDays(-4),
                quantityChange: 20m, valuationRate: 10m, balanceQuantity: 30m, balanceValue: 300m)
            {
                BatchId = _batch2Id,
                StockValue = 200m,
                VoucherType = "StockEntry"
            },
            // Stock Reconciliation (no batch specified) reconciling warehouse balance to 35 units, 350 value:
            new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow.AddDays(-2),
                quantityChange: 5m, valuationRate: 10m, balanceQuantity: 35m, balanceValue: 350m)
            {
                BatchId = null, // dimensionless
                StockValue = 50m,
                VoucherType = "StockReconciliation"
            }
        }.AsQueryable();

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));

        var service = new StockBalanceAppService(_binRepo, _itemRepo, _warehouseRepo, _sleRepo, _batchRepo, _binService)
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await service.GetStockBalanceReportAsync(new GetStockBalanceReportRequestDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ShowDimensionWiseStock = true
        });

        // Assert
        // We have 3 rows: Batch 1, Batch 2, and dimensionless adjustment row.
        result.Rows.Count.ShouldBe(3);

        var batch1Row = result.Rows.First(r => r.BatchId == _batch1Id);
        batch1Row.BalanceQty.ShouldBe(10m);
        batch1Row.BalanceValue.ShouldBe(100m);

        var batch2Row = result.Rows.First(r => r.BatchId == _batch2Id);
        batch2Row.BalanceQty.ShouldBe(20m);
        batch2Row.BalanceValue.ShouldBe(200m);

        var reconciliationRow = result.Rows.First(r => r.BatchId == null);
        // Under PR #60698, reconciliation delta is 35 - 30 = +5 (NOT 35 - 0 = 35)
        reconciliationRow.BalanceQty.ShouldBe(5m);
        reconciliationRow.BalanceValue.ShouldBe(50m);

        // Overall total MUST match reconciled on-hand qty 35 and value 350, NOT 65!
        result.TotalBalanceQty.ShouldBe(35m);
        result.TotalBalanceValue.ShouldBe(350m);
    }

    [Fact]
    public async Task NonDimensionWise_StockBalance_ReconcilesNormally()
    {
        // Arrange: same entries as above, but viewed without dimension breakdown
        var sles = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow.AddDays(-5),
                quantityChange: 10m, valuationRate: 10m, balanceQuantity: 10m, balanceValue: 100m)
            {
                BatchId = _batch1Id,
                StockValue = 100m,
                VoucherType = "StockEntry"
            },
            new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow.AddDays(-4),
                quantityChange: 20m, valuationRate: 10m, balanceQuantity: 30m, balanceValue: 300m)
            {
                BatchId = _batch2Id,
                StockValue = 200m,
                VoucherType = "StockEntry"
            },
            new StockLedgerEntry(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow.AddDays(-2),
                quantityChange: 5m, valuationRate: 10m, balanceQuantity: 35m, balanceValue: 350m)
            {
                BatchId = null,
                StockValue = 50m,
                VoucherType = "StockReconciliation"
            }
        }.AsQueryable();

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));

        var service = new StockBalanceAppService(_binRepo, _itemRepo, _warehouseRepo, _sleRepo, _batchRepo, _binService)
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await service.GetStockBalanceReportAsync(new GetStockBalanceReportRequestDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = _warehouseId,
            ShowDimensionWiseStock = false
        });

        // Assert: 1 row aggregating the warehouse
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].BalanceQty.ShouldBe(35m);
        result.Rows[0].BalanceValue.ShouldBe(350m);
        result.TotalBalanceQty.ShouldBe(35m);
        result.TotalBalanceValue.ShouldBe(350m);
    }

    [Fact]
    public async Task DimensionWise_PooledItem_UsesLatestPoolValuationRateOnDimensionRows()
    {
        // Arrange
        // Per ERPNext PR #60693 / commits 80c155243f & 8430921d87:
        // When stock is valued in pool (e.g. WeightedAverage / Moving Average with batch-wise valuation disabled),
        // dimension rows must be valued at the latest pool valuation rate, not individual historical receipt diffs.
        var movingAvgItem = new Item(Guid.NewGuid(), _companyId, "ITEM-MA", "Moving Average Item", ItemType.Goods)
        {
            HasBatchNo = true,
            ValuationMethod = ValuationMethod.WeightedAverage
        };

        _settingProvider.GetOrNullAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation)
            .Returns(Task.FromResult<string?>("true"));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { movingAvgItem }.AsQueryable()));

        var sles = new List<StockLedgerEntry>
        {
            // Inward Batch 1: 10 units @ 100 = 1000. Balance qty = 10, balance value = 1000, rate = 100.
            new StockLedgerEntry(Guid.NewGuid(), _companyId, movingAvgItem.Id, _warehouseId, DateTime.UtcNow.AddDays(-3),
                quantityChange: 10m, valuationRate: 100m, balanceQuantity: 10m, balanceValue: 1000m)
            {
                BatchId = _batch1Id,
                StockValue = 1000m,
                StockValueDifference = 1000m,
                VoucherType = "StockEntry"
            },
            // Inward Batch 2: 10 units @ 200 = 2000. Balance qty = 20, balance value = 3000, rate = 150.
            new StockLedgerEntry(Guid.NewGuid(), _companyId, movingAvgItem.Id, _warehouseId, DateTime.UtcNow.AddDays(-2),
                quantityChange: 10m, valuationRate: 150m, balanceQuantity: 20m, balanceValue: 3000m)
            {
                BatchId = _batch2Id,
                StockValue = 2000m,
                StockValueDifference = 2000m,
                VoucherType = "StockEntry"
            },
            // Outward from Batch 1: 5 units @ pool rate 150 = -750. Balance qty = 15, balance value = 2250, rate = 150.
            new StockLedgerEntry(Guid.NewGuid(), _companyId, movingAvgItem.Id, _warehouseId, DateTime.UtcNow.AddDays(-1),
                quantityChange: -5m, valuationRate: 150m, balanceQuantity: 15m, balanceValue: 2250m)
            {
                BatchId = _batch1Id,
                StockValue = -750m,
                StockValueDifference = -750m,
                VoucherType = "StockEntry"
            }
        }.AsQueryable();

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(sles));

        var service = new StockBalanceAppService(_binRepo, _itemRepo, _warehouseRepo, _sleRepo, _batchRepo, _binService)
        {
            LazyServiceProvider = _lazySp
        };

        // Act
        var result = await service.GetStockBalanceReportAsync(new GetStockBalanceReportRequestDto
        {
            CompanyId = _companyId,
            ItemId = movingAvgItem.Id,
            WarehouseId = _warehouseId,
            ShowDimensionWiseStock = true
        });

        // Assert
        result.Rows.Count.ShouldBe(2);

        var batch1Row = result.Rows.First(r => r.BatchId == _batch1Id);
        batch1Row.BalanceQty.ShouldBe(5m);
        batch1Row.ValuationRate.ShouldBe(150m);
        batch1Row.BalanceValue.ShouldBe(750m); // 5 * 150 = 750 (not historical 1000 - 750 = 250)

        var batch2Row = result.Rows.First(r => r.BatchId == _batch2Id);
        batch2Row.BalanceQty.ShouldBe(10m);
        batch2Row.ValuationRate.ShouldBe(150m);
        batch2Row.BalanceValue.ShouldBe(1500m); // 10 * 150 = 1500

        result.TotalBalanceQty.ShouldBe(15m);
        result.TotalBalanceValue.ShouldBe(2250m);
    }
}
