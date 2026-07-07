using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Individual item line in a stock entry.
/// </summary>
public class StockEntryItem : CreationAuditedEntity<Guid>
{
    public Guid StockEntryId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Source warehouse (for issue/transfer).</summary>
    public Guid? SourceWarehouseId { get; set; }

    /// <summary>Target warehouse (for receipt/transfer).</summary>
    public Guid? TargetWarehouseId { get; set; }

    /// <summary>Cost per unit at time of transaction.</summary>
    public decimal? ValuationRate { get; set; }

    protected StockEntryItem() { }

    public StockEntryItem(Guid id, Guid stockEntryId, Guid itemId, decimal quantity,
        Guid? sourceWarehouseId, Guid? targetWarehouseId, decimal? valuationRate = null)
        : base(id)
    {
        StockEntryId = stockEntryId;
        ItemId = itemId;
        Quantity = quantity;
        SourceWarehouseId = sourceWarehouseId;
        TargetWarehouseId = targetWarehouseId;
        ValuationRate = valuationRate;
    }
}
