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

    public decimal CurrentQuantity { get; set; }
    public decimal CurrentValuationRate { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal NewValuationRate { get; set; }

    /// <summary>Qty difference: NewQuantity - CurrentQuantity.</summary>
    public decimal QuantityDifference => NewQuantity - CurrentQuantity;

    /// <summary>Value difference: (New × Rate) - (Current × Rate).</summary>
    public decimal DifferenceAmount => (NewQuantity * NewValuationRate) - (CurrentQuantity * CurrentValuationRate);

    protected StockReconciliationItem() { }

    public StockReconciliationItem(Guid id, Guid stockReconciliationId,
        Guid itemId, Guid warehouseId,
        decimal newQuantity, decimal newValuationRate,
        decimal currentQuantity = 0, decimal currentValuationRate = 0)
        : base(id)
    {
        StockReconciliationId = stockReconciliationId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        NewQuantity = newQuantity;
        NewValuationRate = newValuationRate;
        CurrentQuantity = currentQuantity;
        CurrentValuationRate = currentValuationRate;
    }
}
