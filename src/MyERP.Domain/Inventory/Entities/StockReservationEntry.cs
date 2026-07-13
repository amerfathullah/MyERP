using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Reservation Entry — reserves stock for a Sales Order item to prevent overselling.
/// Amendment is entirely blocked (must cancel and recreate).
/// Mutual exclusion: cannot enable reservation with allow_negative_stock.
/// Maps to ERPNext stock/doctype/stock_reservation_entry.
/// </summary>
public class StockReservationEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    /// <summary>Source document (typically Sales Order).</summary>
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }

    /// <summary>Specific row in the voucher.</summary>
    public Guid? VoucherDetailId { get; set; }

    /// <summary>Original reserved qty.</summary>
    public decimal ReservedQty { get; set; }

    /// <summary>Qty already delivered against this reservation.</summary>
    public decimal DeliveredQty { get; set; }

    /// <summary>Remaining: ReservedQty - DeliveredQty.</summary>
    public decimal AvailableQty => ReservedQty - DeliveredQty;

    /// <summary>Batch (if batch-tracked item).</summary>
    public Guid? BatchId { get; set; }

    /// <summary>Serial and Batch Bundle (new system).</summary>
    public Guid? SerialAndBatchBundleId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    protected StockReservationEntry() { }

    public StockReservationEntry(Guid id, Guid companyId, Guid itemId, Guid warehouseId,
        string voucherType, Guid voucherId, decimal reservedQty, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        VoucherType = voucherType;
        VoucherId = voucherId;
        ReservedQty = reservedQty;
        TenantId = tenantId;

        if (reservedQty <= 0)
            throw new ArgumentException("Reserved qty must be positive.", nameof(reservedQty));
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    /// <summary>Record delivery against this reservation.</summary>
    public void RecordDelivery(decimal qty)
    {
        if (qty <= 0) throw new ArgumentException("Qty must be positive.", nameof(qty));
        if (DeliveredQty + qty > ReservedQty)
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("available", AvailableQty)
                .WithData("requested", qty);
        DeliveredQty += qty;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}
