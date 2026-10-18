using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class NegativeBatchReportAppServiceTests
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    private readonly NegativeBatchReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();

    public NegativeBatchReportAppServiceTests()
    {
        _appService = new NegativeBatchReportAppService(
            _sleRepo,
            _bundleRepo,
            _batchRepo,
            _warehouseRepo,
            _itemRepo);
    }

    [Fact]
    public async Task GetReportAsync_NoNegativeBatches_ReturnsEmptyRows()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores WH") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "RAW-STEEL-01", "Steel Plate", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var batch = new Batch(_batchId, _itemId, "BATCH-2026-001");
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var sle = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            DateTime.UtcNow, 100m, 50m, 100m, 5000m)
        {
            BatchId = _batchId,
            IsCancelled = false
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new NegativeBatchFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalIncidents.ShouldBe(0);
        result.DistinctNegativeBatches.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_NegativeBatchOccurred_ReturnsIncident()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores WH") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "RAW-STEEL-01", "Steel Plate", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var batch = new Batch(_batchId, _itemId, "BATCH-2026-001");
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var now = DateTime.UtcNow;
        var sle1 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now.AddHours(-2), 50m, 50m, 50m, 2500m)
        {
            BatchId = _batchId,
            IsCancelled = false,
            VoucherType = "Purchase Receipt",
            VoucherId = Guid.NewGuid()
        };

        var sle2 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now.AddHours(-1), -70m, 50m, -20m, -1000m)
        {
            BatchId = _batchId,
            IsCancelled = false,
            VoucherType = "Delivery Note",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new NegativeBatchFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.TotalIncidents.ShouldBe(1);
        result.DistinctNegativeBatches.ShouldBe(1);

        var row = result.Rows[0];
        row.BatchId.ShouldBe(_batchId);
        row.BatchNo.ShouldBe("BATCH-2026-001");
        row.ItemCode.ShouldBe("RAW-STEEL-01");
        row.WarehouseName.ShouldBe("Stores WH");
        row.PreviousQty.ShouldBe(50m);
        row.TransactionQty.ShouldBe(-70m);
        row.QtyAfterTransaction.ShouldBe(-20m);
        row.VoucherType.ShouldBe("Delivery Note");
    }

    [Fact]
    public async Task GetReportAsync_ViaBundleOutward_DetectsNegativeBalance()
    {
        // Arrange
        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores WH") { IsActive = true, IsGroup = false };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "RAW-STEEL-01", "Steel Plate", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var batch = new Batch(_batchId, _itemId, "BATCH-2026-002");
        _batchRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch }.AsQueryable()));

        var now = DateTime.UtcNow;
        var bundleId = Guid.NewGuid();

        var sle1 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now.AddHours(-3), 30m, 50m, 30m, 1500m)
        {
            BatchId = _batchId,
            IsCancelled = false,
            VoucherType = "Stock Entry",
            VoucherId = Guid.NewGuid()
        };

        var sle2 = new StockLedgerEntry(
            Guid.NewGuid(), _companyId, _itemId, _warehouseId,
            now.AddHours(-1), -50m, 50m, -20m, -1000m)
        {
            SerialAndBatchBundleId = bundleId,
            IsCancelled = false,
            VoucherType = "Sales Invoice",
            VoucherId = Guid.NewGuid()
        };

        _sleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry> { sle1, sle2 }.AsQueryable()));

        var bundle = new SerialAndBatchBundle(bundleId, _companyId, _itemId, _warehouseId, BundleTransactionType.Outward, "Sales Invoice", sle2.VoucherId.Value, now.Date)
        {
            IsCancelled = false
        };
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 50m, 50m, batchId: _batchId));

        _bundleRepo.WithDetailsAsync(Arg.Any<Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new NegativeBatchFilterDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].PreviousQty.ShouldBe(30m);
        result.Rows[0].TransactionQty.ShouldBe(-50m);
        result.Rows[0].QtyAfterTransaction.ShouldBe(-20m);
    }
}
