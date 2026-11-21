using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Reconciliation Item — per-item/warehouse adjustment line.
/// Records current vs new quantity and valuation rate.
/// </summary>
public class StockReconciliationItem : FullAuditedEntity<Guid>
{
    public Guid StockReconciliationId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public string? StockUom { get; set; }

    public decimal CurrentQuantity { get; set; }
    public decimal CurrentValuationRate { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal NewValuationRate { get; set; }

    /// <summary>Serial & Batch Bundle for incoming/reconciled stock.</summary>
    public Guid? SerialAndBatchBundleId { get; set; }

    /// <summary>Serial & Batch Bundle for current/existing stock.</summary>
    public Guid? CurrentSerialAndBatchBundleId { get; set; }

    /// <summary>Allows explicit zero valuation rate (per ERPNext PR #58800).</summary>
    public bool AllowZeroValuationRate { get; set; }

    /// <summary>Qty difference: NewQuantity - CurrentQuantity.</summary>
    public decimal QuantityDifference => NewQuantity - CurrentQuantity;

    /// <summary>Value difference: (New × Rate) - (Current × Rate).</summary>
    public decimal DifferenceAmount => (NewQuantity * NewValuationRate) - (CurrentQuantity * CurrentValuationRate);

    protected StockReconciliationItem() { }

    public StockReconciliationItem(Guid id, Guid stockReconciliationId,
        Guid itemId, Guid warehouseId,
        decimal newQuantity, decimal newValuationRate,
        decimal currentQuantity = 0, decimal currentValuationRate = 0,
        string? stockUom = null,
        Guid? serialAndBatchBundleId = null,
        Guid? currentSerialAndBatchBundleId = null,
        bool allowZeroValuationRate = false)
        : base(id)
    {
        StockReconciliationId = stockReconciliationId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        NewQuantity = newQuantity;
        NewValuationRate = newValuationRate;
        CurrentQuantity = currentQuantity;
        CurrentValuationRate = currentValuationRate;
        StockUom = stockUom;
        SerialAndBatchBundleId = serialAndBatchBundleId;
        CurrentSerialAndBatchBundleId = currentSerialAndBatchBundleId;
        AllowZeroValuationRate = allowZeroValuationRate;
    }

    /// <summary>
    /// Computes the preview amounts and differences for a Stock Reconciliation line
    /// per ERPNext commits 4b8fadae2f, f1eb56f686, 9b3dd19043, and c17e33a06b:
    /// - Blank (<c>null</c>) <paramref name="inputQty"/> or <paramref name="inputValuationRate"/> keeps
    ///   <see cref="CurrentQuantity"/> or <see cref="CurrentValuationRate"/>, whereas an explicit <c>0m</c> is respected.
    /// - Derives <c>CurrentAmount = Round(CurrentQuantity * CurrentValuationRate, amountPrecision)</c>
    ///   and <c>Amount = Round(EffectiveQty * EffectiveRate, amountPrecision)</c>.
    /// - Rounds <c>QuantityDifference</c> and <c>AmountDifference</c> to field precision.
    /// </summary>
    public (decimal EffectiveQty, decimal EffectiveRate, decimal Amount, decimal CurrentAmount, decimal QuantityDifference, decimal AmountDifference)
        CalculatePreviewDifference(
            decimal? inputQty,
            decimal? inputValuationRate,
            int qtyPrecision = 4,
            int amountPrecision = 2)
    {
        var effectiveQty = inputQty ?? CurrentQuantity;
        var effectiveRate = inputValuationRate ?? CurrentValuationRate;
        var amount = Math.Round(effectiveQty * effectiveRate, amountPrecision, MidpointRounding.AwayFromZero);
        var currentAmount = Math.Round(CurrentQuantity * CurrentValuationRate, amountPrecision, MidpointRounding.AwayFromZero);
        var qtyDiff = Math.Round(effectiveQty - CurrentQuantity, qtyPrecision, MidpointRounding.AwayFromZero);
        var amountDiff = Math.Round(amount - currentAmount, amountPrecision, MidpointRounding.AwayFromZero);

        return (effectiveQty, effectiveRate, amount, currentAmount, qtyDiff, amountDiff);
    }

    /// <summary>
    /// Resolves the valuation rate for a Stock Reconciliation row with a selected Serial and Batch Bundle
    /// per ERPNext commits 806c01be1c and 6c0d8e9a97 (<c>stock_reconciliation.py</c>):
    /// The form fills a zero rate for batch items, so a <c>0</c> rate is kept only when
    /// <paramref name="allowZeroValuationRate"/> is true; otherwise a <c>0</c> or <c>null</c> rate
    /// falls back to the bundle's <paramref name="currentValuationRate"/>.
    /// </summary>
    public static decimal ResolveBundleRowValuationRate(
        decimal? valuationRate,
        decimal currentValuationRate,
        bool allowZeroValuationRate)
    {
        var keepZeroRate = valuationRate == 0m && allowZeroValuationRate;
        if ((!valuationRate.HasValue || valuationRate.Value == 0m) && !keepZeroRate)
        {
            return currentValuationRate;
        }

        return valuationRate!.Value;
    }
}
