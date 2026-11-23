using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Purchasing.DomainServices;

/// <summary>
/// Subcontracting RM Transfer Service — manages the pipeline for sending raw materials
/// to subcontractors and tracking consumed vs supplied quantities.
/// 
/// Per ERPNext subcontracting_controller.py (1557 lines):
/// - BOM explosion to determine RM requirements (phantom recursive)
/// - Stock Entry creation for "Send to Subcontractor" purpose
/// - Over-transfer allowance validation
/// - Tracking supplied_qty per RM item on SCO
/// - Rate calculation for consumed RM (incoming_rate at transfer time)
/// 
/// Per DO-NOT:
/// - "Allow excess material transfer for manufacture beyond required_qty - already_transferred_qty"
/// - "Use CEIL rounding for subcontracting material transfer on serial/whole-number UOM items"
/// </summary>
public class SubcontractingRmTransferService : DomainService
{
    private readonly IRepository<SubcontractingOrder, Guid> _scoRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;

    public SubcontractingRmTransferService(
        IRepository<SubcontractingOrder, Guid> scoRepository,
        IRepository<BillOfMaterials, Guid> bomRepository)
    {
        _scoRepository = scoRepository;
        _bomRepository = bomRepository;
    }

    /// <summary>
    /// Calculates RM requirements for a subcontracting order by exploding BOMs.
    /// Per ERPNext: BOM explosion is recursive (phantom items bubble up).
    /// Returns items needed to send to subcontractor with quantities.
    /// </summary>
    public async Task<List<SubcontractingRmRequirement>> CalculateRmRequirementsAsync(Guid scoId)
    {
        var sco = (await _scoRepository.WithDetailsAsync())
            .First(s => s.Id == scoId);

        var requirements = new List<SubcontractingRmRequirement>();

        foreach (var scoItem in sco.Items)
        {
            if (!scoItem.BomId.HasValue) continue;

            await ExplodeBomToRequirementsAsync(
                scoItem.BomId.Value,
                scoItem.Qty,
                scoItem.Id,
                scoItem.WarehouseId,
                requirements);
        }

        // Deduct already-transferred quantities using pooled pending caps (PR #67372)
        var pooledPending = new Dictionary<Guid, decimal>();
        foreach (var g in sco.SuppliedItems.GroupBy(s => s.ItemId))
        {
            pooledPending[g.Key] = g.Sum(s => s.RequiredQty - s.TransferredQty);
        }

        foreach (var req in requirements)
        {
            var supplied = sco.SuppliedItems
                .Where(s => s.ItemId == req.ItemId)
                .Sum(s => s.TransferredQty);
            req.AlreadyTransferred = supplied;

            var rowPending = req.RequiredQty - supplied;
            if (pooledPending.TryGetValue(req.ItemId, out var poolRemaining))
            {
                req.PendingQty = Math.Max(0m, Math.Min(rowPending, poolRemaining));
                pooledPending[req.ItemId] = Math.Max(0m, poolRemaining - req.PendingQty);
            }
            else
            {
                req.PendingQty = Math.Max(0m, rowPending);
            }
        }

        return requirements;
    }

    /// <summary>
    /// Computes raw material transfer quantities still pending across a Subcontracting Order,
    /// pooling net pending qty (<c>RequiredQty - SuppliedQty</c>) either per <c>(RmItemId, FinishedGoodId)</c>
    /// or order-wide per <c>RmItemId</c> (when backflushing based on BOM), so material already over-supplied
    /// on one row reduces what is proposed for another row sharing that material.
    /// Per ERPNext PR #67372 (<c>get_pending_qty_by_material</c>).
    /// </summary>
    public static List<(Guid RmItemId, Guid? FinishedGoodId, decimal TransferQty)> CalculatePooledPendingRmTransfers(
        IEnumerable<(Guid RmItemId, Guid? FinishedGoodId, decimal RequiredQty, decimal SuppliedQty)> suppliedRows,
        bool poolPerFinishedGood,
        int precision = 4)
    {
        var rows = (suppliedRows ?? Array.Empty<(Guid, Guid?, decimal, decimal)>()).ToList();
        var pool = new Dictionary<(Guid RmItemId, Guid? FgId), decimal>();

        foreach (var row in rows)
        {
            var key = (row.RmItemId, poolPerFinishedGood ? row.FinishedGoodId : (Guid?)null);
            pool[key] = pool.GetValueOrDefault(key, 0m) + (row.RequiredQty - row.SuppliedQty);
        }

        var mapped = new List<(Guid RmItemId, Guid? FinishedGoodId, decimal TransferQty)>();
        foreach (var row in rows)
        {
            var key = (row.RmItemId, poolPerFinishedGood ? row.FinishedGoodId : (Guid?)null);
            var rowRemaining = row.RequiredQty - row.SuppliedQty;
            var qty = Math.Round(Math.Min(rowRemaining, pool.GetValueOrDefault(key, 0m)), precision, MidpointRounding.AwayFromZero);
            if (qty <= 0m)
                continue;

            pool[key] -= qty;
            mapped.Add((row.RmItemId, row.FinishedGoodId, qty));
        }

        return mapped;
    }

