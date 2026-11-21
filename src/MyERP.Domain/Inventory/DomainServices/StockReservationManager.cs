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
    /// per ERPNext commits 3bf7091474 and 6e83c51495 (stock_reservation.js):
    /// Every quantity field the reserve dialog reads (<paramref name="stockQty"/>, <paramref name="stockReservedQty"/>)
    /// and sends back is in stock UOM, so the unreserved balance is returned directly in stock UOM
    /// without dividing by <paramref name="conversionFactor"/>.
    /// </summary>
    public static decimal CalculateDialogUnreservedStockQty(
        decimal stockQty,
        decimal stockReservedQty,
        decimal dispatchedQtyInOrderUom = 0m,
        decimal conversionFactor = 1m,
        int precision = 4)
    {
        var factor = conversionFactor > 0m ? conversionFactor : 1m;
        var alreadyCoveredStockQty = stockReservedQty > 0m
            ? stockReservedQty
            : dispatchedQtyInOrderUom * factor;
        var unreserved = Math.Round(stockQty - alreadyCoveredStockQty, precision, MidpointRounding.AwayFromZero);
        return Math.Max(0m, unreserved);
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
        var stillHolds = docStatus == DocumentStatus.Submitted &&
            (deliveredQty + transferredQty + consumedQty) < reservedQty;

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
            return (transferredQty < reservedQty ? "Partially Used" : "Closed", stillHolds);
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
}

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
