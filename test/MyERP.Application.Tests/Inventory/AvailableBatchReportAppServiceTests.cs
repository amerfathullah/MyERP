using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class AvailableBatchReportAppServiceTests
{
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly AvailableBatchReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _batch1Id = Guid.NewGuid();
    private readonly Guid _batch2Id = Guid.NewGuid();

    public AvailableBatchReportAppServiceTests()
    {
        _appService = new AvailableBatchReportAppService(
            _batchRepo,
            _sleRepo,
            _bundleRepo,
            _warehouseRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsAvailableBatches_WithDirectBatchSLE()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-MED-01", "Pain Relief 500mg", ItemType.Goods)
        {
            MaintainStock = true,
            HasBatchNo = true,
            Uom = "Box"
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var warehouse = new Warehouse(_warehouseId, _companyId, "Pharma Stores")
        {
            IsGroup = false,
            IsActive = true
        };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        var batch1 = new Batch(_batch1Id, _itemId, "BATCH-2026-001")
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(90)
        };
        _batchRepo.GetQueryableAsync().Returns(new List<Batch> { batch1 }.AsQueryable());

        var sle1 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow.AddDays(-1), 50m, 10m, 50m, 500m)
        {
            BatchId = _batch1Id,
            IsCancelled = false
        };
        var sle2 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, -10m, 10m, 40m, 400m)
        {
            BatchId = _batch1Id,
            IsCancelled = false
        };
        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new AvailableBatchFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalBatches.ShouldBe(1);
        result.TotalBalanceQty.ShouldBe(40m);
        result.ExpiredBatchesCount.ShouldBe(0);

        var row = result.Rows.ShouldHaveSingleItem();
        row.ItemCode.ShouldBe("ITEM-MED-01");
        row.ItemName.ShouldBe("Pain Relief 500mg");
        row.WarehouseName.ShouldBe("Pharma Stores");
        row.BatchNo.ShouldBe("BATCH-2026-001");
        row.BalanceQty.ShouldBe(40m);
        row.IsExpired.ShouldBeFalse();
        row.StockUom.ShouldBe("Box");
    }

    [Fact]
    public async Task GetReportAsync_ExcludeExpiredBatches_WhenIncludeExpiredIsFalse()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-CHEM-01", "Chemical Reagent", ItemType.Goods)
        {
            MaintainStock = true,
            HasBatchNo = true,
            Uom = "Bottle"
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var warehouse = new Warehouse(_warehouseId, _companyId, "Chemical Lab")
        {
            IsGroup = false,
            IsActive = true
        };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        var expiredBatch = new Batch(_batch1Id, _itemId, "BATCH-EXP-001")
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(-5)
        };
        var validBatch = new Batch(_batch2Id, _itemId, "BATCH-VAL-002")
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(60)
        };
        _batchRepo.GetQueryableAsync().Returns(new List<Batch> { expiredBatch, validBatch }.AsQueryable());

        var sle1 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow.AddDays(-2), 25m, 10m, 25m, 250m)
        {
            BatchId = _batch1Id,
            IsCancelled = false
        };
        var sle2 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow.AddDays(-1), 30m, 10m, 55m, 550m)
        {
            BatchId = _batch2Id,
            IsCancelled = false
        };
        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable());

        // Act - Exclude expired batches (default)
        var result = await _appService.GetReportAsync(new AvailableBatchFilterDto
        {
            CompanyId = _companyId,
            IncludeExpiredBatches = false
        });

        // Assert - Only valid batch returned
        result.ShouldNotBeNull();
        result.TotalBatches.ShouldBe(1);
        result.Rows.ShouldHaveSingleItem().BatchNo.ShouldBe("BATCH-VAL-002");
    }

    [Fact]
    public async Task GetReportAsync_IncludeExpiredBatches_ReturnsBoth()
    {
        // Arrange
        var item = new Item(_itemId, _companyId, "ITEM-CHEM-01", "Chemical Reagent", ItemType.Goods)
        {
            MaintainStock = true,
            HasBatchNo = true,
            Uom = "Bottle"
        };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var warehouse = new Warehouse(_warehouseId, _companyId, "Chemical Lab")
        {
            IsGroup = false,
            IsActive = true
        };
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        var expiredBatch = new Batch(_batch1Id, _itemId, "BATCH-EXP-001")
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(-5)
        };
        var validBatch = new Batch(_batch2Id, _itemId, "BATCH-VAL-002")
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(60)
        };
        _batchRepo.GetQueryableAsync().Returns(new List<Batch> { expiredBatch, validBatch }.AsQueryable());

        var sle1 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow.AddDays(-2), 25m, 10m, 25m, 250m)
        {
            BatchId = _batch1Id,
            IsCancelled = false
        };
        var sle2 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow.AddDays(-1), 30m, 10m, 55m, 550m)
        {
            BatchId = _batch2Id,
            IsCancelled = false
        };
        _sleRepo.GetQueryableAsync().Returns(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable());

        // Act - Include expired
        var result = await _appService.GetReportAsync(new AvailableBatchFilterDto
        {
            CompanyId = _companyId,
            IncludeExpiredBatches = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalBatches.ShouldBe(2);
        result.TotalBalanceQty.ShouldBe(55m);
        result.ExpiredBatchesCount.ShouldBe(1);
    }
}
