using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Individual item line in a stock entry.
/// </summary>
public class StockEntryItem : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StockEntryId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Source warehouse (for issue/transfer).</summary>
    public Guid? SourceWarehouseId { get; set; }

    /// <summary>Target warehouse (for receipt/transfer).</summary>
    public Guid? TargetWarehouseId { get; set; }

    /// <summary>Cost per unit at time of transaction.</summary>
    public decimal? ValuationRate { get; set; }

    /// <summary>Batch this movement is against, for batch-tracked items.</summary>
    public Guid? BatchId { get; set; }

    /// <summary>Marks this item as the finished good in Manufacture/Repack entries.</summary>
    public bool IsFinishedItem { get; set; }

    /// <summary>When true, user must manually enter the valuation rate (multi-FG Repack).</summary>
    public bool SetBasicRateManually { get; set; }

    /// <summary>For secondary items: CoProduct, ByProduct, Scrap.</summary>
    public string? SecondaryItemType { get; set; }

    /// <summary>Per-item process loss percentage for secondary items.</summary>
    public decimal ProcessLossPercentage { get; set; }

    /// <summary>Link to source stock entry detail row (for Disassemble scale factor matching).</summary>
    public Guid? SourceStockEntryDetailId { get; set; }

    /// <summary>Cost center for this line item (falls back to parent StockEntry.CostCenterId).</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Expense account for this line item (e.g., Difference Account on repack/manufacture).</summary>
    public Guid? ExpenseAccountId { get; set; }

    /// <summary>Project for this line item (falls back to parent StockEntry.ProjectId, PR #51014 / commit e57d2b4811).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Stock UOM from Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM (per PR #57710: disassembly aggregates in stock UOM).</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Additional costs distributed onto this incoming item line (per ERPNext commit 074c84e880 / PR #58433).</summary>
    public decimal AdditionalCost { get; set; }

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
