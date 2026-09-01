using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for Stock Entry business rules.
/// Validates warehouse assignments, purpose-specific rules, and material transfer limits.
/// </summary>
public class StockEntryManager : DomainService
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly CompanyRestrictionValidationService _companyRestriction;

    public StockEntryManager(
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository,
        CompanyRestrictionValidationService companyRestriction)
    {
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
        _companyRestriction = companyRestriction;
    }

    /// <summary>
    /// Validates warehouse assignments based on Stock Entry purpose.
    /// Per DO-NOT: "Allow same-warehouse Material Transfer when all inventory dimensions are identical"
    /// </summary>
    public async Task ValidateWarehousesAsync(StockEntry entry)
    {
        var warehouseIds = entry.Items
            .SelectMany(i => new[] { i.SourceWarehouseId, i.TargetWarehouseId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        await _companyRestriction.ValidateTransactionCompanyAsync("StockEntry", entry.CompanyId, warehouseIds: warehouseIds);

        foreach (var item in entry.Items)
        {
            var isTransfer = entry.EntryType is StockEntryType.MaterialTransfer
                or StockEntryType.MaterialTransferForManufacture
                or StockEntryType.SendToWarehouse
                or StockEntryType.SendToSubcontractor
                or StockEntryType.SubcontractingDelivery
                or StockEntryType.SubcontractingReturn;

            if (isTransfer)
            {
                if (!item.SourceWarehouseId.HasValue)
                    throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                        .WithData("field", "SourceWarehouse");

                if (!item.TargetWarehouseId.HasValue)
                    throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                        .WithData("field", "TargetWarehouse");

                if (item.SourceWarehouseId == item.TargetWarehouseId)
                    throw new BusinessException(MyERPDomainErrorCodes.SameWarehouseTransfer);
            }

            var isReceipt = entry.EntryType is StockEntryType.MaterialReceipt
                or StockEntryType.ReceiveAtWarehouse
                or StockEntryType.Manufacture
                or StockEntryType.Adjustment;

            if (isReceipt && !item.TargetWarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "TargetWarehouse");

            var isIssue = entry.EntryType is StockEntryType.MaterialIssue;

            if (isIssue && !item.SourceWarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "SourceWarehouse");

            // Group warehouse validation
            if (item.SourceWarehouseId.HasValue)
            {
                var source = await _warehouseRepository.FindAsync(item.SourceWarehouseId.Value);
                if (source?.IsGroup == true)
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouseName", source.Name);
            }

            if (item.TargetWarehouseId.HasValue)
            {
                var target = await _warehouseRepository.FindAsync(item.TargetWarehouseId.Value);
                if (target?.IsGroup == true)
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouseName", target.Name);
            }
        }
    }

    /// <summary>
    /// Validates all items are active and stock-trackable for stock entries.
    /// Filters out service items (MaintainStock=false) with a warning.
    /// </summary>
    public async Task ValidateItemsAsync(StockEntry entry)
    {
        if (!entry.Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);

        foreach (var seItem in entry.Items)
        {
            var item = await _itemRepository.FindAsync(seItem.ItemId);
            if (item == null) continue;

            if (!item.IsActive)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ItemInactive)
                    .WithData("itemCode", item.ItemCode)
                    .WithData("itemName", item.ItemName);
            }
        }
    }

    /// <summary>
    /// Validates material transfer qty doesn't exceed the limit for manufacturing.
    /// Per DO-NOT: "Allow excess material transfer for manufacture beyond required_qty - already_transferred_qty"
    /// Exception: returns and "Material Transferred" backflush mode.
    /// </summary>
    public void ValidateTransferQty(decimal requiredQty, decimal transferredQty, decimal requestedQty,
        bool isReturn = false, bool isMaterialTransferredMode = false, int qtyPrecision = 6)
    {
        if (isReturn || isMaterialTransferredMode) return;

        var roundedRequest = Math.Round(requestedQty, qtyPrecision);
        var roundedPending = Math.Round(requiredQty - transferredQty, qtyPrecision);
        var allowed = roundedPending < 0 ? 0m : roundedPending;

        if (roundedRequest > allowed)
        {
            throw new BusinessException("MyERP:05030")
                .WithData("required", requiredQty)
                .WithData("transferred", transferredQty)
                .WithData("requested", requestedQty)
                .WithData("allowed", allowed);
        }
    }

    /// <summary>
    /// Calculates the effective finished goods completed quantity covered by transferred raw materials.
    /// Per ERPNext PR #58482 (commit b90e3d4656):
    /// When transferring materials against a Work Order or Job Card, caps fg_completed_qty
    /// to the minimum coverage across all required raw material lines.
    /// </summary>
    public decimal CalculateMaterialCoverage(
        decimal targetFgQty,
        IReadOnlyDictionary<Guid, decimal> requiredQuantities,
        IReadOnlyDictionary<Guid, decimal> transferredQuantities)
    {
        if (targetFgQty <= 0 || requiredQuantities.Count == 0)
            return 0;

        var coverages = new List<decimal>();
        foreach (var (itemId, reqQty) in requiredQuantities)
        {
            if (reqQty <= 0) continue;
            var transferred = transferredQuantities.TryGetValue(itemId, out var t) ? t : 0m;
            var proportion = transferred / reqQty;
            coverages.Add(proportion * targetFgQty);
        }

        if (coverages.Count == 0)
            return targetFgQty;

        var minCoverage = coverages.Min();
        return Math.Round(Math.Min(targetFgQty, Math.Max(0, minCoverage)), 4);
    }

    /// <summary>
    /// Calculates the incremental finished goods completed quantity covered by this transfer entry.
    /// Per ERPNext PR #58382 (commit 5fa68dd068):
    /// covered_by_entry = max(0, covered_after - covered_before).
    /// </summary>
    public decimal CalculateIncrementalMaterialCoverage(
        decimal targetFgQty,
        IReadOnlyDictionary<Guid, decimal> requiredQuantities,
        IReadOnlyDictionary<Guid, decimal> alreadyTransferredQuantities,
        IReadOnlyDictionary<Guid, decimal> entryTransferredQuantities)
    {
        var beforeCoverage = CalculateMaterialCoverage(targetFgQty, requiredQuantities, alreadyTransferredQuantities);

        var totalTransferred = new Dictionary<Guid, decimal>();
        foreach (var (id, req) in requiredQuantities)
        {
            var already = alreadyTransferredQuantities.TryGetValue(id, out var a) ? a : 0m;
            var entry = entryTransferredQuantities.TryGetValue(id, out var e) ? e : 0m;
            totalTransferred[id] = already + entry;
        }

        var afterCoverage = CalculateMaterialCoverage(targetFgQty, requiredQuantities, totalTransferred);
        return Math.Max(0, afterCoverage - beforeCoverage);
    }

    /// <summary>
    /// Validates Repack Stock Entry rules.
    /// Per ERPNext: Repack converts items from one form to another (split/merge/repackage).
    /// 
    /// Rules:
    /// 1. Must have at least one outgoing (source) and one incoming (target/FG) item
    /// 2. Items with only TargetWarehouse are FG (marked IsFinishedItem=true)
    /// 3. Items with only SourceWarehouse are consumed materials
    /// 4. Multiple FG items require SetBasicRateManually=true on each FG
    /// 5. Both source and target warehouse allowed on same item (transfer+repack)
    /// </summary>
    public void ValidateRepackItems(StockEntry entry)
    {
        if (entry.EntryType != StockEntryType.Repack) return;

        var outgoingItems = entry.Items.Where(i => i.SourceWarehouseId.HasValue).ToList();
        var incomingItems = entry.Items.Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue).ToList();

        if (!outgoingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.RepackMissingItems)
                .WithData("reason", "Repack requires at least one outgoing (source) item");
        }

        if (!incomingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.RepackMissingItems)
                .WithData("reason", "Repack requires at least one incoming (target/finished) item");
        }

        // Multiple FG items require manual rate setting
        if (incomingItems.Count > 1)
        {
            foreach (var fg in incomingItems)
            {
                if (!fg.SetBasicRateManually)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.RepackMultiFgManualRate)
                        .WithData("reason", "Multiple finished goods in Repack require SetBasicRateManually=true on each item");
                }
            }
        }
    }

    /// <summary>
    /// Validates Manufacture Stock Entry rules.
    /// Per ERPNext stock_entry.py:
    /// 1. Only ONE unique finished item is allowed per Manufacture entry.
    /// 2. For Quantity (Manufactured Qty / FgCompletedQty) is mandatory when linked to a Work Order (PR #58005).
    /// </summary>
    public void ValidateManufactureItems(StockEntry entry, bool trackSemiFinishedGoods = false)
    {
        if (entry.EntryType != StockEntryType.Manufacture && entry.EntryType != StockEntryType.MaterialConsumptionForManufacture) return;

        if (entry.EntryType == StockEntryType.Manufacture)
        {
            var distinctFgItemCount = entry.Items
                .Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue)
                .Select(i => i.ItemId)
                .Distinct()
                .Count();

            if (distinctFgItemCount > 1)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ManufactureMultiFgItemsNotAllowed);
            }
        }

        // Per ERPNext PR #58005: mandatory manufactured qty check for manufacture entries
        if (entry.WorkOrderId.HasValue && !trackSemiFinishedGoods && entry.FgCompletedQty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "For Quantity (Manufactured Qty) is mandatory for Work Order manufacture stock entries.");
        }
    }

    /// <summary>
    /// Blocks another manufacture entry once existing entries already cover the work order qty plus allowance.
    /// Per ERPNext PR #58004 / PR #58005 (commits 22fa520500, 492ee05727):
    /// Gated to work orders without track_semi_finished_goods.
    /// </summary>
    public async Task ValidateDuplicateManufactureEntryAsync(
        StockEntry entry,
        IRepository<Manufacturing.Entities.WorkOrder, Guid> woRepository,
        IRepository<StockEntry, Guid> stockEntryRepository,
        decimal overproductionPercentage = 0m)
    {
        if (entry.EntryType != StockEntryType.Manufacture || !entry.WorkOrderId.HasValue)
            return;

        var wo = await woRepository.FindAsync(entry.WorkOrderId.Value);
        if (wo == null || wo.TrackSemiFinishedGoods)
            return;

        var seQuery = await stockEntryRepository.GetQueryableAsync();
        var otherEntries = seQuery
            .Where(se => se.WorkOrderId == entry.WorkOrderId.Value
                      && se.EntryType == StockEntryType.Manufacture
                      && se.Status != Core.DocumentStatus.Cancelled
                      && se.Id != entry.Id)
            .ToList();

        if (!otherEntries.Any())
            return;

        var alreadyEnteredFgQty = otherEntries
            .SelectMany(se => se.Items)
            .Where(i => i.ItemId == wo.ItemId && i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue)
            .Sum(i => i.Quantity);

        var allowedQty = wo.Quantity + (overproductionPercentage / 100m * wo.Quantity);

        if (alreadyEnteredFgQty >= allowedQty)
        {
            var otherEntryNumbers = string.Join(", ", otherEntries.Select(e => e.EntryNumber));
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("detail", $"Stock Entries already created for Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}: {otherEntryNumbers}");
        }
    }

    /// <summary>
    /// Validates manufactured quantity is set (> 0) on manufacture stock entries.
    /// Per ERPNext PR #58005 (commit b6ca708d9f): Without fg_completed_qty, submit
    /// never updates or validates the work order's produced qty.
    /// </summary>
    public void ValidateManufacturedQty(StockEntry entry, Manufacturing.Entities.WorkOrder? wo = null)
    {
        if (entry.EntryType != StockEntryType.Manufacture)
            return;

        if (!entry.WorkOrderId.HasValue)
            return;

        if (wo != null && wo.TrackSemiFinishedGoods)
            return;

        if (entry.FgCompletedQty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "For Quantity (Manufactured Qty) is mandatory for manufacture stock entries.");
        }
    }

    /// <summary>
    /// Calculates valuation rate for Repack FG items.
    /// Single FG: rate = total_outgoing_cost / fg_qty
    /// Multiple FGs: each must have rate set manually (validated separately).
    /// </summary>
    public decimal CalculateRepackFgRate(IReadOnlyList<StockEntryItem> items, decimal fgQty)
    {
        var totalOutgoingCost = items
            .Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem)
            .Sum(i => i.Quantity * (i.ValuationRate ?? 0));

        if (fgQty <= 0) return 0;
        return Math.Round(totalOutgoingCost / fgQty, 4);
    }

    /// <summary>
    /// Calculates basic valuation rate for a manufactured FG item.
    /// Per ERPNext PR #57334: when inputs are consumed at zero cost (e.g. free raw materials),
    /// rate remains zero (plus additional operating cost) and must not fall back to BOM or standard rates.
    /// </summary>
    public decimal CalculateManufactureFgRate(
        IReadOnlyList<StockEntryItem> items,
        decimal fgQty,
        decimal additionalOperatingCost = 0m,
        decimal? bomEstimatedCost = null)
    {
        if (fgQty <= 0) return 0m;

        var rawMaterialItems = items.Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem).ToList();
        var hasConsumptionBasis = rawMaterialItems.Count > 0;

        var outgoingCost = rawMaterialItems.Sum(i => i.Quantity * (i.ValuationRate ?? 0m));

        if (!hasConsumptionBasis && bomEstimatedCost.HasValue)
        {
            outgoingCost = bomEstimatedCost.Value;
        }

        var totalFgCost = outgoingCost + additionalOperatingCost;
        return Math.Round(totalFgCost / fgQty, 4);
    }

    /// <summary>
    /// Validates Disassemble Stock Entry rules.
    /// Per ERPNext: Disassemble reverses a Manufacture entry — breaks FG back into components.
    /// 
    /// Rules:
    /// 1. Must have a source stock entry (the original Manufacture entry)
    /// 2. FG consumption qty cannot exceed source manufacture FG qty
    /// 3. Material output qty must follow scale factor: source_row_qty × (disassemble_qty / source_fg_qty)
    /// 4. Cannot disassemble from a different Work Order's stock entry
    /// </summary>
    public void ValidateDisassembleItems(StockEntry entry, StockEntry? sourceEntry)
    {
        if (entry.EntryType != StockEntryType.Disassemble) return;

        if (sourceEntry == null && entry.SourceStockEntryId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.DisassembleSourceNotFound)
                .WithData("reason", "Source manufacture stock entry not found");
        }

        if (sourceEntry != null)
        {
            // Cross-WO guard
            if (entry.WorkOrderId.HasValue && sourceEntry.WorkOrderId.HasValue
                && entry.WorkOrderId != sourceEntry.WorkOrderId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleCrossWorkOrder)
                    .WithData("reason", "Cannot disassemble from a different Work Order's stock entry");
            }

            // FG consumption must not exceed source FG qty
            if (entry.FgCompletedQty > sourceEntry.FgCompletedQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleQtyExceedsSource)
                    .WithData("disassembleQty", entry.FgCompletedQty)
                    .WithData("sourceQty", sourceEntry.FgCompletedQty)
                    .WithData("reason", "Disassemble qty exceeds source manufacture FG qty");
            }
        }
    }

    /// <summary>
    /// Validates scale factor for Disassemble items against source entry.
    /// Per PR #57710: quantities are aggregated in stock UOM (using StockQty) to avoid
    /// cross-UOM mismatches when the same item appears across manufacture entries in different UOMs.
    /// Every non-FG row's stock qty must equal: source_stock_qty × (disassemble_qty / source_fg_qty).
    /// Tolerance: 1/(10^precision) for float rounding only.
    /// </summary>
    public void ValidateDisassembleScaleFactor(
        IReadOnlyList<StockEntryItem> disassemblyItems,
        IReadOnlyList<StockEntryItem> sourceItems,
        decimal disassembleQty,
        decimal sourceFgQty,
        int precision = 4)
    {
        if (sourceFgQty <= 0) return;

        var scaleFactor = disassembleQty / sourceFgQty;
        var tolerance = 1m / (decimal)Math.Pow(10, precision);

        foreach (var item in disassemblyItems.Where(i => !i.IsFinishedItem))
        {
            // Match by source detail ID first, then by item_code
            var sourceItem = item.SourceStockEntryDetailId.HasValue
                ? sourceItems.FirstOrDefault(s => s.Id == item.SourceStockEntryDetailId.Value)
                : sourceItems.FirstOrDefault(s => s.ItemId == item.ItemId && !s.IsFinishedItem);

            if (sourceItem == null) continue;

            // Per PR #57710: use StockQty for comparison (avoids cross-UOM confusion)
            var expectedStockQty = Math.Round(sourceItem.StockQty * scaleFactor, precision);
            var diff = Math.Abs(item.StockQty - expectedStockQty);

            if (diff > tolerance)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleScaleFactorMismatch)
                    .WithData("itemId", item.ItemId)
                    .WithData("expectedQty", expectedStockQty)
                    .WithData("actualQty", item.StockQty)
                    .WithData("scaleFactor", scaleFactor);
            }
        }
    }

    /// <summary>
    /// Validates Finished Good Conversion Repack Stock Entry (upstream PR #58479).
    /// Converts produced finished goods of a Work Order into alternative finished goods.
    /// </summary>
    public async Task ValidateFgConversionAsync(
        StockEntry entry,
        IRepository<MyERP.Manufacturing.Entities.WorkOrder, Guid> woRepo,
        IRepository<ItemAlternative, Guid> altRepo,
        IRepository<StockEntry, Guid> seRepo)
    {
        if (!entry.IsFgConversion) return;

        if (entry.EntryType != StockEntryType.Repack)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "A finished good conversion entry must have the purpose 'Repack'.");
        }

        if (!entry.WorkOrderId.HasValue)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Work Order is mandatory for a finished good conversion entry.");
        }

        var wo = await woRepo.FindAsync(entry.WorkOrderId.Value);
        if (wo == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Linked Work Order not found.");
        }

        var productionItem = wo.ItemId;

        // Fetch valid alternative items for this production item (both 1-way and 2-way)
        var altQuery = await altRepo.GetQueryableAsync();
        var directAlts = altQuery.Where(a => a.ItemId == productionItem).Select(a => a.AlternativeItemId).ToList();
        var twoWayAlts = altQuery.Where(a => a.AlternativeItemId == productionItem && a.TwoWay).Select(a => a.ItemId).ToList();
        var allowedAlternatives = directAlts.Concat(twoWayAlts).ToHashSet();

        if (!allowedAlternatives.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "No Item Alternative records found for the production item.");
        }

        // Validate consumed quantity of production item
        var consumedQty = entry.Items
            .Where(i => i.SourceWarehouseId.HasValue && i.ItemId == productionItem)
            .Sum(i => i.Quantity);

        if (consumedQty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "A finished good conversion entry must consume the production item of the Work Order.");
        }

        // Validate target alternative items and output qty
        var outputItems = entry.Items
            .Where(i => (i.IsFinishedItem || i.TargetWarehouseId.HasValue) && i.ItemId != productionItem)
            .ToList();

        foreach (var outItem in outputItems)
        {
            if (!allowedAlternatives.Contains(outItem.ItemId))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Item is not an alternative item of the production item.");
            }
        }

        var outputQty = outputItems.Sum(i => i.Quantity);
        if (Math.Round(outputQty, 4) != Math.Round(consumedQty, 4))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Total output quantity of alternative finished goods ({outputQty}) must equal converted quantity ({consumedQty}).");
        }

        // Available produced quantity check against WO (produced_qty - already_converted_qty)
        var seQuery = await seRepo.GetQueryableAsync();
        var alreadyConvertedQty = seQuery
            .Where(s => s.WorkOrderId == wo.Id && s.IsFgConversion && s.Id != entry.Id && s.Status == MyERP.Core.DocumentStatus.Posted)
            .SelectMany(s => s.Items)
            .Where(i => i.ItemId == productionItem && i.SourceWarehouseId.HasValue)
            .Sum(i => (decimal?)i.Quantity) ?? 0m;

        var availableQty = wo.ProducedQuantity - alreadyConvertedQty;
        if (consumedQty > availableQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Quantity ({consumedQty}) to convert cannot exceed available produced quantity ({availableQty}) against Work Order {wo.WorkOrderNumber}.");
        }
    }

    /// <summary>
    /// Validates Batch Split Repack Stock Entry (upstream PR #58530).
    /// Splits consumed batch inventory into one child batch per finished piece.
    /// </summary>
    public void ValidateBatchSplit(StockEntry entry)
    {
        if (entry.WeightPerPiece <= 0) return;

        if (entry.EntryType != StockEntryType.Repack)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Batch Split operation is only supported for 'Repack' Stock Entry purpose.");
        }

        var sourceItems = entry.Items.Where(i => i.SourceWarehouseId.HasValue).ToList();
        var uniqueSourceItemIds = sourceItems.Select(i => i.ItemId).Distinct().ToList();

        if (uniqueSourceItemIds.Count > 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Batch Split repack requires exactly one raw material item type to be consumed.");
        }

        var outputItems = entry.Items.Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue).ToList();
        var totalOutputQty = outputItems.Sum(i => i.Quantity);

        if (totalOutputQty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Batch Split repack must have finished/incoming goods.");
        }

        var totalPieces = totalOutputQty / entry.WeightPerPiece;
        if (Math.Abs(totalPieces - Math.Round(totalPieces)) > 0.0001m)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Total output quantity ({totalOutputQty}) must be an exact multiple of Weight Per Piece ({entry.WeightPerPiece}).");
        }

        // Validate each consumed line divides into whole pieces
        foreach (var source in sourceItems)
        {
            var pieces = source.Quantity / entry.WeightPerPiece;
            if (Math.Abs(pieces - Math.Round(pieces)) > 0.0001m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Consumed quantity ({source.Quantity}) must divide evenly into whole pieces of weight {entry.WeightPerPiece}.");
            }
        }
    }
}
