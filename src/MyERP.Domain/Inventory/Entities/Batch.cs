using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Batch — lot tracking for inventory items.
/// Each batch has a unique batch_no per item.
/// Supports expiry dates for consumables/chemicals/food items.
/// </summary>
public class Batch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Unique batch number (auto-generated or manual).</summary>
    public string BatchNo { get; set; } = null!;

    /// <summary>Item this batch belongs to.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Reference document that created this batch (e.g., Purchase Receipt).</summary>
    public string? ReferenceDocType { get; set; }
    public Guid? ReferenceDocId { get; set; }

    /// <summary>Manufacturing/production date.</summary>
    public DateTime? ManufacturingDate { get; set; }

    /// <summary>Expiry date (null = no expiry).</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Shelf life in days (used to auto-calculate expiry from manufacturing date).</summary>
    public int? ShelfLifeInDays { get; set; }

    /// <summary>Whether this batch uses batch-wise valuation (separate cost per batch).</summary>
    public bool UseBatchwiseValuation { get; set; } = true;

    /// <summary>Supplier batch number (for traceability).</summary>
    public string? SupplierBatchNo { get; set; }

    /// <summary>Whether this batch has been disabled (cannot be used in new transactions).</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Whether this batch was cancelled (auto-created batch reversal).</summary>
    public bool IsCancelled { get; set; }

    public string? Description { get; set; }

    protected Batch() { }

    public Batch(Guid id, Guid itemId, string batchNo, Guid? tenantId = null)
        : base(id)
    {
        ItemId = itemId;
        BatchNo = Check.NotNullOrWhiteSpace(batchNo, nameof(batchNo), BatchConsts.MaxBatchNoLength);
        TenantId = tenantId;
    }

    /// <summary>Check if this batch is expired as of the given date.</summary>
    public bool IsExpired(DateTime? asOfDate = null)
    {
        if (!ExpiryDate.HasValue) return false;
        return (asOfDate ?? DateTime.UtcNow) > ExpiryDate.Value;
    }

    /// <summary>Set expiry from manufacturing date + shelf life.</summary>
    public void SetExpiryFromShelfLife()
    {
        if (ManufacturingDate.HasValue && ShelfLifeInDays.HasValue)
            ExpiryDate = ManufacturingDate.Value.AddDays(ShelfLifeInDays.Value);
    }
}
