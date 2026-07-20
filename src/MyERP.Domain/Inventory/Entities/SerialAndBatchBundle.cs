using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Serial and Batch Bundle — modern system for tracking serial/batch items in stock transactions.
/// Each stock movement creates a bundle document tracking which specific serial numbers
/// and/or batches are involved, along with their valuation rates.
/// Replaces old comma-separated serial_no field.
/// Maps to ERPNext stock/doctype/serial_and_batch_bundle.
/// </summary>
public class SerialAndBatchBundle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    /// <summary>Inward (receiving) or Outward (consuming).</summary>
    public BundleTransactionType TypeOfTransaction { get; set; }

    /// <summary>Total quantity across all entries (must equal parent transaction line qty).</summary>
    public decimal TotalQty { get; set; }

    /// <summary>Average incoming/outgoing rate across entries.</summary>
    public decimal AvgRate { get; set; }

    /// <summary>Total stock value of this bundle (sum of entry qty × rate).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Source voucher (Stock Entry, Purchase Receipt, Delivery Note, etc.).</summary>
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }

    /// <summary>Specific row in the voucher this bundle belongs to.</summary>
    public Guid? VoucherDetailId { get; set; }

    /// <summary>Whether this bundle has been cancelled (audit trail preservation).</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Whether this bundle is for rejected quantity.</summary>
    public bool IsRejected { get; set; }

    /// <summary>Posting date/time of the parent transaction.</summary>
    public DateTime PostingDate { get; set; }

    /// <summary>Item has serial number tracking.</summary>
    public bool HasSerialNo { get; set; }

    /// <summary>Item has batch tracking.</summary>
    public bool HasBatchNo { get; set; }

    /// <summary>Individual serial/batch entries in this bundle.</summary>
    public ICollection<SerialAndBatchEntry> Entries { get; private set; } = new List<SerialAndBatchEntry>();

    protected SerialAndBatchBundle() { }

    public SerialAndBatchBundle(Guid id, Guid companyId, Guid itemId, Guid warehouseId,
        BundleTransactionType typeOfTransaction, string voucherType, Guid voucherId,
        DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ItemId = Check.NotDefaultOrNull<Guid>(itemId, nameof(itemId));
        WarehouseId = Check.NotDefaultOrNull<Guid>(warehouseId, nameof(warehouseId));
        TypeOfTransaction = typeOfTransaction;
        VoucherType = voucherType;
        VoucherId = voucherId;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    /// <summary>Add a serial/batch entry to this bundle.</summary>
    public void AddEntry(SerialAndBatchEntry entry)
    {
        if (IsCancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("reason", "Bundle is cancelled");

        Entries.Add(entry);
        Recalculate();
    }

    /// <summary>Recalculate totals from entries.</summary>
    public void Recalculate()
    {
        TotalQty = Entries.Sum(e => e.Qty);
        TotalAmount = Entries.Sum(e => e.Qty * e.IncomingRate);
        AvgRate = TotalQty != 0 ? TotalAmount / TotalQty : 0;
    }

    /// <summary>Mark as cancelled (preserves audit trail — never deleted).</summary>
    public void Cancel()
    {
        IsCancelled = true;
    }

    /// <summary>Validate bundle qty matches transaction line qty.</summary>
    public void ValidateQtyMatch(decimal transactionQty)
    {
        var absTransactionQty = Math.Abs(transactionQty);
        if (Math.Abs(TotalQty - absTransactionQty) > 0.000001m)
            throw new BusinessException(MyERPDomainErrorCodes.BundleQtyMismatch)
                .WithData("bundleQty", TotalQty)
                .WithData("transactionQty", absTransactionQty);
    }
}

/// <summary>
/// Individual entry within a Serial and Batch Bundle.
/// Each row represents one serial number or one batch allocation.
/// </summary>
public class SerialAndBatchEntry : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SerialAndBatchBundleId { get; set; }

    /// <summary>Serial number (for serial-tracked items).</summary>
    public string? SerialNo { get; set; }

    /// <summary>Batch number (for batch-tracked items).</summary>
    public Guid? BatchId { get; set; }

    /// <summary>Quantity for this entry (usually 1 for serial, variable for batch).</summary>
    public decimal Qty { get; set; }

    /// <summary>Valuation/incoming rate for this specific serial/batch.</summary>
    public decimal IncomingRate { get; set; }

    /// <summary>Stock value for this entry (Qty × IncomingRate).</summary>
    public decimal StockValueDifference => Qty * IncomingRate;

    /// <summary>Warehouse (normally matches parent, but can differ for transfers).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>FIFO/LIFO stock queue JSON for non-batchwise valuation scenarios.</summary>
    public string? StockQueue { get; set; }

    protected SerialAndBatchEntry() { }

    public SerialAndBatchEntry(Guid id, Guid serialAndBatchBundleId, decimal qty, decimal incomingRate,
        string? serialNo = null, Guid? batchId = null, Guid? tenantId = null)
        : base(id)
    {
        SerialAndBatchBundleId = serialAndBatchBundleId;
        Qty = qty;
        IncomingRate = incomingRate;
        SerialNo = serialNo;
        BatchId = batchId;
        TenantId = tenantId;
    }
}

/// <summary>Bundle direction: Inward (receiving stock) or Outward (consuming stock).</summary>
public enum BundleTransactionType
{
    Inward = 0,
    Outward = 1
}
