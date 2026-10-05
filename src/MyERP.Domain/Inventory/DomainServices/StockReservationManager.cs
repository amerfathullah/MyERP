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
            .ThenBy(s => s.CreationTime)
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

            var canRestore = Math.Min(remaining, sre.DeliveredQty);
            sre.RevertDelivery(canRestore);
            await _sreRepository.UpdateAsync(sre);

            restored.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = canRestore
            });

            remaining -= canRestore;
        }

        return restored.ToArray();
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

        await ValidateAvailabilityAsync(itemId, warehouseId, qty, batchId, postingDate);

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
                .Sum(s => s.ReservedQty - s.TransferredQty - s.DeliveredQty - (voucherType is "Work Order" or "Subcontracting Order" ? 0m : s.ConsumedQty));

            var allowedQty = Math.Max(0m, demandQty - existingRowReserved);
            if (qty > allowedQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Cannot reserve more than allowed quantity of {allowedQty} for item against {voucherType} {voucherId}. Already reserved: {existingRowReserved}, Required: {demandQty}.");
            }
        }

        var sre = new StockReservationEntry(
            GuidGenerator?.Create() ?? Guid.NewGuid(), companyId, itemId, warehouseId,
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
                GuidGenerator?.Create() ?? Guid.NewGuid(),
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
            GuidGenerator?.Create() ?? Guid.NewGuid(), companyId, itemId, warehouseId,
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

        var binQueryable = await _binRepository.GetQueryableAsync();

        foreach (var sre in activeSres)
        {
            var unreservedQty = sre.AvailableQty;
            sre.Cancel();
            await _sreRepository.UpdateAsync(sre);

            if (unreservedQty > 0)
            {
                var bin = binQueryable.FirstOrDefault(b => b.ItemId == sre.ItemId && b.WarehouseId == sre.WarehouseId);
                if (bin != null)
                {
                    bin.ReservedQty = Math.Max(0m, bin.ReservedQty - unreservedQty);
                    await _binRepository.UpdateAsync(bin);
                }
            }
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
    /// Updates transferred quantity on active Work Order reservations during Material Transfer.
    /// Per ERPNext PR #59424 / commit dbada3f461: only consumes reservations matching the transferred batch.
    /// </summary>
    public async Task<ReservationConsumption[]> ApplyWorkOrderTransferAsync(
        Guid workOrderId, Guid itemId, Guid sourceWarehouseId, decimal transferredQty, Guid? batchId = null, Guid? workOrderItemId = null)
    {
        if (transferredQty <= 0) return Array.Empty<ReservationConsumption>();

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => (s.VoucherType == "WorkOrder" || s.VoucherType == "Work Order")
                && s.VoucherId == workOrderId
                && s.ItemId == itemId
                && s.WarehouseId == sourceWarehouseId
                && s.Status == DocumentStatus.Submitted
                && (workOrderItemId == null || s.VoucherDetailId == workOrderItemId)
                && (s.ReservedQty - s.TransferredQty - s.ConsumedQty) > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenBy(s => s.CreationTime)
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = transferredQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var available = sre.AvailableQty;
            if (available <= 0) continue;

            var transfer = Math.Min(remaining, available);
            sre.TransferredQty += transfer;
            await _sreRepository.UpdateAsync(sre);

            consumed.Add(new ReservationConsumption
            {
                StockReservationEntryId = sre.Id,
                ConsumedQty = transfer
            });

            remaining -= transfer;
        }

        return consumed.ToArray();
    }

    /// <summary>
    /// Reverts transferred quantity on Work Order reservations when a Material Transfer is cancelled.
    /// </summary>
    public async Task RevertWorkOrderTransferAsync(
        Guid workOrderId, Guid itemId, Guid sourceWarehouseId, decimal revertedQty, Guid? batchId = null, Guid? workOrderItemId = null)
    {
        if (revertedQty <= 0) return;

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => (s.VoucherType == "WorkOrder" || s.VoucherType == "Work Order")
                && s.VoucherId == workOrderId
                && s.ItemId == itemId
                && s.WarehouseId == sourceWarehouseId
                && s.Status == DocumentStatus.Submitted
                && (workOrderItemId == null || s.VoucherDetailId == workOrderItemId)
                && s.TransferredQty > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.CreationTime)
            .ToList();

        var remaining = revertedQty;
        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var canRevert = Math.Min(remaining, sre.TransferredQty);
            sre.TransferredQty = Math.Max(0, sre.TransferredQty - canRevert);
            await _sreRepository.UpdateAsync(sre);
            remaining -= canRevert;
        }
    }

    /// <summary>
    /// Updates consumed quantity on active Work Order reservations during Manufacture / Material Consumption.
    /// Per ERPNext PR #59424 / commit dbada3f461: only consumes reservations matching the consumed batch.
    /// </summary>
    public async Task<ReservationConsumption[]> ApplyWorkOrderConsumptionAsync(
        Guid workOrderId, Guid itemId, Guid warehouseId, decimal consumedQty, Guid? batchId = null, Guid? workOrderItemId = null)
    {
        if (consumedQty <= 0) return Array.Empty<ReservationConsumption>();

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => (s.VoucherType == "WorkOrder" || s.VoucherType == "Work Order")
                && s.VoucherId == workOrderId
                && s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (workOrderItemId == null || s.VoucherDetailId == workOrderItemId)
                && (s.ReservedQty - s.DeliveredQty - s.TransferredQty - s.ConsumedQty) > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.BatchId == batchId && batchId != null)
            .ThenBy(s => s.CreationTime)
            .ToList();

        var consumed = new System.Collections.Generic.List<ReservationConsumption>();
        var remaining = consumedQty;

        foreach (var sre in activeSres)
        {
            if (remaining <= 0) break;
            var available = sre.AvailableQty;
            if (available <= 0) continue;

            var consume = Math.Min(remaining, available);
            sre.ConsumedQty += consume;
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
    /// Reverts consumed quantity on Work Order reservations when a Manufacture / Material Consumption entry is cancelled.
    /// </summary>
    public async Task RevertWorkOrderConsumptionAsync(
        Guid workOrderId, Guid itemId, Guid warehouseId, decimal revertedQty, Guid? batchId = null, Guid? workOrderItemId = null)
    {
        if (revertedQty <= 0) return;

        var queryable = await _sreRepository.GetQueryableAsync();
        var activeSres = queryable
            .Where(s => (s.VoucherType == "WorkOrder" || s.VoucherType == "Work Order")
                && s.VoucherId == workOrderId
                && s.ItemId == itemId
                && s.WarehouseId == warehouseId
                && s.Status == DocumentStatus.Submitted
                && (workOrderItemId == null || s.VoucherDetailId == workOrderItemId)
                && s.ConsumedQty > 0
                && (s.BatchId == null || (batchId != null && s.BatchId == batchId)))
            .OrderByDescending(s => s.CreationTime)
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
