using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
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
    private readonly IRepository<Company, Guid>? _companyRepository;

    protected StockEntryManager()
    {
        _warehouseRepository = null!;
        _itemRepository = null!;
        _companyRestriction = null!;
    }

    public StockEntryManager(
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository,
        CompanyRestrictionValidationService companyRestriction,
        IRepository<Company, Guid>? companyRepository = null)
    {
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
        _companyRestriction = companyRestriction;
        _companyRepository = companyRepository;
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
        var itemIds = entry.Items
            .Select(i => i.ItemId)
            .Distinct()
            .ToArray();
        if (_companyRestriction != null)
        {
            await _companyRestriction.ValidateTransactionCompanyAsync(
                "StockEntry", entry.CompanyId,
                itemIds: itemIds,
                warehouseIds: warehouseIds);
        }

        // Per ERPNext PR #60189 (commit 5b1bade8a2): exclude the company's sample retention warehouse from source warehouses
        var compRepo = _companyRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Company, Guid>>();
        if (compRepo != null)
        {
            var company = await compRepo.FindAsync(entry.CompanyId);
            if (company?.SampleRetentionWarehouseId != null)
            {
                var sampleWarehouseId = company.SampleRetentionWarehouseId.Value;
                foreach (var item in entry.Items)
                {
                    if (item.SourceWarehouseId == sampleWarehouseId)
                    {
                        var itemName = (await _itemRepository.FindAsync(item.ItemId))?.ItemName ?? item.ItemId.ToString();
                        throw new BusinessException(MyERPDomainErrorCodes.CannotConsumeFromSampleRetentionWarehouse)
                            .WithData("itemName", itemName)
                            .WithData("warehouseId", sampleWarehouseId);
                    }
                }
            }
        }

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
                or StockEntryType.Adjustment;

            if (isReceipt && !item.TargetWarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "TargetWarehouse");

            // For Manufacture and Repack entries: finished goods and secondary items require TargetWarehouse; raw materials require SourceWarehouse (ERPNext PR #54849, PR #54866, PR #60421)
            if (entry.EntryType is StockEntryType.Manufacture)
            {
                if (item.IsFinishedItem || !string.IsNullOrWhiteSpace(item.SecondaryItemType))
                {
                    if (item.SourceWarehouseId.HasValue) item.SourceWarehouseId = null;
                    if (!item.TargetWarehouseId.HasValue)
                        throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                            .WithData("field", "TargetWarehouse");
                }
                else
                {
                    // Output row that is neither finished good nor secondary item (ERPNext PR #60421 / commit 00ca106bf5)
                    if (item.TargetWarehouseId.HasValue && !item.SourceWarehouseId.HasValue)
                    {
                        var rowIdx = entry.Items.ToList().IndexOf(item) + 1;
                        var itemName = _itemRepository != null ? (await _itemRepository.FindAsync(item.ItemId))?.ItemName : null;
                        itemName ??= item.ItemId.ToString();
                        throw new BusinessException(MyERPDomainErrorCodes.ManufactureUnvaluedOutput)
                            .WithData("row", rowIdx)
                            .WithData("item", itemName);
                    }

                    if (item.TargetWarehouseId.HasValue) item.TargetWarehouseId = null;
                    if (!item.SourceWarehouseId.HasValue)
                        throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                            .WithData("field", "SourceWarehouse");
                }
            }
            else if (entry.EntryType is StockEntryType.Repack)
            {
                if (item.IsFinishedItem)
                {
                    if (item.SourceWarehouseId.HasValue) item.SourceWarehouseId = null;
                    if (!item.TargetWarehouseId.HasValue)
                        throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                            .WithData("field", "TargetWarehouse");
                }
            }
            else if (entry.EntryType is StockEntryType.Disassemble)
            {
                if (item.IsFinishedItem)
                {
                    if (item.TargetWarehouseId.HasValue) item.TargetWarehouseId = null;
                    if (!item.SourceWarehouseId.HasValue)
                        throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                            .WithData("field", "SourceWarehouse");
                }
                else
                {
                    if (item.SourceWarehouseId.HasValue) item.SourceWarehouseId = null;
                    if (!item.TargetWarehouseId.HasValue)
                        throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                            .WithData("field", "TargetWarehouse");
                }
            }

            var isIssue = entry.EntryType is StockEntryType.MaterialIssue;

            if (isIssue && !item.SourceWarehouseId.HasValue)
                throw new BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                    .WithData("field", "SourceWarehouse");

            // Group warehouse validation
            if (item.SourceWarehouseId.HasValue && _warehouseRepository != null)
            {
                var source = await _warehouseRepository.FindAsync(item.SourceWarehouseId.Value);
                if (source?.IsGroup == true)
                    throw new BusinessException(MyERPDomainErrorCodes.GroupWarehouseCannotReceiveStock)
                        .WithData("warehouseName", source.Name);
            }

            if (item.TargetWarehouseId.HasValue && _warehouseRepository != null)
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
        bool isReturn = false, bool isMaterialTransferredMode = false, decimal extraMaterialPercentage = 0m, int qtyPrecision = 6,
        decimal? consumedQty = null)
    {
        if (isReturn)
        {
            // Per ERPNext PR #59616 / commit 73cb8718a9: component return cannot exceed unconsumed materials in WIP
            if (consumedQty.HasValue)
            {
                var returnableQty = Math.Max(0, transferredQty - consumedQty.Value);
                var roundedReturnRequest = Math.Round(requestedQty, qtyPrecision);
                var roundedReturnable = Math.Round(returnableQty, qtyPrecision);
                if (roundedReturnRequest > roundedReturnable)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Return quantity {requestedQty} cannot exceed available unconsumed quantity {returnableQty}.");
                }
            }
            return;
        }

        if (isMaterialTransferredMode) return;

        var extraQty = extraMaterialPercentage > 0 ? (requiredQty * extraMaterialPercentage / 100m) : 0m;
        var maxAllowedTotal = requiredQty + extraQty;

        var roundedRequest = Math.Round(requestedQty, qtyPrecision);
        var roundedPending = Math.Round(maxAllowedTotal - transferredQty, qtyPrecision);
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
    /// Validates that Stock Entry rows linked to Material Request rows match the MR Item.
    /// Per ERPNext PR #59679 (commit bd4bf5ee25): unlinked rows must not abort validation of subsequent rows.
    /// </summary>
    public async Task ValidateMaterialRequestItemsAsync(
        StockEntry entry,
        IRepository<MaterialRequest, Guid> mrRepository)
    {
        var mrLinkedRows = entry.Items.Where(i => i.MaterialRequestItemId.HasValue).ToList();
        if (!mrLinkedRows.Any())
            return;

        var mrItemIds = mrLinkedRows.Select(i => i.MaterialRequestItemId!.Value).Distinct().ToList();
        var mrQuery = await mrRepository.GetQueryableAsync();
        var affectedMRs = mrQuery.Where(mr => mr.Items.Any(i => mrItemIds.Contains(i.Id))).ToList();
        var mrItemMap = affectedMRs.SelectMany(mr => mr.Items).ToDictionary(i => i.Id);

        for (int idx = 0; idx < entry.Items.Count; idx++)
        {
            var row = entry.Items[idx];
            if (!row.MaterialRequestItemId.HasValue)
                continue; // ERPNext PR #59679 / commit bd4bf5ee25: skip unlinked row, do not return!

            if (!mrItemMap.TryGetValue(row.MaterialRequestItemId.Value, out var mrItem))
            {
                throw new BusinessException(MyERPDomainErrorCodes.MaterialRequestItemNotFound)
                    .WithData("materialRequestItemId", row.MaterialRequestItemId.Value);
            }

            if (mrItem.ItemId != row.ItemId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Item for row {idx + 1} does not match Material Request.");
            }
        }
    }

    /// <summary>
    /// Validates Work Order status for stock entries.
    /// Refuses new MaterialTransferForManufacture, Manufacture, and MaterialConsumptionForManufacture
    /// stock entries against a Closed Work Order.
    /// Per ERPNext PR #60647 (commit f9d019aa90) and PR #65859 (commit 55074816c9).
    /// Also refuses transactions against Stopped Work Orders.
    /// </summary>
    public void ValidateWorkOrderStatus(StockEntry entry, WorkOrder? wo)
    {
        if (!entry.WorkOrderId.HasValue || wo == null)
            return;

        if (wo.Status == WorkOrderStatus.Stopped)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", $"Transaction not allowed against stopped Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}");
        }

        if (wo.Status == WorkOrderStatus.Closed &&
            !(entry.EntryType == StockEntryType.MaterialTransferForManufacture && entry.IsReturn) &&
            entry.EntryType is StockEntryType.MaterialTransferForManufacture
                or StockEntryType.Manufacture
                or StockEntryType.MaterialConsumptionForManufacture)
        {
            throw new BusinessException(MyERPDomainErrorCodes.StockEntryAgainstClosedWorkOrder)
                .WithData("workOrder", wo.WorkOrderNumber ?? wo.Id.ToString())
                .WithData("detail", $"Transaction not allowed against closed Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}");
        }
    }

    /// <summary>
    /// Validates that component returns against a Work Order are allowed only after the Work Order is Completed or Closed.
    /// Per ERPNext PR #59617 (commit b4d6595f94).
    /// </summary>
    public void ValidateWorkOrderStatusForReturn(StockEntry entry, WorkOrder? wo)
    {
        if (!entry.IsReturn || !entry.WorkOrderId.HasValue || wo == null)
            return;

        if (wo.Status is not (WorkOrderStatus.Completed or WorkOrderStatus.Closed))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", $"Components can be returned only after Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()} is Completed or Closed");
        }
    }

    /// <summary>
    /// Validates component return quantities against unconsumed transferred materials in a Work Order.
    /// Per ERPNext PR #59616 and PR #60421 (commit 00ca106bf5): aggregates returned qty across rows by item
    /// and ensures it does not exceed unconsumed transferred quantity.
    /// </summary>
    public void ValidateWorkOrderReturnItems(StockEntry entry, WorkOrder? wo)
    {
        if (!entry.IsReturn || !entry.WorkOrderId.HasValue || wo == null)
            return;

        var returnedByItem = entry.Items
            .Where(i => i.SourceWarehouseId.HasValue)
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m)));

        foreach (var (itemId, returnedQty) in returnedByItem)
        {
            var matchingItems = wo.RequiredItems.Where(ri => ri.ItemId == itemId).ToList();
            var returnable = matchingItems.Sum(ri => Math.Max(0m, ri.TransferredQuantity - ri.ConsumedQuantity));
            var existingWoItem = matchingItems.FirstOrDefault();

            if (returnedQty > returnable)
            {
                var itemName = existingWoItem?.ItemName ?? itemId.ToString();
                throw new BusinessException(MyERPDomainErrorCodes.WorkOrderExcessReturn)
                    .WithData("item", itemName)
                    .WithData("returned", returnedQty)
                    .WithData("workOrder", wo.WorkOrderNumber ?? wo.Id.ToString())
                    .WithData("unconsumed", returnable);
            }
        }
    }

    /// <summary>
    /// Checks whether <paramref name="alternativeItemId"/> is a registered alternative for <paramref name="originalItemId"/>
    /// (either direct 1-way or reverse 2-way).
    /// Maps to ERPNext stock/doctype/item_alternative/item_alternative.py: is_alternative_item (PR #60130 / commit 3b014ad602).
    /// </summary>
    public static bool IsAlternativeItem(
        Guid originalItemId,
        Guid alternativeItemId,
        IEnumerable<ItemAlternative> alternatives)
    {
        if (originalItemId == Guid.Empty || alternativeItemId == Guid.Empty || alternatives == null)
            return false;

        return alternatives.Any(a =>
            (a.ItemId == originalItemId && a.AlternativeItemId == alternativeItemId) ||
            (a.ItemId == alternativeItemId && a.AlternativeItemId == originalItemId && a.TwoWay));
    }

    /// <summary>
    /// Validates alternative item substitutions on Work Order Stock Entries
    /// (MaterialTransferForManufacture, Manufacture, MaterialConsumptionForManufacture).
    /// If an alternative item pair was already transferred in a submitted non-return MaterialTransferForManufacture
    /// entry for the Work Order, consuming or returning it is permitted even if alternative approval was later removed.
    /// Maps to ERPNext stock/doctype/stock_entry/stock_entry.py: validate_work_order_alternative_items (PR #60130 / commit 3b014ad602).
    /// </summary>
    public void ValidateWorkOrderAlternativeItems(
        StockEntry entry,
        WorkOrder? wo,
        IEnumerable<ItemAlternative> alternatives,
        IEnumerable<StockEntry>? submittedWorkOrderEntries = null,
        IReadOnlyDictionary<Guid, string>? itemCodeMap = null)
    {
        if (!entry.WorkOrderId.HasValue || wo == null)
            return;

        if (entry.EntryType is not (
            StockEntryType.MaterialTransferForManufacture or
            StockEntryType.Manufacture or
            StockEntryType.MaterialConsumptionForManufacture))
        {
            return;
        }

        var transferredAlternativePairs = new HashSet<(Guid OriginalItemId, Guid AlternativeItemId)>();
        if (submittedWorkOrderEntries != null)
        {
            foreach (var se in submittedWorkOrderEntries)
            {
                if (se.Id == entry.Id ||
                    se.WorkOrderId != wo.Id ||
                    se.EntryType != StockEntryType.MaterialTransferForManufacture ||
                    se.IsReturn ||
                    se.Status is not (Core.DocumentStatus.Submitted or Core.DocumentStatus.Posted))
                {
                    continue;
                }

                foreach (var item in se.Items)
                {
                    if (item.OriginalItemId.HasValue && item.OriginalItemId.Value != item.ItemId)
                    {
                        transferredAlternativePairs.Add((item.OriginalItemId.Value, item.ItemId));
                    }
                }
            }
        }

        var canUseAlreadyTransferred =
            entry.EntryType is StockEntryType.Manufacture or StockEntryType.MaterialConsumptionForManufacture ||
            (entry.EntryType == StockEntryType.MaterialTransferForManufacture && entry.IsReturn);

        var altList = alternatives?.ToList() ?? new List<ItemAlternative>();

        for (int idx = 0; idx < entry.Items.Count; idx++)
        {
            var row = entry.Items[idx];
            if (!row.OriginalItemId.HasValue || row.OriginalItemId.Value == row.ItemId)
                continue;

            var pair = (row.OriginalItemId.Value, row.ItemId);
            if (canUseAlreadyTransferred && transferredAlternativePairs.Contains(pair))
                continue;

            if (!wo.AllowAlternativeItem)
            {
                throw new BusinessException(MyERPDomainErrorCodes.WorkOrderDoesNotAllowAlternativeItems)
                    .WithData("row", idx + 1)
                    .WithData("workOrder", wo.WorkOrderNumber ?? wo.Id.ToString());
            }

            if (!IsAlternativeItem(row.OriginalItemId.Value, row.ItemId, altList))
            {
                var altCode = itemCodeMap != null && itemCodeMap.TryGetValue(row.ItemId, out var ac) ? ac : row.ItemId.ToString();
                var origCode = itemCodeMap != null && itemCodeMap.TryGetValue(row.OriginalItemId.Value, out var oc) ? oc : row.OriginalItemId.Value.ToString();

                throw new BusinessException(MyERPDomainErrorCodes.ItemIsNotAlternativeOfOriginal)
                    .WithData("row", idx + 1)
                    .WithData("item", altCode)
                    .WithData("originalItem", origCode);
            }
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

        foreach (var fg in incomingItems)
        {
            fg.IsFinishedItem = true;
        }

        // Multiple distinct FG items require manual rate setting on every FG row (ERPNext e2ba9cb546, 4cbe5b28e3)
        var distinctFgCount = incomingItems.Select(i => i.ItemId).Distinct().Count();
        if (distinctFgCount > 1)
        {
            var rows = incomingItems.Select(fg => (fg.ItemId, fg.ItemId.ToString(), fg.IsFinishedItem, fg.SetBasicRateManually));
            ValidateRepackFinishedGoodManualRates(rows);
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
            // Per ERPNext PR #61950 (commit 28c89dba2a): ensure at least one finished good row exists
            var hasFgItem = entry.Items.Any(i => i.IsFinishedItem || (i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue && string.IsNullOrWhiteSpace(i.SecondaryItemType)));
            if (entry.Items.Any() && !hasFgItem)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "There must be at least 1 Finished Good in a Manufacture stock entry.");
            }

            // Per ERPNext PR #60421 / commit 00ca106bf5:
            // Output rows received into target warehouse must be either finished good or secondary item
            var hasExplicitFg = entry.Items.Any(i => i.IsFinishedItem);
            foreach (var item in entry.Items)
            {
                if (item.TargetWarehouseId.HasValue && !item.SourceWarehouseId.HasValue)
                {
                    if (hasExplicitFg && !item.IsFinishedItem && string.IsNullOrWhiteSpace(item.SecondaryItemType))
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                            .WithData("detail", $"Item {item.ItemId} is received but is neither the finished good nor a secondary item. Set its Secondary Item Type or remove the row.");
                    }
                }
            }

            var distinctFgItemCount = entry.Items
                .Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue && (i.IsFinishedItem || string.IsNullOrWhiteSpace(i.SecondaryItemType)))
                .Select(i => i.ItemId)
                .Distinct()
                .Count();

            if (distinctFgItemCount > 1)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ManufactureMultiFgItemsNotAllowed);
            }

            // Per ERPNext PR #59881 (commit 8148dd486b / commit 4029c1198a):
            // Reject manual costs above the consumed raw material cost when there are remaining FG rows
            var rawMaterialCost = entry.Items
                .Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem)
                .Sum(i => i.Quantity * (i.ValuationRate ?? 0m));

            var manualFgCost = entry.Items
                .Where(i => (i.IsFinishedItem || !i.SourceWarehouseId.HasValue)
                    && i.TargetWarehouseId.HasValue
                    && i.SetBasicRateManually
                    && !i.AllowZeroValuationRate)
                .Sum(i => i.Quantity * (i.ValuationRate ?? 0m));

            var hasDerivedFg = entry.Items.Any(i =>
                (i.IsFinishedItem || !i.SourceWarehouseId.HasValue)
                && i.TargetWarehouseId.HasValue
                && !i.SetBasicRateManually
                && !i.AllowZeroValuationRate);

            if (hasDerivedFg && rawMaterialCost > 0 && manualFgCost > rawMaterialCost)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Total value of manually rated finished good rows ({manualFgCost}) exceeds the total consumed raw material cost ({rawMaterialCost}).");
            }
        }

        // Per ERPNext PR #58005: mandatory manufactured qty check for manufacture entries
        if (entry.EntryType == StockEntryType.Manufacture && entry.WorkOrderId.HasValue && !trackSemiFinishedGoods && entry.FgCompletedQty <= 0)
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

        var allowedQty = wo.Quantity + (overproductionPercentage / 100m * wo.Quantity);

        // Per ERPNext PR #58004 / PR #58005 (commits 22fa520500, 492ee05727):
        // Block duplicate manufacture entries when existing non-cancelled entries already cover the work order qty plus allowance.
        var alreadyEnteredFgQty = otherEntries.Sum(se =>
        {
            var fg = se.Items
                .Where(i => i.ItemId == wo.ItemId && i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue)
                .Sum(i => i.Quantity);
            var loss = se.ProcessLossQty > 0 ? se.ProcessLossQty : Math.Max(0, se.FgCompletedQty - fg);
            return fg + loss;
        });

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
    /// Validates that operations (Job Cards) for a Work Order have completed sufficient quantity
    /// before allowing a Manufacture or Material Consumption for Manufacture Stock Entry.
    /// Per ERPNext PR #58000 / commit 401eb30963 & 26a05044c0:
    /// Gated to work orders without track_semi_finished_goods.
    /// Non-corrective, non-cancelled job cards are evaluated per operation.
    /// Total completed qty (entry.FgCompletedQty + wo.ProducedQuantity) must not exceed
    /// allowed qty: opCompletedQty + opProcessLossQty + (allowancePct / 100 * opCompletedQty).
    /// </summary>
    public async Task ValidateOperationsCompletedAsync(
        StockEntry entry,
        IRepository<Manufacturing.Entities.WorkOrder, Guid> woRepository,
        IRepository<Manufacturing.Entities.JobCard, Guid> jobCardRepository,
        decimal overproductionPercentage = 0m)
    {
        if (!entry.WorkOrderId.HasValue)
            return;

        if (entry.EntryType != StockEntryType.Manufacture &&
            entry.EntryType != StockEntryType.MaterialConsumptionForManufacture)
            return;

        var wo = await woRepository.FindAsync(entry.WorkOrderId.Value);
        if (wo == null || wo.TrackSemiFinishedGoods)
            return;

        var jcQuery = await jobCardRepository.GetQueryableAsync();
        var jobCards = jcQuery
            .Where(jc => jc.WorkOrderId == entry.WorkOrderId.Value &&
                         jc.Status != Manufacturing.JobCardStatus.Cancelled &&
                         !jc.IsCorrective)
            .ToList();

        if (!jobCards.Any())
            return;

        var totalCompletedQty = Math.Round(entry.FgCompletedQty + wo.ProducedQuantity, 4);

        // Group job cards by operation
        var opGroups = jobCards.GroupBy(jc => jc.BomOperationId ?? jc.OperationId);

        foreach (var group in opGroups)
        {
            var opCompletedQty = group.Sum(jc => jc.CompletedQty);
            var opProcessLossQty = group.Sum(jc => jc.ProcessLossQty);
            var allowedQty = Math.Round(
                opCompletedQty + opProcessLossQty + (overproductionPercentage / 100m * opCompletedQty), 4);

            if (totalCompletedQty > allowedQty)
            {
                var activeJc = group.FirstOrDefault();
                throw new BusinessException(MyERPDomainErrorCodes.OperationsNotComplete)
                    .WithData("workOrder", wo.WorkOrderNumber ?? wo.Id.ToString())
                    .WithData("totalCompletedQty", totalCompletedQty)
                    .WithData("allowedQty", allowedQty)
                    .WithData("jobCardId", activeJc?.Id.ToString() ?? string.Empty)
                    .WithData("detail", $"Operation is not completed for {totalCompletedQty} qty of finished goods in Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}. Please update operation status via Job Card {activeJc?.Id}.");
            }
        }
    }

    /// <summary>
    /// Qty of the received finished good rows whose rate is derived from the consumed cost.
    /// Manual and zero-valued rows take no share, so the others carry the whole cost (PR #59881 / commit 37f16db9d8).
    /// </summary>
    public static decimal GetFinishedItemsQty(IEnumerable<StockEntryItem> items)
    {
        return items
            .Where(d => (d.IsFinishedItem || (!d.SourceWarehouseId.HasValue && string.IsNullOrWhiteSpace(d.SecondaryItemType)))
                && d.TargetWarehouseId.HasValue
                && !d.SetBasicRateManually
                && !d.AllowZeroValuationRate)
            .Sum(d => d.Quantity);
    }

    /// <summary>
    /// Calculates valuation rate for Repack FG items.
    /// Derived rate is split over all eligible finished good rows (PR #59881).
    /// Takes manual finished good value and scrap out of repack cost (PR #60421 / commit 00ca106bf5).
    /// </summary>
    public decimal CalculateRepackFgRate(IReadOnlyList<StockEntryItem> items, decimal? fgQty = null)
    {
        var resolvedFgQty = fgQty.HasValue && fgQty.Value > 0 ? fgQty.Value : GetFinishedItemsQty(items);
        if (resolvedFgQty <= 0) return 0;

        var totalOutgoingCost = items
            .Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem)
            .Sum(i => i.Quantity * (i.ValuationRate ?? 0));

        var manualCost = items
            .Where(d => (d.IsFinishedItem || !d.SourceWarehouseId.HasValue)
                && d.TargetWarehouseId.HasValue
                && d.SetBasicRateManually
                && !d.AllowZeroValuationRate)
            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

        var scrapCost = items
            .Where(d => d.TargetWarehouseId.HasValue
                && !d.SourceWarehouseId.HasValue
                && !d.IsFinishedItem
                && !string.IsNullOrWhiteSpace(d.SecondaryItemType))
            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

        var netOutgoingCost = Math.Max(0m, totalOutgoingCost - scrapCost - manualCost);
        return Math.Round(netOutgoingCost / resolvedFgQty, 4);
    }

    /// <summary>
    /// Calculates basic valuation rate for manufactured FG item(s).
    /// Cost is split over all eligible finished good rows (PR #59881 / commit 37f16db9d8).
    /// Deducts value of manually-rated finished good rows (commit 8148dd486b).
    /// Retains co-product cost share for co-products not made (PR #60421 / commit 00ca106bf5).
    /// Per ERPNext PR #57334: when inputs are consumed at zero cost (e.g. free raw materials),
    /// rate remains zero (plus additional operating cost) and must not fall back to BOM or standard rates.
    /// </summary>
    public decimal CalculateManufactureFgRate(
        IReadOnlyList<StockEntryItem> items,
        decimal? fgQty = null,
        decimal additionalOperatingCost = 0m,
        decimal? bomEstimatedCost = null,
        bool viaRepost = false,
        decimal costAllocationPercentage = 100m)
    {
        var resolvedFgQty = fgQty.HasValue && fgQty.Value > 0 ? fgQty.Value : GetFinishedItemsQty(items);
        if (resolvedFgQty <= 0) return 0m;

        var rawMaterialItems = items.Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem).ToList();
        var hasConsumptionBasis = rawMaterialItems.Count > 0;

        var outgoingCost = rawMaterialItems.Sum(i => i.Quantity * (i.ValuationRate ?? 0m));
        var costedOutCost = GetCostedOutItemsCost(items);

        var manualCost = items
            .Where(d => (d.IsFinishedItem || (!d.SourceWarehouseId.HasValue && !IsCostedOutOfFinishedItem(d)))
                && d.TargetWarehouseId.HasValue
                && d.SetBasicRateManually
                && !d.AllowZeroValuationRate)
            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

        if (!hasConsumptionBasis && bomEstimatedCost.HasValue && manualCost <= 0 && costedOutCost <= 0)
        {
            outgoingCost = bomEstimatedCost.Value;
        }

        var costLeft = outgoingCost - costedOutCost - manualCost;
        if (costAllocationPercentage < 100m && costAllocationPercentage >= 0m)
        {
            costLeft *= (costAllocationPercentage / 100m);
        }

        if (viaRepost || costLeft < 0)
        {
            costLeft = Math.Max(costLeft, 0m);
        }

        var totalFgCost = costLeft + additionalOperatingCost;
        return Math.Round(Math.Max(totalFgCost, 0m) / resolvedFgQty, 4);
    }

    /// <summary>
    /// Whether a secondary output row takes its value out of the finished good instead of adding to it
    /// (Valuation Rate, Manual, or BOM-less legacy scrap).
    /// Per ERPNext PR #59021 / commit bec627c3eb & commit 4e3093831e.
    /// </summary>
    public static bool IsCostedOutOfFinishedItem(StockEntryItem row) =>
        row.TargetWarehouseId.HasValue
        && !row.SourceWarehouseId.HasValue
        && (row.ValuationType is SecondaryItemValuationType.ValuationRate or SecondaryItemValuationType.Manual
            || (!string.IsNullOrWhiteSpace(row.SecondaryItemType) && !row.BomSecondaryItemId.HasValue));

    /// <summary>
    /// Total value of secondary rows deducted from the cost pool before allocating FG/co-product shares.
    /// </summary>
    public static decimal GetCostedOutItemsCost(IEnumerable<StockEntryItem> items) =>
        items.Where(d => IsCostedOutOfFinishedItem(d) && !d.AllowZeroValuationRate)
            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

    /// <summary>
    /// Calculates the per-unit basic rate for a BOM-linked co-product / secondary output row.
    /// Per ERPNext commit 4e3093831e: when multiple rows reference the same BomSecondaryItemId
    /// (e.g., split across warehouses or batches), their quantities are summed so they share the
    /// allocated percentage rather than duplicating the full co-product cost on each row.
    /// </summary>
    public static decimal CalculateCoProductRate(
        IReadOnlyList<StockEntryItem> items,
        Guid bomSecondaryItemId,
        decimal costAllocationPercentage,
        decimal secondaryItemsCostBasis)
    {
        var secondaryItemsQty = items
            .Where(item => item.BomSecondaryItemId == bomSecondaryItemId
                && !item.SourceWarehouseId.HasValue
                && item.TargetWarehouseId.HasValue
                && !item.SetBasicRateManually
                && !item.AllowZeroValuationRate
                && !IsCostedOutOfFinishedItem(item))
            .Sum(item => item.Quantity);

        if (secondaryItemsQty <= 0m) return 0m;

        var allocationBasis = Math.Max(0m, secondaryItemsCostBasis - GetCostedOutItemsCost(items));
        return Math.Round((allocationBasis * (costAllocationPercentage / 100m)) / secondaryItemsQty, 4);
    }

    /// <summary>
    /// Computes cost allocation percentage for finished goods, giving it the share of co-products not made.
    /// Per ERPNext PR #60421 / commit 00ca106bf5: if co-products defined in BOM are omitted in the Stock Entry,
    /// their cost share is retained by the finished good instead of being lost.
    /// </summary>
    public decimal GetFinishedGoodCostAllocationPercentage(
        IReadOnlyList<StockEntryItem> items,
        IReadOnlyList<(Guid BomSecondaryItemId, decimal CostAllocationPercentage)> bomSecondaryItems) =>
        ComputeFinishedGoodCostAllocationPercentage(items, bomSecondaryItems);

    public static decimal ComputeFinishedGoodCostAllocationPercentage(
        IReadOnlyList<StockEntryItem> items,
        IReadOnlyList<(Guid BomSecondaryItemId, decimal CostAllocationPercentage)> bomSecondaryItems)
    {
        var presentBomSecondaryItemIds = items
            .Where(i => i.BomSecondaryItemId.HasValue && i.Quantity > 0 && !IsCostedOutOfFinishedItem(i))
            .Select(i => i.BomSecondaryItemId!.Value)
            .ToHashSet();

        if (presentBomSecondaryItemIds.Count == 0 || bomSecondaryItems == null || bomSecondaryItems.Count == 0)
        {
            return 100m;
        }

        var allocatedPer = bomSecondaryItems
            .Where(b => presentBomSecondaryItemIds.Contains(b.BomSecondaryItemId))
            .Sum(b => b.CostAllocationPercentage);

        return Math.Max(0m, 100m - allocatedPer);
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

            // Per ERPNext PR #67412 (disassemble.py:get_source_fg_qty):
            // FG consumption must not exceed the finished goods put in stock (fg_completed_qty - process_loss_qty).
            var sourceFinishedGoodQty = GetSourceManufactureFinishedGoodQty(sourceEntry.FgCompletedQty, sourceEntry.ProcessLossQty);
            if (entry.FgCompletedQty > sourceFinishedGoodQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleQtyExceedsSource)
                    .WithData("disassembleQty", entry.FgCompletedQty)
                    .WithData("sourceQty", sourceFinishedGoodQty)
                    .WithData("reason", "Disassemble qty exceeds source manufacture FG qty");
            }
        }
    }

    /// <summary>
    /// Returns the finished goods quantity that a Manufacture Stock Entry actually put in stock
    /// (<c>fg_completed_qty - process_loss_qty</c>), floored at 0
    /// per ERPNext PR #67412 (<c>DisassembleStockEntry.get_source_fg_qty</c>).
    /// </summary>
    public static decimal GetSourceManufactureFinishedGoodQty(
        decimal sourceFgCompletedQty,
        decimal sourceProcessLossQty)
    {
        return Math.Max(0m, sourceFgCompletedQty - Math.Max(0m, sourceProcessLossQty));
    }

    /// <summary>
    /// Computes remaining finished good quantity available to disassemble against a source Manufacture
    /// Stock Entry (<c>fg_completed_qty - process_loss_qty - already_disassembled</c>)
    /// per ERPNext PR #67412 (<c>work_order.py:get_disassembly_available_qty</c>).
    /// </summary>
    public static decimal CalculateSourceManufactureDisassembleAvailableQty(
        decimal sourceFgCompletedQty,
        decimal sourceProcessLossQty,
        decimal alreadyDisassembledQty,
        int precision = 4)
    {
        return Math.Round(
            sourceFgCompletedQty - sourceProcessLossQty - alreadyDisassembledQty,
            precision,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Sizes a reset or batchwise finished-good Serial and Batch Bundle on a Manufacture Stock Entry
    /// to the finished-good row's stock quantity (<c>Round(rowQty * conversionFactor, precision)</c>,
    /// after process loss) rather than raw <c>fg_completed_qty</c>
    /// per ERPNext PR #66155 (<c>stock_entry/services/manufacturing.py</c>).
    /// </summary>
    public static decimal ResolveFinishedGoodBundleStockQty(
        decimal rowQty,
        decimal? rowConversionFactor,
        decimal? resolvedUomConversionFactor = null,
        int transferQtyPrecision = 4)
    {
        var conversionFactor = rowConversionFactor.HasValue && rowConversionFactor.Value > 0m
            ? rowConversionFactor.Value
            : (resolvedUomConversionFactor.HasValue && resolvedUomConversionFactor.Value > 0m
                ? resolvedUomConversionFactor.Value
                : 1m);

        return Math.Round(rowQty * conversionFactor, transferQtyPrecision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Validates that every document referenced by a Stock Entry (e.g. Work Order, Pick List, Job Card,
    /// Purchase Order, Subcontracting Order) belongs to <paramref name="stockEntryCompanyId"/>
    /// per ERPNext PR #65952 (<c>StockEntry.validate_reference_companies</c>).
    /// </summary>
    public static void ValidateReferencedDocumentsCompany(
        Guid stockEntryCompanyId,
        IEnumerable<(string DocType, string DocumentName, Guid DocumentCompanyId)>? referencedDocuments)
    {
        if (referencedDocuments == null)
        {
            return;
        }

        foreach (var doc in referencedDocuments)
        {
            if (doc.DocumentCompanyId != stockEntryCompanyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("docType", doc.DocType)
                    .WithData("documentName", doc.DocumentName)
                    .WithData("companyId", stockEntryCompanyId)
                    .WithData("detail", $"{doc.DocType} {doc.DocumentName} does not belong to the Stock Entry company.");
            }
        }
    }

    /// <summary>
    /// Validates that Stock Entry item row links (<c>PickListItem</c> / <c>JobCardItem</c>) belong to
    /// the Stock Entry's own header <paramref name="headerParentId"/> rather than another document
    /// per ERPNext PR #65952 (<c>StockEntry.validate_row_reference_parents</c>).
    /// </summary>
    public static void ValidateRowReferenceParents(
        string childDocType,
        Guid? headerParentId,
        IEnumerable<(Guid ChildRowId, Guid ActualParentId)>? linkedChildRows)
    {
        if (linkedChildRows == null)
        {
            return;
        }

        var expectedParent = headerParentId ?? Guid.Empty;
        foreach (var row in linkedChildRows)
        {
            if (row.ChildRowId == Guid.Empty)
            {
                continue;
            }

            if (row.ActualParentId != expectedParent)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("childDocType", childDocType)
                    .WithData("childRowId", row.ChildRowId)
                    .WithData("detail", $"{childDocType} {row.ChildRowId} does not belong to the linked parent document.");
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

        var groupedDisassembly = disassemblyItems
            .Where(i => !i.IsFinishedItem)
            .GroupBy(i => (i.SourceStockEntryDetailId, i.ItemId));

        foreach (var group in groupedDisassembly)
        {
            decimal sourceStockQty;
            if (group.Key.SourceStockEntryDetailId.HasValue)
            {
                var sourceItem = sourceItems.FirstOrDefault(s => s.Id == group.Key.SourceStockEntryDetailId.Value);
                if (sourceItem == null) continue;
                sourceStockQty = sourceItem.StockQty;
            }
            else
            {
                var matchingSource = sourceItems.Where(s => s.ItemId == group.Key.ItemId && !s.IsFinishedItem).ToList();
                if (matchingSource.Count == 0) continue;
                sourceStockQty = matchingSource.Sum(s => s.StockQty);
            }

            // Per PR #57710 & commits b9121c2188/54fcf4abd5: compare aggregated StockQty across batch-split rows
            var actualStockQty = group.Sum(i => i.StockQty);
            var expectedStockQty = Math.Round(sourceStockQty * scaleFactor, precision);
            var diff = Math.Abs(actualStockQty - expectedStockQty);

            if (diff > tolerance)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleScaleFactorMismatch)
                    .WithData("itemId", group.Key.ItemId)
                    .WithData("expectedQty", expectedStockQty)
                    .WithData("actualQty", actualStockQty)
                    .WithData("scaleFactor", scaleFactor);
            }
        }
    }

    /// <summary>
    /// Validates standalone BOM disassembly rows against the BOM when no source manufacture entry is referenced.
    /// Per ERPNext PR #60421 / commit 00ca106bf5: checks that every disassembled component belongs to the BOM
    /// and its received quantity matches BOM quantity * fgCompletedQty.
    /// </summary>
    public void ValidateDisassembleMaterialsAgainstBom(
        StockEntry entry,
        IReadOnlyList<(Guid ItemId, decimal Qty)> bomItems,
        int precision = 4)
    {
        if (entry.EntryType != StockEntryType.Disassemble || entry.SourceStockEntryId.HasValue)
            return;

        if (bomItems == null || bomItems.Count == 0 || entry.FgCompletedQty <= 0)
            return;

        var expectedQtyMap = bomItems.ToDictionary(
            b => b.ItemId,
            b => Math.Round(b.Qty * entry.FgCompletedQty, precision));

        var tolerance = 1m / (decimal)Math.Pow(10, precision);

        var receivedComponents = entry.Items
            .Where(i => i.TargetWarehouseId.HasValue && !i.IsFinishedItem)
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity * (i.ConversionFactor > 0 ? i.ConversionFactor : 1m)));

        foreach (var (itemId, actualQty) in receivedComponents)
        {
            if (!expectedQtyMap.TryGetValue(itemId, out var expectedQty))
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleItemNotInBom)
                    .WithData("itemId", itemId);
            }

            if (Math.Abs(actualQty - expectedQty) > tolerance)
            {
                throw new BusinessException(MyERPDomainErrorCodes.DisassembleScaleFactorMismatch)
                    .WithData("itemId", itemId)
                    .WithData("expectedQty", expectedQty)
                    .WithData("actualQty", actualQty);
            }
        }
    }

    /// <summary>
    /// Validates consumed raw material quantities against BOM requirements for a Manufacture Stock Entry.
    /// Per ERPNext PR #60090 / commit 00efe4e233:
    /// Aggregates consumed items by OriginalItemId ?? ItemId so that alternative item substitutions
    /// are verified collectively against the required BOM component quantity.
    /// </summary>
    public void ValidateManufactureBomComponentQty(
        StockEntry entry,
        IReadOnlyList<(Guid ItemId, string ItemCode, decimal Qty)> bomItems,
        string? bomNumber = null,
        int precision = 4)
    {
        if (entry.EntryType != StockEntryType.Manufacture || entry.FgCompletedQty <= 0 || bomItems == null || bomItems.Count == 0)
            return;

        var tolerance = 1m / (decimal)Math.Pow(10, precision);

        var consumedByOriginalItem = entry.Items
            .Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem && string.IsNullOrWhiteSpace(i.SecondaryItemType))
            .GroupBy(i => i.OriginalItemId ?? i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        foreach (var bomItem in bomItems)
        {
            var expectedQty = Math.Round(bomItem.Qty * entry.FgCompletedQty, precision);
            if (consumedByOriginalItem.TryGetValue(bomItem.ItemId, out var actualQty))
            {
                if (Math.Abs(actualQty - expectedQty) > tolerance)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"For item {bomItem.ItemCode}, the consumed quantity ({actualQty}) should be {expectedQty} according to BOM {bomNumber ?? "referenced"}.");
                }
            }
        }
    }

    /// <summary>
    /// Sets AllowAlternativeItem on raw material rows when both the Work Order and the Item master allow it.
    /// Per ERPNext PR #60090 / commit 00efe4e233.
    /// </summary>
    public async Task SetAllowAlternativeItemAsync(
        StockEntry entry,
        IRepository<WorkOrder, Guid> woRepository,
        IRepository<Item, Guid>? itemRepository = null)
    {
        if (!entry.WorkOrderId.HasValue)
            return;

        if (entry.EntryType != StockEntryType.MaterialTransferForManufacture &&
            entry.EntryType != StockEntryType.Manufacture &&
            entry.EntryType != StockEntryType.MaterialConsumptionForManufacture)
            return;

        var wo = await woRepository.FindAsync(entry.WorkOrderId.Value);
        if (wo == null || !wo.AllowAlternativeItem)
        {
            foreach (var item in entry.Items.Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem))
            {
                item.AllowAlternativeItem = false;
            }
            return;
        }

        var sourceItems = entry.Items
            .Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem)
            .ToList();

        if (!sourceItems.Any()) return;

        var itemIdsToCheck = sourceItems
            .Select(i => i.OriginalItemId ?? i.ItemId)
            .Distinct()
            .ToList();

        var itemRepo = itemRepository ?? _itemRepository;
        var itemQuery = await itemRepo.GetQueryableAsync();
        var allowedItemIds = itemQuery
            .Where(i => itemIdsToCheck.Contains(i.Id) && i.AllowAlternativeItem)
            .Select(i => i.Id)
            .ToHashSet();

        foreach (var row in sourceItems)
        {
            var baseId = row.OriginalItemId ?? row.ItemId;
            row.AllowAlternativeItem = allowedItemIds.Contains(baseId);
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
        IRepository<StockEntry, Guid> seRepo,
        IRepository<MyERP.Manufacturing.Entities.ManufacturingSettings, Guid>? mfgSettingsRepo = null)
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

        var settingsRepo = mfgSettingsRepo ?? LazyServiceProvider?.LazyGetService<IRepository<MyERP.Manufacturing.Entities.ManufacturingSettings, Guid>>();
        bool isAllowed = false;
        bool hasSettingSource = false;
        if (settingsRepo != null)
        {
            hasSettingSource = true;
            var settings = await settingsRepo.FindAsync(s => s.CompanyId == entry.CompanyId);
            if (settings != null)
            {
                isAllowed = settings.AllowAlternativeFinishedGoods;
            }
        }

        if (!isAllowed && LazyServiceProvider != null)
        {
            var settingProvider = LazyServiceProvider.LazyGetService<Volo.Abp.Settings.ISettingProvider>();
            if (settingProvider != null)
            {
                hasSettingSource = true;
                var abpSettingVal = await settingProvider.GetOrNullAsync(MyERP.Settings.MyERPSettings.Manufacturing.AllowAlternativeFinishedGoods);
                if (!string.IsNullOrEmpty(abpSettingVal) && bool.TryParse(abpSettingVal, out var parsed))
                {
                    isAllowed = parsed;
                }
            }
        }

        if (hasSettingSource && !isAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Enable 'Allow Alternative Finished Goods' in Manufacturing Settings to make a finished good conversion entry.");
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
    /// <summary>
    /// Validates Batch Split Repack or Manufacture Stock Entry (upstream PR #58530).
    /// Splits consumed batch inventory into one child batch per finished piece.
    /// </summary>
    public void ValidateBatchSplit(StockEntry entry)
    {
        if (entry.WeightPerPiece <= 0) return;

        if (entry.EntryType != StockEntryType.Repack && entry.EntryType != StockEntryType.Manufacture)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Batch Split operation is only supported for 'Repack' and 'Manufacture' Stock Entry purposes.");
        }

        var sourceItems = entry.Items.Where(i => i.SourceWarehouseId.HasValue && !i.IsFinishedItem && i.SecondaryItemType == null).ToList();
        var uniqueSourceItemIds = sourceItems.Select(i => i.ItemId).Distinct().ToList();

        if (uniqueSourceItemIds.Count > 1)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Batch Split entry requires exactly one raw material item type to be consumed.");
        }

        var outputItems = entry.Items.Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue && (i.IsFinishedItem || entry.EntryType == StockEntryType.Repack) && i.SecondaryItemType == null).ToList();
        var totalOutputQty = outputItems.Sum(i => i.Quantity);

        if (totalOutputQty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Batch Split entry must have finished/incoming goods.");
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

    /// <summary>
    /// Validates difference / expense accounts on Stock Entry items (ERPNext commit fb819c558e & bba6b0ff45).
    /// </summary>
    public async Task ValidateDifferenceAccountAsync(StockEntry entry)
    {
        var accountIds = entry.Items
            .Where(i => i.ExpenseAccountId.HasValue)
            .Select(i => i.ExpenseAccountId!.Value)
            .Distinct()
            .ToList();

        if (!accountIds.Any()) return;

        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Accounting.Entities.Account, Guid>>();
        var accountMap = (await accountRepo.GetQueryableAsync())
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionary(a => a.Id);

        for (int idx = 0; idx < entry.Items.Count; idx++)
        {
            var item = entry.Items[idx];
            if (!item.ExpenseAccountId.HasValue || !accountMap.TryGetValue(item.ExpenseAccountId.Value, out var acc))
                continue;

            // Difference account must not be Stock account (ERPNext commit fb819c558e)
            if (acc.AccountSubType == Accounting.AccountSubType.Stock)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx + 1}: The Difference Account '{acc.AccountName}' must not be a Stock type account. Please select a different account.");
            }

            // Opening stock entry must use Balance Sheet account (e.g. Temporary Opening), not P&L
            if (entry.IsOpening && (acc.AccountType == Accounting.AccountType.Expense || acc.AccountType == Accounting.AccountType.Revenue))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx + 1}: Difference Account must be an Asset/Liability/Equity account (such as Temporary Opening), since this Stock Entry is an Opening Entry.");
            }

            // Cost of Goods Sold is only valid for Material Issue entries (ERPNext PR #47452 / commit bba6b0ff45)
            if (entry.EntryType != StockEntryType.MaterialIssue && acc.AccountSubType == Accounting.AccountSubType.CostOfGoodsSold)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{idx + 1}: You have selected Difference Account '{acc.AccountName}', which is a Cost of Goods Sold type account. Cost of Goods Sold accounts can only be used with Material Issue purpose.");
            }
        }
    }

    /// <summary>
    /// Distributes total additional costs across incoming rows.
    /// Per ERPNext commit 074c84e880 / PR #58433 and PR #58842 / commit 1728d1b0f5:
    /// - For Repack/Manufacture: incoming items are finished goods (IsFinishedItem = true).
    /// - For other purposes: incoming items have TargetWarehouseId != null.
    /// - Primary allocation basis: proportional to basic amount (Quantity * ValuationRate).
    /// - Fallback allocation basis: if total basic amount is zero (zero-valued items), proportional to Quantity.
    /// - Additional costs are redistributed across incoming rows and valuation rate is updated.
    /// </summary>
    public static void DistributeAdditionalCosts(StockEntry entry)
    {
        foreach (var item in entry.Items)
        {
            item.AdditionalCost = 0;
        }

        if (entry.TotalAdditionalCosts <= 0) return;

        List<StockEntryItem> incomingRows;
        if (entry.EntryType == StockEntryType.Repack || entry.EntryType == StockEntryType.Manufacture)
        {
            incomingRows = entry.Items.Where(i => i.IsFinishedItem).ToList();
        }
        else
        {
            incomingRows = entry.Items.Where(i => i.TargetWarehouseId.HasValue).ToList();
        }

        if (!incomingRows.Any()) return;

        var totalBasicAmount = incomingRows.Sum(i => i.Quantity * (i.ValuationRate ?? 0m));
        var useBasicAmount = totalBasicAmount > 0m;
        var totalBasis = useBasicAmount
            ? totalBasicAmount
            : incomingRows.Sum(i => i.StockQty > 0 ? i.StockQty : i.Quantity);

        if (totalBasis <= 0m) return;

        // Per ERPNext PR #67386 (commit 0ef3c0adba): round running cumulative total via SplitByWeights
        // so row shares always sum exactly to TotalAdditionalCosts and zero-weight rows receive 0.
        var weights = incomingRows
            .Select(row =>
            {
                var basisVal = useBasicAmount
                    ? row.Quantity * (row.ValuationRate ?? 0m)
                    : (row.StockQty > 0 ? row.StockQty : row.Quantity);
                return basisVal / totalBasis;
            })
            .ToList();

        var shares = SplitByWeights(entry.TotalAdditionalCosts, weights, precision: 4);
        for (int i = 0; i < incomingRows.Count; i++)
        {
            var row = incomingRows[i];
            row.AdditionalCost = shares[i];

            if (row.Quantity > 0)
            {
                var rateIncrement = row.AdditionalCost / row.Quantity;
                row.ValuationRate = (row.ValuationRate ?? 0m) + rateIncrement;
            }
        }
    }

    /// <summary>
    /// Splits <paramref name="amount"/> proportionally by <paramref name="weights"/>, rounding the
    /// cumulative target at each step so shares sum exactly to <paramref name="amount"/> and any
    /// zero-weight row receives 0 per ERPNext PR #67386 (commit 0ef3c0adba, <c>split_by_weights</c>).
    /// </summary>
    public static List<decimal> SplitByWeights(
        decimal amount,
        IReadOnlyList<decimal> weights,
        int precision = 2)
    {
        var shares = new List<decimal>(weights?.Count ?? 0);
        if (weights == null || weights.Count == 0)
            return shares;

        decimal previousTotal = 0m;
        decimal cumulativeWeight = 0m;
        foreach (var weight in weights)
        {
            cumulativeWeight += weight;
            var total = Math.Round(amount * cumulativeWeight, precision, MidpointRounding.AwayFromZero);
            shares.Add(Math.Round(total - previousTotal, precision, MidpointRounding.AwayFromZero));
            previousTotal = total;
        }

        return shares;
    }

    /// <summary>
    /// Splits additional costs per expense account across incoming rows using <see cref="SplitByWeights"/>
    /// so each account's row shares sum exactly to that account's cost and each row's total additional cost
    /// matches the sum of its account shares per ERPNext PR #67386 (commit 0ef3c0adba).
    /// </summary>
    public static List<(Guid RowId, Guid ExpenseAccountId, decimal Amount, decimal BaseAmount)> SplitAdditionalCostsByAccount(
        IReadOnlyList<(Guid RowId, decimal BasisValue)> incomingRows,
        IEnumerable<(Guid ExpenseAccountId, decimal Amount, decimal BaseAmount)> additionalCosts,
        int precision = 2)
    {
        var result = new List<(Guid RowId, Guid ExpenseAccountId, decimal Amount, decimal BaseAmount)>();
        if (incomingRows == null || incomingRows.Count == 0 || additionalCosts == null)
            return result;

        var totalBasis = incomingRows.Sum(r => r.BasisValue);
        if (totalBasis <= 0m)
            return result;

        var weights = incomingRows.Select(r => r.BasisValue / totalBasis).ToList();
        var costsByAccount = new Dictionary<Guid, (decimal Amount, decimal BaseAmount)>();
        foreach (var cost in additionalCosts)
        {
            var prev = costsByAccount.GetValueOrDefault(cost.ExpenseAccountId);
            costsByAccount[cost.ExpenseAccountId] = (prev.Amount + cost.Amount, prev.BaseAmount + cost.BaseAmount);
        }

        foreach (var (accountId, (amount, baseAmount)) in costsByAccount)
        {
            var amounts = SplitByWeights(amount, weights, precision);
            var baseAmounts = SplitByWeights(baseAmount, weights, precision);
            for (var i = 0; i < incomingRows.Count; i++)
            {
                result.Add((incomingRows[i].RowId, accountId, amounts[i], baseAmounts[i]));
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves finished good serial numbers and batch numbers produced in this Stock Entry (Manufacture/Repack).
    /// Per ERPNext PR #59706 (commit d4f5d39ee8).
    /// </summary>
    public static async Task<List<(string? FgSerialNo, string? FgBatchNo)>> GetFinishedGoodTargetsAsync(
        StockEntry entry,
        IRepository<SerialAndBatchBundle, Guid> bundleRepo,
        IRepository<Batch, Guid>? batchRepo = null)
    {
        var targets = new List<(string? FgSerialNo, string? FgBatchNo)>();
        if (entry.EntryType != StockEntryType.Manufacture && entry.EntryType != StockEntryType.Repack)
            return targets;

        var fgItems = entry.Items.Where(i => i.IsFinishedItem && i.TargetWarehouseId.HasValue).ToList();
        if (!fgItems.Any()) return targets;

        var fgItemDetailIds = fgItems.Select(i => i.Id).ToList();
        var bundles = (await bundleRepo.WithDetailsAsync(b => b.Entries))
            .Where(b => b.VoucherType == "StockEntry" && b.VoucherId == entry.Id && b.VoucherDetailId.HasValue && fgItemDetailIds.Contains(b.VoucherDetailId.Value) && !b.IsCancelled)
            .ToList();

        var bundleMap = bundles.ToDictionary(b => b.VoucherDetailId!.Value);

        Dictionary<Guid, string>? batchIdToNo = null;
        if (batchRepo != null)
        {
            var batchIds = fgItems.Where(i => i.BatchId.HasValue).Select(i => i.BatchId!.Value)
                .Concat(bundles.SelectMany(b => b.Entries).Where(e => e.BatchId.HasValue).Select(e => e.BatchId!.Value))
                .Distinct().ToList();
            if (batchIds.Any())
            {
                var bQuery = await batchRepo.GetQueryableAsync();
                batchIdToNo = bQuery.Where(b => batchIds.Contains(b.Id)).ToDictionary(b => b.Id, b => b.BatchNo);
            }
        }

        foreach (var item in fgItems)
        {
            if (bundleMap.TryGetValue(item.Id, out var bundle) && bundle.Entries.Any())
            {
                var serialized = bundle.Entries.Any(e => !string.IsNullOrWhiteSpace(e.SerialNo));
                foreach (var e in bundle.Entries)
                {
                    if (serialized && !string.IsNullOrWhiteSpace(e.SerialNo))
                    {
                        if (!targets.Any(t => t.FgSerialNo == e.SerialNo))
                            targets.Add((e.SerialNo, null));
                    }
                    else if (e.BatchId.HasValue)
                    {
                        var bNo = (batchIdToNo != null && batchIdToNo.TryGetValue(e.BatchId.Value, out var bn)) ? bn : e.BatchId.Value.ToString();
                        if (!targets.Any(t => t.FgBatchNo == bNo))
                            targets.Add((null, bNo));
                    }
                }
            }
            else if (item.BatchId.HasValue)
            {
                var bNo = (batchIdToNo != null && batchIdToNo.TryGetValue(item.BatchId.Value, out var bn)) ? bn : item.BatchId.Value.ToString();
                if (!targets.Any(t => t.FgBatchNo == bNo))
                    targets.Add((null, bNo));
            }
        }

        return targets;
    }

    /// <summary>
    /// Validates raw material serial / batch mappings against produced finished goods.
    /// Per ERPNext PR #59706 (commit d4f5d39ee8).
    /// </summary>
    public static async Task ValidateFinishedGoodMappingsAsync(
        StockEntry entry,
        IRepository<SerialAndBatchBundle, Guid> bundleRepo,
        IRepository<Batch, Guid>? batchRepo = null)
    {
        if (entry.EntryType != StockEntryType.Manufacture && entry.EntryType != StockEntryType.Repack)
            return;

        var fgTargets = await GetFinishedGoodTargetsAsync(entry, bundleRepo, batchRepo);
        var fgSerialSet = fgTargets.Where(t => !string.IsNullOrWhiteSpace(t.FgSerialNo)).Select(t => t.FgSerialNo!).ToHashSet();
        var fgBatchSet = fgTargets.Where(t => !string.IsNullOrWhiteSpace(t.FgBatchNo)).Select(t => t.FgBatchNo!).ToHashSet();

        var rmItems = entry.Items.Where(i => !i.IsFinishedItem && i.SourceWarehouseId.HasValue && !i.TargetWarehouseId.HasValue).ToList();
        if (!rmItems.Any()) return;

        var rmDetailIds = rmItems.Select(i => i.Id).ToList();
        var rmBundles = (await bundleRepo.WithDetailsAsync(b => b.Entries))
            .Where(b => b.VoucherType == "StockEntry" && b.VoucherId == entry.Id && b.VoucherDetailId.HasValue && rmDetailIds.Contains(b.VoucherDetailId.Value) && !b.IsCancelled)
            .ToList();

        foreach (var bundle in rmBundles)
        {
            foreach (var e in bundle.Entries)
            {
                if (!string.IsNullOrWhiteSpace(e.FgSerialNo) && !fgSerialSet.Contains(e.FgSerialNo))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidFinishedGoodMapping)
                        .WithData("rawMaterial", e.SerialNo ?? e.BatchId?.ToString() ?? "Raw Material")
                        .WithData("target", e.FgSerialNo);
                }
                if (!string.IsNullOrWhiteSpace(e.FgBatchNo) && !fgBatchSet.Contains(e.FgBatchNo))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidFinishedGoodMapping)
                        .WithData("rawMaterial", e.SerialNo ?? e.BatchId?.ToString() ?? "Raw Material")
                        .WithData("target", e.FgBatchNo);
                }
            }
        }
    }

    /// <summary>
    /// Auto-maps raw materials to finished goods in order when auto_map_raw_materials_to_finished_goods is enabled.
    /// Per ERPNext PR #59706 (commit d4f5d39ee8).
    /// </summary>
    public static async Task AutoMapRawMaterialsToFinishedGoodsAsync(
        StockEntry entry,
        IRepository<SerialAndBatchBundle, Guid> bundleRepo,
        IRepository<Batch, Guid>? batchRepo = null)
    {
        if (entry.EntryType != StockEntryType.Manufacture && entry.EntryType != StockEntryType.Repack)
            return;

        var fgTargets = await GetFinishedGoodTargetsAsync(entry, bundleRepo, batchRepo);
        if (!fgTargets.Any()) return;

        var rmItems = entry.Items.Where(i => !i.IsFinishedItem && i.SourceWarehouseId.HasValue && !i.TargetWarehouseId.HasValue).ToList();
        if (!rmItems.Any()) return;

        var rmDetailIds = rmItems.Select(i => i.Id).ToList();
        var rmBundles = (await bundleRepo.WithDetailsAsync(b => b.Entries))
            .Where(b => b.VoucherType == "StockEntry" && b.VoucherId == entry.Id && b.VoucherDetailId.HasValue && rmDetailIds.Contains(b.VoucherDetailId.Value) && !b.IsCancelled)
            .ToList();

        var updatedBundles = new List<SerialAndBatchBundle>();
        foreach (var bundle in rmBundles)
        {
            var hasUnmapped = bundle.Entries.Any(e => string.IsNullOrWhiteSpace(e.FgSerialNo) && string.IsNullOrWhiteSpace(e.FgBatchNo));
            if (hasUnmapped)
            {
                bundle.AutoMapToFinishedGoods(fgTargets);
                updatedBundles.Add(bundle);
            }
        }

        if (updatedBundles.Any())
        {
            await bundleRepo.UpdateManyAsync(updatedBundles);
        }
    }

    /// <summary>
    /// Applies batch reservations to Stock Entry lines for Material Transfer for Manufacture.
    /// Per ERPNext StockEntrySABB.set_serial_batch_based_on_reservation (PR #59755 & PR #59756):
    /// Distributes reserved batches up to the line quantity, and leaves any unreserved remainder on a row without batch.
    /// </summary>
    public async Task ApplyBatchReservationsForTransferAsync(StockEntry entry, StockReservationManager sreManager)
    {
        if (entry.EntryType != StockEntryType.MaterialTransferForManufacture || !entry.WorkOrderId.HasValue)
            return;

        var voucherId = entry.WorkOrderId.Value;
        var reservedMaterials = await sreManager.GetReservedMaterialsAsync("WorkOrder", voucherId);
        if (!reservedMaterials.Any(r => r.BatchId.HasValue))
            return;

        var itemsToProcess = entry.Items.Where(i => !i.BatchId.HasValue && i.SourceWarehouseId.HasValue).ToList();
        if (!itemsToProcess.Any()) return;

        var reservedBatches = reservedMaterials
            .Where(r => r.BatchId.HasValue && r.AvailableQty > 0)
            .GroupBy(r => (r.ItemId, r.WarehouseId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var newItems = new List<StockEntryItem>();

        foreach (var item in itemsToProcess)
        {
            var key = (item.ItemId, item.SourceWarehouseId!.Value);
            if (!reservedBatches.TryGetValue(key, out var batchList) || !batchList.Any(b => b.AvailableQty > 0))
                continue;

            var remaining = item.Quantity;
            bool firstAllocated = false;

            foreach (var batch in batchList)
            {
                if (remaining <= 0) break;
                if (batch.AvailableQty <= 0) continue;

                var allocQty = Math.Min(remaining, batch.AvailableQty);
                batch.AvailableQty -= allocQty;
                remaining -= allocQty;

                if (!firstAllocated)
                {
                    item.BatchId = batch.BatchId;
                    item.Quantity = allocQty;
                    firstAllocated = true;
                }
                else
                {
                    var added = entry.AddItem(
                        item.ItemId, allocQty, item.SourceWarehouseId, item.TargetWarehouseId,
                        item.ValuationRate, isFinishedItem: false, batchId: batch.BatchId,
                        conversionFactor: item.ConversionFactor, stockUom: item.StockUom);
                    added.CostCenterId = item.CostCenterId;
                    added.ExpenseAccountId = item.ExpenseAccountId;
                    added.ProjectId = item.ProjectId;
                    added.MaterialRequestItemId = item.MaterialRequestItemId;
                }
            }

            if (remaining > 0 && firstAllocated)
            {
                // Unreserved remainder row (PR #59755)
                var unreserved = entry.AddItem(
                    item.ItemId, remaining, item.SourceWarehouseId, item.TargetWarehouseId,
                    item.ValuationRate, isFinishedItem: false, batchId: null,
                    conversionFactor: item.ConversionFactor, stockUom: item.StockUom);
                unreserved.CostCenterId = item.CostCenterId;
                unreserved.ExpenseAccountId = item.ExpenseAccountId;
                unreserved.ProjectId = item.ProjectId;
                unreserved.MaterialRequestItemId = item.MaterialRequestItemId;
            }
        }
    }

    /// <summary>
    /// Builds the remaining batch pool per (ItemId, WarehouseId, IsFinishedItem) from posted Manufacture
    /// Stock Entries after subtracting batches already reversed by non-draft, non-cancelled Disassemble entries.
    /// Per ERPNext commit b9121c2188 (_get_available_batch_nos_from_stock_entries).
    /// </summary>
    public static Dictionary<(Guid ItemId, Guid WarehouseId, bool IsFinishedItem), List<(Guid BatchId, decimal AvailableQty)>>
        BuildAvailableDisassemblyBatchPool(
            IReadOnlyList<StockEntry> sourceManufactureEntries,
            IReadOnlyList<StockEntry> priorDisassembleEntries,
            int precision = 4)
    {
        var pool = new Dictionary<(Guid ItemId, Guid WarehouseId, bool IsFinishedItem), List<(Guid BatchId, decimal AvailableQty)>>();

        foreach (var se in sourceManufactureEntries)
        {
            if (se.EntryType != StockEntryType.Manufacture
                || se.Status == Core.DocumentStatus.Draft
                || se.Status == Core.DocumentStatus.Cancelled)
            {
                continue;
            }

            foreach (var item in se.Items)
            {
                if (!item.BatchId.HasValue) continue;

                var isFg = item.IsFinishedItem || (item.TargetWarehouseId.HasValue && !item.SourceWarehouseId.HasValue);
                var whId = isFg ? item.TargetWarehouseId : item.SourceWarehouseId;
                if (!whId.HasValue) continue;

                var key = (item.ItemId, whId.Value, isFg);
                if (!pool.TryGetValue(key, out var list))
                {
                    list = new List<(Guid BatchId, decimal AvailableQty)>();
                    pool[key] = list;
                }

                var qty = Math.Round(Math.Abs(item.Quantity), precision);
                if (qty <= 0m) continue;

                var existingIdx = list.FindIndex(x => x.BatchId == item.BatchId.Value);
                if (existingIdx >= 0)
                {
                    list[existingIdx] = (item.BatchId.Value, Math.Round(list[existingIdx].AvailableQty + qty, precision));
                }
                else
                {
                    list.Add((item.BatchId.Value, qty));
                }
            }
        }

        foreach (var se in priorDisassembleEntries)
        {
            if (se.EntryType != StockEntryType.Disassemble
                || se.Status == Core.DocumentStatus.Draft
                || se.Status == Core.DocumentStatus.Cancelled)
            {
                continue;
            }

            foreach (var item in se.Items)
            {
                if (!item.BatchId.HasValue) continue;

                var isFg = item.IsFinishedItem || (item.SourceWarehouseId.HasValue && !item.TargetWarehouseId.HasValue);
                var whId = isFg ? item.SourceWarehouseId : item.TargetWarehouseId;
                if (!whId.HasValue) continue;

                var key = (item.ItemId, whId.Value, isFg);
                if (!pool.TryGetValue(key, out var list)) continue;

                var usedQty = Math.Round(Math.Abs(item.Quantity), precision);
                var existingIdx = list.FindIndex(x => x.BatchId == item.BatchId.Value);
                if (existingIdx >= 0)
                {
                    var remaining = Math.Round(list[existingIdx].AvailableQty - usedQty, precision);
                    list[existingIdx] = (item.BatchId.Value, Math.Max(0m, remaining));
                }
            }
        }

        foreach (var key in pool.Keys.ToList())
        {
            pool[key] = pool[key].Where(x => Math.Round(x.AvailableQty, precision) > 0m).ToList();
        }

        return pool;
    }

    /// <summary>
    /// Allocates batch quantities in order up to requiredQty, rounding at precision, skipping exhausted
    /// batches, and deducting allocated quantities from the pool in-place.
    /// Per ERPNext commits b9121c2188 &amp; 54fcf4abd5 (_set_disassembly_serial_and_batch_bundle).
    /// </summary>
    public static List<(Guid BatchId, decimal Qty)> CollectDisassemblyBatches(
        Dictionary<(Guid ItemId, Guid WarehouseId, bool IsFinishedItem), List<(Guid BatchId, decimal AvailableQty)>> pool,
        Guid itemId,
        Guid warehouseId,
        bool isFinishedItem,
        decimal requiredQty,
        int precision = 4)
    {
        var allocations = new List<(Guid BatchId, decimal Qty)>();
        var remaining = Math.Round(Math.Abs(requiredQty), precision);
        if (remaining <= 0m) return allocations;

        var key = (itemId, warehouseId, isFinishedItem);
        if (!pool.TryGetValue(key, out var list) || list.Count == 0)
            return allocations;

        for (var idx = 0; idx < list.Count && remaining > 0m; idx++)
        {
            var (batchId, available) = list[idx];
            var availRounded = Math.Round(available, precision);
            if (availRounded <= 0m) continue;

            var take = Math.Round(Math.Min(availRounded, remaining), precision);
            if (take <= 0m) continue;

            allocations.Add((batchId, take));
            list[idx] = (batchId, Math.Round(availRounded - take, precision));
            remaining = Math.Round(remaining - take, precision);
        }

        return allocations;
    }

    /// <summary>
    /// Calculates signed row quantity for transactions and serial/batch bundles.
    /// Per ERPNext PR #64866 (commits f841624ed7 and bb5656ba05 / get_sbie_signed_qty):
    /// Bundle totals are positive; return rows store negative quantity, EXCEPT Stock Entry rows
    /// (subcontracting and work order return Stock Entries set is_return but keep positive row qty,
    /// where movement direction is governed by source and target warehouses).
    /// </summary>
    public static decimal GetSignedReturnRowQty(string? documentType, bool isReturn, decimal qty)
    {
        if (isReturn && !string.Equals(documentType, "Stock Entry", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(documentType, "StockEntry", StringComparison.OrdinalIgnoreCase)
                     && qty != 0m)
        {
            return -Math.Abs(qty);
        }

        return qty;
    }

    /// <summary>
    /// Refuses <c>get_items</c> on a Manufacture Stock Entry whose items come from a Job Card
    /// per ERPNext commit cb171c980f (<c>stock_entry.py</c>).
    /// </summary>
    public static void ValidateCanFetchItemsForStockEntry(StockEntryType entryType, string? jobCardNumber)
    {
        if (entryType == StockEntryType.Manufacture && !string.IsNullOrWhiteSpace(jobCardNumber))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Items of this Stock Entry come from Job Card {jobCardNumber}. Use Record Output on the Job Card instead of Get Items.");
        }
    }

    /// <summary>
    /// Validates Repack finished good rows after output rows are marked (<c>IsFinishedItem == true</c>),
    /// requiring <c>SetBasicRateManually == true</c> on EVERY finished good row whenever there is more than
    /// one distinct finished good item per ERPNext commits e2ba9cb546 and 4cbe5b28e3 (<c>validate_repack_entry</c>).
    /// </summary>
    public static void ValidateRepackFinishedGoodManualRates(
        IEnumerable<(Guid ItemId, string ItemCode, bool IsFinishedItem, bool SetBasicRateManually)> rows)
    {
        if (rows == null)
        {
            return;
        }

        var fgRows = rows.Where(r => r.IsFinishedItem).ToList();
        var distinctFgItemCodes = fgRows.Select(r => r.ItemCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (distinctFgItemCodes.Count > 1 && !fgRows.All(r => r.SetBasicRateManually))
        {
            throw new BusinessException(MyERPDomainErrorCodes.RepackMultiFgManualRate)
                .WithData("detail", $"When there are multiple finished goods ({string.Join(", ", distinctFgItemCodes)}) in a Repack stock entry, the basic rate for all finished goods must be set manually.");
        }
    }

    /// <summary>
    /// Applies document-level <paramref name="defaultFromWarehouseId"/> and <paramref name="defaultToWarehouseId"/>
    /// to a Stock Entry row only when the row has neither <paramref name="rowSourceWarehouseId"/> nor
    /// <paramref name="rowTargetWarehouseId"/> set, preserving the warehouses of a duplicated row
    /// per ERPNext commits 6479ca47fe and 535b272814 (<c>stock_entry.js</c>).
    /// </summary>
    public static (Guid? SourceWarehouseId, Guid? TargetWarehouseId) ApplyDefaultWarehousesPreservingExistingRow(
        Guid? rowSourceWarehouseId,
        Guid? rowTargetWarehouseId,
        Guid? defaultFromWarehouseId,
        Guid? defaultToWarehouseId)
    {
        var hasSource = rowSourceWarehouseId.HasValue && rowSourceWarehouseId.Value != Guid.Empty;
        var hasTarget = rowTargetWarehouseId.HasValue && rowTargetWarehouseId.Value != Guid.Empty;

        if (!hasSource && !hasTarget)
        {
            return (defaultFromWarehouseId, defaultToWarehouseId);
        }

        return (rowSourceWarehouseId, rowTargetWarehouseId);
    }

    /// <summary>
    /// Resolves the Stock Entry item warehouse availability indicator color by comparing
    /// <c>Round(TransferQty, transferQtyPrecision)</c> against <paramref name="actualWarehouseQty"/>
    /// per ERPNext commits 98d21bd399 and 7dfeb67e39 (<c>stock_entry.js</c>).
    /// </summary>
    public static string GetStockEntryItemIndicatorColor(
        Guid? sourceWarehouseId,
        decimal transferQty,
        decimal actualWarehouseQty,
        int transferQtyPrecision = 4)
    {
        if (!sourceWarehouseId.HasValue || sourceWarehouseId.Value == Guid.Empty)
        {
            return "blue";
        }

        return Math.Round(transferQty, transferQtyPrecision, MidpointRounding.AwayFromZero) <= actualWarehouseQty
            ? "green"
            : "orange";
    }

    /// <summary>
    /// Recalculates total additional costs on a Stock Entry whenever additional cost rows are added, edited,
    /// duplicated, or deleted (including deleting all rows to 0) per ERPNext commits e52b960933 and 6d27618a69.
    /// </summary>
    public static decimal RecalculateTotalAdditionalCosts(IEnumerable<decimal>? additionalCostBaseAmounts)
    {
        if (additionalCostBaseAmounts == null)
        {
            return 0m;
        }

        return Math.Round(additionalCostBaseAmounts.Sum(), 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Clears any stale <c>PurchaseOrderId</c> reference when mapping or linking a Stock Entry to a
    /// <c>SubcontractingOrder</c> so the Stock Entry does not point to an unrelated Purchase Order
    /// per ERPNext commits 997fae727d and db63792514 (<c>stock_entry.py</c> / <c>stock_entry.js</c>).
    /// </summary>
    public static (Guid? SubcontractingOrderId, Guid? PurchaseOrderId) ClearPurchaseOrderWhenMappingSubcontractingOrder(
        Guid? subcontractingOrderId,
        Guid? existingPurchaseOrderId)
    {
        if (subcontractingOrderId.HasValue && subcontractingOrderId.Value != Guid.Empty)
        {
            return (subcontractingOrderId, null);
        }

        return (subcontractingOrderId, existingPurchaseOrderId);
    }

    /// <summary>
    /// Preserves a finished good row's own quantity when a row is duplicated or added rather than overwriting
    /// every finished good row's quantity with <c>FgCompletedQty</c> when finished good rows already carry
    /// explicit quantities per ERPNext commit a4f4ba61f0 (<c>stock_entry.js</c>).
    /// </summary>
    public static decimal ResolveFinishedGoodRowQtyOnAddOrDuplicate(
        bool isFinishedItem,
        decimal existingRowQty,
        decimal fgCompletedQty)
    {
        if (!isFinishedItem)
        {
            return existingRowQty;
        }

        return existingRowQty > 0m ? existingRowQty : fgCompletedQty;
    }

    /// <summary>
    /// Computes the pending finished-goods quantity that a Job-Card-transferred raw material in WIP covers,
    /// sharing consumption across alternative items and warehouses (keyed by <c>OriginalItemId ?? ItemId</c>)
    /// and counting each Manufacture entry once even when the material is used by multiple operations
    /// per ERPNext PR #67406 (commits cedfcdf739, 2c51379659, 2eddd25c6c, 07685bf1f9).
    /// </summary>
    public static decimal CalculateJobCardMaterialPendingQty(
        Guid bomItemId,
        decimal workOrderQty,
        decimal workOrderTransferredQty,
        decimal totalRequiredQtyForItem,
        decimal totalTransferredQtyForItem,
        IEnumerable<(Guid ItemId, Guid? OriginalItemId, IReadOnlyDictionary<Guid, decimal> ConsumedForByManufactureEntry)> availableMaterials)
    {
        var consumedForByEntry = new Dictionary<Guid, decimal>();
        foreach (var row in availableMaterials ?? Array.Empty<(Guid, Guid?, IReadOnlyDictionary<Guid, decimal>)>())
        {
            if ((row.OriginalItemId ?? row.ItemId) != bomItemId)
                continue;
            foreach (var (entryId, fgQty) in row.ConsumedForByManufactureEntry)
            {
                consumedForByEntry[entryId] = fgQty;
            }
        }

        var transferredForQty = totalRequiredQtyForItem > 0m
            ? (totalTransferredQtyForItem * workOrderQty) / totalRequiredQtyForItem
            : workOrderQty;

        // Material moved in excess is spread over the order qty
        transferredForQty = Math.Min(transferredForQty, Math.Max(workOrderQty, workOrderTransferredQty));
        return transferredForQty - consumedForByEntry.Values.Sum();
    }

    /// <summary>
    /// Computes transfer-based raw material consumption qty for a partial Manufacture entry
    /// per ERPNext PR #67430 (commit a88ca080d7: <c>_append_transfer_based_rm</c>).
    /// Rounds to 9 decimal places to drop floating-point noise (so 7.000000000000001 is not ceiled to 8)
    /// while still ceiling genuine fractional whole-number shares (e.g. 0.004 -> 1) below qty precision.
    /// </summary>
    public static decimal CalculateTransferBasedRawMaterialQty(
        decimal availableWipQty,
        decimal fgCompletedQty,
        decimal pendingQtyToMfg,
        bool isReturn,
        bool uomMustBeWholeNumber)
    {
        if (isReturn)
        {
            return uomMustBeWholeNumber ? Math.Ceiling(Math.Round(availableWipQty, 9, MidpointRounding.AwayFromZero)) : availableWipQty;
        }

        if (pendingQtyToMfg <= 0m)
        {
            return 0m;
        }

        var qty = Math.Round((availableWipQty * fgCompletedQty) / pendingQtyToMfg, 9, MidpointRounding.AwayFromZero);
        return uomMustBeWholeNumber ? Math.Ceiling(qty) : qty;
    }

    /// <summary>
    /// Orders Manufacture Stock Entry incoming rows for valuation per ERPNext PR #67449 (commit 6ef895beb4):
    /// 1. Own-cost secondary rows (ValuationRate / Manual / BOM-less scrap)
    /// 2. Sub-assembly percentage secondary rows (valued from their sub-assembly BOM's consumed components)
    /// 3. Top-BOM percentage secondary rows
    /// 4. Finished good rows last
    /// </summary>
    public static List<StockEntryItem> OrderManufactureIncomingRows(
        IEnumerable<StockEntryItem> items,
        IReadOnlySet<Guid>? subAssemblySecondaryItemIds = null)
    {
        return (items ?? Array.Empty<StockEntryItem>())
            .OrderBy(r => r.IsFinishedItem ? 1 : 0)
            .ThenBy(r => IsCostedOut(r, subAssemblySecondaryItemIds) ? 0 : 1)
            .ThenBy(r => IsCostedOutOfFinishedItem(r) ? 0 : 1)
            .ToList();
    }

    /// <summary>
    /// Whether a row is deducted from the top BOM's cost pool: either an own-cost secondary item
    /// or a percentage secondary item originating from a sub-assembly BOM (PR #67449).
    /// </summary>
    public static bool IsCostedOut(StockEntryItem row, IReadOnlySet<Guid>? subAssemblySecondaryItemIds = null) =>
        IsCostedOutOfFinishedItem(row)
        || (subAssemblySecondaryItemIds != null
            && row.BomSecondaryItemId.HasValue
            && subAssemblySecondaryItemIds.Contains(row.BomSecondaryItemId.Value));

    /// <summary>
    /// Total cost of rows deducted from the top BOM's cost pool (own-cost + sub-assembly secondary rows, PR #67449).
    /// </summary>
    public static decimal GetCostedOutItemsCost(
        IEnumerable<StockEntryItem> items,
        IReadOnlySet<Guid>? subAssemblySecondaryItemIds) =>
        items.Where(d => IsCostedOut(d, subAssemblySecondaryItemIds) && !d.AllowZeroValuationRate)
            .Sum(d => d.Quantity * (d.ValuationRate ?? 0m));

    /// <summary>
    /// Computes the net consumed component cost basis for a sub-assembly BOM's percentage co-product
    /// per ERPNext PR #67449 (commits 6ef895beb4, 30162fa85a, 2fa4b9d93d, 7ad2c28e3b, 7f35a5287b):
    /// - Scales by <paramref name="fgCompletedQty"/> (not the secondary row's qty, which process loss/rounding alter).
    /// - Divides sub-assemblies made by the sub-assembly BOM's exploded good qty (ignoring process loss for phantom BOMs).
    /// - Deducts the sub-assembly BOM's own-cost secondary rows (ValuationRate / Manual).
    /// </summary>
    public static decimal CalculateSubAssemblyComponentCost(
        decimal subAssemblySecondaryQtyPerBom,
        decimal subAssemblySecondaryQtyPerFinishedGood,
        decimal fgCompletedQty,
        decimal subAssemblyBomQuantity,
        decimal subAssemblyBomProcessLossQty,
        bool subAssemblyBomIsPhantom,
        IEnumerable<(Guid ComponentItemId, decimal StockQtyPerSubBom)> subAssemblyExplodedComponents,
        IReadOnlyDictionary<Guid, decimal> consumedRatesByBomItemId,
        decimal subAssemblyOwnCostSecondaryValue = 0m,
        int precision = 4)
    {
        if (subAssemblySecondaryQtyPerBom <= 0m || fgCompletedQty <= 0m)
        {
            return 0m;
        }

        var bomQty = subAssemblyBomQuantity > 0m ? subAssemblyBomQuantity : 1m;
        decimal explodedGoodQty;
        if (subAssemblyBomIsPhantom)
        {
            explodedGoodQty = bomQty;
        }
        else
        {
            var good = bomQty - subAssemblyBomProcessLossQty;
            explodedGoodQty = good > 0m ? good : bomQty;
        }

        var subAssembliesMade = (subAssemblySecondaryQtyPerFinishedGood * fgCompletedQty * bomQty) / subAssemblySecondaryQtyPerBom;
        var bomsMade = subAssembliesMade / explodedGoodQty;

        var componentCost = 0m;
        foreach (var (compItemId, stockQty) in subAssemblyExplodedComponents ?? Array.Empty<(Guid, decimal)>())
        {
            var rate = consumedRatesByBomItemId.GetValueOrDefault(compItemId, 0m);
            componentCost += stockQty * bomsMade * rate;
        }

        // Per ERPNext PR #69225: own-cost rows can exceed posted components; net basis never goes below 0
        return Math.Round(Math.Max(componentCost - subAssemblyOwnCostSecondaryValue, 0m), precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes the output quantity of a BOM secondary item row for <paramref name="fgCompletedQty"/>,
    /// applying the BOM's rounded <paramref name="bomRowProcessLossQty"/> ratio
    /// (<c>(bomRowStockQty - bomRowProcessLossQty) / bomRowStockQty</c>), ceiling whole-number UOMs
    /// after 9-decimal float-noise rounding, and rounding to <paramref name="qtyPrecision"/>
    /// so rows that round to <c>0</c> are omitted.
    /// Per ERPNext PR #69225 (<c>manufacturing.py: get_secondary_item_qty</c>).
    /// </summary>
    public static decimal CalculateSecondaryItemQty(
        decimal rowQtyPerFg,
        decimal fgCompletedQty,
        decimal bomRowStockQty,
        decimal bomRowProcessLossQty,
        bool stockUomMustBeWholeNumber,
        int qtyPrecision = 4)
    {
        var qty = rowQtyPerFg * fgCompletedQty;
        if (bomRowStockQty > 0m)
        {
            qty = qty * (bomRowStockQty - bomRowProcessLossQty) / bomRowStockQty;
        }

        if (stockUomMustBeWholeNumber && qty > 0m)
        {
            qty = Math.Ceiling(Math.Round(qty, 9, MidpointRounding.AwayFromZero));
        }

        return Math.Round(qty, qtyPrecision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Recursively collects secondary items across multi-level sub-assembly BOMs accounting for each
    /// sub-assembly's process loss (<c>qtyMade = subAssemblyQty * (bomQty / explodedGoodQty)</c>),
    /// grouping child rows by sub-assembly BOM and merging duplicate <c>BomSecondaryItemId</c> rows
    /// across paths before rounding.
    /// Per ERPNext PR #69225 (<c>get_secondary_items_from_sub_assemblies</c> / <c>merge_secondary_items_by_bom_row</c>).
    /// </summary>
    public static List<(Guid BomSecondaryItemId, Guid SourceBomId, Guid ItemId, decimal ScaledQty)>
        ScaleAndMergeSubAssemblySecondaryItemsWithProcessLoss(
            Guid rootBomId,
            decimal rootQty,
            IReadOnlyDictionary<Guid, (decimal BomQuantity, decimal ProcessLossQty, bool IsPhantomBom)>? bomYieldByBomId,
            IReadOnlyDictionary<Guid, IReadOnlyList<(Guid SubBomId, decimal QtyPerParentUnit)>>? subAssembliesByBom,
            IReadOnlyDictionary<Guid, IReadOnlyList<(Guid BomSecondaryItemId, Guid ItemId, decimal QtyPerSubBomUnit)>>? secondaryItemsByBom)
    {
        var merged = new Dictionary<Guid, (Guid BomSecondaryItemId, Guid SourceBomId, Guid ItemId, decimal ScaledQty)>();
        if (rootQty <= 0m || subAssembliesByBom == null)
        {
            return new List<(Guid, Guid, Guid, decimal)>();
        }

        var visited = new HashSet<Guid> { rootBomId };
        Collect(rootBomId, rootQty);
        return merged.Values.ToList();

        void Collect(Guid bomId, decimal parentQty)
        {
            if (!subAssembliesByBom.TryGetValue(bomId, out var children))
                return;

            // Group by sub-assembly BOM, not item: two BOMs of one sub-assembly have their own yield
            var qtyPerSubBom = new Dictionary<Guid, decimal>();
            foreach (var (subBomId, qtyPerUnit) in children)
            {
                var subQty = parentQty * qtyPerUnit;
                if (subQty <= 0m) continue;
                qtyPerSubBom[subBomId] = qtyPerSubBom.GetValueOrDefault(subBomId, 0m) + subQty;
            }

            foreach (var (subBomId, subAssemblyQty) in qtyPerSubBom)
            {
                var bomQty = 1m;
                var processLossQty = 0m;
                var isPhantom = false;
                if (bomYieldByBomId != null && bomYieldByBomId.TryGetValue(subBomId, out var yieldInfo))
                {
                    bomQty = yieldInfo.BomQuantity > 0m ? yieldInfo.BomQuantity : 1m;
                    processLossQty = yieldInfo.ProcessLossQty;
                    isPhantom = yieldInfo.IsPhantomBom;
                }

                var explodedGoodQty = isPhantom
                    ? bomQty
                    : (bomQty - processLossQty > 0m ? bomQty - processLossQty : bomQty);
                var qtyMade = subAssemblyQty * (bomQty / explodedGoodQty);

                if (secondaryItemsByBom != null && secondaryItemsByBom.TryGetValue(subBomId, out var secs))
                {
                    foreach (var (secRowId, itemId, secPerUnit) in secs)
                    {
                        var scaled = qtyMade * secPerUnit;
                        if (scaled <= 0m) continue;
                        if (merged.TryGetValue(secRowId, out var existing))
                        {
                            merged[secRowId] = (secRowId, existing.SourceBomId, existing.ItemId, existing.ScaledQty + scaled);
                        }
                        else
                        {
                            merged[secRowId] = (secRowId, subBomId, itemId, scaled);
                        }
                    }
                }

                if (visited.Add(subBomId))
                {
                    Collect(subBomId, qtyMade);
                    visited.Remove(subBomId);
                }
            }
        }
    }

    /// <summary>
    /// Computes consumed component rates for sub-assembly co-product valuation dividing each component's
    /// posted value by <c>max(consumedQty, requiredQty)</c> so rounding shortfalls in consumed qty cannot
    /// allocate more cost to a sub-assembly than was actually posted.
    /// Per ERPNext PR #69225 (<c>stock_entry.py: get_consumed_rates</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> CalculateConsumedComponentRatesCappedByBomRequirement(
        IEnumerable<(Guid BomItemId, decimal TransferQty, decimal BasicAmount)>? consumedRows,
        IReadOnlyDictionary<Guid, decimal>? requiredQtyByBomItemId)
    {
        var qty = new Dictionary<Guid, decimal>();
        var value = new Dictionary<Guid, decimal>();
        if (consumedRows != null)
        {
            foreach (var row in consumedRows)
            {
                qty[row.BomItemId] = qty.GetValueOrDefault(row.BomItemId, 0m) + row.TransferQty;
                value[row.BomItemId] = value.GetValueOrDefault(row.BomItemId, 0m) + row.BasicAmount;
            }
        }

        var rates = new Dictionary<Guid, decimal>();
        foreach (var (itemId, consumedQty) in qty)
        {
            if (consumedQty == 0m) continue;
            var requiredQty = requiredQtyByBomItemId != null
                ? requiredQtyByBomItemId.GetValueOrDefault(itemId, 0m)
                : 0m;
            var denom = Math.Max(consumedQty, requiredQty);
            if (denom != 0m)
            {
                rates[itemId] = value[itemId] / denom;
            }
        }

        return rates;
    }

    /// <summary>
    /// Computes the default Finished Good Quantity to propose for a new Stock Entry against a Work Order
    /// based on the Stock Entry purpose per ERPNext PR #67413 (<c>get_pending_fg_qty</c>):
    /// - <c>Disassemble</c>: <c>producedQty - disassembledQty</c>
    /// - <c>MaterialTransferForManufacture</c>: <c>woQty - materialTransferredQty</c>
    /// - <c>Manufacture</c>: <c>min(transferredQty or woQty, woQty) - producedQty - processLossQty</c> (skipping transfer cap when <paramref name="skipTransfer"/> is true)
    /// - Other purposes: <c>woQty - producedQty</c>
    /// Always clamped at <c>&gt;= 0</c>.
    /// </summary>
    public static decimal CalculatePendingFgQtyForPurpose(
        decimal woQty,
        decimal producedQty,
        decimal disassembledQty,
        decimal materialTransferredQty,
        decimal processLossQty,
        bool skipTransfer,
        StockEntryType purpose,
        int precision = 4)
    {
        decimal qty;
        switch (purpose)
        {
            case StockEntryType.Disassemble:
                qty = producedQty - disassembledQty;
                break;
            case StockEntryType.MaterialTransferForManufacture:
                qty = woQty - materialTransferredQty;
                break;
            case StockEntryType.Manufacture:
            {
                var transferredQty = skipTransfer ? 0m : materialTransferredQty;
                // Extra material transferred is no allowance to produce more than the Work Order qty
                var plannedQty = Math.Min(transferredQty > 0m ? transferredQty : woQty, woQty);
                qty = plannedQty - producedQty - processLossQty;
                break;
            }
            default:
                qty = woQty - producedQty;
                break;
        }

        return Math.Max(Math.Round(qty, precision, MidpointRounding.AwayFromZero), 0m);
    }

    /// <summary>
    /// Computes the total secondary-item cost already taken within a nested sub-assembly BOM
    /// (both its own Manual/ValuationRate secondary items AND its proportional share of inner
    /// sub-assembly secondary items) so an outer sub-assembly percentage co-product is valued net
    /// of inner co-products.
    /// Per ERPNext PR #68832 (<c>get_secondary_items_cost_within</c>).
    /// </summary>
    public static decimal CalculateNestedSubAssemblySecondaryCostWithin(
        Guid subBomId,
        decimal subAssembliesMade,
        decimal fgCompletedQty,
        IReadOnlyDictionary<Guid, decimal>? qtyWithinSubBomBySecondaryId,
        IReadOnlyDictionary<Guid, decimal>? qtyPerFinishedGoodBySecondaryId,
        IEnumerable<(Guid? BomSecondaryItemId, Guid? ParentBomId, bool IsCostedOutOfFinishedItem, decimal BasicAmount)>? entryItems,
        int precision = 4)
    {
        if (entryItems == null)
        {
            return 0m;
        }

        var total = 0m;
        foreach (var d in entryItems)
        {
            if (d.BomSecondaryItemId.HasValue
                && qtyWithinSubBomBySecondaryId != null
                && qtyWithinSubBomBySecondaryId.TryGetValue(d.BomSecondaryItemId.Value, out var qtyWithin)
                && qtyWithin > 0m)
            {
                var qtyPerFg = qtyPerFinishedGoodBySecondaryId != null
                    ? qtyPerFinishedGoodBySecondaryId.GetValueOrDefault(d.BomSecondaryItemId.Value, 0m)
                    : 0m;
                var totalQty = qtyPerFg * fgCompletedQty;
                if (totalQty > 0m)
                {
                    total += d.BasicAmount * qtyWithin * subAssembliesMade / totalQty;
                }
            }
            else if (d.BomSecondaryItemId.HasValue
                && d.IsCostedOutOfFinishedItem
                && d.ParentBomId == subBomId)
            {
                total += d.BasicAmount;
            }
        }

        return Math.Round(total, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Proposes the pending Finished Good quantity still to consume for a Material Consumption for Manufacture
    /// entry when backflushing based on BOM: takes the minimum FG qty covered by remaining materials,
    /// capped at what is left to produce, and floors at <paramref name="precision"/> so the proposal never
    /// asks for more material than remains.
    /// Per ERPNext PR #67348 (<c>get_pending_consumption_qty_based_on_bom</c>).
    /// </summary>
    public static decimal CalculatePendingConsumptionQtyBasedOnBom(
        decimal workOrderQty,
        decimal producedQty,
        decimal processLossQty,
        bool skipTransfer,
        IEnumerable<(Guid ItemId, decimal RequiredQty, decimal TransferredQty, decimal ConsumedQty, bool IncludeInManufacturing)>? requiredItems,
        int precision = 4)
    {
        if (workOrderQty <= 0m || requiredItems == null)
        {
            return 0m;
        }

        var materials = new Dictionary<Guid, (decimal LeftQty, decimal RequiredQty)>();
        foreach (var row in requiredItems)
        {
            if (!row.IncludeInManufacturing || row.RequiredQty <= 0m)
                continue;

            var basis = skipTransfer ? row.RequiredQty : row.TransferredQty;
            var left = basis - row.ConsumedQty;
            var prev = materials.GetValueOrDefault(row.ItemId);
            materials[row.ItemId] = (prev.LeftQty + left, prev.RequiredQty + row.RequiredQty);
        }

        if (materials.Count == 0)
        {
            return 0m;
        }

        var leftForQty = materials.Values.Min(m => m.LeftQty * workOrderQty / m.RequiredQty);
        var pendingToProduce = workOrderQty - producedQty - processLossQty;
        var candidate = Math.Min(leftForQty, pendingToProduce);

        // Floor at precision after 9-decimal pre-round so proposal never exceeds remaining material
        var step = (decimal)Math.Pow(10, precision);
        var floored = Math.Floor(Math.Round(candidate * step, 9, MidpointRounding.AwayFromZero)) / step;
        return Math.Max(floored, 0m);
    }

    /// <summary>
    /// Proposes the pending Finished Good quantity still to consume for a Material Consumption for Manufacture
    /// entry when backflushing based on Material Transferred for Manufacture.
    /// Per ERPNext PR #67348 (<c>get_pending_consumption_qty_based_on_transfer</c>).
    /// </summary>
    public static decimal CalculatePendingConsumptionQtyBasedOnTransfer(
        decimal materialTransferredQty,
        decimal producedQty,
        decimal processLossQty,
        IEnumerable<(Guid ItemId, decimal TransferredQty, decimal ConsumedQty)>? requiredItems,
        IReadOnlyDictionary<Guid, decimal>? coveredFgQtyByItemId,
        int precision = 4)
    {
        var pendingToProduce = materialTransferredQty - producedQty - processLossQty;
        if (pendingToProduce <= 0m || requiredItems == null)
        {
            return 0m;
        }

        var maxPending = 0m;
        foreach (var row in requiredItems)
        {
            if (Math.Round(row.TransferredQty - row.ConsumedQty, precision, MidpointRounding.AwayFromZero) <= 0m)
                continue;

            var covered = coveredFgQtyByItemId != null
                ? coveredFgQtyByItemId.GetValueOrDefault(row.ItemId, 0m)
                : 0m;
            var uncovered = Math.Max(Math.Round(materialTransferredQty - covered, precision, MidpointRounding.AwayFromZero), 0m);
            var itemPending = uncovered > 0m ? uncovered : pendingToProduce;
            if (itemPending > maxPending)
            {
                maxPending = itemPending;
            }
        }

        return Math.Max(Math.Round(Math.Min(maxPending, pendingToProduce), precision, MidpointRounding.AwayFromZero), 0m);
    }

    /// <summary>
    /// Recursively collects and scales secondary items (co-products/scrap) of every nested sub-assembly BOM
    /// under <paramref name="rootBomId"/> by the cumulative parent sub-assembly quantity at each level.
    /// Per ERPNext commit <c>05b5ca5548</c> (<c>get_secondary_items_from_sub_assemblies</c>).
    /// </summary>
    public static List<(Guid SourceBomId, Guid ItemId, decimal ScaledQty)> ScaleNestedSubAssemblySecondaryItems(
        Guid rootBomId,
        decimal rootQty,
        IReadOnlyDictionary<Guid, IReadOnlyList<(Guid SubBomId, decimal QtyPerParentUnit)>>? subAssembliesByBom,
        IReadOnlyDictionary<Guid, IReadOnlyList<(Guid ItemId, decimal QtyPerUnit)>>? secondaryItemsByBom,
        int precision = 4)
    {
        var result = new List<(Guid SourceBomId, Guid ItemId, decimal ScaledQty)>();
        if (rootQty <= 0m || subAssembliesByBom == null)
        {
            return result;
        }

        var visited = new HashSet<Guid> { rootBomId };
        Collect(rootBomId, rootQty);
        return result;

        void Collect(Guid bomId, decimal parentQty)
        {
            if (!subAssembliesByBom.TryGetValue(bomId, out var children))
                return;

            foreach (var (subBomId, qtyPerUnit) in children)
            {
                var subQty = parentQty * qtyPerUnit;
                if (subQty <= 0m) continue;

                if (secondaryItemsByBom != null && secondaryItemsByBom.TryGetValue(subBomId, out var secs))
                {
                    foreach (var (itemId, secPerUnit) in secs)
                    {
                        var scaled = Math.Round(subQty * secPerUnit, precision, MidpointRounding.AwayFromZero);
                        if (scaled > 0m)
                        {
                            result.Add((subBomId, itemId, scaled));
                        }
                    }
                }

                if (visited.Add(subBomId))
                {
                    Collect(subBomId, subQty);
                    visited.Remove(subBomId);
                }
            }
        }
    }

    /// <summary>
    /// Computes the net component cost basis available for percentage co-products of <paramref name="bomId"/>:
    /// deducts <paramref name="bomId"/>'s own <c>ValuationRate</c>/<c>Manual</c> secondary items AND every
    /// secondary item belonging to sub-assembly BOMs nested underneath <paramref name="bomId"/> (ignoring
    /// secondary items of unrelated sibling BOMs), matching inner-BOM-first deduction order.
    /// Per ERPNext commits <c>7df8a18e13</c> and <c>7949464d7e</c> (<c>get_secondary_items_cost_within</c>).
    /// </summary>
    public static decimal CalculateOuterCoProductNetCostBasis(
        Guid bomId,
        decimal grossComponentCost,
        IReadOnlySet<Guid>? subAssemblyBomIdsUnderBom,
        IEnumerable<(Guid SourceBomId, Manufacturing.SecondaryItemValuationType ValuationType, decimal BasicAmount)>? secondaryRows,
        int precision = 4)
    {
        var deduction = 0m;
        if (secondaryRows != null)
        {
            foreach (var row in secondaryRows)
            {
                var isUnderSubAssembly = subAssemblyBomIdsUnderBom != null && subAssemblyBomIdsUnderBom.Contains(row.SourceBomId);
                var isOwnCostedOut = row.SourceBomId == bomId
                    && row.ValuationType is Manufacturing.SecondaryItemValuationType.ValuationRate
                        or Manufacturing.SecondaryItemValuationType.Manual;

                if (isUnderSubAssembly || isOwnCostedOut)
                {
                    deduction += row.BasicAmount;
                }
            }
        }

        return Math.Round(Math.Max(grossComponentCost - deduction, 0m), precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes transfer-based Manufacture raw-material share: drops floating-point noise at 9 decimals
    /// (so <c>7.000000000000001</c> is not ceiled to <c>8</c>) while keeping small positive shares below
    /// qty precision (<c>0.004</c>) alive so whole-number UOMs still ceil them to <c>1</c>, then suppresses
    /// rows whose final qty rounds to <c>0</c> at <paramref name="qtyPrecision"/>.
    /// Per ERPNext commit <c>a88ca080d7</c> (<c>ManufactureStockEntry</c> in <c>manufacturing.py</c>).
    /// </summary>
    public static decimal CalculateTransferBasedManufactureRawMaterialQty(
        decimal rowQty,
        decimal fgCompletedQty,
        decimal pendingQtyToManufacture,
        bool isReturn,
        bool uomMustBeWholeNumber,
        int qtyPrecision = 4)
    {
        if (!isReturn && pendingQtyToManufacture <= 0m)
        {
            return 0m;
        }

        var rawQty = isReturn
            ? rowQty
            : Math.Round((rowQty * fgCompletedQty) / pendingQtyToManufacture, 9, MidpointRounding.AwayFromZero);

        var finalQty = uomMustBeWholeNumber && rawQty > 0m
            ? Math.Ceiling(rawQty)
            : rawQty;

        return Math.Round(finalQty, qtyPrecision, MidpointRounding.AwayFromZero) == 0m
            ? 0m
            : finalQty;
    }

    /// <summary>
    /// Determines whether submitting a Stock Entry against a <c>Closed</c> Work Order must be rejected:
    /// refuses <see cref="StockEntryType.Manufacture"/>, <see cref="StockEntryType.MaterialConsumptionForManufacture"/>
    /// (even when flagged as return), and non-return <see cref="StockEntryType.MaterialTransferForManufacture"/>;
    /// permits ONLY return-flagged <see cref="StockEntryType.MaterialTransferForManufacture"/>.
    /// Per ERPNext commit <c>7faed55958</c> (<c>BaseStockEntry</c> in <c>stock_entry_base.py</c>).
    /// </summary>
    public static bool ShouldRejectStockEntryAgainstClosedWorkOrder(
        bool isWorkOrderClosed,
        StockEntryType entryType,
        bool isReturn)
    {
        if (!isWorkOrderClosed)
        {
            return false;
        }

        if (entryType == StockEntryType.MaterialTransferForManufacture && isReturn)
        {
            return false;
        }

        return entryType is StockEntryType.MaterialTransferForManufacture
            or StockEntryType.Manufacture
            or StockEntryType.MaterialConsumptionForManufacture;
    }

    /// <summary>
    /// Evaluates whether a stock ledger repost can skip or stop early at a subsequent entry:
    /// skips vouchers cancelled during the repost (leaving their ledger alone), and stops reposting
    /// at a non-cancelled Stock Reconciliation (which resets qty &amp; valuation) or once both
    /// <c>QtyAfterTransaction</c> and <c>StockValue</c> match the previously stored ledger values.
    /// Per ERPNext commits <c>41d5d3c132</c> and <c>2ec98826fc</c>.
    /// </summary>
    public static (bool SkipVoucher, bool StopRepost) EvaluateStockRepostEntryAction(
        bool isVoucherCancelled,
        bool isStockReconciliation,
        decimal previousQtyAfterTx,
        decimal newQtyAfterTx,
        decimal previousStockValue,
        decimal newStockValue,
        int precision = 4)
    {
        if (isVoucherCancelled)
        {
            return (SkipVoucher: true, StopRepost: false);
        }

        if (isStockReconciliation)
        {
            return (SkipVoucher: false, StopRepost: true);
        }

        var unchanged = Math.Round(previousQtyAfterTx, precision, MidpointRounding.AwayFromZero)
                == Math.Round(newQtyAfterTx, precision, MidpointRounding.AwayFromZero)
            && Math.Round(previousStockValue, precision, MidpointRounding.AwayFromZero)
                == Math.Round(newStockValue, precision, MidpointRounding.AwayFromZero);

        return (SkipVoucher: false, StopRepost: unchanged);
    }

    /// <summary>
    /// Blocks cancelling a Subcontracting Stock Entry (either <see cref="StockEntryType.SendToSubcontractor"/>
    /// or a reserved-components return) when a later entry against the same Subcontracting Order has already
    /// moved the stock this entry reserved (an active submitted <c>StockReservationEntry</c> references this entry
    /// via <c>from_voucher_no</c>).
    /// Per ERPNext commit <c>d12484abad</c> (<c>validate_reserved_stock_not_moved_on</c> in <c>stock_entry.py</c>).
    /// </summary>
    public static void ValidateReservedStockNotMovedOn(
        StockEntryType entryType,
        bool isReservedComponentsReturn,
        bool hasLaterActiveReservationFromThisEntry,
        string? subcontractingOrderNumber = null)
    {
        if ((entryType == StockEntryType.SendToSubcontractor || isReservedComponentsReturn)
            && hasLaterActiveReservationFromThisEntry)
        {
            var orderLabel = string.IsNullOrWhiteSpace(subcontractingOrderNumber)
                ? "the Subcontracting Order"
                : subcontractingOrderNumber;
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"A later entry against {orderLabel} moved the stock this entry reserved. Cancel it first.");
        }
    }

    /// <summary>
    /// Resolves the quantity passed to serial/batch assignment during transfer-based manufacture backflush:
    /// uses the post-rounding / whole-number-ceilinged row quantity (computed by
    /// <see cref="CalculateTransferBasedManufactureRawMaterialQty"/>) rather than the unrounded proportional share,
    /// so a whole-number UOM row rounded up to 2 picks 2 serial numbers instead of 1.
    /// Per ERPNext commit <c>94d3fa6598</c> (<c>stock_entry/services/manufacturing.py</c>).
    /// </summary>
    public static decimal ResolveBackflushSerialBatchAssignmentQty(
        decimal rowQty,
        decimal fgCompletedQty,
        decimal pendingQtyToManufacture,
        bool isReturn,
        bool uomMustBeWholeNumber,
        int qtyPrecision = 4)
    {
        return CalculateTransferBasedManufactureRawMaterialQty(
            rowQty,
            fgCompletedQty,
            pendingQtyToManufacture,
            isReturn,
            uomMustBeWholeNumber,
            qtyPrecision);
    }

    /// <summary>
    /// Refuses a Manufacture Stock Entry whose <paramref name="fgCompletedQty"/> exceeds the
    /// remaining qty covered by material transferred to the Work Order (plus overproduction allowance)
    /// so BOM backflush cannot steal another order's stock from a shared WIP warehouse.
    /// Per ERPNext PR #67354 (<c>ManufactureStockEntry.validate_qty_against_transferred_qty</c>).
    /// </summary>
    public static void ValidateManufactureQtyAgainstTransferredQty(
        decimal fgCompletedQty,
        decimal workOrderQty,
        decimal transferredQty,
        decimal producedQty,
        decimal processLossQty,
        string workOrderNumber,
        bool skipTransfer = false,
        bool trackSemiFinishedGoods = false,
        string? transferMaterialAgainst = null,
        decimal overproductionAllowancePercentage = 0m,
        int qtyPrecision = 4)
    {
        if (skipTransfer
            || trackSemiFinishedGoods
            || string.Equals(transferMaterialAgainst, "Job Card", StringComparison.OrdinalIgnoreCase)
            || transferredQty <= 0m)
        {
            return;
        }

        var allowedQty = Math.Round(
            transferredQty + workOrderQty * overproductionAllowancePercentage / 100m - producedQty - processLossQty,
            qtyPrecision,
            MidpointRounding.AwayFromZero);
        var roundedFg = Math.Round(fgCompletedQty, qtyPrecision, MidpointRounding.AwayFromZero);

        if (roundedFg > allowedQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"For Quantity ({roundedFg}) cannot be more than {allowedQty}, the qty transferred to Work Order {workOrderNumber} that is not yet produced.");
        }
    }

    /// <summary>
    /// Resolves target warehouse for a Manufacture secondary item row: routes <c>Scrap</c> to the
    /// Work Order's scrap warehouse when configured (for both BOM and Job Card scrap rows), else
    /// falls back to the Stock Entry's target warehouse.
    /// Per ERPNext PR #67394 (<c>BaseManufactureStockEntry.get_secondary_item_warehouse</c>).
    /// </summary>
    public static Guid? ResolveSecondaryItemTargetWarehouse(
        Manufacturing.SecondaryItemType secondaryItemType,
        Guid? workOrderScrapWarehouseId,
        Guid? stockEntryTargetWarehouseId)
    {
        if (secondaryItemType == Manufacturing.SecondaryItemType.Scrap && workOrderScrapWarehouseId.HasValue)
            return workOrderScrapWarehouseId;

        return stockEntryTargetWarehouseId;
    }

    /// <summary>
    /// Filters out BOM-estimated secondary items whose <c>(ItemId, SecondaryItemType)</c> was already
    /// recorded on Job Cards so Manufacture entries do not double-count scrap/co-products.
    /// Per ERPNext PR #67394 (<c>ManufactureStockEntry.get_bom_secondary_items</c>).
    /// </summary>
    public static List<(Guid ItemId, Manufacturing.SecondaryItemType SecondaryItemType, decimal Qty)> FilterBomSecondaryItemsNotRecordedOnJobCard(
        IEnumerable<(Guid ItemId, Manufacturing.SecondaryItemType SecondaryItemType, decimal Qty)>? bomSecondaryItems,
        IEnumerable<(Guid ItemId, Manufacturing.SecondaryItemType SecondaryItemType)>? jobCardSecondaryItems)
    {
        if (bomSecondaryItems == null)
            return new List<(Guid, Manufacturing.SecondaryItemType, decimal)>();

        var recorded = (jobCardSecondaryItems ?? Enumerable.Empty<(Guid, Manufacturing.SecondaryItemType)>())
            .ToHashSet();

        return bomSecondaryItems
            .Where(r => !recorded.Contains((r.ItemId, r.SecondaryItemType)))
            .ToList();
    }

    /// <summary>
    /// Takes serial numbers and batch quantities up to <paramref name="requestedQty"/> from mutable
    /// reserved entries (FIFO order), filtering by <paramref name="restrictedSerialNos"/> or
    /// <paramref name="restrictedBatchNo"/> when specified, and decrementing available qty in-place so
    /// subsequent rows on the same Stock Entry do not over-allocate pending customer batches.
    /// Per ERPNext PR #67503 (<c>serial_batch.py: take_reserved_entries</c>).
    /// </summary>
    public static (List<string> TakenSerialNos, Dictionary<string, decimal> TakenBatches) TakeReservedSerialAndBatchEntries(
        IList<ReservedSerialBatchSlot> reservedEntries,
        decimal requestedQty,
        IReadOnlyCollection<string>? restrictedSerialNos = null,
        string? restrictedBatchNo = null,
        int qtyPrecision = 4)
    {
        var takenSerials = new List<string>();
        var takenBatches = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (reservedEntries == null || Math.Round(requestedQty, qtyPrecision, MidpointRounding.AwayFromZero) <= 0m)
            return (takenSerials, takenBatches);

        var serialFilter = restrictedSerialNos != null && restrictedSerialNos.Count > 0
            ? new HashSet<string>(restrictedSerialNos, StringComparer.OrdinalIgnoreCase)
            : null;
        var takenSerialSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remaining = requestedQty;

        foreach (var entry in reservedEntries)
        {
            if (Math.Round(remaining, qtyPrecision, MidpointRounding.AwayFromZero) <= 0m)
                break;

            if (Math.Round(entry.AvailableQty, qtyPrecision, MidpointRounding.AwayFromZero) <= 0m)
                continue;

            if (!string.IsNullOrEmpty(entry.SerialNo) && takenSerialSet.Contains(entry.SerialNo))
                continue;

            if (serialFilter != null && (string.IsNullOrEmpty(entry.SerialNo) || !serialFilter.Contains(entry.SerialNo)))
                continue;

            if (!string.IsNullOrEmpty(restrictedBatchNo)
                && !string.Equals(entry.BatchNo, restrictedBatchNo, StringComparison.OrdinalIgnoreCase))
                continue;

            var take = Math.Min(entry.AvailableQty, remaining);
            entry.AvailableQty -= take;
            remaining -= take;

            if (!string.IsNullOrEmpty(entry.SerialNo))
            {
                takenSerials.Add(entry.SerialNo);
                takenSerialSet.Add(entry.SerialNo);
            }

            if (!string.IsNullOrEmpty(entry.BatchNo))
            {
                takenBatches[entry.BatchNo] = takenBatches.GetValueOrDefault(entry.BatchNo, 0m) + take;
            }
        }

        return (takenSerials, takenBatches);
    }

    /// <summary>
    /// Computes the basic rate of a percentage-allocated co-product after deducting BOTH costed-out
    /// secondary items AND manually rated finished goods from the allocation basis (floored at 0)
    /// when automatic finished goods exist.
    /// Per ERPNext PR #67383 (<c>stock_entry.py</c>).
    /// </summary>
    public static decimal CalculateCoProductRateAfterManualFinishedGoods(
        decimal secondaryItemsCostBasis,
        decimal costedOutItemsCost,
        decimal manuallyRatedFinishedItemsCost,
        decimal finishedItemsQty,
        decimal costAllocationPercentage,
        decimal secondaryItemsQty,
        int precision = 4)
    {
        if (secondaryItemsQty <= 0m)
            return 0m;

        var allocationBasis = secondaryItemsCostBasis - costedOutItemsCost;
        if (finishedItemsQty > 0m && allocationBasis > 0m)
        {
            allocationBasis = Math.Max(allocationBasis - manuallyRatedFinishedItemsCost, 0m);
        }

        return Math.Round(
            allocationBasis * (costAllocationPercentage / 100m) / secondaryItemsQty,
            precision,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Rounds a quantity up to the next whole unit when its Stock UOM requires whole numbers,
    /// rounding to 9 decimals first so floating-point noise (e.g. <c>0.07 * 100</c> or <c>3 * 0.1 / 0.3</c>)
    /// does not add a spurious unit while preserving small non-zero shares (e.g. <c>1 / 2500</c> = 1)
    /// per ERPNext PR #68803 (<c>ceil_qty_if_uom_has_whole_number</c>).
    /// </summary>
    public static decimal CeilQtyIfUomMustBeWholeNumber(decimal qty, bool mustBeWholeNumber)
    {
        if (!mustBeWholeNumber)
            return qty;

        return Math.Ceiling(Math.Round(qty, 9, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Orders Stock Entry rows for manufacturing valuation per ERPNext PR #68832
    /// (<c>stock_entry.py: count_secondary_items_under</c>):
    /// non-finished items before finished items, costed-out rows before non-costed-out rows,
    /// rows costed out of the finished item before others, and inner sub-assembly secondary rows
    /// (fewer nested secondary items under their BOM) before outer sub-assembly co-products
    /// so outer co-products are valued net of inner co-products.
    /// </summary>
    public static List<TRow> OrderFinishedAndSecondaryRowsForCosting<TRow>(
        IEnumerable<TRow> rows,
        Func<TRow, bool> isFinishedItem,
        Func<TRow, bool> isCostedOut,
        Func<TRow, bool> isCostedOutOfFinishedItem,
        Func<TRow, int> nestedSecondaryRowCount)
    {
        return rows
            .OrderBy(r => isFinishedItem(r) ? 1 : 0)
            .ThenBy(r => isCostedOut(r) ? 0 : 1)
            .ThenBy(r => isCostedOutOfFinishedItem(r) ? 0 : 1)
            .ThenBy(nestedSecondaryRowCount)
            .ToList();
    }

    /// <summary>
    /// Resolves the Difference / Expense Account on a Stock Entry row per ERPNext PR #67509 (<c>stock_entry.py</c>):
    /// preserves an explicit Difference Account already set on the row (including on Subcontracting Delivery
    /// and Subcontracting Return rows), and when unset defaults Subcontracting Delivery / Subcontracting Return
    /// to <paramref name="companyDefaultExpenseAccountId"/> and Material Issue to item -&gt; item group -&gt; company.
    /// </summary>
    public static Guid? ResolveStockEntryDifferenceAccount(
        Guid? existingRowExpenseAccountId,
        string? purpose,
        Guid? itemDefaultExpenseAccountId,
        Guid? itemGroupDefaultExpenseAccountId,
        Guid? companyDefaultExpenseAccountId)
    {
        if (existingRowExpenseAccountId.HasValue && existingRowExpenseAccountId.Value != Guid.Empty)
            return existingRowExpenseAccountId;

        if (string.Equals(purpose, "Subcontracting Delivery", StringComparison.OrdinalIgnoreCase)
            || string.Equals(purpose, "SubcontractingDelivery", StringComparison.OrdinalIgnoreCase)
            || string.Equals(purpose, "Subcontracting Return", StringComparison.OrdinalIgnoreCase)
            || string.Equals(purpose, "SubcontractingReturn", StringComparison.OrdinalIgnoreCase))
        {
            return companyDefaultExpenseAccountId;
        }

        if (string.Equals(purpose, "Material Issue", StringComparison.OrdinalIgnoreCase)
            || string.Equals(purpose, "MaterialIssue", StringComparison.OrdinalIgnoreCase))
        {
            return itemDefaultExpenseAccountId ?? itemGroupDefaultExpenseAccountId ?? companyDefaultExpenseAccountId;
        }

        return companyDefaultExpenseAccountId;
    }

    /// <summary>
    /// Validates that a Stock Entry's summed <c>TransferQty</c> (stock UOM) for a BOM component
    /// matches the BOM's required stock qty (ceiled when Stock UOM requires whole numbers) at
    /// <paramref name="transferQtyPrecision"/> per ERPNext PR #67414 (<c>_check_bom_component_qty</c>).
    /// </summary>
    public static void ValidateBomComponentTransferQty(
        decimal bomUnitQty,
        decimal fgCompletedQty,
        bool mustBeWholeNumber,
        IEnumerable<decimal> matchedRowTransferQtys,
        string itemCode,
        int transferQtyPrecision = 3)
    {
        var list = matchedRowTransferQtys?.ToList();
        if (list == null || list.Count == 0)
            return;

        var expectedQty = CeilQtyIfUomMustBeWholeNumber(bomUnitQty * fgCompletedQty, mustBeWholeNumber);
        var actualStockQty = list.Sum();

        var roundedExpected = Math.Round(expectedQty, transferQtyPrecision, MidpointRounding.AwayFromZero);
        var roundedActual = Math.Round(actualStockQty, transferQtyPrecision, MidpointRounding.AwayFromZero);

        if (roundedExpected != roundedActual)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Item {itemCode}: transfer qty {roundedActual} does not match BOM required qty {roundedExpected}.");
        }
    }

    /// <summary>
    /// Validates that a Material Transfer for Manufacture Stock Entry does not exceed the Work Order's
    /// remaining allowed transferable quantity per ERPNext PR #67410
    /// (<c>MaterialTransferForManufactureStockEntry._validate_no_excess_transfer</c>):
    /// resolves the effective backflush mode from the BOM override first (falling back to
    /// <c>ManufacturingSettings.BackflushRawMaterialsBasedOn</c>) and skips the excess check when
    /// returning or when effective backflush is <c>"Material Transferred for Manufacture"</c>.
    /// </summary>
    public static void ValidateNoExcessTransferForManufacture(
        bool isReturn,
        string? bomBackflushBasedOn,
        string? manufacturingSettingsBackflushBasedOn,
        IEnumerable<(string ItemCode, decimal RequiredQty, decimal TransferredQty)> workOrderRequiredItems,
        IEnumerable<(int RowIdx, string ItemCode, string? OriginalItemCode, bool HasSourceWarehouse, decimal Qty, decimal ConversionFactor, string StockUom)> transferRows,
        decimal overproductionAllowancePercentage = 0m,
        int transferQtyPrecision = 3)
    {
        if (isReturn)
            return;

        var effectiveBackflush = !string.IsNullOrWhiteSpace(bomBackflushBasedOn)
            ? bomBackflushBasedOn
            : (!string.IsNullOrWhiteSpace(manufacturingSettingsBackflushBasedOn)
                ? manufacturingSettingsBackflushBasedOn
                : "BOM");

        if (string.Equals(effectiveBackflush, "Material Transferred for Manufacture", StringComparison.OrdinalIgnoreCase))
            return;

        var allowanceMultiplier = 1m + (overproductionAllowancePercentage > 0m ? overproductionAllowancePercentage / 100m : 0m);
        var pendingByItem = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var req in workOrderRequiredItems)
        {
            var allowedQty = req.RequiredQty * allowanceMultiplier;
            pendingByItem.TryGetValue(req.ItemCode, out var existing);
            pendingByItem[req.ItemCode] = existing + allowedQty - req.TransferredQty;
        }

        var transferByItem = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var firstRowByItem = new Dictionary<string, (int RowIdx, string ItemCode, string StockUom)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in transferRows)
        {
            if (!row.HasSourceWarehouse)
                continue;

            var key = !string.IsNullOrWhiteSpace(row.OriginalItemCode) ? row.OriginalItemCode : row.ItemCode;
            if (!pendingByItem.ContainsKey(key))
                continue;

            var factor = row.ConversionFactor > 0m ? row.ConversionFactor : 1m;
            transferByItem.TryGetValue(key, out var existing);
            transferByItem[key] = existing + row.Qty * factor;
            if (!firstRowByItem.ContainsKey(key))
            {
                firstRowByItem[key] = (row.RowIdx, row.ItemCode, row.StockUom);
            }
        }

        foreach (var (key, rawTransferQty) in transferByItem)
        {
            var roundedTransfer = Math.Round(rawTransferQty, transferQtyPrecision, MidpointRounding.AwayFromZero);
            var roundedPending = Math.Round(pendingByItem[key], transferQtyPrecision, MidpointRounding.AwayFromZero);
            if (roundedTransfer > roundedPending)
            {
                var firstRow = firstRowByItem[key];
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row #{firstRow.RowIdx}: Cannot transfer {roundedTransfer} {firstRow.StockUom} of Item {firstRow.ItemCode}. Maximum transferable quantity is {roundedPending} {firstRow.StockUom}.");
            }
        }
    }

    /// <summary>
    /// Guards Stock Entry <c>Get Items</c> per ERPNext PR #67796 (<c>StockEntry.get_items</c>):
    /// refuses fetching BOM items on a <c>Manufacture</c> Stock Entry that belongs to a Job Card,
    /// and requires positive <c>FgCompletedQty</c> when fetching from BOM on Manufacture/Repack.
    /// </summary>
    public static void ValidateGetItemsAllowed(
        StockEntryType entryType,
        Guid? jobCardId,
        string? jobCardNumber,
        bool fromBom,
        decimal fgCompletedQty)
    {
        if (jobCardId.HasValue && jobCardId.Value != Guid.Empty && entryType == StockEntryType.Manufacture)
        {
            var label = string.IsNullOrWhiteSpace(jobCardNumber) ? jobCardId.Value.ToString() : jobCardNumber;
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("jobCard", label)
                .WithData("detail", $"Items of this Stock Entry come from Job Card {label}. Use Record Output on the Job Card instead of Get Items.");
        }

        if ((entryType == StockEntryType.Manufacture || entryType == StockEntryType.Repack)
            && fromBom
            && fgCompletedQty <= 0m)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please set Finished Good Quantity before fetching items from the BOM.");
        }
    }

    /// <summary>
    /// Validates Stock Entry rows linked to Pick List rows per ERPNext PR #67707 and PR #67703
    /// (<c>StockEntry.validate_with_pick_list</c>):
    /// - Refuses rows moving a different item than picked on the Pick List row (allowing valid
    ///   Work Order alternative items where <c>OriginalItemCode == pickedRow.ItemCode</c>).
    /// - Refuses rows moving from a different source warehouse than picked.
    /// - Rounds cumulative <c>pickedRow.TransferredQty + sum(row.TransferQty)</c> to
    ///   <paramref name="transferredQtyPrecision"/> before comparing against <c>PickedQty</c>.
    /// </summary>
    public static void ValidateStockEntryRowsAgainstPickList(
        StockEntryType entryType,
        bool hasWorkOrder,
        string pickListName,
        IReadOnlyDictionary<Guid, (string ItemCode, Guid? WarehouseId, decimal PickedQty, decimal TransferredQty)> pickedRowsById,
        IEnumerable<(int RowIdx, Guid? PickListItemId, string ItemCode, string? OriginalItemCode, Guid? SourceWarehouseId, decimal TransferQty)> entryRows,
        int transferredQtyPrecision = 4)
    {
        var isAlternativePurpose = hasWorkOrder
            && (entryType == StockEntryType.MaterialTransferForManufacture
                || entryType == StockEntryType.Manufacture
                || entryType == StockEntryType.MaterialConsumptionForManufacture);

        var addedByPickRow = new Dictionary<Guid, decimal>();
        foreach (var row in entryRows)
        {
            if (!row.PickListItemId.HasValue || !pickedRowsById.TryGetValue(row.PickListItemId.Value, out var picked))
                continue;

            var isWorkOrderAlternative = isAlternativePurpose
                && !string.IsNullOrWhiteSpace(row.OriginalItemCode)
                && string.Equals(row.OriginalItemCode, picked.ItemCode, StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(row.ItemCode, picked.ItemCode, StringComparison.OrdinalIgnoreCase) && !isWorkOrderAlternative)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("rowIdx", row.RowIdx)
                    .WithData("detail", $"Row #{row.RowIdx}: Item {row.ItemCode} is not the item {picked.ItemCode} picked in {pickListName}.");
            }

            if (row.SourceWarehouseId.HasValue && row.SourceWarehouseId.Value != Guid.Empty
                && picked.WarehouseId.HasValue && row.SourceWarehouseId.Value != picked.WarehouseId.Value)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("rowIdx", row.RowIdx)
                    .WithData("detail", $"Row #{row.RowIdx}: Item {row.ItemCode} was picked from a different warehouse in {pickListName}.");
            }

            addedByPickRow.TryGetValue(row.PickListItemId.Value, out var prevAdded);
            var cumulativeAdded = prevAdded + row.TransferQty;
            addedByPickRow[row.PickListItemId.Value] = cumulativeAdded;

            var totalTransferred = Math.Round(picked.TransferredQty + cumulativeAdded, transferredQtyPrecision, MidpointRounding.AwayFromZero);
            if (totalTransferred > picked.PickedQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("rowIdx", row.RowIdx)
                    .WithData("detail", $"Row #{row.RowIdx}: Item {row.ItemCode} is transferred more than the {picked.PickedQty} picked in {pickListName}.");
            }
        }
    }

    /// <summary>
    /// Computes the cost allocation basis for percentage-allocated secondary items in a Manufacture Stock Entry
    /// per ERPNext PR #67383 (<c>stock_entry.py: set_basic_rate_for_finished_goods</c>):
    /// deducts costed-out items first, and when automatic finished goods exist and the remaining basis is positive,
    /// subtracts manually rated finished goods' total value (clamped at <c>0</c>) so manual finished goods take
    /// their value before automatic finished goods and secondary items split the remainder.
    /// </summary>
    public static decimal CalculateSecondaryItemAllocationBasis(
        decimal secondaryItemsCostBasis,
        decimal costedOutItemsCost,
        decimal automaticFinishedItemsQty,
        decimal manuallyRatedFinishedItemsCost)
    {
        var allocationBasis = secondaryItemsCostBasis - costedOutItemsCost;
        if (automaticFinishedItemsQty > 0m && allocationBasis > 0m)
        {
            allocationBasis = Math.Max(allocationBasis - manuallyRatedFinishedItemsCost, 0m);
        }

        return allocationBasis;
    }

    /// <summary>
    /// Computes the pending secondary-item scaling quantity for Job Card scrap in a Manufacture Stock Entry
    /// per ERPNext PR #67395 (<c>stock_entry/services/manufacturing.py: _get_pending_secondary_qty</c>):
    /// returns <paramref name="fgCompletedQty"/> when linked to a specific Job Card; otherwise adds
    /// <paramref name="pendingProcessLossQty"/> to <c>(completedJobCardQty - workOrderProducedQty)</c>
    /// because completed qty excludes unbooked Job Card process loss that <c>For Quantity</c> includes.
    /// </summary>
    public static decimal CalculatePendingSecondaryQtyForJobCardScrap(
        bool hasJobCard,
        decimal fgCompletedQty,
        decimal completedJobCardQty,
        decimal workOrderProducedQty,
        decimal pendingProcessLossQty)
    {
        if (hasJobCard)
        {
            return fgCompletedQty;
        }

        return completedJobCardQty - workOrderProducedQty + pendingProcessLossQty;
    }

    /// <summary>
    /// Validates a Repack Stock Entry AFTER finished/secondary output rows have been marked per ERPNext PR #67384
    /// (<c>stock_entry/services/manufacturing.py: validate_repack_entry</c> / <c>stock_entry.py</c>):
    /// when more than one distinct finished good item code is present, EVERY finished good row
    /// (not merely one row per item code) must have <c>SetBasicRateManually == true</c>.
    /// </summary>
    public static void ValidateRepackFinishedGoodsManualRates(
        IEnumerable<(string ItemCode, bool IsFinishedItem, bool SetBasicRateManually)>? items)
    {
        if (items == null)
        {
            return;
        }

        var fgRows = items.Where(r => r.IsFinishedItem).ToList();
        var distinctFgItems = fgRows
            .Select(r => r.ItemCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctFgItems.Count > 1 && !fgRows.All(r => r.SetBasicRateManually))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("items", string.Join(", ", distinctFgItems))
                .WithData(
                    "detail",
                    $"When there are multiple finished goods ({string.Join(", ", distinctFgItems)}) in a Repack stock entry, the basic rate for all finished goods must be set manually.");
        }
    }

    /// <summary>
    /// Resolves the quantity passed to serial/batch auto-assignment for a backflushed raw material row
    /// per ERPNext PR #67411 (<c>manufacturing.py</c>): uses the row's already rounded/ceiled quantity
    /// (<paramref name="roundedItemRowQty"/>) rather than the unrounded intermediate quantity so the
    /// assigned serial count matches the saved row quantity.
    /// </summary>
    public static decimal ResolveBackflushSerialBatchAssignQty(decimal rawQty, decimal roundedItemRowQty)
        => roundedItemRowQty;

    /// <summary>
    /// Determines whether a Stock Entry row should preserve its existing <c>BasicRate</c> (skipping
    /// valuation-rate re-fetch) when UOM or batch changes per ERPNext PR #67385 (<c>stock_entry.js</c>):
    /// preserves the rate when <paramref name="setBasicRateManually"/> is true OR on a
    /// <see cref="StockEntryType.MaterialReceipt"/> row where the user already entered a non-zero rate.
    /// </summary>
    public static bool ShouldPreserveRowBasicRateOnUomOrBatchChange(
        StockEntryType purpose,
        bool setBasicRateManually,
        decimal currentBasicRate)
        => setBasicRateManually || (purpose == StockEntryType.MaterialReceipt && currentBasicRate > 0m);

    /// <summary>
    /// Determines whether header <c>FromWarehouse</c>/<c>ToWarehouse</c> defaults should be applied to an
    /// added Stock Entry item row per ERPNext PR #68786 (<c>stock_entry.js</c>):
    /// checks whether <paramref name="itemName"/> is blank (user/item defaults can pre-fill <c>stock_uom</c>
    /// on a brand-new row, never <c>item_name</c>, while duplicated rows already carry <c>item_name</c>
    /// and must retain their own warehouses).
    /// </summary>
    public static bool ShouldApplyHeaderWarehousesToAddedRow(string? itemName)
        => string.IsNullOrWhiteSpace(itemName);

    /// <summary>
    /// Determines whether a Stock Entry is blocked because its linked Work Order is <c>Closed</c>
    /// per ERPNext PR #67988 (<c>stock_entry_base.py</c>):
    /// blocks submitted <c>MaterialTransferForManufacture</c>, <c>Manufacture</c>, and
    /// <c>MaterialConsumptionForManufacture</c> entries against a Closed Work Order, exempting ONLY
    /// <c>MaterialTransferForManufacture</c> returns (and NOT return-flagged <c>Manufacture</c> or
    /// consumption entries).
    /// </summary>
    public static bool IsBlockedByClosedWorkOrder(
        bool isWorkOrderClosed,
        bool isSubmitted,
        StockEntryType purpose,
        bool isReturn)
    {
        if (!isWorkOrderClosed || !isSubmitted)
        {
            return false;
        }

        if (purpose == StockEntryType.MaterialTransferForManufacture && isReturn)
        {
            return false;
        }

        return purpose is StockEntryType.MaterialTransferForManufacture
            or StockEntryType.Manufacture
            or StockEntryType.MaterialConsumptionForManufacture;
    }

    /// <summary>
    /// Builds an overflow batch row when splitting a Stock Entry item row across reserved batches
    /// per ERPNext PR #67531 (<c>stock_entry/services/serial_batch.py: _make_overflow_batch_row</c>):
    /// clears the cloned row identity (<c>RowId = Guid.Empty</c>) and keeps <c>UseSerialBatchFields = true</c>
    /// so split rows remain in manual serial/batch field mode rather than reverting to bundle mode.
    /// </summary>
    public static OverflowReservedBatchRowResult CreateOverflowReservedBatchRow(
        Guid itemId,
        Guid? sourceWarehouseId,
        string batchNo,
        decimal qty)
        => new(
            RowId: Guid.Empty,
            ItemId: itemId,
            SourceWarehouseId: sourceWarehouseId,
            BatchNo: batchNo,
            Qty: qty,
            UseSerialBatchFields: true);

    /// <summary>
    /// Computes the remaining Work Order quantity to produce when appending unconsumed raw materials
    /// on a Manufacture Stock Entry per ERPNext PR #67724
    /// (<c>stock_entry/services/manufacturing.py: ManufactureStockEntry</c>):
    /// subtracts BOTH <paramref name="producedQty"/> AND <paramref name="processLossQty"/> from
    /// <c>(materialTransferredForManufacturing &gt; 0 ? materialTransferredForManufacturing : workOrderQty)</c>
    /// so unconsumed raw materials consume the full BOM rate after a process loss.
    /// </summary>
    public static decimal CalculateRemainingWorkOrderQtyToProduce(
        decimal workOrderQty,
        decimal materialTransferredForManufacturing,
        decimal producedQty,
        decimal processLossQty)
    {
        var baseQty = materialTransferredForManufacturing > 0m
            ? materialTransferredForManufacturing
            : workOrderQty;
        return baseQty - producedQty - processLossQty;
    }

    /// <summary>
    /// Enforces Item select permission, Stock Entry read permission, and Stock Entry-scoped Warehouse and
    /// Company user permissions on <c>get_warehouse_details</c> per ERPNext PR #67132
    /// (<c>stock_entry.py: get_warehouse_details</c> / <c>check_stock_entry_warehouse_permission</c>).
    /// </summary>
    public static void ValidateStockEntryWarehouseDetailsAccess(
        bool hasItemSelectPermission,
        bool hasStockEntryReadPermission,
        Guid? warehouseId,
        Guid? warehouseCompanyId,
        IReadOnlySet<Guid>? allowedStockEntryWarehouses,
        IReadOnlySet<Guid>? allowedStockEntryCompanies)
    {
        if (!hasItemSelectPermission)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Not permitted to select Item.");
        }

        if (!hasStockEntryReadPermission)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Not permitted to read Stock Entry.");
        }

        if (!warehouseId.HasValue || warehouseId.Value == Guid.Empty)
        {
            return;
        }

        if (allowedStockEntryWarehouses != null
            && allowedStockEntryWarehouses.Count > 0
            && !allowedStockEntryWarehouses.Contains(warehouseId.Value))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("warehouseId", warehouseId.Value)
                .WithData("detail", $"Not permitted for warehouse {warehouseId.Value}.");
        }

        if (warehouseCompanyId.HasValue
            && warehouseCompanyId.Value != Guid.Empty
            && allowedStockEntryCompanies != null
            && allowedStockEntryCompanies.Count > 0
            && !allowedStockEntryCompanies.Contains(warehouseCompanyId.Value))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("companyId", warehouseCompanyId.Value)
                .WithData("detail", $"Not permitted for company {warehouseCompanyId.Value}.");
        }
    }
}

public sealed record OverflowReservedBatchRowResult(
    Guid RowId,
    Guid ItemId,
    Guid? SourceWarehouseId,
    string BatchNo,
    decimal Qty,
    bool UseSerialBatchFields);

public sealed class ReservedSerialBatchSlot
{
    public string? SerialNo { get; set; }
    public string? BatchNo { get; set; }
    public decimal AvailableQty { get; set; }
}
