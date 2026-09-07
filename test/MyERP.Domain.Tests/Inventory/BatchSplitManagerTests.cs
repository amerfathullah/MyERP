using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Batch Split operations (ERPNext PR #58530 / commit 0223223385).
/// Verifies parent-to-child batch lineage, apportionment algorithm, whole-piece capacities,
/// inward bundle creation, and cancellation.
/// </summary>
public class BatchSplitManagerTests
{
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _rmItemId = Guid.NewGuid();
    private readonly Guid _fgItemId = Guid.NewGuid();
    private readonly Guid _srcWh = Guid.NewGuid();
    private readonly Guid _tgtWh = Guid.NewGuid();

    private BatchSplitManager CreateManager()
    {
        return new BatchSplitManager(_batchRepo, _itemRepo, _bundleRepo, _sleRepo);
    }

    [Fact]
    public void IsApplicable_ReturnsTrue_ForRepack_WhenWeightPerPiecePositive()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 5m
        };

        Assert.True(manager.IsApplicable(entry));
    }

    [Fact]
    public void IsApplicable_ReturnsTrue_ForManufacture_WhenJobCardHasBatchSplit()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        var jc = new JobCard(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), 10m, 1)
        {
            BatchSplit = true,
            WeightPerPiece = 2m
        };

        Assert.True(manager.IsApplicable(entry, jc));
        Assert.Equal(2m, entry.WeightPerPiece);
    }

    [Fact]
    public void IsApplicable_ReturnsFalse_ForMaterialTransfer()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WeightPerPiece = 5m
        };

        Assert.False(manager.IsApplicable(entry));
    }

    [Fact]
    public void GetFinishedGoodRow_Throws_WhenNoFgRow()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 5m
        };
        entry.AddItem(_rmItemId, 10m, _srcWh, null);

        var ex = Assert.Throws<BusinessException>(() => manager.GetFinishedGoodRow(entry));
        Assert.Contains("must have exactly one finished good row", ex.Data["detail"]!.ToString());
    }

    [Fact]
    public void GetFinishedGoodRow_ReturnsRow_WhenExactlyOne()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 5m
        };
        entry.AddItem(_rmItemId, 10m, _srcWh, null);
        entry.AddItem(_fgItemId, 10m, null, _tgtWh, isFinishedItem: true);
        var fg = entry.Items.Last();

        var row = manager.GetFinishedGoodRow(entry);
        Assert.Equal(fg.Id, row.Id);
    }

    [Fact]
    public void GetPieces_Throws_WhenNotExactMultiple()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 4m
        };
        entry.AddItem(_fgItemId, 10m, null, _tgtWh, isFinishedItem: true);
        var fg = entry.Items.Last();

        var ex = Assert.Throws<BusinessException>(() => manager.GetPieces(entry, fg));
        Assert.Contains("must be an exact multiple", ex.Data["detail"]!.ToString());
    }

    [Fact]
    public void GetPieces_CalculatesCorrectPieces()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 2.5m
        };
        entry.AddItem(_fgItemId, 10m, null, _tgtWh, isFinishedItem: true);
        var fg = entry.Items.Last();

        var pieces = manager.GetPieces(entry, fg);
        Assert.Equal(4, pieces);
    }

    [Fact]
    public void GetParentBatches_Throws_WhenTotalCapacityLessThanPieces()
    {
        var manager = CreateManager();
        var parent1 = Guid.NewGuid();
        var parent2 = Guid.NewGuid();

        // parent1 has 5kg, parent2 has 4kg with weight_per_piece = 3kg
        // capacities: parent1 = 1 piece (floor(5/3)), parent2 = 1 piece (floor(4/3)) -> total 2 pieces
        // but 3 pieces required!
        var inputBatches = new List<(Guid BatchId, string BatchNo, decimal Qty)>
        {
            (parent1, "B1", 5m),
            (parent2, "B2", 4m)
        };

        var ex = Assert.Throws<BusinessException>(() =>
            manager.GetParentBatches(inputBatches, pieces: 3, weightPerPiece: 3m, entryNumber: "SE-001"));

        Assert.Contains("can supply only 2 whole pieces", ex.Data["detail"]!.ToString());
    }

    [Fact]
    public void GetParentBatches_ApportionsProportionally_CappingAtWholePieceCapacity()
    {
        var manager = CreateManager();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();

        // b1 has 12kg (capacity = 4 pieces of 3kg)
        // b2 has 6kg (capacity = 2 pieces of 3kg)
        // required: 6 pieces of 3kg
        var inputBatches = new List<(Guid BatchId, string BatchNo, decimal Qty)>
        {
            (b1, "BATCH-1", 12m),
            (b2, "BATCH-2", 6m)
        };

        var parents = manager.GetParentBatches(inputBatches, pieces: 6, weightPerPiece: 3m, entryNumber: "SE-001");

        Assert.Equal(6, parents.Count);
        Assert.Equal(4, parents.Count(p => p == b1));
        Assert.Equal(2, parents.Count(p => p == b2));
    }

    [Fact]
    public async Task MakeChildBatchesAsync_CreatesBatches_WithParentTraceability()
    {
        var manager = CreateManager();
        var fgItem = new Item(Guid.NewGuid(), _companyId, "FG-001", "Finished Good", ItemType.Goods)
        {
            HasBatchNo = true
        };
        _itemRepo.GetAsync(fgItem.Id).Returns(Task.FromResult(fgItem));

        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        entry.AddItem(fgItem.Id, 6m, null, _tgtWh, isFinishedItem: true);
        var fgRow = entry.Items.Last();

        var parent1 = Guid.NewGuid();
        var parent2 = Guid.NewGuid();
        var parentList = new List<Guid> { parent1, parent1, parent2 };

        var childBatches = await manager.MakeChildBatchesAsync(entry, fgRow, parentList);

        Assert.Equal(3, childBatches.Count);
        Assert.Equal(2, childBatches.Count(c => c.ParentBatchId == parent1));
        Assert.Equal(1, childBatches.Count(c => c.ParentBatchId == parent2));
        Assert.All(childBatches, c =>
        {
            Assert.Equal(fgItem.Id, c.ItemId);
            Assert.Equal("StockEntry", c.ReferenceDocType);
            Assert.Equal(entry.Id, c.ReferenceDocId);
            Assert.Equal(entry.PostingDate, c.ManufacturingDate);
            Assert.StartsWith("FG-001-BATCH-", c.BatchNo);
        });

        await _batchRepo.Received(3).InsertAsync(Arg.Any<Batch>(), autoSave: true);
    }

    [Fact]
    public async Task AttachBundleAsync_CreatesSubmittedBundle_WithEntries()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow)
        {
            WeightPerPiece = 2m
        };
        entry.AddItem(_fgItemId, 4m, null, _tgtWh, valuationRate: 25m, isFinishedItem: true);
        var fgRow = entry.Items.Last();

        var child1 = new Batch(Guid.NewGuid(), _fgItemId, "CHILD-1");
        var child2 = new Batch(Guid.NewGuid(), _fgItemId, "CHILD-2");

        var bundle = await manager.AttachBundleAsync(entry, fgRow, new List<Batch> { child1, child2 });

        Assert.NotNull(bundle);
        Assert.Equal(entry.CompanyId, bundle.CompanyId);
        Assert.Equal(_fgItemId, bundle.ItemId);
        Assert.Equal(_tgtWh, bundle.WarehouseId);
        Assert.Equal(BundleTransactionType.Inward, bundle.TypeOfTransaction);
        Assert.Equal("StockEntry", bundle.VoucherType);
        Assert.Equal(entry.Id, bundle.VoucherId);
        Assert.Equal(fgRow.Id, bundle.VoucherDetailId);
        Assert.Equal(DocumentStatus.Submitted, bundle.Status);
        Assert.Equal(2, bundle.Entries.Count);
        Assert.Equal(4m, bundle.TotalQty);
        Assert.Null(fgRow.BatchId); // BatchId cleared as bundle tracks individual pieces

        await _bundleRepo.Received(1).InsertAsync(bundle, autoSave: true);
    }

    [Fact]
    public async Task CancelBatchSplitAsync_CancelsBatchesAndBundles()
    {
        var manager = CreateManager();
        var entry = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.Repack, DateTime.UtcNow);

        var childBatch = new Batch(Guid.NewGuid(), _fgItemId, "C1")
        {
            ReferenceDocType = "StockEntry",
            ReferenceDocId = entry.Id
        };
        _batchRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Batch, bool>>>())
            .Returns(Task.FromResult(new List<Batch> { childBatch }));

        var bundle = new SerialAndBatchBundle(Guid.NewGuid(), _companyId, _fgItemId, _tgtWh,
            BundleTransactionType.Inward, "StockEntry", entry.Id, DateTime.UtcNow);
        _bundleRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, bool>>>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { bundle }));

        await manager.CancelBatchSplitAsync(entry);

        Assert.True(childBatch.IsCancelled);
        Assert.True(bundle.IsCancelled);
        await _batchRepo.Received(1).UpdateAsync(childBatch);
        await _bundleRepo.Received(1).UpdateAsync(bundle);
    }
}
