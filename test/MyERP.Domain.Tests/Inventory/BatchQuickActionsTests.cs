using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Batch Split and Move quick actions.
/// Verifies rules from erpnext/stock/doctype/batch/batch.py (#5992).
/// </summary>
public class BatchQuickActionsTests
{
    private readonly IRepository<Batch, Guid> _batchRepository = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepository = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IStockEntryAppService _stockEntryAppService = Substitute.For<IStockEntryAppService>();

    private readonly BatchAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _warehouseAId = Guid.NewGuid();
    private readonly Guid _warehouseBId = Guid.NewGuid();

    public BatchQuickActionsTests()
    {
        _appService = new BatchAppService(
            _batchRepository,
            _sleRepository,
            _warehouseRepository,
            _stockEntryAppService);
    }

    [Fact]
    public async Task SplitBatchAsync_WithSufficientStock_CreatesTargetBatchAndRepackEntry()
    {
        var sourceBatchId = Guid.NewGuid();
        var sourceBatch = new Batch(sourceBatchId, _itemId, "BATCH-ORIG", null)
        {
            ManufacturingDate = DateTime.UtcNow.AddMonths(-1),
            ExpiryDate = DateTime.UtcNow.AddMonths(11),
            ShelfLifeInDays = 365
        };

        var warehouseA = new Warehouse(_warehouseAId, _companyId, "Store WH") { WarehouseType = WarehouseType.Standard };

        _batchRepository.GetAsync(sourceBatchId).Returns(sourceBatch);
        _warehouseRepository.GetAsync(_warehouseAId).Returns(warehouseA);

        var existingSles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseAId, DateTime.UtcNow, 100m, 10m, 100m, 1000m)
            {
                BatchId = sourceBatchId
            }
        };
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(existingSles.AsQueryable()));

        var allBatches = new List<Batch> { sourceBatch };
        _batchRepository.GetQueryableAsync().Returns(Task.FromResult(allBatches.AsQueryable()));

        Batch? insertedBatch = null;
        await _batchRepository.InsertAsync(Arg.Do<Batch>(b => insertedBatch = b), autoSave: true);

        var stockEntryId = Guid.NewGuid();
        var createdStockEntryDto = new StockEntryDto
        {
            Id = stockEntryId,
            EntryNumber = "STE-2026-0001",
            EntryType = StockEntryType.Repack
        };

        CreateStockEntryDto? capturedCreateDto = null;
        _stockEntryAppService.CreateAsync(Arg.Do<CreateStockEntryDto>(dto => capturedCreateDto = dto))
            .Returns(createdStockEntryDto);

        var input = new SplitBatchDto
        {
            SourceBatchId = sourceBatchId,
            NewBatchNo = "BATCH-SPLIT-01",
            WarehouseId = _warehouseAId,
            SplitQuantity = 30m,
            Description = "Split 30 units for QC test"
        };

        var result = await _appService.SplitBatchAsync(input);

        Assert.NotNull(result);
        Assert.Equal("BATCH-SPLIT-01", result.NewBatchNo);
        Assert.Equal(stockEntryId, result.StockEntryId);

        Assert.NotNull(insertedBatch);
        Assert.Equal("BATCH-SPLIT-01", insertedBatch.BatchNo);
        Assert.Equal(sourceBatch.ItemId, insertedBatch.ItemId);
        Assert.Equal(sourceBatch.ExpiryDate, insertedBatch.ExpiryDate);

        Assert.NotNull(capturedCreateDto);
        Assert.Equal(StockEntryType.Repack, capturedCreateDto.EntryType);
        Assert.Equal(2, capturedCreateDto.Items.Count);
        Assert.Equal(_warehouseAId, capturedCreateDto.Items[0].SourceWarehouseId);
        Assert.Equal(30m, capturedCreateDto.Items[0].Quantity);
        Assert.False(capturedCreateDto.Items[0].IsFinishedItem);
        Assert.Equal(sourceBatchId, capturedCreateDto.Items[0].BatchId);
        Assert.Equal(_warehouseAId, capturedCreateDto.Items[1].TargetWarehouseId);
        Assert.Equal(30m, capturedCreateDto.Items[1].Quantity);
        Assert.True(capturedCreateDto.Items[1].IsFinishedItem);
        Assert.Equal(insertedBatch!.Id, capturedCreateDto.Items[1].BatchId);

        // The outward leg must consume the SOURCE batch and the inward leg must produce the
        // NEW batch — not both tagged with the same id — otherwise GetStockBalanceAsync would
        // never show the source batch's quantity actually decreasing after a split.
        Assert.NotEqual(capturedCreateDto.Items[0].BatchId, capturedCreateDto.Items[1].BatchId);

        await _stockEntryAppService.Received(1).SubmitAsync(stockEntryId);
        // CreateAsync + SubmitAsync alone leave a Stock Entry at "Submitted" with zero real
        // effect — StockEntryAppService.PostAsync is the step that actually creates SLEs/Bin
        // updates. Without this call, Split/Move never moved any real stock (round-93 fix).
        await _stockEntryAppService.Received(1).PostAsync(stockEntryId);
    }

    [Fact]
    public async Task SplitBatchAsync_WithInsufficientStock_ThrowsException()
    {
        var sourceBatchId = Guid.NewGuid();
        var sourceBatch = new Batch(sourceBatchId, _itemId, "BATCH-ORIG", null);
        var warehouseA = new Warehouse(_warehouseAId, _companyId, "Store WH") { WarehouseType = WarehouseType.Standard };

        _batchRepository.GetAsync(sourceBatchId).Returns(sourceBatch);
        _warehouseRepository.GetAsync(_warehouseAId).Returns(warehouseA);

        var existingSles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseAId, DateTime.UtcNow, 20m, 10m, 20m, 200m)
            {
                BatchId = sourceBatchId
            }
        };
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(existingSles.AsQueryable()));

        var input = new SplitBatchDto
        {
            SourceBatchId = sourceBatchId,
            NewBatchNo = "BATCH-SPLIT-01",
            WarehouseId = _warehouseAId,
            SplitQuantity = 50m // 50 > 20 available
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.SplitBatchAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.InsufficientStock, ex.Code);
    }

    [Fact]
    public async Task MoveBatchAsync_WithSufficientStock_CreatesMaterialTransferEntry()
    {
        var batchId = Guid.NewGuid();
        var batch = new Batch(batchId, _itemId, "BATCH-001", null);

        var sourceWarehouse = new Warehouse(_warehouseAId, _companyId, "Main Warehouse") { WarehouseType = WarehouseType.Standard };
        var targetWarehouse = new Warehouse(_warehouseBId, _companyId, "Finished Goods") { WarehouseType = WarehouseType.Standard };

        _batchRepository.GetAsync(batchId).Returns(batch);
        _warehouseRepository.GetAsync(_warehouseAId).Returns(sourceWarehouse);
        _warehouseRepository.GetAsync(_warehouseBId).Returns(targetWarehouse);

        var existingSles = new List<StockLedgerEntry>
        {
            new(Guid.NewGuid(), _companyId, _itemId, _warehouseAId, DateTime.UtcNow, 100m, 10m, 100m, 1000m)
            {
                BatchId = batchId
            }
        };
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(existingSles.AsQueryable()));

        var stockEntryId = Guid.NewGuid();
        var createdStockEntryDto = new StockEntryDto
        {
            Id = stockEntryId,
            EntryNumber = "STE-2026-0002",
            EntryType = StockEntryType.MaterialTransfer
        };

        CreateStockEntryDto? capturedCreateDto = null;
        _stockEntryAppService.CreateAsync(Arg.Do<CreateStockEntryDto>(dto => capturedCreateDto = dto))
            .Returns(createdStockEntryDto);

        var input = new MoveBatchDto
        {
            BatchId = batchId,
            SourceWarehouseId = _warehouseAId,
            TargetWarehouseId = _warehouseBId,
            Quantity = 40m,
            Description = "Transfer to Finished Goods WH"
        };

        var result = await _appService.MoveBatchAsync(input);

        Assert.NotNull(result);
        Assert.Equal(batchId, result.BatchId);
        Assert.Equal(stockEntryId, result.StockEntryId);

        Assert.NotNull(capturedCreateDto);
        Assert.Equal(StockEntryType.MaterialTransfer, capturedCreateDto.EntryType);
        Assert.Single(capturedCreateDto.Items);
        Assert.Equal(_warehouseAId, capturedCreateDto.Items[0].SourceWarehouseId);
        Assert.Equal(_warehouseBId, capturedCreateDto.Items[0].TargetWarehouseId);
        Assert.Equal(40m, capturedCreateDto.Items[0].Quantity);
        Assert.Equal(batchId, capturedCreateDto.Items[0].BatchId);

        await _stockEntryAppService.Received(1).SubmitAsync(stockEntryId);
        await _stockEntryAppService.Received(1).PostAsync(stockEntryId);
    }

    [Fact]
    public async Task MoveBatchAsync_SameWarehouse_ThrowsValidationException()
    {
        var input = new MoveBatchDto
        {
            BatchId = Guid.NewGuid(),
            SourceWarehouseId = _warehouseAId,
            TargetWarehouseId = _warehouseAId, // Same warehouse
            Quantity = 10m
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.MoveBatchAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task GetBatchSplitTreeAsync_ReturnsHierarchicalTree()
    {
        var parentBatchId = Guid.NewGuid();
        var childBatchId1 = Guid.NewGuid();
        var childBatchId2 = Guid.NewGuid();
        var grandChildBatchId = Guid.NewGuid();

        var parentBatch = new Batch(parentBatchId, _itemId, "PARENT-BATCH", null);
        var child1 = new Batch(childBatchId1, _itemId, "CHILD-1", null) { ParentBatchId = parentBatchId, ReferenceDocType = "StockEntry" };
        var child2 = new Batch(childBatchId2, _itemId, "CHILD-2", null) { ParentBatchId = parentBatchId, ReferenceDocType = "JobCard" };
        var grandChild = new Batch(grandChildBatchId, _itemId, "GRANDCHILD-1", null) { ParentBatchId = childBatchId1 };

        var allBatches = new List<Batch> { parentBatch, child1, child2, grandChild }.AsQueryable();

        _batchRepository.GetQueryableAsync().Returns(Task.FromResult(allBatches));
        _sleRepository.GetQueryableAsync().Returns(Task.FromResult(new List<StockLedgerEntry>().AsQueryable()));

        var result = await _appService.GetBatchSplitTreeAsync(new GetBatchSplitTreeDto());

        Assert.NotNull(result);
        Assert.Single(result);
        var root = result[0];
        Assert.Equal("PARENT-BATCH", root.BatchNo);
        Assert.Equal(0, root.Indent);
        Assert.Equal(2, root.Children.Count);

        var firstChild = root.Children.FirstOrDefault(c => c.BatchNo == "CHILD-1");
        Assert.NotNull(firstChild);
        Assert.Equal(1, firstChild.Indent);
        Assert.Single(firstChild.Children);
        Assert.Equal("GRANDCHILD-1", firstChild.Children[0].BatchNo);
        Assert.Equal(2, firstChild.Children[0].Indent);
    }
}
