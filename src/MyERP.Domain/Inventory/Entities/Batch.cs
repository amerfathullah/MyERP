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

    /// <summary>Parent batch ID when split from an upstream batch (PR #58530).</summary>
    public Guid? ParentBatchId { get; set; }

    /// <summary>Manufacturing/production date.</summary>
    public DateTime? ManufacturingDate { get; set; }

    /// <summary>Expiry date (null = no expiry).</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Shelf life in days (used to auto-calculate expiry from manufacturing date).</summary>
    public int? ShelfLifeInDays { get; set; }

    /// <summary>Whether this batch uses batch-wise valuation (separate cost per batch).</summary>
    public bool UseBatchwiseValuation { get; set; } = true;

    /// <summary>Whether negative stock is allowed for this batch, overriding global stock settings (PR #56079).</summary>
    public bool AllowNegativeStock { get; set; }

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

    /// <summary>
    /// Check if this batch is expired as of the given date.
    /// Per ERPNext PR #58736 (commit 00f04fc084): show Expired status only after expiry date has passed.
    /// </summary>
    public bool IsExpired(DateTime? asOfDate = null)
    {
        if (!ExpiryDate.HasValue) return false;
        return (asOfDate ?? DateTime.UtcNow).Date > ExpiryDate.Value.Date;
    }

    /// <summary>Set expiry from manufacturing date + shelf life.</summary>
    public void SetExpiryFromShelfLife()
    {
        if (ManufacturingDate.HasValue && ShelfLifeInDays.HasValue)
            ExpiryDate = ManufacturingDate.Value.AddDays(ShelfLifeInDays.Value);
    }

    /// <summary>
    /// Auto-derives ManufacturingDate from reference doc posting date if unset, then computes ExpiryDate from shelf life (gotcha #242).
    /// </summary>
    public void DeriveManufacturingDateAndExpiry(DateTime? referenceDocPostingDate)
    {
        if (!ManufacturingDate.HasValue && referenceDocPostingDate.HasValue)
        {
            ManufacturingDate = referenceDocPostingDate.Value;
        }

        SetExpiryFromShelfLife();
    }

    /// <summary>
    /// Evaluates whether this batch should use batch-wise valuation based on item valuation method and stock settings.
    /// Per ERPNext commits 65ba79bb85 and cc171d9706:
    /// Batchwise valuation is ALLOWED for Moving Average items, UNLESS StockSettings.DoNotUseBatchwiseValuation is enabled.
    /// </summary>
    public void EvaluateBatchwiseValuation(ValuationMethod itemValuationMethod, bool doNotUseBatchwiseValuation)
    {
        if (itemValuationMethod == ValuationMethod.WeightedAverage && doNotUseBatchwiseValuation)
        {
            UseBatchwiseValuation = false;
        }
        else
        {
            UseBatchwiseValuation = true;
        }
    }
}
