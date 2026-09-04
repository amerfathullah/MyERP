using System;
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

    public StockReservationManager(
        IRepository<StockReservationEntry, Guid> sreRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<StockLedgerEntry, Guid> sleRepository)
    {
        _sreRepository = sreRepository;
        _binRepository = binRepository;
        _sleRepository = sleRepository;
    }

    /// <summary>
    /// Validates that sufficient unreserved stock exists before creating a reservation.
    /// Available = ActualQty (as of postingDate, ignoring future stock) - SUM(active SRE reserved qty for same item+warehouse).
    /// Per ERPNext PR #58303 (commit 478a2f4f4b): ignore future stock during batch/stock reservation.
    /// </summary>
    public async Task ValidateAvailabilityAsync(Guid itemId, Guid warehouseId, decimal requestedQty, Guid? batchId = null, DateTime? asOfDate = null)
    {
        // Round to stock reservation precision to avoid floating-point / sub-unit representation rejections (ERPNext PR #46973 / commit 860699ee7b)
        requestedQty = Math.Round(requestedQty, 4);

        decimal actualQty;
        if (asOfDate.HasValue)
        {
            var sleQueryable = await _sleRepository.GetQueryableAsync();
            var lastSle = sleQueryable
                .Where(s => s.ItemId == itemId
                    && s.WarehouseId == warehouseId
                    && (batchId == null || s.BatchId == batchId)
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
        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var reservedQty = sreQueryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && (batchId == null || s.BatchId == batchId)
                && s.Status == DocumentStatus.Submitted
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
    /// Consumes reserved stock when delivery is made (FIFO by creation date).
    /// Per ERPNext PR #49082 (commit dbaa44688e): filters delivered_qty < reserved_qty.
    /// Returns list of consumed SRE IDs with quantities.
    /// </summary>
    public async Task<ReservationConsumption[]> ConsumeOnDeliveryAsync(
        Guid itemId, Guid warehouseId, decimal deliveredQty, Guid? salesOrderId = null)
    {
        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0
                && (salesOrderId == null || s.VoucherId == salesOrderId))
            .OrderBy(s => s.CreationTime)
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = deliveredQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;

            var available = sre.AvailableQty;
            if (available <= 0) continue;

            var consume = Math.Min(remaining, available);
            sre.RecordDelivery(consume);
            await _sreRepository.UpdateAsync(sre);

            consumed.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = consume
            });

            remaining -= consume;
        }

        return consumed.ToArray();
    }

    /// <summary>
    /// Cancels all active reservations for a voucher (used on SO/WO cancel/close).
    /// Per ERPNext PR #50773 / commit 9b5d215a7a.
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
            sre.Cancel();
            await _sreRepository.UpdateAsync(sre);
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
        decimal? voucherDemandQty = null, Guid? voucherDetailId = null, DateTime? postingDate = null)
    {
        qty = Math.Round(qty, 4);
        if (qty <= 0) return;

        await ValidateAvailabilityAsync(itemId, warehouseId, qty, batchId, postingDate);

        var demandQty = voucherDemandQty.HasValue ? Math.Round(voucherDemandQty.Value, 4) : qty;

        var sre = new StockReservationEntry(
            GuidGenerator.Create(), companyId, itemId, warehouseId,
            voucherType, voucherId, qty, voucherQty: demandQty, tenantId: tenantId)
        {
            BatchId = batchId,
            VoucherDetailId = voucherDetailId
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
    /// Transfers reservation entries from a source voucher (e.g. Production Plan) to a target voucher (e.g. Work Order).
    /// Updates source entries (marks transferred_qty) and creates target reservation entries linked via FromVoucher.
    /// Per ERPNext commit 0bc3cfe29d: transfer_reservation_entries_to.
    /// </summary>
    public async Task TransferReservationEntriesAsync(
        string fromVoucherType, Guid fromVoucherId,
        string toVoucherType, Guid toVoucherId,
        Guid itemId, Guid warehouseId, decimal qty,
        Guid? toVoucherDetailId = null)
    {
        var sreQueryable = await _sreRepository.GetQueryableAsync();
        var sourceEntries = sreQueryable
            .Where(s => s.VoucherType == fromVoucherType
                && s.VoucherId == fromVoucherId
                && s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0)
            .OrderBy(s => s.CreationTime)
            .ToList();

        var remaining = qty;
        foreach (var src in sourceEntries)
        {
            if (remaining <= 0) break;
            var available = src.ReservedQty - src.DeliveredQty - src.TransferredQty - src.ConsumedQty;
            var transferQty = Math.Min(available, remaining);

            src.TransferredQty += transferQty;
            await _sreRepository.UpdateAsync(src);

            var newSre = new StockReservationEntry(
                GuidGenerator.Create(),
                src.CompanyId,
                src.ItemId,
                src.WarehouseId,
                toVoucherType,
                toVoucherId,
                transferQty,
                transferQty,
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

            remaining -= transferQty;
        }
    }
}

public class ReservationConsumption
{
    public Guid StockReservationEntryId { get; set; }
    public decimal ConsumedQty { get; set; }
}
