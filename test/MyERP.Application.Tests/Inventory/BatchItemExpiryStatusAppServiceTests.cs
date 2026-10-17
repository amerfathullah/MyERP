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

public class BatchItemExpiryStatusAppServiceTests
{
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly BatchItemExpiryStatusAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public BatchItemExpiryStatusAppServiceTests()
    {
        _appService = new BatchItemExpiryStatusAppService(
            _batchRepo,
            _itemRepo,
            _sleRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetReportAsync_IdentifiesExpiredAndExpiringSoonBatches()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "MED-001", "Amoxicillin 500mg", ItemType.Goods) { Uom = "Bottle" };

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Pharma Stores");

        var today = DateTime.UtcNow.Date;

        // Batch 1: Expired (10 days ago)
        var batch1 = new Batch(Guid.NewGuid(), itemId, "BATCH-EXPIRED")
        {
            CreationTime = today.AddDays(-60),
            ExpiryDate = today.AddDays(-10)
        };

        // Batch 2: Expiring Soon (15 days from now)
        var batch2 = new Batch(Guid.NewGuid(), itemId, "BATCH-SOON")
        {
            CreationTime = today.AddDays(-30),
            ExpiryDate = today.AddDays(15)
        };

        // Batch 3: Safe (180 days from now)
        var batch3 = new Batch(Guid.NewGuid(), itemId, "BATCH-SAFE")
        {
            CreationTime = today.AddDays(-10),
            ExpiryDate = today.AddDays(180)
        };

        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-60), 5m, 10m, 5m, 50m)
        {
            BatchId = batch1.Id
        };
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-30), 10m, 10m, 15m, 150m)
        {
            BatchId = batch2.Id
        };
        var sle3 = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-10), 20m, 10m, 35m, 350m)
        {
            BatchId = batch3.Id
        };

        _batchRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Batch> { batch1, batch2, batch3 }.AsQueryable()));

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { sle1, sle2, sle3 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BatchItemExpiryStatusRequestDto
        {
            FromDate = today.AddDays(-90),
            ToDate = today,
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(3);
        result.TotalBatches.ShouldBe(3);
        result.ExpiredCount.ShouldBe(1);
        result.ExpiringSoonCount.ShouldBe(1);
        result.TotalQuantity.ShouldBe(35m);

        // Ordered by ExpiryInDays ascending (Expired first)
        result.Rows[0].BatchNo.ShouldBe("BATCH-EXPIRED");
        result.Rows[0].IsExpired.ShouldBeTrue();
        result.Rows[0].IsExpiringSoon.ShouldBeFalse();
        result.Rows[0].ExpiryInDays.ShouldBe(-10);

        result.Rows[1].BatchNo.ShouldBe("BATCH-SOON");
        result.Rows[1].IsExpired.ShouldBeFalse();
        result.Rows[1].IsExpiringSoon.ShouldBeTrue();
        result.Rows[1].ExpiryInDays.ShouldBe(15);

        result.Rows[2].BatchNo.ShouldBe("BATCH-SAFE");
        result.Rows[2].IsExpired.ShouldBeFalse();
        result.Rows[2].IsExpiringSoon.ShouldBeFalse();
        result.Rows[2].ExpiryInDays.ShouldBe(180);
    }

    [Fact]
    public async Task GetReportAsync_ExcludesZeroBalanceBatches()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-001", "Chemical", ItemType.Goods);
        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Stores");
        var today = DateTime.UtcNow.Date;

        var batchInStock = new Batch(Guid.NewGuid(), itemId, "BATCH-IN-STOCK")
        {
            CreationTime = today.AddDays(-20),
            ExpiryDate = today.AddDays(40)
        };
        var batchDepleted = new Batch(Guid.NewGuid(), itemId, "BATCH-DEPLETED")
        {
            CreationTime = today.AddDays(-20),
            ExpiryDate = today.AddDays(40)
        };

        var sleIn = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-20), 10m, 5m, 10m, 50m) { BatchId = batchInStock.Id };
        var sleDepIn = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-20), 10m, 5m, 10m, 50m) { BatchId = batchDepleted.Id };
        var sleDepOut = new StockLedgerEntry(Guid.NewGuid(), _companyId, itemId, whId, today.AddDays(-10), -10m, 5m, 0m, 0m) { BatchId = batchDepleted.Id };

        _batchRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Batch> { batchInStock, batchDepleted }.AsQueryable()));

        _sleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<StockLedgerEntry> { sleIn, sleDepIn, sleDepOut }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BatchItemExpiryStatusRequestDto
        {
            FromDate = today.AddDays(-30),
            ToDate = today
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].BatchNo.ShouldBe("BATCH-IN-STOCK");
        result.Rows[0].Quantity.ShouldBe(10m);
    }

    [Fact]
    public async Task GetReportAsync_WhenNoBatchesInDateRange_ReturnsEmptyReport()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        _batchRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Batch>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new BatchItemExpiryStatusRequestDto
        {
            FromDate = today.AddDays(-30),
            ToDate = today
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalBatches.ShouldBe(0);
        result.TotalQuantity.ShouldBe(0m);
    }
}