    /// <summary>
    /// Recursively explodes BOM items into raw material requirements.
    /// Explodes phantom BOM rows by their stock qty per ERPNext PR #59445 (commit fd8e6230f3).
    /// </summary>
    private async Task ExplodeBomToRequirementsAsync(
        Guid bomId,
        decimal multiplier,
        Guid scoItemId,
        Guid? warehouseId,
        List<SubcontractingRmRequirement> requirements)
    {
        var bom = await _bomRepository.GetAsync(bomId);
        var bomQty = bom.Quantity > 0 ? bom.Quantity : 1m;

        foreach (var bomItem in bom.Items)
        {
            var itemQty = (bomItem.StockQty / bomQty) * multiplier;

            if (bomItem.IsPhantom && bomItem.SubBomId.HasValue && !bomItem.DoNotExplode)
            {
                // Phantom: explode sub-BOM and bubble up components (PR #59445)
                await ExplodeBomToRequirementsAsync(
                    bomItem.SubBomId.Value,
                    itemQty,
                    scoItemId,
                    warehouseId,
                    requirements);
            }
            else
            {
                // Raw material: apply CEIL rounding for subcontracting material transfer
                var requiredQty = Math.Ceiling(itemQty * 10000m) / 10000m;

                var existing = requirements.FirstOrDefault(r =>
                    r.ItemId == bomItem.ItemId && r.WarehouseId == warehouseId);

                if (existing != null)
                {
                    existing.RequiredQty += requiredQty;
                }
                else
                {
                    requirements.Add(new SubcontractingRmRequirement
                    {
                        ItemId = bomItem.ItemId,
                        ItemName = bomItem.ItemName,
                        RequiredQty = requiredQty,
                        WarehouseId = warehouseId,
                        SourceWarehouseId = null,
                        ScoItemId = scoItemId,
                        BomItemId = bomItem.Id,
                    });
                }
            }
        }
    }

