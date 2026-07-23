using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// BOM Secondary Item — Co-Products, By-Products, and Scrap produced alongside the main FG.
/// Per ERPNext v16 (gotcha #85): Renamed from "BOM Scrap Item" with type classification.
/// 
/// Key rules per DO-NOT:
/// - FG item CANNOT appear in secondary_items table (circular cost allocation)
/// - process_loss_per cannot be >= 100% per secondary item
/// - cost_allocation_per across FG + all secondary items MUST total exactly 100%
/// </summary>
public class BomSecondaryItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid BomId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }

    /// <summary>Type of secondary output: CoProduct, ByProduct, or Scrap.</summary>
    public SecondaryItemType SecondaryItemType { get; set; }

    /// <summary>Quantity produced per BOM quantity (before process loss).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Stock UOM for this secondary item.</summary>
    public string? StockUom { get; set; }

    /// <summary>Rate per unit (from valuation or BOM costing).</summary>
    public decimal Rate { get; set; }

    /// <summary>Total amount: Quantity × Rate (after process loss deduction).</summary>
    public decimal Amount => EffectiveQuantity * Rate;

    /// <summary>
    /// Percentage of FG raw material cost allocated to this secondary item.
    /// Per DO-NOT: FG + all secondary items MUST total exactly 100%.
    /// Per gotcha #518: FG auto-reduces when secondary items have cost_allocation.
    /// </summary>
    public decimal CostAllocationPercentage { get; set; }

    /// <summary>
    /// Per-item process loss percentage (0-99.99%).
    /// Per gotcha #442: BOM has BOTH per-BOM process_loss AND per-secondary-item loss.
    /// Effective qty = Quantity × (1 - ProcessLossPercentage / 100).
    /// </summary>
    public decimal ProcessLossPercentage { get; set; }

    /// <summary>Effective quantity after process loss deduction.</summary>
    public decimal EffectiveQuantity =>
        ProcessLossPercentage > 0
            ? Quantity * (1 - ProcessLossPercentage / 100m)
            : Quantity;

    /// <summary>Target warehouse for this secondary item (scrap → scrap warehouse, others → FG warehouse).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Whether this is a legacy scrap item (migrated from v15 BOM Scrap Item).</summary>
    public bool IsLegacy { get; set; }

    protected BomSecondaryItem() { }

    public BomSecondaryItem(Guid id, Guid bomId, Guid itemId, SecondaryItemType type, decimal quantity, Guid? tenantId = null)
        : base(id)
    {
        BomId = bomId;
        ItemId = itemId;
        SecondaryItemType = type;
        Quantity = quantity;
        TenantId = tenantId;
    }
}
