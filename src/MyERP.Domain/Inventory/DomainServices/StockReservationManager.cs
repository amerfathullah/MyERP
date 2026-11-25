using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for Stock Reservation Entry management.
/// Handles FIFO consumption on delivery, cancel/recreate pattern, and availability validation.
/// Per DO-NOT: "Allow stock reservation beyond available qty (actual - already_reserved)"
/// Per DO-NOT: "Allow Stock Reservation Entry amendment (must cancel and recreate)"
/// Per DO-NOT: "Allow pick list modification after stock reservation entries exist"
/// </summary>
public class StockReservationManager : DomainService
{
    private readonly IRepository<StockReservationEntry, Guid> _sreRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Warehouse, Guid>? _warehouseRepository;

    protected StockReservationManager()
    {
        _sreRepository = null!;
        _binRepository = null!;
        _sleRepository = null!;
    }

    public StockReservationManager(
        IRepository<StockReservationEntry, Guid> sreRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository)
        : this(sreRepository, binRepository, sleRepository, null)
    {
    }

    public StockReservationManager(
        IRepository<StockReservationEntry, Guid> sreRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Warehouse, Guid>? warehouseRepository)
    {
        _sreRepository = sreRepository;
        _binRepository = binRepository;
        _sleRepository = sleRepository;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// Validates that sufficient unreserved stock exists before creating a reservation.
    /// Available = ActualQty (as of postingDate, ignoring future stock) - SUM(active SRE reserved qty for same item+warehouse).
    /// Per ERPNext PR #58303 (commit 478a2f4f4b): ignore future stock during batch/stock reservation.
    /// Per ERPNext PR #59425 (commit 4dd9f3bf67): exclude the voucher's own active reservation (releases reservation for preview/transfer).
    /// </summary>
    public async Task ValidateAvailabilityAsync(
        Guid itemId,
        Guid warehouseId,
        decimal requestedQty,
        Guid? batchId = null,
        DateTime? asOfDate = null,
        string? ignoreVoucherType = null,
        Guid? ignoreVoucherId = null)
    {
        // Round to stock reservation precision to avoid floating-point / sub-unit representation rejections (ERPNext PR #46973 / commit 860699ee7b)
        requestedQty = Math.Round(requestedQty, 4);

        decimal actualQty;
        if (batchId.HasValue)
        {
            var sleQueryable = await _sleRepository.GetQueryableAsync();
            var sleQuery = sleQueryable
                .Where(s => s.ItemId == itemId
                    && s.WarehouseId == warehouseId
                    && s.BatchId == batchId
                    && !s.IsCancelled);

            if (asOfDate.HasValue)
            {
                sleQuery = sleQuery.Where(s => s.PostingDate <= asOfDate.Value);
            }

            var lastSle = sleQuery
                .OrderByDescending(s => s.PostingDate)
                .ThenByDescending(s => s.CreationTime)
                .FirstOrDefault();

            actualQty = lastSle?.BalanceQuantity ?? 0m;
        }
        else if (asOfDate.HasValue)
        {
            var sleQueryable = await _sleRepository.GetQueryableAsync();
            var lastSle = sleQueryable
                .Where(s => s.ItemId == itemId
                    && s.WarehouseId == warehouseId
                    && s.PostingDate <= asOfDate.Value
                    && !s.IsCancelled)
                .OrderByDescending(s => s.PostingDate)
                .ThenByDescending(s => s.CreationTime)
                .FirstOrDefault();

            actualQty = lastSle?.BalanceQuantity ?? 0m;
        }
        else
        {
            // Get actual stock from Bin
            var binQueryable = await _binRepository.GetQueryableAsync();
            actualQty = binQueryable
                .Where(b => b.ItemId == itemId && b.WarehouseId == warehouseId)
                .Select(b => b.ActualQty)
                .FirstOrDefault();
        }

        // Get already reserved
        // Per ERPNext PR #47049 / commit 27d674d54a: deduct delivered, transferred, and consumed quantities
        // Per ERPNext PR #59425 / commit 4dd9f3bf67: exclude own voucher reservation from blocking itself
        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var reservedQty = sreQueryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && (batchId == null || s.BatchId == batchId)
                && s.Status == DocumentStatus.Submitted
                && (ignoreVoucherId == null || !(s.VoucherType == ignoreVoucherType && s.VoucherId == ignoreVoucherId))
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .Sum(s => s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty);

        var available = Math.Round(actualQty - reservedQty, 4);

        if (requestedQty > available)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("itemId", itemId)
                .WithData("warehouseId", warehouseId)
                .WithData("available", available)
                .WithData("requested", requestedQty);
        }
    }

    /// <summary>
    /// Gets the available unreserved quantities for a list of batches in a warehouse.
    /// Per ERPNext PR #59601 (commit 92d24ba583): skips batch qty reserved by other vouchers in manufacture entries.
    /// </summary>
    public virtual async Task<Dictionary<Guid, decimal>> GetUnreservedBatchQuantitiesAsync(
        Guid itemId,
        Guid warehouseId,
        IEnumerable<Guid> batchIds,
        string? ignoreVoucherType = null,
        Guid? ignoreVoucherId = null)
    {
        var batchIdList = batchIds.Distinct().ToList();
        if (!batchIdList.Any()) return new();

        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var reservations = sreQueryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.BatchId.HasValue
                && batchIdList.Contains(s.BatchId.Value)
                && s.Status == DocumentStatus.Submitted
                && (ignoreVoucherId == null || !(s.VoucherType == ignoreVoucherType && s.VoucherId == ignoreVoucherId))
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .GroupBy(s => s.BatchId!.Value)
            .Select(g => new { BatchId = g.Key, Reserved = g.Sum(s => s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) })
            .ToList();

        var reservedMap = reservations.ToDictionary(r => r.BatchId, r => r.Reserved);

        // Get actual stock per batch from SLE
        var sleQueryable = await _sleRepository.GetQueryableAsync();
        var result = new Dictionary<Guid, decimal>();

        foreach (var bId in batchIdList)
        {
            var lastSle = sleQueryable
                .Where(s => s.ItemId == itemId
                    && s.WarehouseId == warehouseId
                    && s.BatchId == bId
                    && !s.IsCancelled)
                .OrderByDescending(s => s.PostingDate)
                .ThenByDescending(s => s.CreationTime)
                .FirstOrDefault();

            var actual = lastSle?.BalanceQuantity ?? 0m;
            var reserved = reservedMap.TryGetValue(bId, out var res) ? res : 0m;
            result[bId] = Math.Max(0, actual - reserved);
        }

        return result;
    }

    /// <summary>
    /// Consumes reserved stock when delivery is made (FIFO by creation date).
    /// Per ERPNext PR #49082 (commit dbaa44688e): filters delivered_qty < reserved_qty.
    /// Per ERPNext PR #59424 (commit dbada3f461): count only matched batches on reservations.
    /// Returns list of consumed SRE IDs with quantities.
    /// <summary>
    /// Returns <paramref name="fullQty"/> when <paramref name="qty"/> falls short of it only by ledger rounding residue.
    /// Per ERPNext PR #66566 (get_qty_up_to_rounding).
    /// </summary>
    public static decimal GetQtyUpToRounding(decimal qty, decimal fullQty, int precision = 4)
    {
        return Math.Round(fullQty - qty, precision, MidpointRounding.AwayFromZero) == 0m ? fullQty : qty;
    }

    /// <summary>
    /// Undelivers <paramref name="qtyToRestore"/> while leaving <paramref name="alreadyDeliveredQty"/>
    /// aligned to ledger precision with no rounding residue. Per ERPNext PR #66566 (get_qty_to_undeliver).
    /// </summary>
    public static decimal GetQtyToUndeliver(decimal alreadyDeliveredQty, decimal qtyToRestore, int precision = 4)
    {
        var capped = Math.Min(alreadyDeliveredQty, qtyToRestore);
        return alreadyDeliveredQty - Math.Round(alreadyDeliveredQty - capped, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Consumes reserved stock when a Delivery Note / update_stock SI is submitted.
    /// </summary>
    public async Task<ReservationConsumption[]> ConsumeOnDeliveryAsync(
        Guid itemId, Guid warehouseId, decimal deliveredQty, Guid? salesOrderId = null, Guid? batchId = null)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0
                && (salesOrderId == null || s.VoucherId == salesOrderId)
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenByDescending(s => s.BatchId != null || s.SerialAndBatchBundleId != null)
            .ThenBy(s => s.CreationTime)
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = deliveredQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;

            var available = sre.AvailableQty;
            if (available <= 0) continue;

            var consume = GetQtyUpToRounding(Math.Min(remaining, available), available);
            sre.RecordDelivery(consume);
            await _sreRepository.UpdateAsync(sre);

            consumed.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = consume
            });