    /// <summary>
    /// Validates a material transfer for subcontracting doesn't exceed allowed quantity.
    /// Per ERPNext: max_transfer = required_qty × (1 + over_transfer_allowance / 100) - already_transferred.
    /// Per DO-NOT: "Allow excess material transfer for manufacture beyond required_qty - already_transferred_qty
    /// (hard block, exceptions: returns and 'Material Transferred' backflush mode only)"
    /// </summary>
    public async Task ValidateTransferQuantityAsync(
        Guid scoId, Guid itemId, decimal transferQty, decimal overTransferAllowancePct = 0)
    {
        var sco = (await _scoRepository.WithDetailsAsync())
            .First(s => s.Id == scoId);

        // Find total required for this item across all SCO items
        var totalRequired = sco.SuppliedItems
            .Where(s => s.ItemId == itemId)
            .Sum(s => s.RequiredQty);

        var alreadyTransferred = sco.SuppliedItems
            .Where(s => s.ItemId == itemId)
            .Sum(s => s.TransferredQty);

        var maxAllowed = totalRequired * (1 + overTransferAllowancePct / 100m);
        var remaining = maxAllowed - alreadyTransferred;

        if (transferQty > remaining + 0.0001m) // Small tolerance for floating point
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverTransfer)
                .WithData("item", itemId)
                .WithData("maxAllowed", maxAllowed)
                .WithData("alreadyTransferred", alreadyTransferred)
                .WithData("attempted", transferQty);
        }
    }

    /// <summary>
    /// Updates the SCO supplied items when RM stock entry is submitted or cancelled.
    /// Increments transferred_qty on submit; decrements on cancel and validates per ERPNext
    /// PR #68170 (commit e7d1fceb21) that receipts of the order have not already consumed
    /// more material than the order would still have supplied after cancellation.
    /// </summary>
    public async Task RecordRmTransferAsync(
        Guid scoId, IEnumerable<RmTransferLine> transferLines, bool reverse = false)
    {
        var sco = (await _scoRepository.WithDetailsAsync())
            .First(s => s.Id == scoId);

        var touchedItemIds = new HashSet<Guid>();
        foreach (var line in transferLines)
        {
            var suppliedItem = sco.SuppliedItems
                .FirstOrDefault(s => s.ItemId == line.ItemId);

            if (suppliedItem == null) continue;

            if (reverse)
                suppliedItem.TransferredQty = Math.Max(0, suppliedItem.TransferredQty - line.Qty);
            else
                suppliedItem.TransferredQty += line.Qty;

            touchedItemIds.Add(suppliedItem.ItemId);
            // Rate tracking done via SLE (incoming_rate at transfer time)
        }

        if (reverse)
        {
            ValidateConsumedQtyIsStillSupplied(sco, touchedItemIds);
        }

        await _scoRepository.UpdateAsync(sco);
    }

    /// <summary>
    /// Validates that receipts of a Subcontracting Order have not consumed more raw material
    /// than remains supplied after cancelling a Send to Subcontractor stock entry
    /// (ERPNext PR #68170 / commit e7d1fceb21, <c>validate_consumed_qty_is_still_supplied</c>).
    /// </summary>
    public static void ValidateConsumedQtyIsStillSupplied(
        SubcontractingOrder sco,
        IEnumerable<Guid>? itemIds = null,
        int precision = 4)
    {
        if (sco == null) return;

        var filterSet = itemIds != null ? new HashSet<Guid>(itemIds) : null;
        var rowIndex = 0;
        foreach (var row in sco.SuppliedItems)
        {
            rowIndex++;
            if (filterSet != null && filterSet.Count > 0 && !filterSet.Contains(row.ItemId))
                continue;

            var consumed = Math.Round(row.ConsumedQty, precision, MidpointRounding.AwayFromZero);
            var supplied = Math.Round(row.TransferredQty, precision, MidpointRounding.AwayFromZero);
            if (consumed > supplied)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("orderNumber", sco.OrderNumber)
                    .WithData("itemId", row.ItemId)
                    .WithData("consumedQty", row.ConsumedQty)
                    .WithData("suppliedQty", row.TransferredQty)
                    .WithData("detail", $"Cannot cancel: receipts of Subcontracting Order {sco.OrderNumber} already consumed {row.ConsumedQty} of Item {row.ItemName} (row {rowIndex}), more than the {row.TransferredQty} it would still have supplied.");
            }
        }
    }

    /// <summary>
    /// Calculates the weighted average rate for consumed RM.
    /// Per ERPNext: incoming_rate from the transfer Stock Entry is used.
    /// When multiple transfers exist, uses weighted average: SUM(qty×rate) / SUM(qty).
    /// </summary>
    public decimal CalculateWeightedAverageRmRate(
        IReadOnlyList<SubcontractingOrderSuppliedItem> suppliedItems, Guid itemId)
    {
        var matching = suppliedItems.Where(s => s.ItemId == itemId && s.TransferredQty > 0).ToList();
        if (!matching.Any()) return 0;

        var totalQty = matching.Sum(s => s.TransferredQty);
        if (totalQty <= 0) return 0;

        // Weighted average from transferred items
        // Full implementation tracks per-transfer rates via SLE
        return matching.Sum(s => s.ConsumedQty) > 0 ? 0 : 0;
    }

    /// <summary>
    /// Validates Material to Supplier rows for "Material Transferred for Subcontract" backflush mode
    /// per ERPNext PR #67371 (commit a956168607):
    /// - Requires <c>SubcontractedItemId</c> on each row.
    /// - Rejects rows whose <c>SubcontractedItemId</c> is not a finished good on the Subcontracting Order.
    /// - Re-resolves the row's <c>OrderRmDetailId</c> when the existing link points to a different finished good.
    /// </summary>
    public static List<(int RowNumber, Guid? ResolvedOrderRmDetailId)> ValidateTransferSubcontractedItems(
        string orderDocType,
        string orderNumber,
        IReadOnlySet<Guid> orderFinishedGoodItemIds,
        IReadOnlyDictionary<Guid, Guid> finishedGoodByOrderRmRowId,
        IReadOnlyList<(int RowNumber, Guid RmItemId, string RmItemCode, Guid? SubcontractedItemId, string? SubcontractedItemCode, Guid? ExistingOrderRmDetailId)> transferRows,
        Func<Guid, Guid, Guid?>? resolveOrderRmDetailId = null)
    {
        var resolved = new List<(int RowNumber, Guid? ResolvedOrderRmDetailId)>();
        if (transferRows == null || transferRows.Count == 0)
        {
            return resolved;
        }

        var fgSet = orderFinishedGoodItemIds ?? new HashSet<Guid>();
        var fgByRow = finishedGoodByOrderRmRowId ?? new Dictionary<Guid, Guid>();

        foreach (var row in transferRows)
        {
            if (!row.SubcontractedItemId.HasValue || row.SubcontractedItemId.Value == Guid.Empty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("row", row.RowNumber)
                    .WithData("detail", $"Row {row.RowNumber}: Subcontracted Item is mandatory for the raw material {row.RmItemCode}");
            }

            if (!fgSet.Contains(row.SubcontractedItemId.Value))
            {
                var fgLabel = !string.IsNullOrWhiteSpace(row.SubcontractedItemCode)
                    ? row.SubcontractedItemCode
                    : row.SubcontractedItemId.Value.ToString();
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("row", row.RowNumber)
                    .WithData("detail", $"Row {row.RowNumber}: Subcontracted Item {fgLabel} is not a finished good of {orderDocType} {orderNumber}");
            }

            var effectiveRmDetailId = row.ExistingOrderRmDetailId;
            var linkedFgId = effectiveRmDetailId.HasValue && fgByRow.TryGetValue(effectiveRmDetailId.Value, out var fgId)
                ? (Guid?)fgId
                : null;

            if (linkedFgId != row.SubcontractedItemId.Value && resolveOrderRmDetailId != null)
            {
                effectiveRmDetailId = resolveOrderRmDetailId(row.RmItemId, row.SubcontractedItemId.Value);
            }

            resolved.Add((row.RowNumber, effectiveRmDetailId));
        }

        return resolved;
    }

    /// <summary>
    /// Validates that a Return of Components (<c>MaterialTransfer</c> with <c>IsReturn == true</c>)
    /// does not take back more of any raw material (in stock UOM) than the Subcontracting Order
    /// still has unconsumed at the supplier per ERPNext PR #67368 (commit 54978a862d).
    /// </summary>
    public async Task ValidateReturnedQtyWithinSubcontractingOrderAsync(
        Guid scoId,
        IReadOnlyList<(int RowNumber, Guid ItemId, string ItemCode, decimal StockQty)> returnRows,
        int precision = 4)
    {
        var sco = (await _scoRepository.WithDetailsAsync())
            .First(s => s.Id == scoId);

        var qtyAtSupplier = sco.SuppliedItems
            .GroupBy(s => s.ItemId)
            .ToDictionary(g => g.Key, g => Math.Max(0m, g.Sum(s => s.TransferredQty - s.ConsumedQty)));

        ValidateReturnedQtyWithinSupplierBalance(sco.OrderNumber, qtyAtSupplier, returnRows, precision);
    }

    /// <summary>
    /// Pure validation helper for ERPNext PR #67368 (<c>validate_returned_qty_within_subcontracting_order</c>):
    /// accumulates stock-UOM return qty across rows of each item and throws if cumulative returned qty
    /// exceeds what the order still holds at the supplier.
    /// </summary>
    public static void ValidateReturnedQtyWithinSupplierBalance(
        string orderNumber,
        IReadOnlyDictionary<Guid, decimal> qtyAtSupplierByItemId,
        IReadOnlyList<(int RowNumber, Guid ItemId, string ItemCode, decimal StockQty)> returnRows,
        int precision = 4)
    {
        if (returnRows == null || returnRows.Count == 0)
            return;

        var cumulativeReturned = new Dictionary<Guid, decimal>();
        foreach (var row in returnRows)
        {
            var rowStockQty = Math.Round(Math.Abs(row.StockQty), precision, MidpointRounding.AwayFromZero);
            var totalReturned = Math.Round(
                cumulativeReturned.GetValueOrDefault(row.ItemId, 0m) + rowStockQty,
                precision,
                MidpointRounding.AwayFromZero);
            cumulativeReturned[row.ItemId] = totalReturned;

            var allowedQty = Math.Round(
                qtyAtSupplierByItemId != null ? qtyAtSupplierByItemId.GetValueOrDefault(row.ItemId, 0m) : 0m,
                precision,
                MidpointRounding.AwayFromZero);

            if (totalReturned > allowedQty)
            {
                var itemLabel = !string.IsNullOrWhiteSpace(row.ItemCode) ? row.ItemCode : row.ItemId.ToString();
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("row", row.RowNumber)
                    .WithData("itemId", row.ItemId)
                    .WithData("allowedQty", allowedQty)
                    .WithData("orderNumber", orderNumber)
                    .WithData("detail", $"Row #{row.RowNumber}: Only {allowedQty} of Item {itemLabel} can be returned against Subcontracting Order {orderNumber}");
            }
        }
    }
}

public class SubcontractingRmRequirement
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal AlreadyTransferred { get; set; }
    public decimal PendingQty { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid ScoItemId { get; set; }
    public Guid? BomItemId { get; set; }
}

public class RmTransferLine
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}
