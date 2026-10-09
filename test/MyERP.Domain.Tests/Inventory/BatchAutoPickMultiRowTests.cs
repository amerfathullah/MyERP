using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class BatchAutoPickMultiRowTests
{
    private readonly IRepository<Batch, Guid> _batchRepository = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepository = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepository = Substitute.For<IRepository<Item, Guid>>();
    private readonly IStockEntryAppService _stockEntryAppService = Substitute.For<IStockEntryAppService>();
    private readonly IAbpLazyServiceProvider _lazyProvider = Substitute.For<IAbpLazyServiceProvider>();

    private readonly BatchAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    public BatchAutoPickMultiRowTests()
    {
        _lazyProvider.LazyGetRequiredService<IRepository<Item, Guid>>().Returns(_itemRepository);
        _lazyProvider.LazyGetService<IRepository<StockReservationEntry, Guid>>().Returns((IRepository<StockReservationEntry, Guid>?)null);
        _lazyProvider.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>().Returns((IRepository<SerialAndBatchBundle, Guid>?)null);

        _appService = new BatchAppService(
            _batchRepository,
            _sleRepository,
            _warehouseRepository,
            _stockEntryAppService)
        {
            LazyServiceProvider = _lazyProvider
        };
    }

    [Fact]
    public async Task AutoPickBatches_MultipleRows_PicksDifferentBatchesPerCumulativeExclusions()
    {
        // Setup: 3 batches with 1 qty each in Warehouse
        var batch1 = new Batch(Guid.NewGuid(), _itemId, "BATCH-001", null) { CreationTime = DateTime.UtcNow.AddMinutes(-30) };
        var batch2 = new Batch(Guid.NewGuid(), _itemId, "BATCH-002", null) { CreationTime = DateTime.UtcNow.AddMinutes(-20) };
        var batch3 = new Batch(Guid.NewGuid(), _itemId, "BATCH-003", null) { CreationTime = DateTime.UtcNow.AddMinutes(-10) };
        var allBatches = new List<Batch> { batch1, batch2, batch3 };

        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores");
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));
        _batchRepository.GetQueryableAsync().Returns(Task.FromResult(allBatches.AsQueryable()));

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-BATCH", "Batch Item", ItemType.Goods) { HasBatchNo = true };
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 1m, 10m, 1m, 10m) { BatchId = batch1.Id },
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 1m, 10m, 1m, 10m) { BatchId = batch2.Id },
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 1m, 10m, 1m, 10m) { BatchId = batch3.Id }
        };
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        // Document with 3 rows of 1 qty each without preselected batch
        var row1Id = Guid.NewGuid();
        var row2Id = Guid.NewGuid();
        var row3Id = Guid.NewGuid();

        var input = new AutoPickBatchesForDocumentDto
        {
            CompanyId = _companyId,
            Rows = new List<AutoPickDocumentRowDto>
            {
                new() { RowId = row1Id, ItemId = _itemId, WarehouseId = _warehouseId, RequiredStockQty = 1m },
                new() { RowId = row2Id, ItemId = _itemId, WarehouseId = _warehouseId, RequiredStockQty = 1m },
                new() { RowId = row3Id, ItemId = _itemId, WarehouseId = _warehouseId, RequiredStockQty = 1m },
            }
        };

        var result = await _appService.AutoPickBatchesForDocumentAsync(input);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.True(r.IsAllocated));

        var pickedBatchIds = result.Select(r => r.BatchId).Distinct().ToList();
        // Crucial invariant from PR #60120 test_auto_picked_batches_differ_across_rows: all 3 rows pick distinct batches!
        Assert.Equal(3, pickedBatchIds.Count);
        Assert.Equal(batch1.Id, result[0].BatchId);
        Assert.Equal(batch2.Id, result[1].BatchId);
        Assert.Equal(batch3.Id, result[2].BatchId);
    }

    [Fact]
    public async Task AutoPickBatches_WithPreselectedRow_ExcludesPreselectedQtyFromSubsequentRows()
    {
        // Setup: Batch1 has 2 qty, Batch2 has 2 qty
        var batch1 = new Batch(Guid.NewGuid(), _itemId, "BATCH-001", null) { CreationTime = DateTime.UtcNow.AddMinutes(-30) };
        var batch2 = new Batch(Guid.NewGuid(), _itemId, "BATCH-002", null) { CreationTime = DateTime.UtcNow.AddMinutes(-20) };
        var allBatches = new List<Batch> { batch1, batch2 };

        var warehouse = new Warehouse(_warehouseId, _companyId, "Stores");
        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouse }.AsQueryable()));
        _batchRepository.GetQueryableAsync().Returns(Task.FromResult(allBatches.AsQueryable()));

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-BATCH", "Batch Item", ItemType.Goods) { HasBatchNo = true };
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 2m, 10m, 2m, 20m) { BatchId = batch1.Id },
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseId, DateTime.UtcNow, 2m, 10m, 2m, 20m) { BatchId = batch2.Id }
        };
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        // Row 1 already claims Batch1 for 2 qty (full capacity of Batch1)
        // Row 2 needs 2 qty without preselected batch
        var row1Id = Guid.NewGuid();
        var row2Id = Guid.NewGuid();

        var input = new AutoPickBatchesForDocumentDto
        {
            CompanyId = _companyId,
            Rows = new List<AutoPickDocumentRowDto>
            {
                new() { RowId = row1Id, ItemId = _itemId, WarehouseId = _warehouseId, RequiredStockQty = 2m, PreselectedBatchId = batch1.Id },
                new() { RowId = row2Id, ItemId = _itemId, WarehouseId = _warehouseId, RequiredStockQty = 2m }
            }
        };

        var result = await _appService.AutoPickBatchesForDocumentAsync(input);

        Assert.Equal(2, result.Count);
        Assert.Equal(batch1.Id, result[0].BatchId);
        // Row 2 must pick Batch2 because Batch1 is fully claimed by row 1 (PR #60120 test_transfer_with_picked_and_auto_picked_rows_keeps_batch_qty)
        Assert.Equal(batch2.Id, result[1].BatchId);
    }

    [Fact]
    public async Task GetAvailableBatchesAsync_ExcludesQuantitiesScopedByWarehouse()
    {
        var batch1 = new Batch(Guid.NewGuid(), _itemId, "BATCH-001", null);
        var warehouseA = new Warehouse(Guid.NewGuid(), _companyId, "WH-A");
        var warehouseB = new Warehouse(Guid.NewGuid(), _companyId, "WH-B");

        _warehouseRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { warehouseA, warehouseB }.AsQueryable()));
        _batchRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Batch> { batch1 }.AsQueryable()));

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-BATCH", "Batch Item", ItemType.Goods);
        _itemRepository.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var sles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), _companyId, _itemId, warehouseA.Id, DateTime.UtcNow, 5m, 10m, 5m, 50m) { BatchId = batch1.Id },
            new(Guid.NewGuid(), _companyId, _itemId, warehouseB.Id, DateTime.UtcNow, 5m, 10m, 5m, 50m) { BatchId = batch1.Id }
        };
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(sles.AsQueryable()));

        // Exclude 3 qty from Warehouse A only
        var input = new GetAvailableBatchesDto
        {
            CompanyId = _companyId,
            ItemId = _itemId,
            WarehouseId = warehouseB.Id,
            SameDocumentBatchQuantities = new List<ExcludedBatchQtyDto>
            {
                new() { BatchId = batch1.Id, WarehouseId = warehouseA.Id, StockQty = 3m }
            }
        };

        var availableInB = await _appService.GetAvailableBatchesAsync(input);

        Assert.Single(availableInB);
        // Available in WH-B must still be 5 because exclusion was scoped to WH-A
        Assert.Equal(5m, availableInB[0].AvailableQuantity);
    }
}