            remaining = Math.Max(0m, remaining - consume);
        }

        return consumed.ToArray();
    }

    /// <summary>
    /// Restores reserved stock when a delivery document (DN or update_stock SI) is cancelled (reverses FIFO consumption).
    /// Per ERPNext PR #58613 / commit 7ecfa6b356: restores DeliveredQty on active SREs in LIFO order so reserved stock becomes available again.
    /// Per ERPNext PR #59424 / commit dbada3f461: match batch where applicable.
    /// </summary>
    public async Task<ReservationConsumption[]> RestoreOnCancelDeliveryAsync(
        Guid itemId, Guid warehouseId, decimal deliveredQty, Guid? voucherId = null, Guid? batchId = null)
    {
        if (deliveredQty <= 0) return Array.Empty<ReservationConsumption>();

        var queryable = await _sreRepository.GetQueryableAsync();
        var deliveredSres = queryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && s.DeliveredQty > 0
                && (voucherId == null || s.VoucherId == voucherId)
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.CreationTime)
            .ToList();

        var restored = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = deliveredQty;

        foreach (var sre in deliveredSres)
        {
            if (remaining <= 0) break;

            var canRestore = GetQtyToUndeliver(sre.DeliveredQty, remaining);
            if (canRestore <= 0) continue;

            sre.RevertDelivery(canRestore);
            await _sreRepository.UpdateAsync(sre);

            restored.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = canRestore
            });

            remaining = Math.Max(0m, remaining - canRestore);
        }

        return restored.ToArray();
    }

    /// <summary>
    /// Cancels or releases all active reservations for a voucher (used on SO/WO/SCO cancel/close).
    /// Per ERPNext PR #50773 / commit 9b5d215a7a, and PR #60510 / commit eecf91e143:
    /// When closing a voucher, if a reservation has already transferred, consumed, or delivered stock,
    /// its reserved qty is trimmed to the used qty and the unused balance is released from Bin.
    /// If none was used, the reservation is cancelled completely and its reserved qty released from Bin.
    /// </summary>
    public async Task CancelReservationsForVoucherAsync(Guid voucherId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.VoucherId == voucherId
                && s.Status == DocumentStatus.Submitted)
            .ToList();

        foreach (var sre in activeSres)
        {
            var usedQty = sre.DeliveredQty + sre.TransferredQty + sre.ConsumedQty;
            var unusedQty = Math.Max(0m, sre.ReservedQty - usedQty);

            if (usedQty > 0)
            {
                sre.ReservedQty = usedQty;
                await _sreRepository.UpdateAsync(sre);

                if (unusedQty > 0 && _binRepository != null)
                {
                    var binQueryable = await _binRepository.GetQueryableAsync();
                    var bin = binQueryable.FirstOrDefault(b => b.ItemId == sre.ItemId && b.WarehouseId == sre.WarehouseId);
                    if (bin != null)
                    {
                        bin.ReservedQty = Math.Max(0m, bin.ReservedQty - unusedQty);
                        await _binRepository.UpdateAsync(bin);
                    }
                }
            }
            else
            {
                sre.Cancel();
                await _sreRepository.UpdateAsync(sre);

                if (sre.ReservedQty > 0 && _binRepository != null)
                {
                    var binQueryable = await _binRepository.GetQueryableAsync();
                    var bin = binQueryable.FirstOrDefault(b => b.ItemId == sre.ItemId && b.WarehouseId == sre.WarehouseId);
                    if (bin != null)
                    {
                        bin.ReservedQty = Math.Max(0m, bin.ReservedQty - sre.ReservedQty);
                        await _binRepository.UpdateAsync(bin);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates whether a Stock Reservation Entry can be cancelled directly.
    /// Per ERPNext PR #60510 (commit b7e3383f5c / 2bee21840a) and commits 4bf8b4ded2 &amp; 719a5cdd31:
    /// Refuse to cancel a Work Order or Subcontracting Order reservation if it has already transferred or consumed stock.
    /// The stock is released by closing the order, not by cancelling the reservation.
    /// A closed Subcontracting Order can be re-opened, so its used reservations cannot be cancelled while closed.
    /// </summary>
    public async Task ValidateCanCancelAsync(
        StockReservationEntry sre,
        IRepository<Manufacturing.Entities.WorkOrder, Guid>? woRepository = null,
        IRepository<Purchasing.Entities.SubcontractingOrder, Guid>? scoRepository = null)
    {
        if (sre.TransferredQty <= 0 && sre.ConsumedQty <= 0)
        {
            return;
        }

        if (sre.VoucherType is "Work Order" or "WorkOrder")
        {
            if (woRepository != null)
            {
                var wo = await woRepository.FindAsync(sre.VoucherId);
                if (wo != null && wo.Status != Manufacturing.WorkOrderStatus.Closed && wo.Status != Manufacturing.WorkOrderStatus.Cancelled)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled)
                        .WithData("reservationId", sre.Id)
                        .WithData("voucherNo", wo.WorkOrderNumber);
                }
            }
            else
            {
                throw new BusinessException(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled)
                    .WithData("reservationId", sre.Id)
                    .WithData("voucherNo", sre.VoucherId);
            }
        }
        else if (sre.VoucherType is "Subcontracting Order" or "SubcontractingOrder")
        {
            if (scoRepository != null)
            {
                var sco = await scoRepository.FindAsync(sre.VoucherId);
                // Per ERPNext commit 719a5cdd31: a closed Subcontracting Order keeps its used reservations and can be re-opened
                if (sco != null && sco.Status != Purchasing.SubcontractingOrderStatus.Cancelled)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled)
                        .WithData("reservationId", sre.Id)
                        .WithData("voucherNo", sco.OrderNumber);
                }
            }
            else
            {
                throw new BusinessException(MyERPDomainErrorCodes.UsedWorkOrderReservationCannotBeCancelled)
                    .WithData("reservationId", sre.Id)
                    .WithData("voucherNo", sre.VoucherId);
            }
        }
    }

    /// <summary>
    /// Cancels all active reservations for a Sales Order (used on SO cancel/close).
    /// </summary>
    public async Task CancelReservationsForOrderAsync(Guid salesOrderId) =>
        await CancelReservationsForVoucherAsync(salesOrderId);

    /// <summary>
    /// Checks whether an individual item row has active reserved stock (per ERPNext PR #57596 has_reserved_stock).
    /// Used before closing an item row in Sales Order.
    /// </summary>
    /// <summary>
    /// Checks whether a voucher (e.g. Sales Order) has any active reserved stock remaining (per ERPNext commit b8154bedb4).
    /// Closing must not strand reserved stock; user must unreserve deliberately first.
    /// </summary>
    public async Task<bool> HasReservedStockAsync(Guid voucherId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        return queryable.Any(s =>
            s.VoucherId == voucherId
            && s.Status == DocumentStatus.Submitted
            && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0);
    }

    public async Task<bool> HasReservedStockForItemAsync(Guid voucherId, Guid voucherDetailId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        return queryable.Any(s =>
            s.VoucherId == voucherId
            && s.VoucherDetailId == voucherDetailId
            && s.Status == DocumentStatus.Submitted
            && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0);
    }

    /// <summary>
    /// Validates a Delivery Note item's warehouse against active reservations for its SO item,
    /// auto-resolving it when unset. Per ERPNext validate_against_stock_reservation_entries:
    /// no-op when the item has no active reservations at all (nothing to fulfil against);
    /// auto-set from the first reserved warehouse when the DN item's own warehouse is unset;
    /// hard error when the DN item's warehouse is set but doesn't match ANY reserved warehouse
    /// for that item — delivering from the wrong warehouse would silently strand the
    /// reservation (ConsumeOnDeliveryAsync filters by warehouse, so a mismatched delivery
    /// consumes nothing, leaving the reservation dangling until the auto-cancel job clears it).
    /// Returns the resolved warehouse id: the reserved one when the DN item had none set, the
    /// validated existing one when it matches a reservation, or null unchanged when the item has
    /// no active reservations at all (nothing for this method to validate or resolve — a
    /// separately-enforced "warehouse required for stock items" rule owns that case).
    /// </summary>
    public async Task<Guid?> ValidateOrResolveWarehouseAsync(Guid itemId, Guid salesOrderId, Guid? currentWarehouseId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        var reservedWarehouseIds = queryable
            .Where(s => s.ItemId == itemId
                && s.VoucherId == salesOrderId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .Select(s => s.WarehouseId)
            .Distinct()
            .ToList();

        if (reservedWarehouseIds.Count == 0)
            return currentWarehouseId;

        if (!currentWarehouseId.HasValue)
            return reservedWarehouseIds[0];

        if (!reservedWarehouseIds.Contains(currentWarehouseId.Value))
            throw new BusinessException(MyERPDomainErrorCodes.DeliveryWarehouseNotReserved)
                .WithData("itemId", itemId)
                .WithData("warehouseId", currentWarehouseId.Value);

        return currentWarehouseId.Value;
    }

    /// <summary>
    /// Gets total reserved qty for an item+warehouse across all active SREs.
    /// Per ERPNext PR #47049 / commit 27d674d54a: deduct delivered, transferred, and consumed quantities.
    /// </summary>
    public async Task<decimal> GetReservedQtyAsync(Guid itemId, Guid warehouseId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        return queryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .Sum(s => s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty);
    }

    /// <summary>
    /// Creates a new Stock Reservation Entry and validates availability.
    /// Per ERPNext auto_reserve_stock_for_sales_order_on_purchase: auto-reserves on PR submit.
    /// Supports batch-specific stock reservation and as-of posting date stock validation.
    /// </summary>
    public async Task ReserveStockAsync(
        Guid itemId, Guid warehouseId, Guid companyId,
        decimal qty, string voucherType, Guid voucherId, Guid? batchId = null, Guid? tenantId = null,
        decimal? voucherDemandQty = null, Guid? voucherDetailId = null, DateTime? postingDate = null,
        string? fromVoucherType = null, Guid? fromVoucherId = null)
    {
        qty = Math.Round(qty, 4);
        if (qty <= 0) return;

        // Validate reservation warehouse belongs to the company (ERPNext PR #59647 / commit c7a9f069b7)
        if (_warehouseRepository != null)
        {
            var warehouse = await _warehouseRepository.FindAsync(warehouseId);
            if (warehouse != null && warehouse.CompanyId != companyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("warehouseCompany", warehouse.CompanyId)
                    .WithData("companyId", companyId);
            }
        }

        // Per ERPNext PR #60725 / commit f8a1b44e98:
        // Auto-reserved batches and stock are picked as of the reservation time (now), not the order date.
        // Therefore, stock availability check uses current stock without capping to a past voucher posting date,
        // allowing batches and stock received after the voucher order date to be reserved.
        await ValidateAvailabilityAsync(itemId, warehouseId, qty, batchId);

        var demandQty = voucherDemandQty.HasValue ? Math.Round(voucherDemandQty.Value, 4) : qty;

        // Per ERPNext PR #59604 (commit 62bf0ff1ae): prevent duplicate serial and batch reservations.
        // Check allowed quantity across all entries for the voucher row across all warehouses.
        if (voucherDemandQty.HasValue)
        {
            var sreQueryable = await _sreRepository.GetQueryableAsync();
            var rowReservedQuery = sreQueryable
                .Where(s => s.VoucherType == voucherType
                    && s.VoucherId == voucherId
                    && s.ItemId == itemId
                    && s.Status == DocumentStatus.Submitted);

            if (voucherDetailId.HasValue)
            {
                rowReservedQuery = rowReservedQuery.Where(s => s.VoucherDetailId == voucherDetailId);
            }

            // For Work Order and Subcontracting Order, consumed stock stays counted in the row reservation limit
            // so the row cannot be reserved past its required quantity.
            var existingRowReserved = rowReservedQuery
                .ToList()
                .Sum(s => s.ReservedQty - s.TransferredQty - s.DeliveredQty - (voucherType is "Work Order" or "WorkOrder" or "Subcontracting Order" or "SubcontractingOrder" ? 0m : s.ConsumedQty));

            var roundedRequested = Math.Round(qty, 4, MidpointRounding.AwayFromZero);
            var allowedQty = Math.Max(0m, Math.Round(demandQty - existingRowReserved, 4, MidpointRounding.AwayFromZero));
            if (roundedRequested > allowedQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot reserve more than allowed quantity of {allowedQty} for item against {voucherType} {voucherId}. Already reserved: {existingRowReserved}, Required: {demandQty}.");
            }
        }

        var sre = new StockReservationEntry(
            CreateGuid(), companyId, itemId, warehouseId,
            voucherType, voucherId, qty, voucherQty: demandQty, tenantId: tenantId)
        {
            BatchId = batchId,
            VoucherDetailId = voucherDetailId,
            FromVoucherType = fromVoucherType,
            FromVoucherId = fromVoucherId
        };

        sre.Submit();
        await _sreRepository.InsertAsync(sre);

        // Update Bin reserved qty
        var binQueryable = await _binRepository.GetQueryableAsync();
        var bin = binQueryable
            .FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);
        if (bin != null)
        {
            bin.ReservedQty += qty;
            await _binRepository.UpdateAsync(bin);
        }
    }

    /// <summary>
    /// Transfers reservation entries from a source voucher (e.g. Production Plan) to a target voucher (e.g. Work Order or Subcontracting Order).
    /// Updates source entries (marks transferred_qty) and creates target reservation entries linked via FromVoucher.
    /// Per ERPNext commits 0bc3cfe29d, 3d4ca309aa, da63d14d7e: transfer_reservation_entries_to.
    /// </summary>
    public async Task TransferReservationEntriesAsync(
        string fromVoucherType, Guid fromVoucherId,
        string toVoucherType, Guid toVoucherId,
        Guid itemId, Guid warehouseId, decimal qty,
        Guid? toVoucherDetailId = null,
        decimal? voucherDemandQty = null)
    {
        await TransferReservationEntriesWithTotalAsync(
            fromVoucherType, fromVoucherId, toVoucherType, toVoucherId,
            itemId, warehouseId, qty, toVoucherDetailId, voucherDemandQty);
    }

    public async Task<decimal> TransferReservationEntriesWithTotalAsync(
        string fromVoucherType, Guid fromVoucherId,
        string toVoucherType, Guid toVoucherId,
        Guid itemId, Guid warehouseId, decimal qty,
        Guid? toVoucherDetailId = null,
        decimal? voucherDemandQty = null)
    {
        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var sourceEntries = sreQueryable
            .Where(s => s.VoucherType == fromVoucherType
                && s.VoucherId == fromVoucherId
                && s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .OrderByDescending(s => s.BatchId != null || s.SerialAndBatchBundleId != null)
            .ThenBy(s => s.CreationTime)
            .ToList();

        var remaining = Math.Round(qty, 4);
        var totalTransferred = 0m;
        foreach (var src in sourceEntries)
        {
            if (remaining <= 0) break;
            var available = src.ReservedQty - src.DeliveredQty - src.TransferredQty - src.ConsumedQty;
            var transferQty = Math.Round(Math.Min(available, remaining), 4);
            if (transferQty <= 0) continue;

            src.TransferredQty = Math.Round(src.TransferredQty + transferQty, 4);
            await _sreRepository.UpdateAsync(src);

            var targetVoucherQty = voucherDemandQty.HasValue ? Math.Round(voucherDemandQty.Value, 4) : transferQty;
            var newSre = new StockReservationEntry(
                CreateGuid(),
                src.CompanyId,
                src.ItemId,
                src.WarehouseId,
                toVoucherType,
                toVoucherId,
                transferQty,
                targetVoucherQty,
                src.TenantId)
            {
                VoucherDetailId = toVoucherDetailId,
                FromVoucherType = fromVoucherType,
                FromVoucherId = fromVoucherId,
                FromVoucherDetailId = src.VoucherDetailId,
                BatchId = src.BatchId,
                SerialAndBatchBundleId = src.SerialAndBatchBundleId
            };
            newSre.Submit();
            await _sreRepository.InsertAsync(newSre);

            totalTransferred = Math.Round(totalTransferred + transferQty, 4);
            remaining = Math.Max(0m, Math.Round(remaining - transferQty, 4));
        }

        return totalTransferred;
    }

    /// <summary>
    /// Creates a new Stock Reservation Entry originating from a Pick List (per ERPNext PR #59134).
    /// Links VoucherType = SalesOrder, FromVoucherType = Pick List, and updates Bin reserved qty.
    /// </summary>
    public async Task<StockReservationEntry> ReserveStockFromPickListAsync(
        Guid itemId, Guid warehouseId, Guid companyId,
        decimal qty, string voucherType, Guid voucherId,
        string fromVoucherType, Guid fromVoucherId,
        Guid? batchId = null, Guid? voucherDetailId = null,
        Guid? fromVoucherDetailId = null, Guid? tenantId = null)
    {
        qty = Math.Round(qty, 4);
        if (qty <= 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive).WithData("field", nameof(qty));

        // Validate reservation warehouse belongs to the company (ERPNext PR #59647 / commit c7a9f069b7)
        if (_warehouseRepository != null)
        {
            var warehouse = await _warehouseRepository.FindAsync(warehouseId);
            if (warehouse != null && warehouse.CompanyId != companyId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.CompanyMismatch)
                    .WithData("warehouseCompany", warehouse.CompanyId)
                    .WithData("companyId", companyId);
            }
        }

        await ValidateAvailabilityAsync(itemId, warehouseId, qty, batchId);

        var sre = new StockReservationEntry(
            CreateGuid(), companyId, itemId, warehouseId,
            voucherType, voucherId, qty, voucherQty: qty, tenantId: tenantId)
        {
            BatchId = batchId,
            VoucherDetailId = voucherDetailId,
            FromVoucherType = fromVoucherType,
            FromVoucherId = fromVoucherId,
            FromVoucherDetailId = fromVoucherDetailId
        };

        sre.Submit();
        await _sreRepository.InsertAsync(sre);

        var binQueryable = await _binRepository.GetQueryableAsync();
        var bin = binQueryable.FirstOrDefault(b => b.ItemId == itemId && b.WarehouseId == warehouseId);
        if (bin != null)
        {
            bin.ReservedQty += qty;
            await _binRepository.UpdateAsync(bin);
        }

        return sre;
    }

    /// <summary>
    /// Cancels all active reservations originating from a specific source voucher (e.g. Pick List).
    /// Per ERPNext PR #59134: unreserving from a Pick List releases reserved stock and restores bin reserved qty.
    /// </summary>
    public async Task<List<StockReservationEntry>> CancelReservationsFromVoucherAsync(string fromVoucherType, Guid fromVoucherId)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.FromVoucherType == fromVoucherType
                && s.FromVoucherId == fromVoucherId
                && s.Status == DocumentStatus.Submitted)
            .ToList();

        var binQueryable = _binRepository != null ? await _binRepository.GetQueryableAsync() : null;

        foreach (var sre in activeSres)
        {
            var unreservedQty = sre.AvailableQty;
            sre.Cancel();
            await _sreRepository.UpdateAsync(sre);

            if (unreservedQty > 0 && _binRepository != null && binQueryable != null)
            {
                var bin = binQueryable.FirstOrDefault(b => b.ItemId == sre.ItemId && b.WarehouseId == sre.WarehouseId);
                if (bin != null)
                {
                    bin.ReservedQty = Math.Max(0m, bin.ReservedQty - unreservedQty);
                    await _binRepository.UpdateAsync(bin);
                }
            }
        }

        var uow = LazyServiceProvider?.LazyGetService<Volo.Abp.Uow.IUnitOfWorkManager>()?.Current;
        if (activeSres.Count > 0 && uow != null)
        {
            await uow.SaveChangesAsync();
        }

        return activeSres;
    }

    /// <summary>
    /// Gets remaining held reserved quantity on a Stock Reservation Entry (PR #59424 / commit dbada3f461).
    /// </summary>
    public static decimal GetHeldQty(StockReservationEntry sre)
    {
        return Math.Max(0m, sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty);
    }

    /// <summary>
    /// Computes the unreserved quantity for each supplied item row of a Subcontracting Order.
    /// Per ERPNext commits 3bf9fc0a98 and abd21367b0:
    /// A transfer's reservation (FromVoucherType == "StockEntry" / "Stock Entry") holds stock in the
    /// supplier warehouse that <see cref="Purchasing.Entities.SubcontractingOrderSuppliedItem.TransferredQty"/>
    /// already counts, so it must not be subtracted again as unsupplied reserved stock.
    /// Only rows with positive unreserved quantity are returned.
    /// </summary>
    public static Dictionary<Guid, decimal> GetSubcontractingUnreservedQty(
        Purchasing.Entities.SubcontractingOrder sco,
        IEnumerable<StockReservationEntry> activeReservations,
        int precision = 4)
    {
        var heldByRow = new Dictionary<Guid, decimal>();
        var heldByItem = new Dictionary<Guid, decimal>();

        foreach (var sre in activeReservations)
        {
            if (sre.Status != DocumentStatus.Submitted)
                continue;

            if (string.Equals(sre.FromVoucherType, "StockEntry", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sre.FromVoucherType, "Stock Entry", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var held = GetHeldQty(sre);
            if (held <= 0m)
                continue;

            if (sre.VoucherDetailId.HasValue && sre.VoucherDetailId.Value != Guid.Empty)
            {
                heldByRow[sre.VoucherDetailId.Value] = heldByRow.GetValueOrDefault(sre.VoucherDetailId.Value) + held;
            }
            else
            {
                heldByItem[sre.ItemId] = heldByItem.GetValueOrDefault(sre.ItemId) + held;
            }
        }

        var unreserved = new Dictionary<Guid, decimal>();
        foreach (var supplied in sco.SuppliedItems)
        {
            var held = heldByRow.TryGetValue(supplied.Id, out var rowHeld)
                ? rowHeld
                : heldByItem.GetValueOrDefault(supplied.ItemId);

            var qty = Math.Round(supplied.RequiredQty - supplied.TransferredQty - held, precision);
            if (qty > 0m)
            {
                unreserved[supplied.Id] = qty;
            }
        }

        return unreserved;
    }

    /// <summary>
    /// Updates transferred quantity on active Work Order reservations during Material Transfer.
    /// Per ERPNext PR #59424 / commit dbada3f461: only consumes reservations matching the transferred batch.
    /// Per ERPNext PR #60647 / commit 260b828b09: creates WIP warehouse reservations linked to the transfer stock entry.
    /// </summary>
    private static bool IsWorkOrderVoucher(string? voucherType) =>
        voucherType is "WorkOrder" or "Work Order";

    private static bool IsSubcontractingOrderVoucher(string? voucherType) =>
        voucherType is "SubcontractingOrder" or "Subcontracting Order";

    private async Task<ReservationConsumption[]> ApplyOrderTransferAsync(
        Func<string?, bool> matchVoucherType,
        Guid voucherId,
        Guid itemId,
        Guid sourceWarehouseId,
        decimal transferredQty,
        Guid? batchId,
        Guid? voucherDetailId,
        Guid? targetWarehouseId,
        Guid? stockEntryId,
        Guid? stockEntryItemId,
        IDictionary<Guid, decimal>? sharedBatchRemaining)
    {
        if (transferredQty <= 0) return Array.Empty<ReservationConsumption>();
        // PR #66749: supplier/target warehouse reservations belong to the receipt, never the transfer.
        if (targetWarehouseId.HasValue && sourceWarehouseId == targetWarehouseId.Value)
            return Array.Empty<ReservationConsumption>();

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.VoucherId == voucherId
                && s.ItemId == itemId
                && s.WarehouseId == sourceWarehouseId
                && (!targetWarehouseId.HasValue || s.WarehouseId != targetWarehouseId.Value)
                && (s.Status == DocumentStatus.Submitted || s.TransferredQty > 0)
                && (voucherDetailId == null || s.VoucherDetailId == voucherDetailId)
                && (s.ReservedQty - s.TransferredQty - s.ConsumedQty) > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenByDescending(s => s.BatchId != null || s.SerialAndBatchBundleId != null)
            .ThenBy(s => s.CreationTime)
            .ToList()
            .Where(s => matchVoucherType(s.VoucherType))
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = transferredQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var available = sre.AvailableQty;
            if (available <= 0) continue;

            var maxAllowed = Math.Min(remaining, available);
            // PR #66645: share a batch's transferred qty across reservations so no batch is counted twice.
            var trackedBatchId = sre.BatchId ?? batchId;
            if (trackedBatchId.HasValue && sharedBatchRemaining != null
                && sharedBatchRemaining.TryGetValue(trackedBatchId.Value, out var batchPool))
            {
                if (batchPool <= 0m) continue;
                maxAllowed = Math.Min(maxAllowed, batchPool);
            }

            if (maxAllowed <= 0m) continue;

            sre.TransferredQty = Math.Round(sre.TransferredQty + maxAllowed, 4);
            await _sreRepository.UpdateAsync(sre);

            if (trackedBatchId.HasValue && sharedBatchRemaining != null
                && sharedBatchRemaining.TryGetValue(trackedBatchId.Value, out var currentPool))
            {
                sharedBatchRemaining[trackedBatchId.Value] = Math.Max(0m, Math.Round(currentPool - maxAllowed, 4));
            }

            if (targetWarehouseId.HasValue)
            {
                var targetSre = new StockReservationEntry(
                    CreateGuid(),
                    sre.CompanyId,
                    sre.ItemId,
                    targetWarehouseId.Value,
                    sre.VoucherType,
                    sre.VoucherId,
                    maxAllowed,
                    voucherQty: maxAllowed,
                    tenantId: sre.TenantId)
                {
                    VoucherDetailId = sre.VoucherDetailId ?? voucherDetailId,
                    BatchId = sre.BatchId ?? batchId,
                    FromVoucherType = "StockEntry",
                    FromVoucherId = stockEntryId ?? sre.Id,
                    FromVoucherDetailId = stockEntryItemId ?? sre.VoucherDetailId
                };
                targetSre.Submit();
                await _sreRepository.InsertAsync(targetSre);
            }

            consumed.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = maxAllowed
            });

            remaining = Math.Max(0m, Math.Round(remaining - maxAllowed, 4));
        }

        return consumed.ToArray();
    }

    private async Task RevertOrderTransferAsync(
        Func<string?, bool> matchVoucherType,
        Guid voucherId,
        Guid itemId,
        Guid sourceWarehouseId,
        decimal revertedQty,
        Guid? batchId,
        Guid? voucherDetailId,
        Guid? targetWarehouseId,
        Guid? stockEntryId)
    {
        if (revertedQty <= 0) return;

        var queryable = await _sreRepository.GetQueryableAsync();

        if (targetWarehouseId.HasValue || stockEntryId.HasValue)
        {
            var targetSres = queryable
                .Where(s => s.VoucherId == voucherId
                    && s.ItemId == itemId
                    && (targetWarehouseId == null || s.WarehouseId == targetWarehouseId.Value)
                    && s.Status == DocumentStatus.Submitted
                    && (stockEntryId == null || s.FromVoucherId == stockEntryId.Value))
                .ToList()
                .Where(s => matchVoucherType(s.VoucherType))
                .ToList();

            var remainingTargetRevert = revertedQty;
            foreach (var tsre in targetSres)
            {
                if (remainingTargetRevert <= 0) break;
                var unconsumed = tsre.ReservedQty - tsre.ConsumedQty;
                if (unconsumed <= 0) continue;

                var cancelQty = Math.Min(remainingTargetRevert, unconsumed);
                if (cancelQty >= tsre.ReservedQty)
                {
                    tsre.Cancel();
                    await _sreRepository.UpdateAsync(tsre);
                }
                else
                {
                    tsre.ReservedQty -= cancelQty;
                    await _sreRepository.UpdateAsync(tsre);
                }
                remainingTargetRevert -= cancelQty;
            }
        }

        var activeSres = queryable
            .Where(s => s.VoucherId == voucherId
                && s.ItemId == itemId
                && s.WarehouseId == sourceWarehouseId
                && (!targetWarehouseId.HasValue || s.WarehouseId != targetWarehouseId.Value)
                && (s.Status == DocumentStatus.Submitted || s.TransferredQty > 0)
                && (voucherDetailId == null || s.VoucherDetailId == voucherDetailId)
                && s.TransferredQty > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenByDescending(s => s.CreationTime)
            .ToList()
            .Where(s => matchVoucherType(s.VoucherType))
            .ToList();

        var remaining = revertedQty;
        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var canRevert = Math.Min(remaining, sre.TransferredQty);
            sre.TransferredQty = Math.Round(Math.Max(0m, sre.TransferredQty - canRevert), 4);
            await _sreRepository.UpdateAsync(sre);
            remaining -= canRevert;
        }
    }

    public Task<ReservationConsumption[]> ApplyWorkOrderTransferAsync(
        Guid workOrderId,
        Guid itemId,
        Guid sourceWarehouseId,
        decimal transferredQty,
        Guid? batchId = null,
        Guid? workOrderItemId = null,
        Guid? targetWarehouseId = null,
        Guid? stockEntryId = null,
        Guid? stockEntryItemId = null,
        IDictionary<Guid, decimal>? sharedBatchRemaining = null)
        => ApplyOrderTransferAsync(
            IsWorkOrderVoucher, workOrderId, itemId, sourceWarehouseId, transferredQty,
            batchId, workOrderItemId, targetWarehouseId, stockEntryId, stockEntryItemId, sharedBatchRemaining);

    /// <summary>
    /// Updates transferred quantity on active Subcontracting Order reservations during SendToSubcontractor transfer
    /// (PR #66645 &amp; PR #66749): shares transferred batch qty and skips supplier warehouse reservations.
    /// </summary>
    public Task<ReservationConsumption[]> ApplySubcontractingTransferAsync(
        Guid subcontractingOrderId,
        Guid itemId,
        Guid sourceWarehouseId,
        decimal transferredQty,
        Guid? batchId = null,
        Guid? subcontractingSuppliedItemId = null,
        Guid? targetWarehouseId = null,
        Guid? stockEntryId = null,
        Guid? stockEntryItemId = null,
        IDictionary<Guid, decimal>? sharedBatchRemaining = null)
        => ApplyOrderTransferAsync(
            IsSubcontractingOrderVoucher, subcontractingOrderId, itemId, sourceWarehouseId, transferredQty,
            batchId, subcontractingSuppliedItemId, targetWarehouseId, stockEntryId, stockEntryItemId, sharedBatchRemaining);

    /// <summary>
    /// Reverts transferred quantity on Work Order reservations when a Material Transfer is cancelled.
    /// Per ERPNext PR #60647: cancels WIP reservations created by the transfer.
    /// </summary>
    public Task RevertWorkOrderTransferAsync(
        Guid workOrderId,
        Guid itemId,
        Guid sourceWarehouseId,
        decimal revertedQty,
        Guid? batchId = null,
        Guid? workOrderItemId = null,
        Guid? targetWarehouseId = null,
        Guid? stockEntryId = null)
        => RevertOrderTransferAsync(
            IsWorkOrderVoucher, workOrderId, itemId, sourceWarehouseId, revertedQty,
            batchId, workOrderItemId, targetWarehouseId, stockEntryId);

    /// <summary>
    /// Reverts transferred quantity on Subcontracting Order reservations when a SendToSubcontractor entry is cancelled.
    /// </summary>
    public Task RevertSubcontractingTransferAsync(
        Guid subcontractingOrderId,
        Guid itemId,
        Guid sourceWarehouseId,
        decimal revertedQty,
        Guid? batchId = null,
        Guid? subcontractingSuppliedItemId = null,
        Guid? targetWarehouseId = null,
        Guid? stockEntryId = null)
        => RevertOrderTransferAsync(
            IsSubcontractingOrderVoucher, subcontractingOrderId, itemId, sourceWarehouseId, revertedQty,
            batchId, subcontractingSuppliedItemId, targetWarehouseId, stockEntryId);

    /// <summary>
    /// Rebuilds source-side TransferredQty on Subcontracting Order reservations when a SendToSubcontractor
    /// Stock Entry is cancelled (ERPNext commits aac9fc9a8a &amp; 47bd8f9c7f). Cancels target-warehouse
    /// reservations created by <paramref name="excludedStockEntryId"/> and replays each remaining active
    /// SendToSubcontractor transfer oldest-first—timed by its first active outward Stock Ledger Entry
    /// (fallback StockEntry.CreationTime)—only against reservations created before that transfer.
    /// </summary>
    public async Task RebuildSubcontractingTransfersAsync(
        Guid subcontractingOrderId,
        IRepository<StockEntry, Guid> stockEntryRepository,
        IRepository<StockLedgerEntry, Guid>? sleRepository = null,
        Guid? excludedStockEntryId = null,
        IEnumerable<Guid>? itemIds = null)
    {
        var sreQueryable = await _sreRepository.GetQueryableAsync();

        if (excludedStockEntryId.HasValue)
        {
            var cancelledTargetSres = sreQueryable
                .Where(s => s.VoucherId == subcontractingOrderId
                    && s.Status == DocumentStatus.Submitted
                    && s.FromVoucherType == "StockEntry"
                    && s.FromVoucherId == excludedStockEntryId.Value)
                .ToList()
                .Where(s => IsSubcontractingOrderVoucher(s.VoucherType))
                .ToList();

            foreach (var tsre in cancelledTargetSres)
            {
                if (tsre.ConsumedQty > 0m)
                {
                    tsre.ReservedQty = tsre.ConsumedQty;
                }
                else
                {
                    tsre.Cancel();
                }
                await _sreRepository.UpdateAsync(tsre);
            }
        }

        var itemFilter = itemIds?.Distinct().ToHashSet();
        var sourceSres = sreQueryable
            .Where(s => s.VoucherId == subcontractingOrderId
                && s.FromVoucherType != "StockEntry"
                && (s.Status == DocumentStatus.Submitted || s.TransferredQty > 0m))
            .ToList()
            .Where(s => IsSubcontractingOrderVoucher(s.VoucherType)
                && (itemFilter == null || itemFilter.Count == 0 || itemFilter.Contains(s.ItemId)))
            .ToList();

        if (sourceSres.Count == 0)
            return;

        foreach (var sre in sourceSres)
        {
            sre.TransferredQty = 0m;
        }

        var seQuery = await stockEntryRepository.WithDetailsAsync(se => se.Items);
        var remainingTransfers = seQuery
            .Where(se => se.SubcontractingOrderId == subcontractingOrderId
                && se.EntryType == StockEntryType.SendToSubcontractor
                && se.Status != DocumentStatus.Draft
                && se.Status != DocumentStatus.Cancelled
                && (!excludedStockEntryId.HasValue || se.Id != excludedStockEntryId.Value))
            .ToList();

        if (remainingTransfers.Count > 0)
        {
            var firstSleTimeBySe = new Dictionary<Guid, DateTime>();
            var activeSleRepo = sleRepository ?? _sleRepository;
            if (activeSleRepo != null)
            {
                var remainingSeIds = remainingTransfers.Select(se => se.Id).ToList();
                var sleQuery = await activeSleRepo.GetQueryableAsync();
                firstSleTimeBySe = sleQuery
                    .Where(sle => !sle.IsCancelled
                        && sle.VoucherType == "StockEntry"
                        && sle.VoucherId.HasValue
                        && remainingSeIds.Contains(sle.VoucherId.Value)
                        && sle.QuantityChange < 0m)
                    .ToList()
                    .GroupBy(sle => sle.VoucherId!.Value)
                    .ToDictionary(g => g.Key, g => g.Min(x => x.CreationTime));
            }

            var timedTransfers = remainingTransfers
                .Select(se => (
                    StockEntry: se,
                    TransferTime: firstSleTimeBySe.TryGetValue(se.Id, out var sleTime) ? sleTime : se.CreationTime))
                .OrderBy(x => x.TransferTime)
                .ThenBy(x => x.StockEntry.CreationTime)
                .ThenBy(x => x.StockEntry.Id)
                .ToList();

            foreach (var (se, transferTime) in timedTransfers)
            {
                var batchPool = se.Items
                    .Where(i => i.BatchId.HasValue && i.SourceWarehouseId.HasValue)
                    .GroupBy(i => i.BatchId!.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.StockQty > 0m ? i.StockQty : i.Quantity));

                foreach (var seItem in se.Items.Where(i => i.SourceWarehouseId.HasValue))
                {
                    if (itemFilter != null && itemFilter.Count > 0 && !itemFilter.Contains(seItem.ItemId))
                        continue;

                    var transferQty = seItem.StockQty > 0m ? seItem.StockQty : seItem.Quantity;
                    if (transferQty <= 0m) continue;

                    var candidates = sourceSres
                        .Where(s => s.ItemId == seItem.ItemId
                            && s.WarehouseId == seItem.SourceWarehouseId!.Value
                            && (!seItem.TargetWarehouseId.HasValue || s.WarehouseId != seItem.TargetWarehouseId.Value)
                            && s.CreationTime < transferTime
                            && (s.ReservedQty - s.TransferredQty - s.ConsumedQty) > 0m
                            && (s.BatchId == null || (seItem.BatchId != null && s.BatchId == seItem.BatchId)))
                        .OrderByDescending(s => s.BatchId == seItem.BatchId && seItem.BatchId != null)
                        .ThenByDescending(s => s.BatchId != null || s.SerialAndBatchBundleId != null)
                        .ThenBy(s => s.CreationTime)
                        .ToList();

                    var remaining = transferQty;
                    foreach (var sre in candidates)
                    {
                        if (remaining <= 0m) break;
                        var available = sre.AvailableQty;
                        if (available <= 0m) continue;

                        var maxAllowed = Math.Min(remaining, available);
                        var trackedBatchId = sre.BatchId ?? seItem.BatchId;
                        if (trackedBatchId.HasValue && batchPool.TryGetValue(trackedBatchId.Value, out var batchAvail))
                        {
                            if (batchAvail <= 0m) continue;
                            maxAllowed = Math.Min(maxAllowed, batchAvail);
                        }

                        if (maxAllowed <= 0m) continue;

                        sre.TransferredQty = Math.Round(sre.TransferredQty + maxAllowed, 4);
                        if (trackedBatchId.HasValue && batchPool.TryGetValue(trackedBatchId.Value, out var currentPool))
                        {
                            batchPool[trackedBatchId.Value] = Math.Max(0m, Math.Round(currentPool - maxAllowed, 4));
                        }
                        remaining = Math.Max(0m, Math.Round(remaining - maxAllowed, 4));
                    }
                }
            }
        }

        foreach (var sre in sourceSres)
        {
            await _sreRepository.UpdateAsync(sre);
        }
    }

    /// <summary>
    /// Updates consumed quantity on active Work Order reservations during Manufacture / Material Consumption.
    /// Per ERPNext PR #59424 / commit dbada3f461: only consumes reservations matching the consumed batch.
    /// Per ERPNext PR #60510 / commit 99ea89af89: split consumption over reservations a closed Work Order still holds.
    /// </summary>
    public Task<ReservationConsumption[]> ApplyWorkOrderConsumptionAsync(
        Guid workOrderId, Guid itemId, Guid warehouseId, decimal consumedQty, Guid? batchId = null, Guid? workOrderItemId = null)
        => ApplyOrderConsumptionAsync(
            IsWorkOrderVoucher, workOrderId, itemId, warehouseId, consumedQty, batchId, workOrderItemId);

    /// <summary>
    /// Updates consumed quantity on active Subcontracting Order reservations during Subcontracting Receipt submission
    /// (ERPNext commits 4bf8b4ded2 &amp; 7d20a4bf36).
    /// </summary>
    public Task<ReservationConsumption[]> ApplySubcontractingConsumptionAsync(
        Guid subcontractingOrderId, Guid itemId, Guid warehouseId, decimal consumedQty, Guid? batchId = null, Guid? subcontractingSuppliedItemId = null)
        => ApplyOrderConsumptionAsync(
            IsSubcontractingOrderVoucher, subcontractingOrderId, itemId, warehouseId, consumedQty, batchId, subcontractingSuppliedItemId, allowAnyWarehouseFallback: true);

    private async Task<ReservationConsumption[]> ApplyOrderConsumptionAsync(
        Func<string?, bool> matchVoucherType,
        Guid voucherId,
        Guid itemId,
        Guid warehouseId,
        decimal consumedQty,
        Guid? batchId = null,
        Guid? voucherDetailId = null,
        bool allowAnyWarehouseFallback = false)
    {
        if (consumedQty <= 0) return Array.Empty<ReservationConsumption>();

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.VoucherId == voucherId
                && s.ItemId == itemId
                && (s.WarehouseId == warehouseId || allowAnyWarehouseFallback)
                && (s.Status == DocumentStatus.Submitted || s.ConsumedQty > 0 || s.TransferredQty > 0)
                && (voucherDetailId == null || s.VoucherDetailId == null || s.VoucherDetailId == voucherDetailId)
                && ((s.TransferredQty > 0 ? s.TransferredQty : s.ReservedQty) - s.ConsumedQty) > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.WarehouseId == warehouseId)
            .ThenByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenByDescending(s => s.BatchId != null || s.SerialAndBatchBundleId != null)
            .ThenBy(s => s.CreationTime)
            .ToList()
            .Where(s => matchVoucherType(s.VoucherType))
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = consumedQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var available = (sre.TransferredQty > 0 ? sre.TransferredQty : sre.ReservedQty) - sre.ConsumedQty;
            if (available <= 0) continue;

            var consume = Math.Min(remaining, available);
            sre.ConsumedQty = Math.Round(sre.ConsumedQty + consume, 4);
            await _sreRepository.UpdateAsync(sre);

            consumed.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = consume
            });

            remaining = Math.Max(0m, Math.Round(remaining - consume, 4));
        }

        return consumed.ToArray();
    }

    /// <summary>
    /// Reverts consumed quantity on Work Order reservations when a Manufacture / Material Consumption entry is cancelled.
    /// Per ERPNext PR #60510 / commit 99ea89af89: match holding reservations on closed Work Orders.
    /// </summary>
    public Task RevertWorkOrderConsumptionAsync(
        Guid workOrderId, Guid itemId, Guid warehouseId, decimal revertedQty, Guid? batchId = null, Guid? workOrderItemId = null)
        => RevertOrderConsumptionAsync(
            IsWorkOrderVoucher, workOrderId, itemId, warehouseId, revertedQty, batchId, workOrderItemId);

    /// <summary>
    /// Reverts consumed quantity on Subcontracting Order reservations when a Subcontracting Receipt is cancelled.
    /// </summary>
    public Task RevertSubcontractingConsumptionAsync(
        Guid subcontractingOrderId, Guid itemId, Guid warehouseId, decimal revertedQty, Guid? batchId = null, Guid? subcontractingSuppliedItemId = null)
        => RevertOrderConsumptionAsync(
            IsSubcontractingOrderVoucher, subcontractingOrderId, itemId, warehouseId, revertedQty, batchId, subcontractingSuppliedItemId, allowAnyWarehouseFallback: true);

    private async Task RevertOrderConsumptionAsync(
        Func<string?, bool> matchVoucherType,
        Guid voucherId,
        Guid itemId,
        Guid warehouseId,
        decimal revertedQty,
        Guid? batchId = null,
        Guid? voucherDetailId = null,
        bool allowAnyWarehouseFallback = false)
    {
        if (revertedQty <= 0) return;

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.VoucherId == voucherId
                && s.ItemId == itemId
                && (s.WarehouseId == warehouseId || allowAnyWarehouseFallback)
                && (s.Status == DocumentStatus.Submitted || s.ConsumedQty > 0)
                && (voucherDetailId == null || s.VoucherDetailId == null || s.VoucherDetailId == voucherDetailId)
                && s.ConsumedQty > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.WarehouseId == warehouseId)
            .ThenByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenByDescending(s => s.CreationTime)
            .ToList()
            .Where(s => matchVoucherType(s.VoucherType))
            .ToList();

        var remaining = revertedQty;
        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var canRevert = Math.Min(remaining, sre.ConsumedQty);
            sre.ConsumedQty = Math.Max(0, sre.ConsumedQty - canRevert);
            await _sreRepository.UpdateAsync(sre);
            remaining -= canRevert;
        }
    }

    /// <summary>
    /// Gets active reserved materials for a voucher (Work Order, Subcontracting Order, etc.).
    /// Returns only untransferred and undelivered quantities (ERPNext PR #59756 / commit 0ee92cb885).
    /// </summary>
    public async Task<List<ReservedMaterialInfo>> GetReservedMaterialsAsync(string voucherType, Guid voucherId)
    {
        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var entries = sreQueryable
            .Where(s => (s.VoucherType == voucherType || s.VoucherType == voucherType.Replace(" ", ""))
                && s.VoucherId == voucherId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .OrderBy(s => s.CreationTime)
            .ToList();

        return entries.Select(s => new ReservedMaterialInfo
        {
            StockReservationEntryId = s.Id,
            ItemId = s.ItemId,
            WarehouseId = s.WarehouseId,
            BatchId = s.BatchId,
            VoucherDetailId = s.VoucherDetailId,
            AvailableQty = Math.Max(0m, s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty)
        }).ToList();
    }

    /// <summary>
    /// Allocates reserved batches up to the requested transfer quantity.
    /// Caps each batch row at the quantity still to allocate, and puts any quantity that no reserved batch covers on an unreserved row.
    /// Per ERPNext PR #59755 (commit 43fed6e512) and PR #59756 (commit 0ee92cb885).
    /// </summary>
    public async Task<List<MaterialTransferAllocation>> AllocateReservedMaterialsForTransferAsync(
        string voucherType,
        Guid voucherId,
        Guid itemId,
        Guid warehouseId,
        decimal requestedQty,
        Guid? voucherDetailId = null)
    {
        var result = new List<MaterialTransferAllocation>();
        if (requestedQty <= 0) return result;

        var reservedMaterials = await GetReservedMaterialsAsync(voucherType, voucherId);
        var activeForLine = reservedMaterials
            .Where(r => r.ItemId == itemId
                && r.WarehouseId == warehouseId
                && (voucherDetailId == null || r.VoucherDetailId == null || r.VoucherDetailId == voucherDetailId))
            .ToList();

        if (!activeForLine.Any())
        {
            result.Add(new MaterialTransferAllocation
            {
                BatchId = null,
                Quantity = requestedQty
            });
            return result;
        }

        var remaining = requestedQty;
        var batchEntries = activeForLine.Where(r => r.BatchId.HasValue).ToList();

        if (batchEntries.Any())
        {
            foreach (var batchEntry in batchEntries)
            {
                if (remaining <= 0) break;
                if (batchEntry.AvailableQty <= 0) continue;

                var allocQty = Math.Min(remaining, batchEntry.AvailableQty);
                result.Add(new MaterialTransferAllocation
                {
                    BatchId = batchEntry.BatchId,
                    Quantity = allocQty,
                    StockReservationEntryId = batchEntry.StockReservationEntryId
                });
                remaining -= allocQty;
            }

            // Cap each batch row at the qty still to allocate, and put the qty that no reserved batch covers on an unreserved row.
            // (PR #59755 test_transfer_adds_unreserved_row_for_short_reservation)
            if (remaining > 0)
            {
                result.Add(new MaterialTransferAllocation
                {
                    BatchId = null,
                    Quantity = remaining
                });
            }
        }
        else
        {
            // Only unreserved entries exist
            result.Add(new MaterialTransferAllocation
            {
                BatchId = null,
                Quantity = requestedQty
            });
        }

        return result;
    }

    private Guid CreateGuid()
    {
        try
        {
            return (LazyServiceProvider?.LazyGetService<Volo.Abp.Guids.IGuidGenerator>()?.Create()) ?? Guid.NewGuid();
        }
        catch
        {
            return Guid.NewGuid();
        }
    }

    /// <summary>
    /// Reserves received stock from a submitted Purchase Receipt or stock-updating Purchase Invoice
    /// for Production Plans whose Material Request items triggered the purchase, then hands off
    /// reservations to any linked reserving Work Orders (ERPNext ReceivedStockReservation / PR #65180).
    /// </summary>
    public async Task ReserveReceivedStockForProductionPlansAsync(
        string receiptVoucherType,
        Guid receiptVoucherId,
        Guid companyId,
        Guid? tenantId,
        IEnumerable<(Guid ItemId, Guid WarehouseId, decimal StockQty, Guid? MaterialRequestItemId)> lines)
    {
        var settingProvider = LazyServiceProvider?.LazyGetService<Volo.Abp.Settings.ISettingProvider>();
        if (settingProvider != null)
        {
            var enabled = await settingProvider.GetOrNullAsync(Settings.MyERPSettings.Stock.EnableStockReservation);
            if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var mrItemLines = lines
            .Where(l => l.MaterialRequestItemId.HasValue && l.StockQty > 0m)
            .ToList();
        if (mrItemLines.Count == 0)
        {
            return;
        }

        var mrRepo = LazyServiceProvider?.LazyGetService<IRepository<Purchasing.Entities.MaterialRequest, Guid>>();
        var planRepo = LazyServiceProvider?.LazyGetService<IRepository<Manufacturing.Entities.ProductionPlan, Guid>>();
        if (mrRepo == null || planRepo == null)
        {
            return;
        }

        var mrItemIds = mrItemLines.Select(l => l.MaterialRequestItemId!.Value).Distinct().ToHashSet();
        var mrWithDetails = await mrRepo.WithDetailsAsync(mr => mr.Items);
        var mrs = (mrWithDetails != null
                ? mrWithDetails.Where(mr =>
                    mr.CompanyId == companyId
                    && mr.Status != DocumentStatus.Draft
                    && mr.Status != DocumentStatus.Cancelled).ToList()
                : await mrRepo.GetListAsync(mr =>
                    mr.CompanyId == companyId
                    && mr.Status != DocumentStatus.Draft
                    && mr.Status != DocumentStatus.Cancelled));

        var mrItemPlanLookup = new Dictionary<Guid, (Guid ProductionPlanId, Guid? ProductionPlanMrItemId)>();
        foreach (var mr in mrs)
        {
            foreach (var mrItem in mr.Items)
            {
                if (mrItemIds.Contains(mrItem.Id) && mrItem.ProductionPlanId.HasValue)
                {
                    mrItemPlanLookup[mrItem.Id] = (mrItem.ProductionPlanId.Value, mrItem.ProductionPlanMrItemId);
                }
            }
        }

        if (mrItemPlanLookup.Count == 0)
        {
            return;
        }

        var planIds = mrItemPlanLookup.Values.Select(v => v.ProductionPlanId).Distinct().ToList();
        var planQueryable = await planRepo.WithDetailsAsync(p => p.MaterialRequirements);
        var reservablePlans = planQueryable
            .Where(p => planIds.Contains(p.Id)
                && p.ReserveStock
                && p.Status != Manufacturing.ProductionPlanStatus.Cancelled
                && p.Status != Manufacturing.ProductionPlanStatus.Completed
                && p.Status != Manufacturing.ProductionPlanStatus.Closed)
            .ToDictionary(p => p.Id);

        if (reservablePlans.Count == 0)
        {
            return;
        }

        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var existingClaimedByDetail = sreQueryable
            .Where(s => s.VoucherType == "ProductionPlan"
                && planIds.Contains(s.VoucherId)
                && s.Status == DocumentStatus.Submitted
                && s.VoucherDetailId.HasValue)
            .ToList()
            .GroupBy(s => s.VoucherDetailId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.ReservedQty));

        var reservedPlanItems = new Dictionary<Guid, HashSet<(Guid ItemId, Guid WarehouseId)>>();

        foreach (var line in mrItemLines)
        {
            if (!mrItemPlanLookup.TryGetValue(line.MaterialRequestItemId!.Value, out var planRef))
                continue;
            if (!reservablePlans.TryGetValue(planRef.ProductionPlanId, out var plan))
                continue;

            var planMrItem = planRef.ProductionPlanMrItemId.HasValue
                ? plan.MaterialRequirements.FirstOrDefault(m => m.Id == planRef.ProductionPlanMrItemId.Value)
                : plan.MaterialRequirements.FirstOrDefault(m => m.ItemId == line.ItemId);
            if (planMrItem == null)
                continue;

            var alreadyClaimed = existingClaimedByDetail.GetValueOrDefault(planMrItem.Id, 0m);
            var unclaimed = Math.Max(0m, Math.Round(planMrItem.RequiredQty - alreadyClaimed, 4));
            var qtyToReserve = Math.Round(Math.Min(line.StockQty, unclaimed), 4);
            if (qtyToReserve <= 0m)
                continue;

            existingClaimedByDetail[planMrItem.Id] = Math.Round(alreadyClaimed + qtyToReserve, 4);

            await ReserveStockAsync(
                line.ItemId,
                line.WarehouseId,
                companyId,
                qtyToReserve,
                voucherType: "ProductionPlan",
                voucherId: plan.Id,
                tenantId: tenantId,
                voucherDemandQty: planMrItem.RequiredQty,
                voucherDetailId: planMrItem.Id,
                fromVoucherType: receiptVoucherType,
                fromVoucherId: receiptVoucherId);

            if (!reservedPlanItems.TryGetValue(plan.Id, out var pairs))
            {
                pairs = new HashSet<(Guid, Guid)>();
                reservedPlanItems[plan.Id] = pairs;
            }
            pairs.Add((line.ItemId, line.WarehouseId));
        }

        if (reservedPlanItems.Count == 0)
            return;

        // Flush newly inserted ProductionPlan SREs so TransferReservationEntriesWithTotalAsync and
        // same-UoW queries (including CancelReservationsFromVoucherAsync) see them immediately.
        var uow = LazyServiceProvider?.LazyGetService<Volo.Abp.Uow.IUnitOfWorkManager>()?.Current;
        if (uow != null)
        {
            await uow.SaveChangesAsync();
        }

        var woRepo = LazyServiceProvider?.LazyGetService<IRepository<Manufacturing.Entities.WorkOrder, Guid>>();
        if (woRepo == null)
            return;

        var woQueryable = await woRepo.WithDetailsAsync(w => w.RequiredItems);
        var reservingWos = woQueryable
            .Where(w => w.ProductionPlanId.HasValue
                && reservedPlanItems.Keys.Contains(w.ProductionPlanId.Value)
                && w.ReserveStock
                && w.Status != Manufacturing.WorkOrderStatus.Draft
                && w.Status != Manufacturing.WorkOrderStatus.Cancelled
                && w.Status != Manufacturing.WorkOrderStatus.Closed
                && w.Status != Manufacturing.WorkOrderStatus.Completed)
            .ToList();

        foreach (var wo in reservingWos)
        {
            if (!wo.ProductionPlanId.HasValue || !reservedPlanItems.TryGetValue(wo.ProductionPlanId.Value, out var pairs))
                continue;

            var anyTransferred = false;
            foreach (var reqItem in wo.RequiredItems)
            {
                var whId = reqItem.SourceWarehouseId;
                if (!whId.HasValue)
                    continue;
                if (!pairs.Contains((reqItem.ItemId, whId.Value)))
                    continue;

                var remainingWoQty = Math.Max(0m, Math.Round(reqItem.RequiredQuantity - reqItem.StockReservedQty, 4));
                if (remainingWoQty <= 0m)
                    continue;

                var transferredQty = await TransferReservationEntriesWithTotalAsync(
                    "ProductionPlan", wo.ProductionPlanId.Value,
                    "WorkOrder", wo.Id,
                    reqItem.ItemId, whId.Value,
                    remainingWoQty, reqItem.Id,
                    voucherDemandQty: reqItem.RequiredQuantity);

                if (transferredQty > 0m)
                {
                    reqItem.SetStockReservedQty(Math.Round(reqItem.StockReservedQty + transferredQty, 4));
                    anyTransferred = true;
                }
            }

            if (anyTransferred)
            {
                wo.UpdateReservationStatus();
                await woRepo.UpdateAsync(wo);
            }
        }

        if (uow != null)
        {
            await uow.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Computes the default unreserved quantity offered in the Stock Reservation dialog for a document row
    /// per ERPNext commits 3bf7091474, 6e83c51495, and PR #67452 (commit a7ce0050fe):
    /// <paramref name="stockReservedQty"/> is what is still reserved, so both delivered/dispatched qty
    /// (<paramref name="dispatchedQtyInOrderUom"/> * <paramref name="conversionFactor"/>) AND
    /// <paramref name="stockReservedQty"/> are subtracted from <paramref name="stockQty"/>.
    /// </summary>
    public static decimal CalculateDialogUnreservedStockQty(
        decimal stockQty,
        decimal stockReservedQty,
        decimal dispatchedQtyInOrderUom = 0m,
        decimal conversionFactor = 1m,
        int precision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var unreservedStockQty = stockQty - (dispatchedQtyInOrderUom * factor) - stockReservedQty;
        var unreserved = Math.Round(unreservedStockQty, precision, MidpointRounding.AwayFromZero);
        return Math.Max(0m, unreserved);
    }

    /// <summary>
    /// Computes the unreserved quantity in order UOM for a Sales Order Item row
    /// per ERPNext PR #67452 (commit a7ce0050fe, <c>get_unreserved_qty</c> in <c>sales_order.js</c>):
    /// subtracts both delivered stock qty and still-reserved stock qty, returning 0 when rounded
    /// unreserved stock qty is &lt;= 0, and leaving the division by <paramref name="conversionFactor"/>
    /// unrounded so the server reconstructs the exact stock qty.
    /// </summary>
    public static decimal CalculateSalesOrderUnreservedOrderQty(
        decimal stockQty,
        decimal stockReservedQty,
        decimal deliveredQtyInOrderUom,
        decimal conversionFactor = 1m,
        int stockQtyPrecision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var unreservedStockQty = stockQty - (deliveredQtyInOrderUom * factor) - stockReservedQty;
        if (Math.Round(unreservedStockQty, stockQtyPrecision, MidpointRounding.AwayFromZero) <= 0m)
        {
            return 0m;
        }

        return unreservedStockQty / factor;
    }

    /// <summary>
    /// Rounds the total quantity of a Serial and Batch Bundle created from Stock Reservation Entries
    /// on the last entry only per ERPNext commit 6d0dad665d (round_to_ledger_qty):
    /// Keeps every earlier entry's exact reserved quantity so each reservation matches what it delivers,
    /// and places only the rounding of the combined total onto the last entry.
    /// </summary>
    public static List<(Guid EntryId, decimal Qty)> RoundReservationBundleEntriesToLedgerTotal(
        IReadOnlyList<(Guid EntryId, decimal ReservedQty)> entries,
        int precision = 4)
    {
        var result = new List<(Guid EntryId, decimal Qty)>();
        if (entries == null || entries.Count == 0)
        {
            return result;
        }

        var qtyBeforeLast = 0m;
        for (var i = 0; i < entries.Count - 1; i++)
        {
            result.Add((entries[i].EntryId, entries[i].ReservedQty));
            qtyBeforeLast += entries[i].ReservedQty;
        }

        var last = entries[^1];
        var total = Math.Round(qtyBeforeLast + last.ReservedQty, precision, MidpointRounding.AwayFromZero);
        result.Add((last.EntryId, total - qtyBeforeLast));
        return result;
    }

    /// <summary>
    /// Computes the quantity to undeliver from a Stock Reservation Entry (or its Serial and Batch Entry)
    /// when a delivery is cancelled per ERPNext commit f681eaca6a (get_qty_to_undeliver):
    /// Leaves the remaining <c>DeliveredQty</c> at the rounded quantity the stock ledger posts,
    /// clearing any rounding residue that closed the reservation on the final delivery.
    /// </summary>
    public static decimal CalculateQtyToUndeliverOnCancel(
        decimal currentDeliveredQty,
        decimal cancelledQty,
        int precision = 4)
    {
        var remainingAfterCancel = Math.Round(
            currentDeliveredQty - Math.Min(currentDeliveredQty, cancelledQty),
            precision,
            MidpointRounding.AwayFromZero);
        return currentDeliveredQty - remainingAfterCancel;
    }

    /// <summary>
    /// Computes the undelivered picked quantity per Sales Order Item from submitted Pick List rows
    /// per ERPNext commits e7ee6255f0, e4e0d9c707, and b053136fb2 (<c>get_undelivered_picked_qty_details</c>):
    /// Sums <c>PickedQty - DeliveredQty</c> only across submitted Pick List rows where <c>PickedQty &gt; DeliveredQty</c>,
    /// so an over-delivered Pick List row does not offset another row's undelivered pick, and an order row whose
    /// picked stock has already been delivered is allowed to reserve remaining stock.
    /// </summary>
    public static Dictionary<Guid, decimal> CalculateUndeliveredPickedQtyBySalesOrderItem(
        IEnumerable<(Guid SalesOrderItemId, bool IsSubmitted, decimal PickedQty, decimal DeliveredQty)>? pickListRows)
    {
        var result = new Dictionary<Guid, decimal>();
        if (pickListRows == null)
        {
            return result;
        }

        foreach (var row in pickListRows)
        {
            if (!row.IsSubmitted || row.SalesOrderItemId == Guid.Empty || row.PickedQty <= row.DeliveredQty)
            {
                continue;
            }

            result[row.SalesOrderItemId] = result.GetValueOrDefault(row.SalesOrderItemId, 0m) + (row.PickedQty - row.DeliveredQty);
        }

        return result;
    }

    /// <summary>
    /// Computes the unreserved quantity of a Sales Order Item (or its Packed Item row), returning <c>0</c>
    /// when only floating-point residue below field precision remains, and determines whether a reservation of
    /// <paramref name="qtyToBeReserved"/> is partial per ERPNext commits fff90c71e6, 072fe92f40, and 784d9cbe09.
    /// </summary>
    public static (decimal UnreservedQty, bool IsPartialReservation) EvaluateSalesOrderRowUnreservedAndPartialReservation(
        decimal rowStockQty,
        decimal rowDeliveredQty,
        decimal rowConversionFactor,
        decimal existingReservedQty,
        decimal qtyToBeReserved,
        int precision = 4)
    {
        var factor = rowConversionFactor > 0m ? rowConversionFactor : 1m;
        var rawUnreserved = rowStockQty - (rowDeliveredQty * factor) - existingReservedQty;
        var unreservedQty = Math.Round(rawUnreserved, precision, MidpointRounding.AwayFromZero) > 0m
            ? rawUnreserved
            : 0m;
        var isPartial = Math.Round(unreservedQty - qtyToBeReserved, precision, MidpointRounding.AwayFromZero) > 0m;
        return (unreservedQty, isPartial);
    }

    /// <summary>
    /// Resolves the status of a Stock Reservation Entry and whether it still holds stock
    /// per ERPNext commits 9533f3f19d and 0a134f3d98 (<c>stock_reservation_entry.py</c>):
    /// - A reservation holds stock while <c>DeliveredQty + TransferredQty + ConsumedQty &lt; ReservedQty</c>.
    /// - Float residue on <paramref name="deliveredQty"/> that rounds to 0 at <paramref name="reservedQtyPrecision"/>
    ///   does not mark the reservation <c>"Partially Delivered"</c>.
    /// </summary>
    public static (string Status, bool StillHoldsStock) EvaluateStockReservationEntryStatus(
        DocumentStatus docStatus,
        decimal voucherQty,
        decimal reservedQty,
        decimal deliveredQty,
        decimal transferredQty,
        decimal consumedQty,
        int reservedQtyPrecision = 4)
    {
        var roundedReserved = Math.Round(reservedQty, reservedQtyPrecision, MidpointRounding.AwayFromZero);
        var roundedTotalUsed = Math.Round(deliveredQty + transferredQty + consumedQty, reservedQtyPrecision, MidpointRounding.AwayFromZero);
        var stillHolds = docStatus == DocumentStatus.Submitted && roundedTotalUsed < roundedReserved;

        if (docStatus == DocumentStatus.Draft)
        {
            return ("Draft", false);
        }

        if (docStatus == DocumentStatus.Cancelled)
        {
            return ("Cancelled", false);
        }

        if (transferredQty > 0m)
        {
            // Per ERPNext commits 0f3a1a2ed3 & b14a643fb4: a supplier warehouse reservation is both consumed
            // and returned; round used_qty (transferred + consumed) and reserved_qty to precision before comparing.
            var usedQty = Math.Round(transferredQty + consumedQty, reservedQtyPrecision, MidpointRounding.AwayFromZero);
            return (usedQty < roundedReserved ? "Partially Used" : "Closed", stillHolds);
        }

        if (deliveredQty + consumedQty >= reservedQty)
        {
            return ("Delivered", stillHolds);
        }

        if (Math.Round(deliveredQty, reservedQtyPrecision, MidpointRounding.AwayFromZero) != 0m)
        {
            return ("Partially Delivered", stillHolds);
        }

        if (consumedQty > 0m)
        {
            return ("Partially Used", stillHolds);
        }

        if (Math.Round(reservedQty, reservedQtyPrecision, MidpointRounding.AwayFromZero) >=
            Math.Round(voucherQty, reservedQtyPrecision, MidpointRounding.AwayFromZero))
        {
            return ("Reserved", stillHolds);
        }

        return ("Partially Reserved", stillHolds);
    }

    /// <summary>
    /// Matches a Stock Reservation Entry against target voucher or voucher-row IDs
    /// (supporting Sales Order line / Packed Item row-level matching as well as header matching).
    /// Per ERPNext PR #63784 (<c>is_reserved_for_voucher</c>).
    /// </summary>
    public static bool IsReservationForVoucherOrRow(
        Guid? sreVoucherId,
        Guid? sreVoucherDetailId,
        IReadOnlySet<Guid>? targetVoucherOrRowIds)
    {
        if (targetVoucherOrRowIds == null || targetVoucherOrRowIds.Count == 0)
        {
            return false;
        }

        return (sreVoucherId.HasValue && targetVoucherOrRowIds.Contains(sreVoucherId.Value))
            || (sreVoucherDetailId.HasValue && targetVoucherOrRowIds.Contains(sreVoucherDetailId.Value));
    }

    /// <summary>
    /// Picks serial numbers prioritizing those reserved for the target Sales Order row first,
    /// then filling any remaining quantity from unreserved available stock while skipping ignored
    /// and already-reserved serials.
    /// Per ERPNext PR #63784 (<c>get_available_serial_nos</c> / <c>get_reserved_serial_nos_in_stock</c>).
    /// </summary>
    public static List<string> PickSerialNosPrioritizingSalesOrderRow(
        int requiredQty,
        IEnumerable<string>? reservedForSoRowSerials,
        IEnumerable<string>? unreservedAvailableSerials,
        IEnumerable<string>? ignoreSerials = null)
    {
        if (requiredQty <= 0)
        {
            return new List<string>();
        }

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ignoreSerials != null)
        {
            foreach (var sn in ignoreSerials)
            {
                if (!string.IsNullOrWhiteSpace(sn))
                    skip.Add(sn.Trim());
            }
        }

        var picked = new List<string>();
        if (reservedForSoRowSerials != null)
        {
            foreach (var sn in reservedForSoRowSerials)
            {
                if (string.IsNullOrWhiteSpace(sn)) continue;
                var trimmed = sn.Trim();
                if (!skip.Add(trimmed)) continue;
                picked.Add(trimmed);
                if (picked.Count >= requiredQty)
                    return picked;
            }
        }

        if (unreservedAvailableSerials != null)
        {
            foreach (var sn in unreservedAvailableSerials)
            {
                if (string.IsNullOrWhiteSpace(sn)) continue;
                var trimmed = sn.Trim();
                if (!skip.Add(trimmed)) continue;
                picked.Add(trimmed);
                if (picked.Count >= requiredQty)
                    break;
            }
        }

        return picked;
    }

    /// <summary>
    /// Replays stock reservation usage across submits and earlier cancels in chronological order
    /// (<c>(EventTime, IsCancel)</c>), keeping <paramref name="cancelledVoucherId"/> in place during replay
    /// so later vouchers remain attached to the reservations they originally consumed, and then reverses
    /// ONLY <paramref name="cancelledVoucherId"/>'s shares.
    /// Per ERPNext commits <c>a2c2bc92c2</c> and <c>f4a3af2745</c> (<c>replay_reservation_usage</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> ReplayReservationUsageOnCancel(
        IEnumerable<(Guid ReservationId, DateTime CreatedAt, decimal ReservedQty)>? reservations,
        IEnumerable<(Guid VoucherId, DateTime SubmittedAt, DateTime? CancelledAt, decimal Qty)>? usages,
        Guid cancelledVoucherId,
        int precision = 4)
    {
        var usedBySre = new Dictionary<Guid, decimal>();
        if (reservations == null)
        {
            return usedBySre;
        }

        var sreList = reservations.OrderBy(r => r.CreatedAt).ToList();
        var capBySre = new Dictionary<Guid, decimal>();
        foreach (var sre in sreList)
        {
            capBySre[sre.ReservationId] = sre.ReservedQty;
            usedBySre[sre.ReservationId] = 0m;
        }

        if (usages == null)
        {
            return usedBySre;
        }

        var usageList = usages.ToList();
        var sharesByVoucher = new Dictionary<Guid, List<(Guid ReservationId, decimal Share)>>();

        var events = new List<(DateTime Time, bool IsCancel, Guid VoucherId, DateTime SubmittedAt, decimal Qty)>();
        foreach (var u in usageList)
        {
            events.Add((u.SubmittedAt, false, u.VoucherId, u.SubmittedAt, u.Qty));
            if (u.CancelledAt.HasValue && u.VoucherId != cancelledVoucherId)
            {
                events.Add((u.CancelledAt.Value, true, u.VoucherId, u.SubmittedAt, u.Qty));
            }
        }

        foreach (var ev in events.OrderBy(e => e.Time).ThenBy(e => e.IsCancel))
        {
            if (ev.IsCancel)
            {
                if (sharesByVoucher.TryGetValue(ev.VoucherId, out var shares))
                {
                    foreach (var (sreId, share) in shares)
                    {
                        usedBySre[sreId] = Math.Max(0m, usedBySre[sreId] - share);
                    }
                }
            }
            else
            {
                var remaining = ev.Qty;
                if (!sharesByVoucher.TryGetValue(ev.VoucherId, out var shares))
                {
                    shares = new List<(Guid, decimal)>();
                    sharesByVoucher[ev.VoucherId] = shares;
                }

                foreach (var sre in sreList)
                {
                    if (remaining <= 0m) break;
                    if (sre.CreatedAt >= ev.SubmittedAt) continue;

                    var free = capBySre[sre.ReservationId] - usedBySre[sre.ReservationId];
                    if (free <= 0m) continue;

                    var take = Math.Min(remaining, free);
                    usedBySre[sre.ReservationId] += take;
                    shares.Add((sre.ReservationId, take));
                    remaining -= take;
                }
            }
        }

        // Remove ONLY the currently cancelling voucher's share after replay
        if (sharesByVoucher.TryGetValue(cancelledVoucherId, out var cancelledShares))
        {
            foreach (var (sreId, share) in cancelledShares)
            {
                usedBySre[sreId] = Math.Max(0m, usedBySre[sreId] - share);
            }
        }

        foreach (var sreId in usedBySre.Keys.ToList())
        {
            usedBySre[sreId] = Math.Round(usedBySre[sreId], precision, MidpointRounding.AwayFromZero);
        }

        return usedBySre;
    }

    /// <summary>
    /// Computes the maximum allowed reservation quantity for a Work Order required-item row:
    /// leaves <paramref name="subcontractedQty"/> out ONLY for new reservations (<paramref name="isNewReservation"/> = true)
    /// so existing reservations partly sent to a subcontractor can still be updated without false over-reservation errors,
    /// rounded to <paramref name="precision"/>.
    /// Per ERPNext commits <c>07915b5632</c>, <c>83976ff833</c>, and <c>f0483b53a5</c>.
    /// </summary>
    public static decimal CalculateWorkOrderAllowedReservationQty(
        decimal requiredQty,
        decimal subcontractedQty,
        decimal alreadyReservedQty,
        decimal consumedQty,
        decimal transferredQty,
        bool isNewReservation,
        int precision = 4)
    {
        var effectiveSubcontracted = isNewReservation ? Math.Max(subcontractedQty, 0m) : 0m;
        var fulfilled = Math.Max(alreadyReservedQty + consumedQty, transferredQty);
        var allowed = requiredQty - effectiveSubcontracted - fulfilled;
        return Math.Max(Math.Round(allowed, precision, MidpointRounding.AwayFromZero), 0m);
    }

    /// <summary>
    /// Releases unused customer-material reservation quantity from Work Order reservations when a
    /// customer material return exceeds what the Subcontracting Inward Order still holds:
    /// never releases below <c>UsedQty</c> (<c>delivered + transferred + consumed</c>) and returns
    /// only the quantities actually released (preserving each reservation's <c>VoucherQty</c>).
    /// Per ERPNext commits <c>a442d94273</c> and <c>e00d5c12b7</c> (<c>release_unused_inward_reservations</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> ReleaseUnusedInwardWorkOrderReservations(
        IEnumerable<(Guid ReservationId, Guid FromScioDetailId, decimal ReservedQty, decimal UsedQty)>? reservations,
        IReadOnlyDictionary<Guid, decimal>? shortQtyByScioDetailId,
        int precision = 4)
    {
        var releasedBySre = new Dictionary<Guid, decimal>();
        if (reservations == null || shortQtyByScioDetailId == null)
        {
            return releasedBySre;
        }

        var remainingShort = new Dictionary<Guid, decimal>(shortQtyByScioDetailId);
        foreach (var sre in reservations)
        {
            if (!remainingShort.TryGetValue(sre.FromScioDetailId, out var needed) || needed <= 0m)
                continue;

            var releasable = Math.Max(sre.ReservedQty - sre.UsedQty, 0m);
            var releaseQty = Math.Round(Math.Min(needed, releasable), precision, MidpointRounding.AwayFromZero);
            if (releaseQty <= 0m)
                continue;

            releasedBySre[sre.ReservationId] = releaseQty;
            remainingShort[sre.FromScioDetailId] = needed - releaseQty;
        }

        return releasedBySre;
    }

    /// <summary>
    /// Computes per-inward-order-row shortfall (<c>entryRowQty - currentInwardReservedQty &gt; 0</c>)
    /// that must be reclaimed from unused Work Order reservations when returning customer material
    /// OR cancelling a <c>Receive from Customer</c> entry.
    /// Per ERPNext PR #69284 (<c>subcontracting_inward_controller.py: release_unused_work_order_reservations</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> CalculateInwardReservationShortfall(
        IReadOnlyDictionary<Guid, decimal>? entryRowQtyByScioDetailId,
        IReadOnlyDictionary<Guid, decimal>? reservedQtyByScioDetailId,
        int precision = 4)
    {
        var shortQty = new Dictionary<Guid, decimal>();
        if (entryRowQtyByScioDetailId == null)
        {
            return shortQty;
        }

        foreach (var (scioDetailId, qty) in entryRowQtyByScioDetailId)
        {
            var reserved = reservedQtyByScioDetailId != null
                ? reservedQtyByScioDetailId.GetValueOrDefault(scioDetailId, 0m)
                : 0m;
            var missing = Math.Round(qty - reserved, precision, MidpointRounding.AwayFromZero);
            if (missing > 0m)
            {
                shortQty[scioDetailId] = missing;
            }
        }

        return shortQty;
    }

    /// <summary>
    /// Releases unused customer-material reservation quantity from Work Order reservations, returning
    /// each reservation's <c>ReleasedQty</c>, <c>NewReservedQty</c>, and <c>NewVoucherQty</c>.
    /// Per ERPNext PR #69284 (<c>reservation.py: release_unused_inward_reservations</c> / <c>release_inward_reservation</c>):
    /// - Never lowers <c>ReservedQty</c> below <c>UsedQty</c> (<c>delivered + transferred + consumed</c>).
    /// - When <paramref name="lowerVoucherQty"/> is <c>true</c> (cancelling a <c>Receive from Customer</c> entry),
    ///   lowers <c>VoucherQty</c> by <c>ReleasedQty</c> too so a later return cancellation cannot restore
    ///   material that left permanently.
    /// - When <paramref name="lowerVoucherQty"/> is <c>false</c> (customer return), preserves <c>VoucherQty</c>.
    /// </summary>
    public static Dictionary<Guid, (decimal ReleasedQty, decimal NewReservedQty, decimal NewVoucherQty)>
        ReleaseUnusedInwardWorkOrderReservationsWithVoucherAdjustment(
            IEnumerable<(Guid ReservationId, Guid FromScioDetailId, decimal VoucherQty, decimal ReservedQty, decimal UsedQty)>? reservations,
            IReadOnlyDictionary<Guid, decimal>? shortQtyByScioDetailId,
            bool lowerVoucherQty = false,
            int precision = 4)
    {
        var results = new Dictionary<Guid, (decimal ReleasedQty, decimal NewReservedQty, decimal NewVoucherQty)>();
        if (reservations == null || shortQtyByScioDetailId == null)
        {
            return results;
        }

        var remainingShort = new Dictionary<Guid, decimal>(shortQtyByScioDetailId);
        foreach (var sre in reservations)
        {
            if (!remainingShort.TryGetValue(sre.FromScioDetailId, out var needed) || needed <= 0m)
                continue;

            var newReserved = Math.Round(Math.Max(sre.ReservedQty - needed, sre.UsedQty), precision, MidpointRounding.AwayFromZero);
            var releasedQty = Math.Round(sre.ReservedQty - newReserved, precision, MidpointRounding.AwayFromZero);
            if (releasedQty <= 0m)
                continue;

            var newVoucherQty = lowerVoucherQty
                ? Math.Round(sre.VoucherQty - releasedQty, precision, MidpointRounding.AwayFromZero)
                : sre.VoucherQty;

            results[sre.ReservationId] = (releasedQty, newReserved, newVoucherQty);
            remainingShort[sre.FromScioDetailId] = needed - releasedQty;
        }

        return results;
    }

    /// <summary>
    /// Replays all historical reservation usages (submits and earlier cancels) in chronological order,
    /// keeping <paramref name="cancelledVoucherId"/>'s share in place during the replay so later usages
    /// land on the exact reservations they originally consumed on submit, then removes ONLY
    /// <paramref name="cancelledVoucherId"/>'s own recorded share.
    /// Per ERPNext PR #67174 (<c>stock_controller.py: rebuild_reservation_usage</c> / <c>replay_reservation_usage</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> ReplayReservationUsageAndUndoCancelledVoucher(
        IEnumerable<(Guid SreId, DateTime CreatedAt, decimal ReservedQty)>? reservations,
        IEnumerable<(Guid VoucherId, DateTime SubmittedAt, DateTime? CancelledAt, decimal Qty)>? usages,
        Guid cancelledVoucherId,
        int precision = 4)
    {
        var sreList = (reservations ?? Array.Empty<(Guid, DateTime, decimal)>()).ToList();
        var usedBySre = sreList.ToDictionary(s => s.SreId, _ => 0m);
        if (sreList.Count == 0 || usages == null)
        {
            return usedBySre;
        }

        var usageList = usages.ToList();
        var sharesByUsageIndex = new Dictionary<int, List<(Guid SreId, decimal Qty)>>();
        var events = new List<(DateTime Time, bool IsCancel, int UsageIndex)>();

        for (var i = 0; i < usageList.Count; i++)
        {
            var u = usageList[i];
            events.Add((u.SubmittedAt, false, i));
            if (u.CancelledAt.HasValue && u.VoucherId != cancelledVoucherId)
            {
                events.Add((u.CancelledAt.Value, true, i));
            }
        }

        events.Sort((a, b) =>
        {
            var cmp = a.Time.CompareTo(b.Time);
            return cmp != 0 ? cmp : a.IsCancel.CompareTo(b.IsCancel);
        });

        foreach (var (_, isCancel, idx) in events)
        {
            if (isCancel)
            {
                if (sharesByUsageIndex.TryGetValue(idx, out var priorShares))
                {
                    foreach (var (sreId, q) in priorShares)
                    {
                        usedBySre[sreId] = Math.Round(usedBySre[sreId] - q, precision, MidpointRounding.AwayFromZero);
                    }
                }
            }
            else
            {
                var u = usageList[idx];
                var remaining = u.Qty;
                var taken = new List<(Guid SreId, decimal Qty)>();
                foreach (var sre in sreList)
                {
                    if (remaining <= 0m) break;
                    if (sre.CreatedAt >= u.SubmittedAt) continue;

                    var room = Math.Max(sre.ReservedQty - usedBySre[sre.SreId], 0m);
                    var share = Math.Round(Math.Min(room, remaining), precision, MidpointRounding.AwayFromZero);
                    if (share <= 0m) continue;

                    usedBySre[sre.SreId] = Math.Round(usedBySre[sre.SreId] + share, precision, MidpointRounding.AwayFromZero);
                    remaining = Math.Round(remaining - share, precision, MidpointRounding.AwayFromZero);
                    taken.Add((sre.SreId, share));
                }

                sharesByUsageIndex[idx] = taken;
            }
        }

        // Now undo ONLY the currently-cancelling voucher's own shares
        for (var i = 0; i < usageList.Count; i++)
        {
            if (usageList[i].VoucherId == cancelledVoucherId
                && sharesByUsageIndex.TryGetValue(i, out var cancelledShares))
            {
                foreach (var (sreId, q) in cancelledShares)
                {
                    usedBySre[sreId] = Math.Round(usedBySre[sreId] - q, precision, MidpointRounding.AwayFromZero);
                }
            }
        }

        return usedBySre;
    }

    /// <summary>
    /// Returns <c>true</c> ONLY when at least one Stock Reservation Entry for the voucher still holds
    /// unused stock (<c>DeliveredQty + TransferredQty + ConsumedQty &lt; ReservedQty</c>) per ERPNext PR #68075
    /// (<c>stock_reservation_entry.py: has_reserved_stock</c> / <c>_get_stock_reservation_entries_for_voucher</c>),
    /// hiding the Unreserve action once all reserved stock has been transferred or consumed.
    /// </summary>
    public static bool HasRemainingReservedStockForVoucher(
        IEnumerable<(decimal ReservedQty, decimal DeliveredQty, decimal TransferredQty, decimal ConsumedQty)>? entries)
    {
        if (entries == null)
        {
            return false;
        }

        return entries.Any(e => (e.DeliveredQty + e.TransferredQty + e.ConsumedQty) < e.ReservedQty);
    }

    /// <summary>
    /// Restores Work Order reservations up to <c>min(VoucherQty - ReservedQty, FreeInwardQty, ReturnedQty)</c>
    /// per inward order row when a customer material return is cancelled.
    /// Per ERPNext commit <c>d6d9be7765</c> (<c>restore_inward_reservations</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> RestoreInwardWorkOrderReservationsOnReturnCancel(
        IEnumerable<(Guid ReservationId, Guid FromScioDetailId, decimal VoucherQty, decimal ReservedQty)>? reservations,
        IReadOnlyDictionary<Guid, decimal>? freeInwardQtyByScioDetailId,
        IReadOnlyDictionary<Guid, decimal>? returnQtyByScioDetailId,
        int precision = 4)
    {
        var restoredBySre = new Dictionary<Guid, decimal>();
        if (reservations == null || freeInwardQtyByScioDetailId == null || returnQtyByScioDetailId == null)
        {
            return restoredBySre;
        }

        var freePool = new Dictionary<Guid, decimal>(freeInwardQtyByScioDetailId);
        var retPool = new Dictionary<Guid, decimal>(returnQtyByScioDetailId);

        foreach (var sre in reservations)
        {
            var gap = Math.Max(sre.VoucherQty - sre.ReservedQty, 0m);
            var free = freePool.GetValueOrDefault(sre.FromScioDetailId, 0m);
            var ret = retPool.GetValueOrDefault(sre.FromScioDetailId, 0m);
            var restoreQty = Math.Round(Math.Min(gap, Math.Min(free, ret)), precision, MidpointRounding.AwayFromZero);
            if (restoreQty <= 0m)
                continue;

            restoredBySre[sre.ReservationId] = restoreQty;
            freePool[sre.FromScioDetailId] = free - restoreQty;
            retPool[sre.FromScioDetailId] = ret - restoreQty;
        }

        return restoredBySre;
    }

    /// <summary>
    /// Computes a Sales Order row's unreserved stock quantity, rounding at <paramref name="stockQtyPrecision"/>
    /// before checking positivity so sub-precision float remainders do not leave phantom unreserved qty.
    /// Per ERPNext commit <c>072fe92f40</c>.
    /// </summary>
    public static decimal CalculateSalesOrderUnreservedStockQty(
        decimal stockQty,
        decimal deliveredStockQty,
        decimal stockReservedQty,
        int stockQtyPrecision = 4)
    {
        var unreserved = stockQty - deliveredStockQty - stockReservedQty;
        return Math.Round(unreserved, stockQtyPrecision, MidpointRounding.AwayFromZero) > 0m
            ? unreserved
            : 0m;
    }

    /// <summary>
    /// Allocates re-reservation quantities across each target warehouse receiving returned components from a
    /// Reserve-Stock Subcontracting Order, capping each order row at its unreserved shortfall
    /// (<c>RequiredQty - CurrentReservedQty</c>) so returned stock is reserved in each destination warehouse
    /// and any surplus stays unreserved.
    /// Per ERPNext commits <c>b14a643fb4</c> and <c>2cec6e36eb</c> (<c>stock_entry.py</c>).
    /// </summary>
    public static List<(Guid OrderRowId, Guid WarehouseId, decimal ReserveQty)> AllocateReturnedComponentReReservations(
        IEnumerable<(Guid OrderRowId, Guid WarehouseId, decimal ReturnedQty)>? returnedRows,
        IReadOnlyDictionary<Guid, (decimal RequiredQty, decimal CurrentReservedQty)>? orderRowDemand,
        int precision = 4)
    {
        var allocations = new List<(Guid OrderRowId, Guid WarehouseId, decimal ReserveQty)>();
        if (returnedRows == null || orderRowDemand == null)
        {
            return allocations;
        }

        var remainingShortfall = new Dictionary<Guid, decimal>();
        foreach (var (rowId, (required, reserved)) in orderRowDemand)
        {
            remainingShortfall[rowId] = Math.Max(0m, required - reserved);
        }

        foreach (var row in returnedRows)
        {
            if (row.ReturnedQty <= 0m)
            {
                continue;
            }

            if (!remainingShortfall.TryGetValue(row.OrderRowId, out var shortfall) || shortfall <= 0m)
            {
                continue;
            }

            var toReserve = Math.Round(Math.Min(row.ReturnedQty, shortfall), precision, MidpointRounding.AwayFromZero);
            if (toReserve <= 0m)
            {
                continue;
            }

            allocations.Add((row.OrderRowId, row.WarehouseId, toReserve));
            remainingShortfall[row.OrderRowId] = shortfall - toReserve;
        }

        return allocations;
    }

    /// <summary>
    /// Resolves all <c>(ItemId, WarehouseId, BinField)</c> targets whose Bin balances must be refreshed when a
    /// child item row is deleted via Update Items:
    /// - For a <c>SalesOrder</c>: refreshes <c>reserved_qty</c> for the row's warehouse AND for every linked
    ///   Product Bundle packed component's <c>(ItemId, WarehouseId)</c>.
    /// - For a <c>PurchaseOrder</c>: refreshes <c>ordered_qty</c> for the row's warehouse AND refreshes
    ///   <c>indented_qty</c> via the linked <c>MaterialRequestItemId</c>'s own warehouse (not the PO row's warehouse).
    /// Per ERPNext commits <c>3c92ab4303</c> and <c>7608f562dd</c> (<c>child_item_update.py</c>).
    /// </summary>
    public static (
        List<(Guid ItemId, Guid WarehouseId, string BinField)> BinRefreshes,
        List<Guid> MaterialRequestItemIdsToRefresh) ResolveDeletedRowBinRefreshTargets(
        string parentDocType,
        Guid rowItemId,
        Guid? rowWarehouseId,
        IEnumerable<(Guid PackedItemId, Guid? PackedWarehouseId)>? linkedPackedItems = null,
        Guid? linkedMaterialRequestItemId = null)
    {
        var bins = new List<(Guid ItemId, Guid WarehouseId, string BinField)>();
        var mrItemIds = new List<Guid>();

        if (string.Equals(parentDocType, "SalesOrder", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parentDocType, "Sales Order", StringComparison.OrdinalIgnoreCase))
        {
            if (rowWarehouseId.HasValue && rowWarehouseId.Value != Guid.Empty)
            {
                bins.Add((rowItemId, rowWarehouseId.Value, "reserved_qty"));
            }

            if (linkedPackedItems != null)
            {
                foreach (var (packedItemId, packedWhId) in linkedPackedItems)
                {
                    if (packedWhId.HasValue && packedWhId.Value != Guid.Empty)
                    {
                        bins.Add((packedItemId, packedWhId.Value, "reserved_qty"));
                    }
                }
            }
        }
        else
        {
            if (rowWarehouseId.HasValue && rowWarehouseId.Value != Guid.Empty)
            {
                bins.Add((rowItemId, rowWarehouseId.Value, "ordered_qty"));
            }

            if (linkedMaterialRequestItemId.HasValue && linkedMaterialRequestItemId.Value != Guid.Empty)
            {
                mrItemIds.Add(linkedMaterialRequestItemId.Value);
            }
        }

        return (bins, mrItemIds);
    }

    /// <summary>
    /// Computes the remaining open (unfulfilled) quantity of a Stock Reservation Entry across ALL
    /// fulfillment channels (<c>DeliveredQty + TransferredQty + ConsumedQty</c>), rounded to stock precision.
    /// Per ERPNext PR #67522 (<c>get_reservation_open_qty</c> in <c>stock_controller.py</c>).
    /// </summary>
    public static decimal GetReservationOpenQty(StockReservationEntry sre, int precision = 4)
    {
        var open = sre.ReservedQty - sre.DeliveredQty - sre.TransferredQty - sre.ConsumedQty;
        return Math.Max(0m, Math.Round(open, precision, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Resolves how Subcontracting Order component-return and receipt reservation replay groups and
    /// filters supplier-warehouse reservations per ERPNext PR #67522 (<c>stock_controller.py</c>):
    /// - Return of Components on submit: scopes to supplier warehouse (<paramref name="sourceWarehouseId"/>)
    ///   and <paramref name="scoRmDetailId"/>, grouping under <c>(SubcontractingOrderId, ItemId, SourceWarehouseId)</c>.
    /// - Return of Components on cancel: replays BOTH returns and receipts across ALL supplier-warehouse
    ///   reservations of <c>(ItemId, SourceWarehouseId)</c> (so <c>FilterByVoucherDetail</c> is false).
    /// </summary>
    public static (
        bool UsesSupplierWarehouseReservations,
        bool FilterByVoucherDetail,
        Guid TargetWarehouseId,
        (Guid OrderId, Guid ItemId, Guid WarehouseId) ReplayGroupKey) ResolveSubcontractingReservationReplayScope(
        bool isComponentsReturn,
        bool isCancel,
        Guid subcontractingOrderId,
        Guid itemId,
        Guid sourceWarehouseId,
        Guid supplierWarehouseId)
    {
        if (isComponentsReturn)
        {
            return (
                UsesSupplierWarehouseReservations: true,
                FilterByVoucherDetail: !isCancel,
                TargetWarehouseId: sourceWarehouseId,
                ReplayGroupKey: (subcontractingOrderId, itemId, sourceWarehouseId));
        }

        return (
            UsesSupplierWarehouseReservations: true,
            FilterByVoucherDetail: false,
            TargetWarehouseId: supplierWarehouseId,
            ReplayGroupKey: (subcontractingOrderId, itemId, supplierWarehouseId));
    }

    /// <summary>
    /// Excludes Return of Components rows submitted at or before <paramref name="reservedComponentsReturnCutoff"/>
    /// from supplier-warehouse reservation cancel replay per ERPNext PR #69314
    /// (<c>StockController.get_component_return_usage</c>): returns submitted prior to the cutoff never
    /// consumed supplier-warehouse reservations and must not be deducted on replay.
    /// </summary>
    public static List<T> FilterComponentsReturnRowsByCutoff<T>(
        IEnumerable<T>? returnRows,
        Func<T, DateTime> getSubmittedAt,
        DateTime? reservedComponentsReturnCutoff)
    {
        if (returnRows == null)
        {
            return new List<T>();
        }

        if (!reservedComponentsReturnCutoff.HasValue)
        {
            return returnRows.ToList();
        }

        var cutoff = reservedComponentsReturnCutoff.Value;
        return returnRows.Where(r => getSubmittedAt(r) > cutoff).ToList();
    }

    /// <summary>
    /// Returns true when a row carries a Serial and Batch Bundle whose absolute total quantity differs
    /// from the row's absolute transfer quantity per ERPNext PR #67503 (<c>stock_entry_sabb.py: is_bundle_resized</c>).
    /// </summary>
    public static bool IsBundleResized(bool hasBundle, decimal rowTransferQty, decimal bundleTotalQty, int precision = 4)
    {
        if (!hasBundle)
            return false;

        return Math.Round(Math.Abs(rowTransferQty), precision, MidpointRounding.AwayFromZero)
            != Math.Round(Math.Abs(bundleTotalQty), precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Allocates serial numbers and batch quantities up to <paramref name="qty"/> from mutable reserved
    /// entries in order per ERPNext PR #67503 (<c>stock_entry_sabb.py: take_reserved_entries</c>):
    /// respects optional <paramref name="filterSerialNos"/> and <paramref name="filterBatchNo"/>,
    /// prevents duplicate serial selection, and deducts taken quantities from each entry's <c>Available</c>.
    /// </summary>
    public static (List<string> TakenSerialNos, Dictionary<string, decimal> TakenBatches) TakeReservedSerialBatchEntries(
        IList<ReservedSerialBatchPoolEntry>? entries,
        decimal qty,
        IReadOnlyCollection<string>? filterSerialNos = null,
        string? filterBatchNo = null,
        int precision = 4)
    {
        var takenSerialNos = new List<string>();
        var takenSerialSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var takenBatches = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (entries == null || Math.Round(qty, precision, MidpointRounding.AwayFromZero) <= 0m)
        {
            return (takenSerialNos, takenBatches);
        }

        var serialFilter = filterSerialNos != null && filterSerialNos.Count > 0
            ? new HashSet<string>(filterSerialNos, StringComparer.OrdinalIgnoreCase)
            : null;
        var hasBatchFilter = !string.IsNullOrWhiteSpace(filterBatchNo);

        var remaining = qty;
        foreach (var entry in entries)
        {
            if (Math.Round(remaining, precision, MidpointRounding.AwayFromZero) <= 0m)
                break;

            if (Math.Round(entry.Available, precision, MidpointRounding.AwayFromZero) <= 0m)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.SerialNo) && takenSerialSet.Contains(entry.SerialNo))
                continue;

            if (serialFilter != null && (string.IsNullOrWhiteSpace(entry.SerialNo) || !serialFilter.Contains(entry.SerialNo)))
                continue;

            if (hasBatchFilter && !string.Equals(entry.BatchNo, filterBatchNo, StringComparison.OrdinalIgnoreCase))
                continue;

            var takeQty = Math.Min(entry.Available, remaining);
            entry.Available -= takeQty;
            remaining -= takeQty;

            if (!string.IsNullOrWhiteSpace(entry.SerialNo))
            {
                takenSerialNos.Add(entry.SerialNo);
                takenSerialSet.Add(entry.SerialNo);
            }

            if (!string.IsNullOrWhiteSpace(entry.BatchNo))
            {
                takenBatches[entry.BatchNo] = takenBatches.GetValueOrDefault(entry.BatchNo, 0m) + takeQty;
            }
        }

        return (takenSerialNos, takenBatches);
    }

    /// <summary>
    /// Picks up to <paramref name="requestedQty"/> serial numbers for a Sales Order row (or its Packed Item
    /// rows) per ERPNext PR #63784 (<c>serial_batch_bundle.py: get_available_serial_nos</c> &amp;
    /// <c>is_reserved_for_voucher</c>):
    /// - Matches reserved serials against the specific <paramref name="allowedVoucherDetailIds"/> (SO item +
    ///   packed item rows) rather than the entire Sales Order header.
    /// - Takes available reserved serials in stock first (excluding <paramref name="ignoreSerialNos"/>), then
    ///   fills any remaining <c>pendingQty</c> from unreserved in-stock serials (excluding both
    ///   <paramref name="ignoreSerialNos"/> and all reserved serials).
    /// </summary>
    public static List<string> PickReservedAndUnreservedSerialNos(
        int requestedQty,
        IReadOnlySet<Guid> allowedVoucherDetailIds,
        IReadOnlyList<(string SerialNo, Guid? VoucherDetailId)> allReservedSerials,
        IReadOnlyList<string> inStockSerialsInOrder,
        IReadOnlyCollection<string>? ignoreSerialNos = null)
    {
        if (requestedQty <= 0 || inStockSerialsInOrder == null || inStockSerialsInOrder.Count == 0)
            return new List<string>();

        var ignore = new HashSet<string>(
            (ignoreSerialNos ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var reservedForThisRow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reservedForAnyRow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (allReservedSerials != null)
        {
            foreach (var (sn, detailId) in allReservedSerials)
            {
                if (string.IsNullOrWhiteSpace(sn))
                    continue;
                var trimmed = sn.Trim();
                reservedForAnyRow.Add(trimmed);
                if (detailId.HasValue && allowedVoucherDetailIds.Contains(detailId.Value))
                {
                    reservedForThisRow.Add(trimmed);
                }
            }
        }

        var picked = new List<string>(requestedQty);
        var pickedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Reserved serials for this specific SO row / packed item row that are in stock and not ignored
        foreach (var sn in inStockSerialsInOrder)
        {
            if (picked.Count >= requestedQty)
                break;
            var trimmed = sn.Trim();
            if (ignore.Contains(trimmed) || !reservedForThisRow.Contains(trimmed))
                continue;
            if (pickedSet.Add(trimmed))
                picked.Add(trimmed);
        }

        // 2. Remainder from unreserved stock (skip ignore + any reserved serials)
        foreach (var sn in inStockSerialsInOrder)
        {
            if (picked.Count >= requestedQty)
                break;
            var trimmed = sn.Trim();
            if (ignore.Contains(trimmed) || reservedForAnyRow.Contains(trimmed))
                continue;
            if (pickedSet.Add(trimmed))
                picked.Add(trimmed);
        }

        return picked;
    }

    /// <summary>
    /// Computes the maximum allowed reservation quantity for a Work Order material row
    /// per ERPNext PR #68805 (<c>StockReservationEntry.validate_qty_to_be_reserved</c>):
    /// counts the row's <paramref name="subcontractedQty"/> as already delivered only when submitting
    /// a new Work Order reservation (not linked from an upstream voucher), so stock taken over by a
    /// Subcontracting Order is not reserved a second time.
    /// </summary>
    public static decimal CalculateWorkOrderAllowedReservationQty(
        decimal availableQty,
        decimal voucherQty,
        decimal totalReservedQty,
        decimal subcontractedQty,
        bool hasFromVoucher,
        bool isSubmitAction,
        int precision = 3)
    {
        var voucherDeliveredQty = !hasFromVoucher && isSubmitAction
            ? Math.Max(subcontractedQty, 0m)
            : 0m;

        var remainingDemand = voucherQty - voucherDeliveredQty - totalReservedQty;
        var allowed = Math.Min(availableQty, remainingDemand);
        return Math.Max(0m, Math.Round(allowed, precision, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Enforces ERPNext commit <c>43c3dc5428</c> (<c>StockReservationEntry.validate_before_update_after_submit</c>):
    /// once a reservation has delivered, consumed, or transferred stock (rounded at <paramref name="reservedQtyPrecision"/>),
    /// reject the update only when its Serial and Batch Reservation rows actually changed; a save leaving
    /// <c>sb_entries</c> unchanged must pass.
    /// </summary>
    public static void ValidateUsedReservationSerialBatchUnchanged(
        decimal deliveredQty,
        decimal consumedQty,
        decimal transferredQty,
        IEnumerable<(string? SerialNo, Guid? BatchId, decimal Qty)>? previousEntries,
        IEnumerable<(string? SerialNo, Guid? BatchId, decimal Qty)>? updatedEntries,
        int reservedQtyPrecision = 3)
    {
        var usedQty = Math.Round(deliveredQty + consumedQty + transferredQty, reservedQtyPrecision, MidpointRounding.AwayFromZero);
        if (usedQty <= 0m)
        {
            return;
        }

        var prev = (previousEntries ?? Array.Empty<(string?, Guid?, decimal)>()).ToList();
        var next = (updatedEntries ?? Array.Empty<(string?, Guid?, decimal)>()).ToList();

        var unchanged = prev.Count == next.Count && prev.Zip(next).All(pair =>
            string.Equals(pair.First.SerialNo ?? string.Empty, pair.Second.SerialNo ?? string.Empty, StringComparison.Ordinal)
            && pair.First.BatchId == pair.Second.BatchId
            && Math.Round(pair.First.Qty, reservedQtyPrecision, MidpointRounding.AwayFromZero)
                == Math.Round(pair.Second.Qty, reservedQtyPrecision, MidpointRounding.AwayFromZero));

        if (!unchanged)
        {
            throw new BusinessException(MyERPDomainErrorCodes.UsedReservationSerialBatchImmutable)
                .WithData("usedQty", usedQty)
                .WithData("reason", "Serial and Batch Reservation cannot be updated as the Stock Reservation Entry has already been delivered, consumed or transferred.");
        }
    }

    /// <summary>
    /// Computes per-inward-order-row shortage quantities that must be reclaimed from unused
    /// Work Order reservations when returning customer material per ERPNext PR #67523
    /// (<c>SubcontractingInwardController.release_unused_work_order_reservations</c>).
    /// </summary>
    public static Dictionary<Guid, decimal> CalculateInwardReturnWorkOrderShortage(
        IReadOnlyDictionary<Guid, decimal> returnQtyByScioDetail,
        IReadOnlyDictionary<Guid, decimal> scioFreeReservedQtyByDetail)
    {
        var shortQty = new Dictionary<Guid, decimal>();
        foreach (var (scioDetailId, returnQty) in returnQtyByScioDetail)
        {
            scioFreeReservedQtyByDetail.TryGetValue(scioDetailId, out var freeQty);
            var missing = returnQty - freeQty;
            if (missing > 0m)
            {
                shortQty[scioDetailId] = missing;
            }
        }

        return shortQty;
    }

    /// <summary>
    /// Computes per-SRE release quantities to reclaim extra customer material held by Work Order
    /// qty-based reservations beyond their required or used qty per ERPNext PR #67523
    /// (<c>work_order/services/reservation.py: release_unused_inward_reservations</c>).
    /// Input <paramref name="sresNewestFirst"/> must be ordered newest-first.
    /// </summary>
    public static List<(Guid StockReservationEntryId, decimal ReleaseQty)> ComputeUnusedInwardWorkOrderReservationReleasePlan(
        IReadOnlyList<InwardWorkOrderReservationRow> sresNewestFirst,
        IReadOnlyDictionary<Guid, decimal> shortQtyByScioDetail,
        int qtyPrecision = 3)
    {
        var remainingShort = new Dictionary<Guid, decimal>(shortQtyByScioDetail);
        var unusedByWoDetail = new Dictionary<Guid, decimal>();
        foreach (var group in sresNewestFirst.GroupBy(s => s.WorkOrderItemId))
        {
            var rows = group.ToList();
            var totalUsed = rows.Sum(r => r.UsedQty);
            var needed = Math.Max(rows[0].WorkOrderRequiredQty, totalUsed);
            unusedByWoDetail[group.Key] = rows.Sum(r => r.ReservedQty) - needed;
        }

        var plan = new List<(Guid StockReservationEntryId, decimal ReleaseQty)>();
        foreach (var sre in sresNewestFirst)
        {
            if (!remainingShort.TryGetValue(sre.ScioDetailId, out var shortLeft) || shortLeft <= 0m)
                continue;
            if (!unusedByWoDetail.TryGetValue(sre.WorkOrderItemId, out var unusedLeft) || unusedLeft <= 0m)
                continue;

            var releasableOnSre = sre.ReservedQty - sre.UsedQty;
            var releaseQty = Math.Round(
                Math.Min(releasableOnSre, Math.Min(unusedLeft, shortLeft)),
                qtyPrecision,
                MidpointRounding.AwayFromZero);

            if (releaseQty <= 0m)
                continue;

            var clampedReserved = Math.Round(
                Math.Max(sre.ReservedQty - releaseQty, sre.UsedQty),
                qtyPrecision,
                MidpointRounding.AwayFromZero);
            var actualReleased = sre.ReservedQty - clampedReserved;
            if (actualReleased <= 0m)
                continue;

            plan.Add((sre.StockReservationEntryId, actualReleased));
            unusedByWoDetail[sre.WorkOrderItemId] = unusedLeft - actualReleased;
            remainingShort[sre.ScioDetailId] = shortLeft - actualReleased;
        }

        return plan;
    }

    /// <summary>
    /// Computes per-SRE restore quantities when cancelling a "Return Raw Material to Customer"
    /// entry to return previously released qty back to Work Order reservations per ERPNext PR #67523
    /// (<c>work_order/services/reservation.py: restore_inward_reservations</c>).
    /// Input <paramref name="sresNewestFirst"/> must be ordered newest-first.
    /// </summary>
    public static List<(Guid StockReservationEntryId, decimal RestoreQty)> ComputeRestoreInwardWorkOrderReservationPlan(
        IReadOnlyList<InwardWorkOrderReservationRow> sresNewestFirst,
        IReadOnlyDictionary<Guid, decimal> scioFreeReservedQtyByDetail,
        IReadOnlyDictionary<Guid, decimal> returnQtyByScioDetail,
        int qtyPrecision = 3)
    {
        var freeRemaining = new Dictionary<Guid, decimal>(scioFreeReservedQtyByDetail);
        var returnRemaining = new Dictionary<Guid, decimal>(returnQtyByScioDetail);
        var plan = new List<(Guid StockReservationEntryId, decimal RestoreQty)>();

        foreach (var sre in sresNewestFirst)
        {
            if (!returnRemaining.TryGetValue(sre.ScioDetailId, out var returnLeft) || returnLeft <= 0m)
                continue;
            if (!freeRemaining.TryGetValue(sre.ScioDetailId, out var freeLeft) || freeLeft <= 0m)
                continue;

            var capacity = sre.VoucherQty - sre.ReservedQty;
            var restoreQty = Math.Round(
                Math.Min(capacity, Math.Min(freeLeft, returnLeft)),
                qtyPrecision,
                MidpointRounding.AwayFromZero);

            if (restoreQty <= 0m)
                continue;

            plan.Add((sre.StockReservationEntryId, restoreQty));
            freeRemaining[sre.ScioDetailId] = freeLeft - restoreQty;
            returnRemaining[sre.ScioDetailId] = returnLeft - restoreQty;
        }

        return plan;
    }

    /// <summary>
    /// Refuses direct Sales Order stock reservation (when <paramref name="fromVoucherType"/> is not <c>"Pick List"</c>)
    /// ONLY when submitted Pick Lists still hold undelivered picked qty (<c>Sum(pickedQty - deliveredQty) &gt; 0</c>)
    /// for the Sales Order item per ERPNext PR #67791 (<c>get_undelivered_picked_qty_details</c>):
    /// once picked stock has already been delivered, remaining Sales Order qty can be reserved directly.
    /// </summary>
    public static void ValidateNoUndeliveredPickedQtyBeforeSalesOrderReserve(
        string? fromVoucherType,
        int rowIdx,
        Guid salesOrderItemId,
        string itemCode,
        IEnumerable<(Guid SalesOrderItemId, decimal PickedQty, decimal DeliveredQty)> submittedPickListRows,
        int pickedQtyPrecision = 4)
    {
        if (string.Equals(fromVoucherType, "Pick List", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fromVoucherType, "PickList", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var undeliveredPicked = submittedPickListRows
            .Where(r => r.SalesOrderItemId == salesOrderItemId && r.PickedQty > r.DeliveredQty)
            .Sum(r => r.PickedQty - r.DeliveredQty);

        if (Math.Round(undeliveredPicked, pickedQtyPrecision, MidpointRounding.AwayFromZero) > 0m)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("rowIdx", rowIdx)
                .WithData("itemCode", itemCode)
                .WithData("detail", $"Row #{rowIdx}: Item {itemCode} has been picked, please reserve stock from the Pick List.");
        }
    }

    /// <summary>
    /// Computes a Sales Order row's unreserved stock qty ignoring sub-precision conversion-factor
    /// residue per ERPNext PR #67709 (<c>sales_order/services/reservation.py: get_unreserved_qty</c>):
    /// returns <c>0</c> whenever <c>Round(unreservedQty, stockQtyPrecision) &lt;= 0</c>.
    /// </summary>
    public static decimal CalculateSalesOrderRowUnreservedQty(
        decimal stockQty,
        decimal deliveredQty,
        decimal conversionFactor,
        decimal existingReservedQty,
        int stockQtyPrecision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var unreserved = stockQty - deliveredQty * factor - existingReservedQty;
        return Math.Round(unreserved, stockQtyPrecision, MidpointRounding.AwayFromZero) > 0m
            ? unreserved
            : 0m;
    }

    /// <summary>
    /// Determines whether reserving <paramref name="qtyToReserve"/> against <paramref name="unreservedQty"/>
    /// is a partial reservation, ignoring sub-precision residue per ERPNext PR #67709.
    /// </summary>
    public static bool IsPartialSalesOrderReservation(
        decimal unreservedQty,
        decimal qtyToReserve,
        int stockQtyPrecision = 4)
        => Math.Round(unreservedQty - qtyToReserve, stockQtyPrecision, MidpointRounding.AwayFromZero) > 0m;

    /// <summary>
    /// Computes the status string of a submitted Stock Reservation Entry per ERPNext PR #67701
    /// (<c>stock_reservation_entry.py: get_status</c>):
    /// ignores sub-precision floating/decimal residue in <paramref name="deliveredQty"/>
    /// (<c>Round(deliveredQty, reservedQtyPrecision) &gt; 0</c>) so serial/batch arithmetic residue
    /// does not falsely mark a reservation as <c>"Partially Delivered"</c>.
    /// </summary>
    public static string ResolveStockReservationEntryStatus(
        decimal reservedQty,
        decimal deliveredQty,
        decimal consumedQty,
        decimal transferredQty,
        bool isWorkOrderOrSubcontractingTransfer = false,
        int reservedQtyPrecision = 4)
    {
        if (isWorkOrderOrSubcontractingTransfer && transferredQty > 0m)
        {
            return transferredQty < reservedQty ? "Partially Used" : "Closed";
        }

        if (deliveredQty + consumedQty >= reservedQty && reservedQty > 0m)
        {
            return "Delivered";
        }

        if (Math.Round(deliveredQty, reservedQtyPrecision, MidpointRounding.AwayFromZero) > 0m)
        {
            return "Partially Delivered";
        }

        if (Math.Round(consumedQty, reservedQtyPrecision, MidpointRounding.AwayFromZero) > 0m)
        {
            return "Partially Consumed";
        }

        return reservedQty > 0m ? "Reserved" : "Draft";
    }

    /// <summary>
    /// Rounds the unreserved allowed quantity (<c>abs(actualQty) - abs(shortageDiff)</c>) to
    /// <paramref name="qtyPrecision"/> before displaying the reserved-stock shortage message
    /// per ERPNext PR #65578 (<c>stock_ledger.py</c>).
    /// </summary>
    public static decimal CalculateUnreservedAllowedQtyOnShortage(
        decimal actualQty,
        decimal shortageDiff,
        int qtyPrecision = 4)
    {
        return Math.Round(
            Math.Abs(actualQty) - Math.Abs(shortageDiff),
            qtyPrecision,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes the Sales Order reserved stock quantity contributed by a single direct or packed SO row
    /// per ERPNext PR #67445 (<c>erpnext/stock/stock_balance.py: get_reserved_qty</c>):
    /// - Excludes non-submitted, On Hold, Closed, drop-ship (<c>deliveredBySupplier</c>), row-closed, zero-qty,
    ///   and over-delivered (<c>qty &lt; deliveredQty</c>) rows.
    /// - When <paramref name="dontReserveOnReturn"/> is enabled, <c>netReserved = qty - deliveredQty - returnedQty</c>
    ///   is clamped at <c>0</c> so a replacement delivery after a return never produces negative reserved qty.
    /// </summary>
    public static decimal CalculateSalesOrderRowReservedStockQty(
        bool isSubmitted,
        string orderStatus,
        bool deliveredBySupplier,
        bool isRowClosed,
        decimal qty,
        decimal stockQty,
        decimal deliveredQty,
        decimal returnedQty,
        bool dontReserveOnReturn,
        decimal? packedComponentQty = null,
        bool isPackedComponentSameAsParent = false)
    {
        if (!isSubmitted
            || string.Equals(orderStatus, "On Hold", StringComparison.OrdinalIgnoreCase)
            || string.Equals(orderStatus, "Closed", StringComparison.OrdinalIgnoreCase)
            || deliveredBySupplier
            || isRowClosed
            || isPackedComponentSameAsParent)
        {
            return 0m;
        }

        if (qty == 0m || qty < deliveredQty)
        {
            return 0m;
        }

        var netReserved = dontReserveOnReturn
            ? Math.Max(0m, qty - deliveredQty - returnedQty)
            : qty - deliveredQty;

        var basisQty = packedComponentQty ?? stockQty;
        return basisQty * netReserved / qty;
    }

    /// <summary>
    /// Aggregates reserved stock quantity across multiple Sales Order rows per ERPNext PR #67445.
    /// </summary>
    public static decimal SumSalesOrderReservedStockQty(
        IEnumerable<SalesOrderReservedQtyRow>? rows,
        bool dontReserveOnReturn)
    {
        if (rows == null)
        {
            return 0m;
        }

        var total = 0m;
        foreach (var row in rows)
        {
            total += CalculateSalesOrderRowReservedStockQty(
                row.IsSubmitted,
                row.OrderStatus,
                row.DeliveredBySupplier,
                row.IsRowClosed,
                row.Qty,
                row.StockQty,
                row.DeliveredQty,
                row.ReturnedQty,
                dontReserveOnReturn,
                row.PackedComponentQty,
                row.IsPackedComponentSameAsParent);
        }

        return total;
    }

    /// <summary>
    /// Builds the Subcontracting Order Unreserve dialog rows using remaining held quantity
    /// (<c>ReservedQty - DeliveredQty - TransferredQty - ConsumedQty</c>) rather than
    /// <c>ReservedQty - DeliveredQty</c> per ERPNext PR #67551
    /// (<c>subcontracting_order.js: unreserve_stock</c> / <c>stock_reservation.js: get_held_qty</c>):
    /// omits entries with <c>heldQty &lt;= 0</c> so transferred or consumed stock is never shown for unreserve.
    /// </summary>
    public static IReadOnlyList<SubcontractingUnreserveDialogRow> BuildSubcontractingOrderUnreserveDialogRows(
        IEnumerable<(Guid SreId, Guid ItemId, Guid WarehouseId, decimal ReservedQty, decimal DeliveredQty, decimal TransferredQty, decimal ConsumedQty)>? entries)
    {
        var rows = new List<SubcontractingUnreserveDialogRow>();
        if (entries == null)
        {
            return rows;
        }

        foreach (var entry in entries)
        {
            var heldQty = entry.ReservedQty - entry.DeliveredQty - entry.TransferredQty - entry.ConsumedQty;
            if (heldQty > 0m)
            {
                rows.Add(new SubcontractingUnreserveDialogRow(entry.SreId, entry.ItemId, entry.WarehouseId, heldQty));
            }
        }

        return rows;
    }
}

public readonly record struct SubcontractingUnreserveDialogRow(
    Guid StockReservationEntryId,
    Guid ItemId,
    Guid WarehouseId,
    decimal HeldQty);

public readonly record struct SalesOrderReservedQtyRow(
    bool IsSubmitted,
    string OrderStatus,
    bool DeliveredBySupplier,
    bool IsRowClosed,
    decimal Qty,
    decimal StockQty,
    decimal DeliveredQty,
    decimal ReturnedQty,
    decimal? PackedComponentQty = null,
    bool IsPackedComponentSameAsParent = false);

public readonly record struct InwardWorkOrderReservationRow(
    Guid StockReservationEntryId,
    Guid WorkOrderItemId,
    Guid ScioDetailId,
    decimal ReservedQty,
    decimal VoucherQty,
    decimal UsedQty,
    decimal WorkOrderRequiredQty);

public class ReservationConsumption
{
    public Guid StockReservationEntryId { get; set; }
    public decimal ConsumedQty { get; set; }
}

public class ReservedMaterialInfo
{
    public Guid StockReservationEntryId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? VoucherDetailId { get; set; }
    public decimal AvailableQty { get; set; }
}

public class MaterialTransferAllocation
{
    public Guid? BatchId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? StockReservationEntryId { get; set; }
}

public class ReservedSerialBatchPoolEntry
{
    public string? SerialNo { get; set; }
    public string? BatchNo { get; set; }
    public decimal Available { get; set; }
}
