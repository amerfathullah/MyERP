using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Bin — per-(item, warehouse) stock balance summary.
/// This is the fast-read cache for current stock levels.
/// Updated synchronously when SLE/PO/SO/WO/MR are submitted or cancelled.
/// Implements IHasConcurrencyStamp for optimistic concurrency protection
/// (prevents data corruption when concurrent stock movements update the same bin).
/// </summary>
public class Bin : AuditedEntity<Guid>, IMultiTenant, IHasConcurrencyStamp
{
    public Guid? TenantId { get; set; }

    /// <summary>Optimistic concurrency token — prevents lost updates on concurrent bin modifications.</summary>
    [ConcurrencyCheck]
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    /// <summary>Actual stock on hand (from SLE aggregation).</summary>
    public decimal ActualQty { get; set; }

    /// <summary>Ordered from suppliers (PO qty - received qty).</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Planned to manufacture (WO qty - produced qty).</summary>
    public decimal PlannedQty { get; set; }

    /// <summary>Reserved for customers (SO qty - delivered qty).</summary>
    public decimal ReservedQty { get; set; }

    /// <summary>Indented/requested (MR qty - ordered qty).</summary>
    public decimal IndentedQty { get; set; }

    /// <summary>Reserved for manufacturing (WO material reservations).</summary>
    public decimal ReservedQtyForProduction { get; set; }

    /// <summary>Reserved for subcontracting (SCO required - transferred).</summary>
    public decimal ReservedQtyForSubContract { get; set; }

    /// <summary>Reserved for production plan.</summary>
    public decimal ReservedQtyForProductionPlan { get; set; }

    /// <summary>Total stock value = actual_qty × valuation_rate.</summary>
    public decimal StockValue { get; set; }

    /// <summary>Current valuation rate (stock_value / actual_qty).</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>
    /// Projected stock = actual + ordered + indented + planned
    ///                  - reserved - reserved_production - reserved_subcontract - reserved_pp
    /// </summary>
    public decimal ProjectedQty =>
        ActualQty
        + OrderedQty
        + IndentedQty
        + PlannedQty
        - ReservedQty
        - ReservedQtyForProduction
        - ReservedQtyForSubContract
        - ReservedQtyForProductionPlan;

    protected Bin() { }

    public Bin(Guid id, Guid itemId, Guid warehouseId, Guid? tenantId = null)
        : base(id)
    {
        ItemId = itemId;
        WarehouseId = warehouseId;
        TenantId = tenantId;
    }

    /// <summary>Update actual qty and valuation from stock ledger. Resets value and rate when empty (commit 6fbcfade6c).</summary>
    public void UpdateActualQty(decimal actualQty, decimal stockValue)
    {
        ActualQty = actualQty;
        if (Math.Abs(actualQty) < 0.0000001m)
        {
            ActualQty = 0;
            StockValue = 0;
            ValuationRate = 0;
        }
        else
        {
            StockValue = stockValue;
            ValuationRate = stockValue / actualQty;
        }
    }

    /// <summary>Add to actual qty (from a single SLE movement). Resets value and rate when empty (commit 6fbcfade6c).</summary>
    public void ApplyStockMovement(decimal qtyChange, decimal valueChange)
    {
        ActualQty += qtyChange;
        StockValue += valueChange;
        if (Math.Abs(ActualQty) < 0.0000001m)
        {
            ActualQty = 0;
            StockValue = 0;
            ValuationRate = 0;
        }
        else
        {
            ValuationRate = StockValue / ActualQty;
        }
    }

    /// <summary>
    /// Recalculates quantities in the Bin.
    /// Per ERPNext PR #47125 / commit 36081413d8: provision to recalculate the qty in the Bin.
    /// </summary>
    public void RecalculateQty(
        decimal actualQty,
        decimal stockValue,
        decimal? plannedQty = null,
        decimal? indentedQty = null,
        decimal? orderedQty = null,
        decimal? reservedQty = null,
        decimal? reservedQtyForProduction = null,
        decimal? reservedQtyForSubContract = null,
        decimal? reservedQtyForProductionPlan = null)
    {
        UpdateActualQty(actualQty, stockValue);
        if (plannedQty.HasValue) PlannedQty = plannedQty.Value;
        if (indentedQty.HasValue) IndentedQty = indentedQty.Value;
        if (orderedQty.HasValue) OrderedQty = orderedQty.Value;
        if (reservedQty.HasValue) ReservedQty = reservedQty.Value;
        if (reservedQtyForProduction.HasValue) ReservedQtyForProduction = reservedQtyForProduction.Value;
        if (reservedQtyForSubContract.HasValue) ReservedQtyForSubContract = reservedQtyForSubContract.Value;
        if (reservedQtyForProductionPlan.HasValue) ReservedQtyForProductionPlan = reservedQtyForProductionPlan.Value;
    }
}
