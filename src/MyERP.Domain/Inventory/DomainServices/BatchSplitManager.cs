using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service that implements the Batch Split operation (ERPNext PR #58530 / commit 0223223385).
/// Produces one child batch per finished piece from consumed parent batches, tracking parentage
/// and generating inward Serial and Batch Bundles.
/// </summary>
public class BatchSplitManager : DomainService
{
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;

    public BatchSplitManager(
        IRepository<Batch, Guid> batchRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository)
    {
        _batchRepository = batchRepository;
        _itemRepository = itemRepository;
        _bundleRepository = bundleRepository;
        _sleRepository = sleRepository;
    }

    private Guid NewGuid() => LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid();

    /// <summary>
    /// Checks if Batch Split is applicable for the given stock entry and optional job card.
    /// </summary>
    public bool IsApplicable(StockEntry entry, JobCard? jobCard = null)
    {
        if (entry.EntryType == StockEntryType.Repack)
        {
            return entry.WeightPerPiece > 0;
        }

        if (entry.EntryType == StockEntryType.Manufacture)
        {
            if (entry.WeightPerPiece > 0)
            {
                return true;
            }

            if (jobCard != null && jobCard.BatchSplit && jobCard.WeightPerPiece.HasValue && jobCard.WeightPerPiece.Value > 0)
            {
                entry.WeightPerPiece = jobCard.WeightPerPiece.Value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Identifies and validates the single finished good item line in the entry.
    /// </summary>
    public StockEntryItem GetFinishedGoodRow(StockEntry entry)
    {
        var fgRows = entry.Items
            .Where(row => (row.IsFinishedItem || (entry.EntryType == StockEntryType.Repack && row.TargetWarehouseId.HasValue && !row.SourceWarehouseId.HasValue))
                          && string.IsNullOrEmpty(row.SecondaryItemType))
            .ToList();

        if (fgRows.Count != 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The Batch Split entry {entry.EntryNumber} must have exactly one finished good row.");
        }

        return fgRows[0];
    }

    /// <summary>
    /// Calculates the number of whole pieces produced based on transfer quantity and WeightPerPiece.
    /// </summary>
    public int GetPieces(StockEntry entry, StockEntryItem fgRow)
    {
        if (entry.WeightPerPiece <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Please set the Weight Per Piece to split the produced quantity into batches in Stock Entry {entry.EntryNumber}.");
        }

        var pieces = fgRow.Quantity / entry.WeightPerPiece;
        if (pieces < 1 || Math.Abs(pieces - Math.Floor(pieces)) > 0.0001m)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The quantity {fgRow.Quantity} of the Batch Split item must be an exact multiple of the Weight Per Piece {entry.WeightPerPiece}.");
        }

        return (int)Math.Round(pieces);
    }

    /// <summary>
    /// Gathers consumed raw material batches across input rows.
    /// Requires exactly one batch-tracked raw material item type.
    /// </summary>
    public async Task<List<(Guid BatchId, string BatchNo, decimal Qty)>> GetInputBatchesAsync(StockEntry entry)
    {
        var inputRows = entry.Items
            .Where(row => !row.IsFinishedItem && row.SourceWarehouseId.HasValue && string.IsNullOrEmpty(row.SecondaryItemType))
            .ToList();

        if (!inputRows.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The Batch Split operation requires a batch tracked raw material to be consumed in Stock Entry {entry.EntryNumber}.");
        }

        var itemIds = inputRows.Select(r => r.ItemId).Distinct().ToList();
        if (itemIds.Count > 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The Batch Split entry {entry.EntryNumber} must consume exactly one batch tracked raw material, found {itemIds.Count}.");
        }

        var rmItemId = itemIds[0];
        var rmItem = await _itemRepository.FindAsync(rmItemId);
        if (rmItem == null || !rmItem.HasBatchNo)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The Batch Split operation requires a batch tracked raw material to be consumed in Stock Entry {entry.EntryNumber}.");
        }

        var batches = new List<(Guid BatchId, string BatchNo, decimal Qty)>();

        foreach (var row in inputRows)
        {
            if (row.BatchId.HasValue)
            {
                var batch = await _batchRepository.FindAsync(row.BatchId.Value);
                if (batch != null)
                {
                    batches.Add((batch.Id, batch.BatchNo, row.Quantity));
                }
            }
            else
            {
                // Check if a SerialAndBatchBundle is linked to this row
                var bundle = await _bundleRepository.FirstOrDefaultAsync(b =>
                    b.VoucherType == "StockEntry" &&
                    b.VoucherId == entry.Id &&
                    b.VoucherDetailId == row.Id &&
                    !b.IsCancelled);

                if (bundle != null && bundle.Entries.Any())
                {
                    foreach (var be in bundle.Entries.Where(e => e.BatchId.HasValue))
                    {
                        var b = await _batchRepository.FindAsync(be.BatchId!.Value);
                        batches.Add((be.BatchId!.Value, b?.BatchNo ?? string.Empty, Math.Abs(be.Qty)));
                    }
                }
                else if (row.SourceWarehouseId.HasValue)
                {
                    // Fallback to available batches in the source warehouse from active ledger entries
                    var available = await GetAvailableWarehouseBatchesAsync(entry.CompanyId, rmItemId, row.SourceWarehouseId.Value);
                    var remaining = row.Quantity;
                    foreach (var (bId, bNo, bQty) in available)
                    {
                        if (remaining <= 0) break;
                        var take = Math.Min(bQty, remaining);
                        batches.Add((bId, bNo, take));
                        remaining -= take;
                    }
                }
            }
        }

        if (!batches.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The Batch Split operation requires a batch tracked raw material to be consumed in Stock Entry {entry.EntryNumber}.");
        }

        return batches;
    }

    /// <summary>
    /// Apportions parent batches proportionally across pieces using the largest remainder method
    /// and capping at the whole piece capacity of each parent batch.
    /// </summary>
    public List<Guid> GetParentBatches(List<(Guid BatchId, string BatchNo, decimal Qty)> inputBatches, int pieces, decimal weightPerPiece, string? entryNumber)
    {
        var pool = inputBatches.Where(b => b.Qty > 0).ToList();
        var capacities = pool.Select(b => (int)Math.Floor(b.Qty / weightPerPiece)).ToList();
        var totalCapacity = capacities.Sum();

        if (totalCapacity < pieces)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The batches consumed in Stock Entry {entryNumber} can supply only {totalCapacity} whole pieces of {weightPerPiece} units each, but {pieces} pieces are required. Reduce the finished quantity or consume larger batches.");
        }

        var totalQty = pool.Sum(b => b.Qty);
        var shares = pool.Select(b => (double)(pieces * b.Qty / totalQty)).ToList();
        var counts = shares.Zip(capacities, (share, cap) => Math.Min((int)Math.Floor(share), cap)).ToList();

        while (counts.Sum() < pieces)
        {
            var eligibleIndices = Enumerable.Range(0, pool.Count).Where(i => counts[i] < capacities[i]).ToList();
            if (!eligibleIndices.Any()) break;

            var bestIndex = eligibleIndices
                .OrderBy(i => counts[i] - shares[i])
                .ThenBy(i => i)
                .First();
            counts[bestIndex]++;
        }

        var parents = new List<Guid>();
        for (var i = 0; i < pool.Count; i++)
        {
            for (var c = 0; c < counts[i]; c++)
            {
                parents.Add(pool[i].BatchId);
            }
        }

        return parents;
    }

    /// <summary>
    /// Mints child batches for the finished goods line with lineage pointing to parent batches.
    /// </summary>
    public async Task<List<Batch>> MakeChildBatchesAsync(StockEntry entry, StockEntryItem fgRow, List<Guid> parentBatchIds)
    {
        var fgItem = await _itemRepository.GetAsync(fgRow.ItemId);
        if (!fgItem.HasBatchNo)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"The item {fgItem.ItemCode} must have batch tracking enabled for the Batch Split operation.");
        }

        var childBatches = new List<Batch>();
        foreach (var parentBatchId in parentBatchIds)
        {
            var batchNo = $"{fgItem.ItemCode}-BATCH-{NewGuid().ToString("N")[..8].ToUpper()}";
            var child = new Batch(
                NewGuid(),
                fgItem.Id,
                batchNo,
                entry.TenantId)
            {
                ParentBatchId = parentBatchId,
                ReferenceDocType = "StockEntry",
                ReferenceDocId = entry.Id,
                ManufacturingDate = entry.PostingDate
            };

            await _batchRepository.InsertAsync(child, autoSave: true);
            childBatches.Add(child);
        }

        return childBatches;
    }

    /// <summary>
    /// Attaches an inward SerialAndBatchBundle to the finished good item row containing all child batches.
    /// </summary>
    public async Task<SerialAndBatchBundle> AttachBundleAsync(StockEntry entry, StockEntryItem fgRow, List<Batch> childBatches)
    {
        var bundle = new SerialAndBatchBundle(
            NewGuid(),
            entry.CompanyId,
            fgRow.ItemId,
            fgRow.TargetWarehouseId!.Value,
            BundleTransactionType.Inward,
            "StockEntry",
            entry.Id,
            entry.PostingDate,
            entry.TenantId)
        {
            VoucherDetailId = fgRow.Id,
            HasBatchNo = true
        };

        foreach (var child in childBatches)
        {
            bundle.AddEntry(new SerialAndBatchEntry(
                NewGuid(),
                bundle.Id,
                entry.WeightPerPiece,
                fgRow.ValuationRate ?? 0,
                batchId: child.Id,
                tenantId: entry.TenantId));
        }

        bundle.Submit();
        await _bundleRepository.InsertAsync(bundle, autoSave: true);

        fgRow.BatchId = null; // batches tracked via child batch bundle
        return bundle;
    }

    /// <summary>
    /// Executes the full Batch Split flow: validation, parent batch apportionment,
    /// child batch minting, and inward bundle attachment.
    /// </summary>
    public async Task ProcessBatchSplitAsync(StockEntry entry, JobCard? jobCard = null)
    {
        if (!IsApplicable(entry, jobCard))
        {
            return;
        }

        var fgRow = GetFinishedGoodRow(entry);
        var pieces = GetPieces(entry, fgRow);
        var inputBatches = await GetInputBatchesAsync(entry);
        var parentBatchIds = GetParentBatches(inputBatches, pieces, entry.WeightPerPiece, entry.EntryNumber);
        var childBatches = await MakeChildBatchesAsync(entry, fgRow, parentBatchIds);
        await AttachBundleAsync(entry, fgRow, childBatches);
    }

    /// <summary>
    /// Cleans up child batches and bundles created by this Stock Entry on cancellation.
    /// </summary>
    public async Task CancelBatchSplitAsync(StockEntry entry)
    {
        var childBatches = await _batchRepository.GetListAsync(b =>
            b.ReferenceDocType == "StockEntry" && b.ReferenceDocId == entry.Id);

        foreach (var batch in childBatches)
        {
            batch.Cancel();
            await _batchRepository.UpdateAsync(batch);
        }

        var bundles = await _bundleRepository.GetListAsync(b =>
            b.VoucherType == "StockEntry" && b.VoucherId == entry.Id);

        foreach (var bundle in bundles)
        {
            bundle.Cancel();
            await _bundleRepository.UpdateAsync(bundle);
        }
    }

    private async Task<List<(Guid BatchId, string BatchNo, decimal Qty)>> GetAvailableWarehouseBatchesAsync(
        Guid companyId, Guid itemId, Guid warehouseId)
    {
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var batchQuery = await _batchRepository.GetQueryableAsync();

        var balances = sleQuery
            .Where(s => s.CompanyId == companyId && s.ItemId == itemId && s.WarehouseId == warehouseId && s.BatchId.HasValue && !s.IsCancelled)
            .GroupBy(s => s.BatchId!.Value)
            .Select(g => new { BatchId = g.Key, Balance = g.Sum(s => s.QuantityChange) })
            .Where(x => x.Balance > 0)
            .ToList();

        var batchIds = balances.Select(b => b.BatchId).ToList();
        var batchDict = batchQuery
            .Where(b => batchIds.Contains(b.Id) && !b.IsDisabled && !b.IsCancelled)
            .ToDictionary(b => b.Id, b => b.BatchNo);

        return balances
            .Where(b => batchDict.ContainsKey(b.BatchId))
            .Select(b => (b.BatchId, batchDict[b.BatchId], b.Balance))
            .ToList();
    }
}
